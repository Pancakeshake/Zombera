#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    /// Builds prefabs for the modular highway bridge FBX kit and the
    /// collider-only portal gate used by non-enterable v1 crossings.
    /// </summary>
    public static class BridgeKitPrefabBuildTool
    {
        private const string MeshesRoot = "Assets/02_Shared/Meshes/Roads/Bridge";
        private const string PrefabsRoot = "Assets/02_Shared/Prefabs/Props/Road/Bridge";
        private const string ExteriorMatPath =
            "Assets/02_Shared/Materials/Generic/Concrete/concrete_floor_worn_001.mat";
        private const string DeckMatPath =
            "Assets/02_Shared/Materials/Generic/Concrete/asphalt_02.mat";
        private const string GatePrefabPath = PrefabsRoot + "/Bridge_PortalGate.prefab";

        [MenuItem(MenuPaths.WorldRoads + "Create or Update Bridge Kit Prefabs", priority = -898)]
        private static void CreateOrUpdateBridgeKitPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(MeshesRoot))
            {
                Debug.LogError($"[BridgeKitPrefabBuildTool] Missing meshes folder: {MeshesRoot}");
                return;
            }

            var exterior = AssetDatabase.LoadAssetAtPath<Material>(ExteriorMatPath);
            var deck = AssetDatabase.LoadAssetAtPath<Material>(DeckMatPath);
            if (exterior == null || deck == null)
            {
                Debug.LogError(
                    "[BridgeKitPrefabBuildTool] Missing materials:\n" +
                    $"  {ExteriorMatPath}\n  {DeckMatPath}");
                return;
            }

            RoadEditorAssetUtility.EnsureFolderHierarchy(PrefabsRoot);

            var fbxPaths = CollectBridgeFbxPaths();
            var created = 0;
            var updated = 0;
            var failed = 0;

            for (var i = 0; i < fbxPaths.Count; i++)
            {
                if (!TryProcessFbx(fbxPaths[i], exterior, deck, out var wasCreated))
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
                $"[BridgeKitPrefabBuildTool] Done — {created} created, {updated} updated, " +
                $"{failed} failed (fbx={fbxPaths.Count} + gate).");
        }

        private static bool TryProcessFbx(
            string fbxPath,
            Material exterior,
            Material deck,
            out bool wasCreated)
        {
            wasCreated = false;
            ConfigureModelImporter(fbxPath, exterior, deck);

            var modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelRoot == null)
            {
                Debug.LogError($"[BridgeKitPrefabBuildTool] Could not load: {fbxPath}");
                return false;
            }

            var prefabPath = PrefabsRoot + "/" + Path.GetFileNameWithoutExtension(fbxPath) + ".prefab";
            wasCreated = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null;
            if (wasCreated && !TryCreateVariantPrefab(modelRoot, prefabPath))
                return false;

            return TryPrepareVisualPrefab(prefabPath, exterior, deck);
        }

        private static bool TryCreateVariantPrefab(GameObject modelRoot, string prefabPath)
        {
            var instance = PrefabUtility.InstantiatePrefab(modelRoot) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"[BridgeKitPrefabBuildTool] Instantiate failed for {modelRoot.name}");
                return false;
            }

            try
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out var success);
                if (!success)
                {
                    Debug.LogError($"[BridgeKitPrefabBuildTool] Create failed: {prefabPath}");
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
            Material deck)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
                return false;

            try
            {
                RemapRendererMaterials(contents, exterior, deck);
                StripColliders(contents);
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
            var root = new GameObject("Bridge_PortalGate");
            try
            {
                // Clear width 14 x clear height 5.5 x depth 1; pivot at bed centerline.
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 2.75f, 0f);
                box.size = new Vector3(14f, 5.5f, 1f);

                PrefabUtility.SaveAsPrefabAsset(root, GatePrefabPath, out var success);
                if (!success)
                {
                    Debug.LogError($"[BridgeKitPrefabBuildTool] Gate save failed: {GatePrefabPath}");
                    return false;
                }

                Debug.Log($"[BridgeKitPrefabBuildTool] Portal gate: {GatePrefabPath}");
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
            Material deck)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            var changed = false;
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

            changed |= RemapImporterMaterial(importer, "M_Bridge_Exterior", exterior);
            changed |= RemapImporterMaterial(importer, "M_Bridge_Deck", deck);
            changed |= RemapImporterMaterial(importer, "M_Bridge_Rail", exterior);
            changed |= RemapImporterMaterial(importer, "Bridge_Exterior", exterior);
            changed |= RemapImporterMaterial(importer, "Bridge_Deck", deck);
            changed |= RemapImporterMaterial(importer, "Bridge_Rail", exterior);

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
            Material deck)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                var dirty = false;
                for (var m = 0; m < mats.Length; m++)
                {
                    var resolved = ResolveBridgeMaterial(mats[m], exterior, deck);
                    if (resolved == null || resolved == mats[m])
                        continue;
                    mats[m] = resolved;
                    dirty = true;
                }

                if (dirty)
                    renderers[i].sharedMaterials = mats;
            }
        }

        private static Material ResolveBridgeMaterial(
            Material current,
            Material exterior,
            Material deck)
        {
            if (current == null)
                return null;

            var name = current.name;
            if (name.IndexOf("Deck", StringComparison.OrdinalIgnoreCase) >= 0)
                return deck;
            if (name.IndexOf("Rail", StringComparison.OrdinalIgnoreCase) >= 0)
                return exterior;
            if (name.IndexOf("Exterior", StringComparison.OrdinalIgnoreCase) >= 0)
                return exterior;
            if (name.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) >= 0)
                return exterior;

            return null;
        }

        private static void StripColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                UnityEngine.Object.DestroyImmediate(colliders[i]);
        }

        private static List<string> CollectBridgeFbxPaths()
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
