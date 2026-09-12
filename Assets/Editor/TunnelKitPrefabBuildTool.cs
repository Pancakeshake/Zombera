#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    /// Builds prefabs for the modular highway tunnel FBX kit and the
    /// collider-only portal gate used by Track B non-enterable v1.
    /// </summary>
    public static class TunnelKitPrefabBuildTool
    {
        private const string MeshesRoot = "Assets/02_Shared/Meshes/Roads/Tunnel";
        private const string PrefabsRoot = "Assets/02_Shared/Prefabs/Props/Road/Tunnel";
        private const string ExteriorMatPath =
            "Assets/02_Shared/Materials/Generic/Concrete/concrete_floor_worn_001.mat";
        private const string InteriorMatPath =
            "Assets/02_Shared/Materials/Generic/Concrete/asphalt_02.mat";
        private const string GatePrefabPath = PrefabsRoot + "/Tunnel_PortalGate.prefab";

        [MenuItem(MenuPaths.WorldRoads + "Create or Update Tunnel Kit Prefabs", priority = -899)]
        private static void CreateOrUpdateTunnelKitPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(MeshesRoot))
            {
                Debug.LogError($"[TunnelKitPrefabBuildTool] Missing meshes folder: {MeshesRoot}");
                return;
            }

            var exterior = AssetDatabase.LoadAssetAtPath<Material>(ExteriorMatPath);
            var interior = AssetDatabase.LoadAssetAtPath<Material>(InteriorMatPath);
            if (exterior == null || interior == null)
            {
                Debug.LogError(
                    "[TunnelKitPrefabBuildTool] Missing materials:\n" +
                    $"  {ExteriorMatPath}\n  {InteriorMatPath}");
                return;
            }

            RoadEditorAssetUtility.EnsureFolderHierarchy(PrefabsRoot);

            var fbxPaths = CollectTunnelFbxPaths();
            var created = 0;
            var updated = 0;
            var failed = 0;

            for (var i = 0; i < fbxPaths.Count; i++)
            {
                if (!TryProcessFbx(fbxPaths[i], exterior, interior, out var wasCreated))
                {
                    failed++;
                    continue;
                }

                if (wasCreated)
                    created++;
                else
                    updated++;
            }

            if (!TryCreateOrUpdatePortalGate())
                failed++;
            else if (AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath) != null)
                updated++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[TunnelKitPrefabBuildTool] Done — {created} created, {updated} updated, " +
                $"{failed} failed (fbx={fbxPaths.Count} + gate).");
        }

        private static bool TryProcessFbx(
            string fbxPath,
            Material exterior,
            Material interior,
            out bool wasCreated)
        {
            wasCreated = false;
            ConfigureModelImporter(fbxPath, exterior, interior);

            var modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelRoot == null)
            {
                Debug.LogError($"[TunnelKitPrefabBuildTool] Could not load: {fbxPath}");
                return false;
            }

            var prefabPath = PrefabsRoot + "/" + Path.GetFileNameWithoutExtension(fbxPath) + ".prefab";
            wasCreated = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null;
            if (wasCreated && !TryCreateVariantPrefab(modelRoot, prefabPath))
                return false;

            return TryPrepareVisualPrefab(prefabPath, exterior, interior);
        }

        private static bool TryCreateVariantPrefab(GameObject modelRoot, string prefabPath)
        {
            var instance = PrefabUtility.InstantiatePrefab(modelRoot) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"[TunnelKitPrefabBuildTool] Instantiate failed for {modelRoot.name}");
                return false;
            }

            try
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out var success);
                if (!success)
                {
                    Debug.LogError($"[TunnelKitPrefabBuildTool] Create failed: {prefabPath}");
                    return false;
                }

                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool TryPrepareVisualPrefab(
            string prefabPath,
            Material exterior,
            Material interior)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
                return false;

            try
            {
                RemapRendererMaterials(contents, exterior, interior);
                var isMid = prefabPath.IndexOf("Mid", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isMid)
                {
                    EnsureMeshColliders(contents);
                    Zombera.World.CityPipeline.WorldBuilder.InfrastructureKitSockets.EnsureMidSockets(contents);
                    var count = contents.GetComponentsInChildren<MeshCollider>(true).Length;
                    Debug.Log($"[TunnelKitPrefabBuildTool] Mid MeshColliders after ensure: {count} ({prefabPath})");
                }
                else
                {
                    StripColliders(contents);
                    Zombera.World.CityPipeline.WorldBuilder.InfrastructureKitSockets.EnsurePortalSockets(contents);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath, out var success);
                return success;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static bool TryCreateOrUpdatePortalGate()
        {
            var root = new GameObject("Tunnel_PortalGate");
            try
            {
                // Clear width 14 x clear height 5.5 x depth 1; pivot at bed centerline.
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 2.75f, 0f);
                box.size = new Vector3(14f, 5.5f, 1f);

                PrefabUtility.SaveAsPrefabAsset(root, GatePrefabPath, out var success);
                if (!success)
                {
                    Debug.LogError($"[TunnelKitPrefabBuildTool] Gate save failed: {GatePrefabPath}");
                    return false;
                }

                Debug.Log($"[TunnelKitPrefabBuildTool] Portal gate: {GatePrefabPath}");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureModelImporter(
            string fbxPath,
            Material exterior,
            Material interior)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            var changed = false;
            var isMid = fbxPath.IndexOf("Mid", StringComparison.OrdinalIgnoreCase) >= 0;
            // Mid: generate MeshCollider on import so enterable floors exist even before prefab prep.
            if (importer.addCollider != isMid)
            {
                importer.addCollider = isMid;
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

            changed |= RemapImporterMaterial(importer, "M_Tunnel_Exterior", exterior);
            changed |= RemapImporterMaterial(importer, "M_Tunnel_Interior", interior);
            changed |= RemapImporterMaterial(importer, "Tunnel_Exterior", exterior);
            changed |= RemapImporterMaterial(importer, "Tunnel_Interior", interior);

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
            Material exterior,
            Material interior)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                var dirty = false;
                for (var m = 0; m < mats.Length; m++)
                {
                    var resolved = ResolveTunnelMaterial(mats[m], exterior, interior);
                    if (resolved == null || resolved == mats[m])
                        continue;
                    mats[m] = resolved;
                    dirty = true;
                }

                if (dirty)
                    renderers[i].sharedMaterials = mats;
            }
        }

        private static Material ResolveTunnelMaterial(
            Material current,
            Material exterior,
            Material interior)
        {
            if (current == null)
                return null;

            var name = current.name;
            if (name.IndexOf("Interior", StringComparison.OrdinalIgnoreCase) >= 0)
                return interior;
            if (name.IndexOf("Exterior", StringComparison.OrdinalIgnoreCase) >= 0)
                return exterior;
            if (name.IndexOf("Tunnel", StringComparison.OrdinalIgnoreCase) >= 0)
                return exterior;

            return null;
        }

        private static void EnsureMeshColliders(GameObject root)
        {
            // Enterable mid: keep MeshColliders on floor/shell for physics + NavMesh floors.
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var col = filter.GetComponent<MeshCollider>();
                if (col == null)
                    col = filter.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = filter.sharedMesh;
                col.convex = false;
            }
        }

        private static void StripColliders(GameObject root)
        {
            // Portals stay visual-only; non-enterable uses Tunnel_PortalGate.
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                UnityEngine.Object.DestroyImmediate(colliders[i]);
        }

        private static List<string> CollectTunnelFbxPaths()
        {
            var results = new List<string>(4);
            var guids = AssetDatabase.FindAssets("t:Model", new[] { MeshesRoot });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (path.IndexOf("/Source/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                results.Add(path);
            }

            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }
    }
}
#endif
