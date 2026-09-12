#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Batch-builds Prefab Variants for the modular RoadKit FBXs under
    ///     <c>Assets/02_Shared/Meshes/Roads/RoadKit</c>, remapping shared road
    ///     materials and ensuring MeshColliders + Connection_* children.
    /// </summary>
    public static class RoadKitPrefabBuildTool
    {
        private const string MeshesRoot = "Assets/02_Shared/Meshes/Roads/RoadKit";
        private const string PrefabsRoot = "Assets/02_Shared/Prefabs/Roads/RoadKit";
        private const string AsphaltMatPath =
            "Assets/02_Shared/Materials/Generic/Concrete/asphalt_02.mat";
        private const string ConcreteMatPath =
            "Assets/02_Shared/Materials/Generic/Concrete/concrete_floor_worn_001.mat";

        private static readonly string[] ConnectionPrefixes =
        {
            "Connection_",
            "Connection",
            "LaneDir_"
        };

        [MenuItem(MenuPaths.WorldRoads + "Create or Update RoadKit Prefabs", priority = -900)]
        private static void CreateOrUpdateRoadKitPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(MeshesRoot))
            {
                Debug.LogError($"[RoadKitPrefabBuildTool] Missing meshes folder: {MeshesRoot}");
                return;
            }

            var asphalt = AssetDatabase.LoadAssetAtPath<Material>(AsphaltMatPath);
            var concrete = AssetDatabase.LoadAssetAtPath<Material>(ConcreteMatPath);
            if (asphalt == null || concrete == null)
            {
                Debug.LogError(
                    "[RoadKitPrefabBuildTool] Missing shared materials. Expected:\n" +
                    $"  {AsphaltMatPath}\n  {ConcreteMatPath}");
                return;
            }

            var fbxPaths = CollectFbxPaths(MeshesRoot);
            if (fbxPaths.Count == 0)
            {
                Debug.LogWarning($"[RoadKitPrefabBuildTool] No FBX files under {MeshesRoot}");
                return;
            }

            RoadEditorAssetUtility.EnsureFolderHierarchy(PrefabsRoot);

            var created = 0;
            var updated = 0;
            var failed = 0;

            try
            {
                for (var i = 0; i < fbxPaths.Count; i++)
                {
                    var fbxPath = fbxPaths[i];
                    var moduleName = Path.GetFileNameWithoutExtension(fbxPath);

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "RoadKit Prefabs",
                            moduleName,
                            (float)i / fbxPaths.Count))
                    {
                        Debug.Log(
                            $"[RoadKitPrefabBuildTool] Cancelled after {i} of {fbxPaths.Count}.");
                        break;
                    }

                    if (!TryProcessModule(fbxPath, asphalt, concrete, out var wasCreated))
                    {
                        failed++;
                        continue;
                    }

                    if (wasCreated)
                        created++;
                    else
                        updated++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[RoadKitPrefabBuildTool] Done — {created} created, {updated} updated, " +
                $"{failed} failed of {fbxPaths.Count} FBX.");
        }

        private static bool TryProcessModule(
            string fbxPath,
            Material asphalt,
            Material concrete,
            out bool wasCreated)
        {
            wasCreated = false;

            ConfigureModelImporter(fbxPath, asphalt, concrete);

            var modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelRoot == null)
            {
                Debug.LogError($"[RoadKitPrefabBuildTool] Could not load model: {fbxPath}");
                return false;
            }

            var prefabPath = BuildPrefabPath(fbxPath);
            var prefabFolder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(prefabFolder))
                RoadEditorAssetUtility.EnsureFolderHierarchy(prefabFolder);

            wasCreated = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null;
            if (wasCreated && !TryCreateVariantPrefab(modelRoot, prefabPath))
                return false;

            return TryPreparePrefabContents(prefabPath, asphalt, concrete);
        }

        private static bool TryCreateVariantPrefab(GameObject modelRoot, string prefabPath)
        {
            var instance = PrefabUtility.InstantiatePrefab(modelRoot) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    $"[RoadKitPrefabBuildTool] InstantiatePrefab failed for {modelRoot.name}");
                return false;
            }

            try
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out var success);
                if (!success)
                {
                    Debug.LogError($"[RoadKitPrefabBuildTool] Failed creating prefab: {prefabPath}");
                    return false;
                }

                Debug.Log($"[RoadKitPrefabBuildTool] Created variant: {prefabPath}");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool TryPreparePrefabContents(
            string prefabPath,
            Material asphalt,
            Material concrete)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogError($"[RoadKitPrefabBuildTool] Could not open prefab: {prefabPath}");
                return false;
            }

            try
            {
                RemapRendererMaterials(contents, asphalt, concrete);
                EnsureMeshColliders(contents);
                var connectionCount = CountConnectionChildren(contents);
                if (connectionCount == 0)
                {
                    Debug.LogWarning(
                        $"[RoadKitPrefabBuildTool] No Connection_* children on {contents.name}. " +
                        "Re-export FBX with empties and Preserve Hierarchy enabled.");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath, out var success);
                if (!success)
                {
                    Debug.LogError($"[RoadKitPrefabBuildTool] Failed saving prefab: {prefabPath}");
                    return false;
                }

                Debug.Log(
                    $"[RoadKitPrefabBuildTool] Prepared {Path.GetFileName(prefabPath)} " +
                    $"(connections={connectionCount})");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureModelImporter(
            string fbxPath,
            Material asphalt,
            Material concrete)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            var changed = false;

            if (!importer.preserveHierarchy)
            {
                importer.preserveHierarchy = true;
                changed = true;
            }

            if (importer.addCollider)
            {
                importer.addCollider = false;
                changed = true;
            }

            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                changed = true;
            }

            if (importer.materialLocation != ModelImporterMaterialLocation.External)
            {
                importer.materialLocation = ModelImporterMaterialLocation.External;
                changed = true;
            }

            changed |= RemapImporterMaterial(importer, "Mat_Asphalt", asphalt);
            changed |= RemapImporterMaterial(importer, "Mat_Concrete", concrete);
            // Blender/FBX may strip the Mat_ prefix on import.
            changed |= RemapImporterMaterial(importer, "Asphalt", asphalt);
            changed |= RemapImporterMaterial(importer, "Concrete", concrete);

            if (changed)
                importer.SaveAndReimport();
        }

        private static bool RemapImporterMaterial(
            ModelImporter importer,
            string sourceName,
            Material material)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName);
            var map = importer.GetExternalObjectMap();
            if (map.TryGetValue(id, out var current) && current == material)
                return false;

            importer.AddRemap(id, material);
            return true;
        }

        private static void RemapRendererMaterials(
            GameObject root,
            Material asphalt,
            Material concrete)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var mats = renderer.sharedMaterials;
                var dirty = false;

                for (var m = 0; m < mats.Length; m++)
                {
                    var resolved = ResolveKitMaterial(mats[m], asphalt, concrete);
                    if (resolved == null || resolved == mats[m])
                        continue;

                    mats[m] = resolved;
                    dirty = true;
                }

                if (dirty)
                    renderer.sharedMaterials = mats;
            }
        }

        private static Material ResolveKitMaterial(
            Material current,
            Material asphalt,
            Material concrete)
        {
            if (current == null)
                return null;

            var name = current.name;
            if (name.IndexOf("Asphalt", StringComparison.OrdinalIgnoreCase) >= 0)
                return asphalt;
            if (name.IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0)
                return concrete;

            return null;
        }

        private static void EnsureMeshColliders(GameObject root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter.sharedMesh == null)
                    continue;

                var go = filter.gameObject;
                if (IsConnectionOrMarker(go.name))
                    continue;

                var collider = go.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = go.AddComponent<MeshCollider>();

                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }
        }

        private static int CountConnectionChildren(GameObject root)
        {
            var count = 0;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (IsConnectionOrMarker(transforms[i].name))
                    count++;
            }

            return count;
        }

        private static bool IsConnectionOrMarker(string objectName)
        {
            for (var i = 0; i < ConnectionPrefixes.Length; i++)
            {
                if (objectName.StartsWith(ConnectionPrefixes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static List<string> CollectFbxPaths(string rootFolder)
        {
            var results = new List<string>(16);
            var guids = AssetDatabase.FindAssets("t:Model", new[] { rootFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;
                results.Add(path);
            }

            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }

        private static string BuildPrefabPath(string fbxPath)
        {
            // Meshes/.../RoadKit/Road/Foo.fbx → Prefabs/.../RoadKit/Road/Foo.prefab
            var relative = fbxPath.Substring(MeshesRoot.Length).TrimStart('/', '\\');
            var withoutExt = Path.ChangeExtension(relative, ".prefab");
            return (PrefabsRoot + "/" + withoutExt).Replace('\\', '/');
        }
    }
}
#endif
