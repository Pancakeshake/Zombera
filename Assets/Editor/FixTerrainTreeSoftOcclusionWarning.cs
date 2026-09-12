#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    /// URP-safe fix for Terrain Soft Occlusion warnings on third-party sample trees.
    /// Soft Occlusion shaders are Built-in-only (pink in URP); Terrain skips that path when the
    /// tree prefab has an LODGroup, so we wrap the models and retarget TerrainData prototypes.
    /// </summary>
    internal static class FixTerrainTreeSoftOcclusionWarning
    {
        private const string PrefabFolder = "Assets/03_ThirdParty/_ZomberaFixes/TerrainTreeLods";

        private static readonly (string SourcePath, string PrefabName)[] Trees =
        {
            (
                "Assets/03_ThirdParty/Mind Code Interactive/Easy Build System/Packages/Samples/Shared/Art/Models/Stylized Nature/Models/SM_Birch_A.fbx",
                "SM_Birch_A_TerrainLod"
            ),
            (
                "Assets/03_ThirdParty/Mind Code Interactive/Easy Build System/Packages/Samples/Shared/Art/Models/Stylized Nature/Models/SM_Bush_A.fbx",
                "SM_Bush_A_TerrainLod"
            ),
            (
                "Assets/03_ThirdParty/EasyRoads3D Scenes/Terrain Assets/Terrain Assets v3.1/Trees Ambient-Occlusion/Palm.fbx",
                "Palm_TerrainLod"
            ),
            (
                "Assets/02_Shared/Prefabs/Props/Nature/SM_Tree_A Variant.prefab",
                "SM_Tree_A_TerrainLod"
            ),
            (
                "Assets/02_Shared/Prefabs/Props/Nature/SM_Tree_B  Variant.prefab",
                "SM_Tree_B_TerrainLod"
            ),
            (
                "Assets/02_Shared/Prefabs/Props/Nature/SM_Tree_C Variant.prefab",
                "SM_Tree_C_TerrainLod"
            )
        };

        private const string NatureProfilePath =
            "Assets/02_Shared/ScriptableObjects/World/Profiles/WorldNatureProfile.asset";

        [MenuItem("Zombera/Fixes/Fix Terrain Tree Soft Occlusion Warnings")]
        private static void Run()
        {
            EnsureFolders();

            var prefabBySource = new Dictionary<string, GameObject>();
            foreach (var (sourcePath, prefabName) in Trees)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null)
                {
                    Debug.LogWarning($"[FixTerrainTreeSoftOcclusion] Missing source: {sourcePath}");
                    continue;
                }

                var prefabPath = $"{PrefabFolder}/{prefabName}.prefab";
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null
                    ? PrefabUtility.LoadPrefabContents(prefabPath)
                    : (GameObject)PrefabUtility.InstantiatePrefab(source);

                root.name = prefabName;
                EnsureLodGroup(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                prefabBySource[sourcePath] = saved;
            }

            RetargetTerrainDataPrototypes(prefabBySource);
            RetargetWorldNatureProfile(prefabBySource);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[FixTerrainTreeSoftOcclusion] Created/updated {prefabBySource.Count} LOD prefab(s); " +
                "TerrainData prototypes and WorldNatureProfile retargeted where matched.");
        }

        private static void RetargetTerrainDataPrototypes(Dictionary<string, GameObject> prefabBySource)
        {
            var updatedTerrains = 0;
            var updatedProtos = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TerrainData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (terrainData?.treePrototypes == null || terrainData.treePrototypes.Length == 0)
                    continue;

                var prototypes = terrainData.treePrototypes;
                var updated = RetargetPrototypePrefabs(prototypes, prefabBySource);
                if (updated <= 0)
                    continue;

                terrainData.treePrototypes = prototypes;
                EditorUtility.SetDirty(terrainData);
                updatedTerrains++;
                updatedProtos += updated;
            }

            Debug.Log(
                $"[FixTerrainTreeSoftOcclusion] Updated {updatedProtos} prototype(s) on {updatedTerrains} TerrainData asset(s).");
        }

        /// <summary>Retargets matching tree prototypes in place; returns how many entries changed.</summary>
        private static int RetargetPrototypePrefabs(
            TreePrototype[] prototypes,
            Dictionary<string, GameObject> prefabBySource)
        {
            var updated = 0;
            for (var i = 0; i < prototypes.Length; i++)
            {
                var prefab = prototypes[i].prefab;
                if (prefab == null)
                    continue;

                var sourcePath = AssetDatabase.GetAssetPath(prefab);
                if (!prefabBySource.TryGetValue(sourcePath, out var lodPrefab))
                    continue;
                if (prototypes[i].prefab == lodPrefab)
                    continue;

                prototypes[i].prefab = lodPrefab;
                updated++;
            }

            return updated;
        }

        private static void RetargetWorldNatureProfile(Dictionary<string, GameObject> prefabBySource)
        {
            var profile = AssetDatabase.LoadAssetAtPath<Zombera.World.CityPipeline.WorldBuilder.WorldNatureProfile>(
                NatureProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"[FixTerrainTreeSoftOcclusion] Missing nature profile: {NatureProfilePath}");
                return;
            }

            var so = new SerializedObject(profile);
            var entries = so.FindProperty("entries");
            if (entries == null || !entries.isArray)
                return;

            var updated = 0;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var prefabProp = entry.FindPropertyRelative("Prefab");
                if (prefabProp == null)
                    continue;

                var current = prefabProp.objectReferenceValue as GameObject;
                if (current == null)
                    continue;

                var sourcePath = AssetDatabase.GetAssetPath(current);
                if (!prefabBySource.TryGetValue(sourcePath, out var lodPrefab))
                    continue;
                if (current == lodPrefab)
                    continue;

                prefabProp.objectReferenceValue = lodPrefab;
                updated++;
            }

            if (updated <= 0)
                return;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            Debug.Log($"[FixTerrainTreeSoftOcclusion] Retargeted {updated} WorldNatureProfile entry prefab(s).");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/03_ThirdParty/_ZomberaFixes"))
                AssetDatabase.CreateFolder("Assets/03_ThirdParty", "_ZomberaFixes");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/03_ThirdParty/_ZomberaFixes", "TerrainTreeLods");
        }

        private static void EnsureLodGroup(GameObject root)
        {
            var lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = root.AddComponent<LODGroup>();

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            lodGroup.SetLODs(new[]
            {
                new LOD(0.3f, renderers),
                new LOD(0.01f, System.Array.Empty<Renderer>())
            });
            lodGroup.RecalculateBounds();
        }
    }
}
#endif
