#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>One-shot verify for wilderness Terrain tree placement.</summary>
    internal static class WorldNatureTerrainTreeVerify
    {
        [MenuItem("Tools/World/Verify Wilderness Nature Terrain Trees")]
        private static void Run()
        {
            var builder = Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>(FindObjectsInactive.Include);
            if (builder == null)
            {
                Debug.LogError("[NatureVerify] CityPrefabRoadNetworkBuilder not found.");
                return;
            }

            var window = ScriptableObject.CreateInstance<CityPipelineRunnerWindow>();
            try
            {
                window.SetBuilder(builder);
                window.ApplyVerticalSliceTestConfiguration();
                var ok = window.TryRunPipelineStageRangeSynchronously(
                    WorldBuildStageId.PlaceWildernessNature,
                    WorldBuildStageId.PlaceWildernessNature,
                    out var error,
                    600f);
                Debug.Log($"[NatureVerify] PlaceWildernessNature ok={ok} error={error}");
                if (!ok)
                    Debug.LogError("[NatureVerify] stage log:\n" + window.GetLastPipelineStageLog());
            }
            finally
            {
                Object.DestroyImmediate(window);
            }

            LogTreeCounts("afterRun");
        }

        [MenuItem("Tools/World/Log Wilderness Nature Tree Counts")]
        private static void LogCountsOnly() => LogTreeCounts("snapshot");

        private static void LogTreeCounts(string tag)
        {
            var terrains = Terrain.activeTerrains;
            var totalTrees = 0;
            var terrainsWithTrees = 0;
            for (var i = 0; i < terrains.Length; i++)
            {
                var data = terrains[i] != null ? terrains[i].terrainData : null;
                if (data == null)
                    continue;
                var count = data.treeInstanceCount;
                totalTrees += count;
                if (count > 0)
                    terrainsWithTrees++;
            }

            var placer = Object.FindFirstObjectByType<WorldNaturePlacer>(FindObjectsInactive.Include);
            var rootChildren = 0;
            if (placer != null)
            {
                var root = placer.transform.Find("NatureRoot");
                if (root != null)
                    rootChildren = root.childCount;
            }

            Debug.Log(
                $"[NatureVerify] {tag} treeInstances={totalTrees} " +
                $"terrainsWithTrees={terrainsWithTrees} natureRootChildren={rootChildren}");
        }
    }
}
#endif
