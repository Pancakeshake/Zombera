#if UNITY_EDITOR
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor
{
    /// <summary>Registry-order helpers for partial hub pipeline runs.</summary>
    internal static class WorldBuildStageOrder
    {
        public static bool TryGetRegistryIndex(WorldBuildStageId id, out int index)
        {
            var stages = WorldBuildStageRegistry.Default.Stages;
            for (var i = 0; i < stages.Count; i++)
            {
                if (stages[i].Id != id)
                    continue;

                index = i;
                return true;
            }

            index = -1;
            return false;
        }

        public static bool IsBeforeOrEqual(WorldBuildStageId first, WorldBuildStageId second)
        {
            if (!TryGetRegistryIndex(first, out var firstIndex) ||
                !TryGetRegistryIndex(second, out var secondIndex))
                return false;

            return firstIndex <= secondIndex;
        }
    }
}
#endif
