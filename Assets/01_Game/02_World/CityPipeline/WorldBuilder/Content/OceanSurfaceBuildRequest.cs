using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Inputs for Crest ocean placement (full-map square or map-edge strips).</summary>
    public readonly struct OceanSurfaceBuildRequest
    {
        public HydrologyPlan Hydrology { get; }
        public WorldBuildScope Scope { get; }
        public WorldMapBoundaryLayout BoundaryLayout { get; }
        public float StripDepthMeters { get; }

        /// <summary>Authoritative Crest WaterBody / depth-cache AABB (session full bounds).</summary>
        public Rect SurfaceBoundsXZ { get; }

        /// <summary>Playable core (excludes ocean ring). Falls back to <see cref="SurfaceBoundsXZ"/> when empty.</summary>
        public Rect CoreWorldBoundsXZ { get; }

        public int OceanRingTiles { get; }

        public OceanSurfaceBuildRequest(
            HydrologyPlan hydrology,
            WorldBuildScope scope,
            WorldMapBoundaryLayout boundaryLayout,
            float stripDepthMeters,
            Rect surfaceBoundsXZ = default,
            int oceanRingTiles = 0,
            Rect coreWorldBoundsXZ = default)
        {
            Hydrology = hydrology;
            Scope = scope;
            BoundaryLayout = boundaryLayout;
            StripDepthMeters = stripDepthMeters;
            SurfaceBoundsXZ = surfaceBoundsXZ.width > 0f && surfaceBoundsXZ.height > 0f
                ? surfaceBoundsXZ
                : scope.BoundsXZ;
            OceanRingTiles = Mathf.Max(0, oceanRingTiles);
            CoreWorldBoundsXZ = coreWorldBoundsXZ.width > 0f && coreWorldBoundsXZ.height > 0f
                ? coreWorldBoundsXZ
                : SurfaceBoundsXZ;
        }
    }
}
