using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Hydrology planning, carving, and water-crossing gate thresholds.</summary>
    [CreateAssetMenu(
        fileName = "HydrologyProfile",
        menuName = "Zombera/World/Hydrology Profile")]
    public sealed class HydrologyProfile : ScriptableObject
    {
        [Header("Planning")]
        [SerializeField] private float seaLevelWorldY;
        [SerializeField] private float cellSizeMeters = 16f;
        [SerializeField] private float riverFlowThreshold = 48f;
        [SerializeField] private float minimumRiverLengthMeters = 120f;
        [SerializeField, Min(0f)] private float riverPlacementCoastBufferMeters;
        [SerializeField, Min(0f)] private float riverPlacementObstacleBufferMeters = 10f;
        [SerializeField, Min(1)] private int maxRiverSystems = 2;
        [SerializeField, Min(1)] private int maxVisibleRiverBranches = 6;
        [SerializeField, Min(0f)] private float outletClusterRadiusMeters = 400f;
        [SerializeField, Range(0f, 1f)] private float secondarySystemScoreRatio = 0.45f;
        [SerializeField, Min(0f)] private float primaryOutletSeparationMeters = 1500f;
        [SerializeField, Min(0f)] private float primaryRiverMinimumLengthMeters = 3000f;
        [SerializeField, Min(0f)] private float tributaryMinimumLengthMeters = 500f;
        [SerializeField, Range(0f, 1f)] private float tributaryMinimumFlowFraction = 0.12f;
        [SerializeField, Min(0)] private int maxTributariesPerSystem = 4;
        [SerializeField, Min(0f)] private float visibleHeadwaterFlowThreshold = 48f;
        [Tooltip(
            "Among upstream inflows within this fraction of the strongest candidate flow, " +
            "prefer the lowest terrain cell (valley following).")]
        [SerializeField, Range(0.05f, 1f)] private float valleyFlowPreference = 0.7f;
        [SerializeField, Min(0f)] private float cityWaterSetbackMeters = 64f;
        [SerializeField, Min(0f)] private float waterfrontPreferenceDistanceMeters = 500f;

        [Header("River Geometry")]
        [SerializeField] private float minRiverWidthMeters = 4f;
        [SerializeField] private float maxRiverWidthMeters = 72f;
        [SerializeField] private float minRiverDepthMeters = 0.4f;
        [SerializeField] private float maxRiverDepthMeters = 8f;
        [SerializeField] private AnimationCurve riverWidthByAccumulation =
            new AnimationCurve(
                new Keyframe(0f, 0.05f),
                new Keyframe(0.5f, 0.35f),
                new Keyframe(1f, 1f));
        [SerializeField] private AnimationCurve riverDepthByAccumulation =
            new AnimationCurve(
                new Keyframe(0f, 0.05f),
                new Keyframe(0.5f, 0.35f),
                new Keyframe(1f, 1f));

        [Header("Lakes / Shores")]
        [SerializeField] private float lakeMinAreaMetersSq = 40000f;
        [SerializeField] private float lakeMinFillDepthMeters = 0.75f;
        [SerializeField, Min(0f)] private float lakeMinimumVolumeMetersCubed = 50000f;
        [SerializeField, Min(0f)] private float lakeMaximumAreaFraction = 0.005f;
        [SerializeField, Min(0f)] private float lakeMaximumTotalAreaFraction = 0.01f;
        [SerializeField, Min(1f)] private float lakeMaximumAspectRatio = 6f;
        [SerializeField, Min(0)] private int maxVisibleLakes = 3;
        [SerializeField, Min(0f)] private float lakeMinimumDepthMeters = 2f;
        [SerializeField, Min(0f)] private float lakeMinimumCoastDistanceMeters = 300f;
        [SerializeField] private bool requireLakeRiverConnection = true;
        [SerializeField] private float shoreBlendWidthMeters = 18f;
        [SerializeField] private float carveShoulderWidthMeters = 36f;
        [SerializeField] private float riverCarveStrength = 0.9f;
        [SerializeField, Min(1f)] private float riverMouthFlareMultiplier = 1.5f;
        [SerializeField, Range(0f, 1f)] private float riverMouthFlareFraction = 0.08f;
        [SerializeField, Min(0f)] private float meanderMaximumBankWidths = 2.5f;
        [SerializeField, Min(0f)] private float meanderMinimumValleySlopeDegrees = 5f;
        [SerializeField, Min(0f)] private float meanderWavelengthMeters = 420f;
        [Tooltip(
            "Upstream river tracing and sea-flood carve stop above sea + this. " +
            "Keeps coast-origin rivers on low land instead of cutting mountains.")]
        [SerializeField, Min(4f)] private float maxRiverLandElevationAboveSeaMeters = 120f;
        [Tooltip(
            "Low-land Crest flood: lake basin bed floor is at most sea − this. " +
            "Only applied on cells within MaxRiverLandElevationAboveSea.")]
        [SerializeField, Min(0.5f)] private float minLakeBedDepthBelowSea = 4f;
        [Tooltip(
            "Low-land Crest flood: river bed floor is at most sea − this. " +
            "Only applied on cells within MaxRiverLandElevationAboveSea.")]
        [SerializeField, Min(0.25f)] private float minRiverBedDepthBelowSea = 2f;
        [Tooltip("Bumped when carve/bathymetry policy changes so fingerprints invalidate old seeds.")]
        [SerializeField] private int hydrologyAlgorithmVersion = 10;
        [Tooltip("Bumped when ocean water-crossing scan/policy changes so fingerprints invalidate old seeds.")]
        [SerializeField] private int waterCrossingAlgorithmVersion = 3;

        [Header("Ford Gate")]
        [SerializeField] private float fordMaxDepthMeters = 0.3f;
        [SerializeField] private float fordMaxWidthMeters = 12f;
        [SerializeField] private float fordMaxBankSlopeDegrees = 5f;

        [Header("Causeway Gate (Highway/Arterial)")]
        [SerializeField] private float causewayMaxDepthMeters = 1.5f;
        [SerializeField] private float causewayMaxWidthMeters = 40f;

        [Header("Bridge Corridor Pathfinding")]
        [Tooltip("Max water depth pathfinding may enter for highway/arterial (committed bridge/causeway spans).")]
        [SerializeField] private float bridgeCorridorMaxDepthMeters = 18f;
        [Tooltip("Soft cost multiplier applied as waterDepth * this value (higher = prefer dry detours).")]
        [SerializeField] private float waterSoftCostPerMeterDepth = 28f;
        [Tooltip("Deck clearance above the higher bank sample when resolving Bridge DeckWorldY.")]
        [SerializeField] private float bridgeDeckClearanceMeters = 1.5f;

        public float SeaLevelWorldY => seaLevelWorldY;
        public float CellSizeMeters => cellSizeMeters;
        public float RiverFlowThreshold => riverFlowThreshold;
        public float MinimumRiverLengthMeters => minimumRiverLengthMeters;
        public float RiverPlacementCoastBufferMeters => riverPlacementCoastBufferMeters;
        public float RiverPlacementObstacleBufferMeters => riverPlacementObstacleBufferMeters;
        public int MaxRiverSystems => maxRiverSystems;
        public int MaxVisibleRiverBranches => maxVisibleRiverBranches;
        public float OutletClusterRadiusMeters => outletClusterRadiusMeters;
        public float SecondarySystemScoreRatio => secondarySystemScoreRatio;
        public float PrimaryOutletSeparationMeters => primaryOutletSeparationMeters;
        public float PrimaryRiverMinimumLengthMeters => primaryRiverMinimumLengthMeters;
        public float TributaryMinimumLengthMeters => tributaryMinimumLengthMeters;
        public float TributaryMinimumFlowFraction => tributaryMinimumFlowFraction;
        public int MaxTributariesPerSystem => maxTributariesPerSystem;
        public float VisibleHeadwaterFlowThreshold => visibleHeadwaterFlowThreshold;
        public float ValleyFlowPreference => valleyFlowPreference;
        public float CityWaterSetbackMeters => cityWaterSetbackMeters;
        public float WaterfrontPreferenceDistanceMeters => waterfrontPreferenceDistanceMeters;

        public float MinRiverWidthMeters => minRiverWidthMeters;
        public float MaxRiverWidthMeters => maxRiverWidthMeters;
        public float MinRiverDepthMeters => minRiverDepthMeters;
        public float MaxRiverDepthMeters => maxRiverDepthMeters;
        public AnimationCurve RiverWidthByAccumulation => riverWidthByAccumulation;
        public AnimationCurve RiverDepthByAccumulation => riverDepthByAccumulation;

        public float LakeMinAreaMetersSq => lakeMinAreaMetersSq;
        public float LakeMinFillDepthMeters => lakeMinFillDepthMeters;
        public float LakeMinimumVolumeMetersCubed => lakeMinimumVolumeMetersCubed;
        public float LakeMaximumAreaFraction => lakeMaximumAreaFraction;
        public float LakeMaximumTotalAreaFraction => lakeMaximumTotalAreaFraction;
        public float LakeMaximumAspectRatio => lakeMaximumAspectRatio;
        public int MaxVisibleLakes => maxVisibleLakes;
        public float LakeMinimumDepthMeters => lakeMinimumDepthMeters;
        public float LakeMinimumCoastDistanceMeters => lakeMinimumCoastDistanceMeters;
        public bool RequireLakeRiverConnection => requireLakeRiverConnection;
        public float ShoreBlendWidthMeters => shoreBlendWidthMeters;
        public float CarveShoulderWidthMeters => carveShoulderWidthMeters;
        public float RiverCarveStrength => riverCarveStrength;
        public float RiverMouthFlareMultiplier => riverMouthFlareMultiplier;
        public float RiverMouthFlareFraction => riverMouthFlareFraction;
        public float MeanderMaximumBankWidths => meanderMaximumBankWidths;
        public float MeanderMinimumValleySlopeDegrees => meanderMinimumValleySlopeDegrees;
        public float MeanderWavelengthMeters => meanderWavelengthMeters;
        public float MaxRiverLandElevationAboveSeaMeters => maxRiverLandElevationAboveSeaMeters;
        public float MinLakeBedDepthBelowSea => minLakeBedDepthBelowSea;
        public float MinRiverBedDepthBelowSea => minRiverBedDepthBelowSea;
        public int HydrologyAlgorithmVersion => hydrologyAlgorithmVersion;
        public int WaterCrossingAlgorithmVersion => waterCrossingAlgorithmVersion;

        public float FordMaxDepthMeters => fordMaxDepthMeters;
        public float FordMaxWidthMeters => fordMaxWidthMeters;
        public float FordMaxBankSlopeDegrees => fordMaxBankSlopeDegrees;

        public float CausewayMaxDepthMeters => causewayMaxDepthMeters;
        public float CausewayMaxWidthMeters => causewayMaxWidthMeters;

        public float BridgeCorridorMaxDepthMeters => bridgeCorridorMaxDepthMeters;
        public float WaterSoftCostPerMeterDepth => waterSoftCostPerMeterDepth;
        public float BridgeDeckClearanceMeters => bridgeDeckClearanceMeters;
    }
}
