#region

using UnityEngine;
using UnityEngine.AI;

#endregion

namespace Zombera.Systems
{
    [CreateAssetMenu(fileName = "MovementGroundingProfile", menuName = "Zombera/Movement/Grounding Profile")]
    public sealed partial class MovementGroundingProfile : ScriptableObject
    {
        [Header("1 · Ground Probe")]
        [SerializeField] private GroundProbeConfig ground = new();

        [Header("2 · Foot Height")]
        [SerializeField] private FootHeightConfig height = new();

        [Header("3 · Streamed NavMesh Bake")]
        [SerializeField] private NavMeshBakeConfig bake = new();

        [Header("4 · Runtime Foot Snap (Player)")]
        [SerializeField] private RuntimeFootSnapConfig footSnap = new();

        [Header("5 · Fallback Movement")]
        [SerializeField] private FallbackMovementConfig fallback = new();

        [Header("6 · Tile Seams")]
        [SerializeField] private TileSeamConfig seam = new();

        [Header("Diagnostics")]
        [SerializeField] private GroundingDiagnosticsConfig diagnostics = new();

        public GroundProbeConfig Ground => ground;
        public FootHeightConfig Height => height;
        public NavMeshBakeConfig Bake => bake;
        public RuntimeFootSnapConfig FootSnap => footSnap;
        public FallbackMovementConfig Fallback => fallback;
        public TileSeamConfig Seam => seam;
        public GroundingDiagnosticsConfig Diagnostics => diagnostics;

        // Legacy accessors — keep call sites stable while Inspector uses grouped config.
        public LayerMask GroundLayerMask => ground.layers;
        public float GroundProjectUpOffset => ground.probeUpMeters;
        public float GroundProjectDownDistance => ground.probeDownMeters;
        public float FootGroundOffsetY => ground.footPivotOffsetY;
        public float NavSampleUpOffset => height.navSampleUpMeters;
        public float MaxNavMeshVerticalDeltaFromGround => height.maxVerticalDelta;
        public bool PreferTerrainHeightOverNavMesh => height.terrainAuthority;

        public bool PreferTerrainHeightOverNavMeshAt(Vector3 worldPosition) =>
            UndergroundTraversalGate.ShouldPreferTerrainHeight(height.terrainAuthority, worldPosition);
        public float NavMeshVoxelSize => bake.voxelSize;
        public int NavMeshTileSize => bake.tileSize;
        public float NavMeshMaxSlopeDegrees => bake.maxSlopeDegrees;
        public float NavMeshStepHeightMeters => bake.stepHeightMeters;
        public float NavMeshMinRegionArea => bake.minRegionArea;
        public float NavMeshVerticalHalfExtent => bake.verticalHalfExtent;
        public float NavMeshTileBoundsPadding => bake.tileBoundsPadding;
        public bool EnableRuntimeFootGrounding => footSnap.enabled;
        public float FootGroundSnapThreshold => footSnap.threshold;
        public float FootGroundSnapSpeed => footSnap.speed;
        public float FootGroundInterval => footSnap.interval;
        public bool EnableFallbackTerrainClamp => fallback.enabled;
        public float FallbackGroundClampThreshold => fallback.threshold;
        public float FallbackGroundClampSpeed => fallback.speed;
        public float FallbackGroundClampInterval => fallback.interval;
        public float SeamVerticalDeltaThreshold => seam.verticalDeltaThreshold;
        public bool LogMovementDestinationResolution => diagnostics.logDestinations;
        public bool LogUnitGroundingState => diagnostics.logUnitState;

        private void OnEnable() => RebuildCache();
        private void OnValidate() => RebuildCache();

        public LayerMask ResolveGroundMask(LayerMask requestMask = default)
        {
            if (requestMask.value != 0 && requestMask.value != -1)
                return requestMask;

            EnsureCache();
            return _resolvedGroundMask;
        }

        public void ApplyNavMeshBuildSettings(ref NavMeshBuildSettings settings)
        {
            settings.overrideVoxelSize = true;
            settings.voxelSize = Mathf.Max(0.1f, bake.voxelSize);
            settings.overrideTileSize = true;
            settings.tileSize = Mathf.Clamp(bake.tileSize, 64, 1024);
            settings.agentSlope = Mathf.Clamp(bake.maxSlopeDegrees, 0f, 75f);
            settings.agentClimb = Mathf.Max(0f, bake.stepHeightMeters);
            settings.minRegionArea = Mathf.Max(0f, bake.minRegionArea);
        }
    }
}
