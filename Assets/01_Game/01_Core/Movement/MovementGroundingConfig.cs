#region

using System;
using UnityEngine;

#endregion

namespace Zombera.Systems
{
    [Serializable]
    public sealed class GroundProbeConfig
    {
        [Tooltip("Layers for terrain raycasts. Leave empty to auto-use Ground/Terrain/Default.")]
        public LayerMask layers;

        [Min(0.1f)] public float probeUpMeters = 3f;
        [Min(1f)] public float probeDownMeters = 120f;

        [Tooltip("Pivot Y offset after terrain hit. Tune until feet touch the ground.")]
        public float footPivotOffsetY;
    }

    [Serializable]
    public sealed class FootHeightConfig
    {
        [Tooltip("Use NavMesh XZ for pathing but terrain Y for foot contact.")]
        public bool terrainAuthority = true;

        [Min(0.05f)] public float maxVerticalDelta = 1.5f;
        [Min(0.1f)] public float navSampleUpMeters = 2f;

        [Tooltip("NavMesh sample radii in meters, smallest to largest.")]
        public float[] navSampleRadiiMeters = { 2f, 6f, 15f };
    }

    [Serializable]
    public sealed class NavMeshBakeConfig
    {
        [Min(0.1f)] public float voxelSize = 0.25f;
        [Range(64, 1024)] public int tileSize = 256;
        [Range(0f, 75f)] public float maxSlopeDegrees = 45f;
        [Min(0f)] public float stepHeightMeters = 0.75f;
        [Min(0f)] public float minRegionArea = 2f;
        [Min(20f)] public float verticalHalfExtent = 180f;
        [Min(0f)] public float tileBoundsPadding = 16f;
    }

    [Serializable]
    public sealed class RuntimeFootSnapConfig
    {
        [Tooltip("Continuous snap-down while moving. Usually off when Terrain Authority is enabled.")]
        public bool enabled;

        [Min(0.01f)] public float threshold = 0.2f;
        [Min(0.1f)] public float speed = 8f;
        [Min(0.02f)] public float interval = 0.12f;
    }

    [Serializable]
    public sealed class FallbackMovementConfig
    {
        public bool enabled = true;
        [Min(0.01f)] public float threshold = 0.35f;
        [Min(0.1f)] public float speed = 12f;
        [Min(0.02f)] public float interval = 0.1f;
    }

    [Serializable]
    public sealed class TileSeamConfig
    {
        [Min(0.01f)] public float verticalDeltaThreshold = 0.45f;
    }

    [Serializable]
    public sealed class GroundingDiagnosticsConfig
    {
        public bool logDestinations;
        public bool logUnitState;
    }
}
