#if UNITY_EDITOR
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>Guards partial hub runs when artifacts or terrains are missing.</summary>
    internal static class StyleMatchPartialPipelineBootstrap
    {
        public static WorldBuildStageId Resolve(WorldBuildStageId requested, CityPrefabRoadNetworkBuilder builder)
        {
            if (requested <= WorldBuildStageId.ResetGeneratedWorld)
                return requested;

            var window = ScriptableObject.CreateInstance<CityPipelineRunnerWindow>();
            try
            {
                window.SetBuilder(builder);
                return Resolve(requested, window.TryGetWorldBuilderService());
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        public static WorldBuildStageId Resolve(WorldBuildStageId requested, WorldBuilderService service)
        {
            if (requested <= WorldBuildStageId.ResetGeneratedWorld)
                return requested;

            if (!HasAllocatedTerrains(service))
            {
                Debug.LogWarning(
                    "[StyleMatch] Partial start " + requested +
                    " needs terrains — bootstrapping from ResetGeneratedWorld.");
                return WorldBuildStageId.ResetGeneratedWorld;
            }

            if (requested >= WorldBuildStageId.SolveHydrology && service?.Artifacts?.Hydrology == null)
            {
                Debug.LogWarning(
                    "[StyleMatch] Partial start " + requested +
                    " needs hydrology — bootstrapping from ResetGeneratedWorld.");
                return WorldBuildStageId.ResetGeneratedWorld;
            }

            if (requested >= WorldBuildStageId.ClassifyBiomesAndBuildability &&
                !HasPaintArtifacts(service))
            {
                Debug.LogWarning(
                    "[StyleMatch] Partial start " + requested +
                    " needs landforms/biomes — bootstrapping from ResetGeneratedWorld.");
                return WorldBuildStageId.ResetGeneratedWorld;
            }

            return requested;
        }

        public static bool IsSceneReadyForStyleMatchCapture(WorldBuilderService service, out string detail)
        {
            detail = null;
            if (!HasPaintArtifacts(service))
            {
                detail = "artifacts missing — run pipeline first";
                return false;
            }

            if (!HasAllocatedTerrains(service))
            {
                detail = "no allocated terrains";
                return false;
            }

            detail = "ok";
            return true;
        }

        private static bool HasPaintArtifacts(WorldBuilderService service) =>
            service?.Artifacts?.Landforms != null && service.Artifacts.Biomes != null;

        private static bool HasAllocatedTerrains(WorldBuilderService service)
        {
            var catalog = service?.TileCatalog;
            return catalog != null && catalog.TryGetWorldBoundsXZ(out _, requireAllocatedTerrain: true);
        }
    }
}
#endif
