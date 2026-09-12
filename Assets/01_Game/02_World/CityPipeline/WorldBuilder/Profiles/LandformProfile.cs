using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Noise and thermal-erosion tuning for base landforms.</summary>
    [CreateAssetMenu(
        fileName = "LandformProfile",
        menuName = "Zombera/World/Landform Profile")]
    public sealed class LandformProfile : ScriptableObject
    {
        [Header("Seed Offsets")]
        [SerializeField] private int continentalnessSeedOffset;
        [SerializeField] private int domainWarpSeedOffset = 17;
        [SerializeField] private int hillsSeedOffset = 31;
        [SerializeField] private int mountainSeedOffset = 53;

        [Header("Scales (world meters)")]
        [SerializeField] private float continentalnessScale = 13000f;
        [SerializeField] private float domainWarpScale = 1800f;
        [SerializeField] private float hillsScale = 640f;
        [SerializeField] private float mountainScale = 1100f;

        [Header("Amplitudes (meters)")]
        [SerializeField] private float continentalnessAmplitude = 120f;
        [SerializeField] private float domainWarpAmplitude = 180f;
        [SerializeField] private float hillsAmplitude = 55f;
        [SerializeField] private float mountainAmplitude = 320f;

        [Header("Shape")]
        [SerializeField] private float plainsBias = 0.15f;
        [Tooltip("Continentalness SmoothStep low gate for mainland mask (lower = more land).")]
        [SerializeField, Range(0.05f, 0.55f)] private float mainlandMaskLo = 0.28f;
        [Tooltip("Continentalness SmoothStep high gate for mainland mask.")]
        [SerializeField, Range(0.2f, 0.85f)] private float mainlandMaskHi = 0.50f;
        [Tooltip("Highs-only soft approach inland of the beach core toward a dry shoulder. Effective width is clamped to Edge Barrier Depth.")]
        [SerializeField] private float coastFalloffWidthMeters = 2400f;
        [SerializeField] private AnimationCurve mountainMaskCurve = AnimationCurve.EaseInOut(0.45f, 0f, 1f, 1f);

        [Header("Edge Barriers")]
        [Tooltip("When true, every map edge is ocean coast. When false, each edge rolls Ocean vs Mountains from Ocean Edge Chance.")]
        [SerializeField] private bool forceOceanOnAllEdges;
        [Tooltip("Per-edge probability of Ocean when Force Ocean On All Edges is false.")]
        [SerializeField, Range(0f, 1f)] private float oceanEdgeChance = 0.55f;
        [Tooltip(
            "When false (default), Resolve forces at least one ocean edge so hydrology/Crest always have an outlet. " +
            "Set true only for explicit continental / landlocked experiments.")]
        [SerializeField] private bool allowLandlockedMaps;
        [SerializeField] private bool useMacroBiomeRegions = true;
        [SerializeField, Min(0.5f)] private float biomeForcedBoostScale = 1.2f;
        [SerializeField, Range(0.7f, 0.98f)] private float biomeDominantThreshold = 0.88f;
        [SerializeField] private float edgeBarrierDepthMeters = 2200f;
        [SerializeField] private float edgeBarrierPeakMeters = 220f;
        [SerializeField] private float edgeBarrierFalloffMeters = 350f;
        [SerializeField] private float edgeBarrierNoiseScaleMeters = 1400f;
        [SerializeField] private float edgeBarrierNoiseAmplitudeMeters = 90f;
        [SerializeField] private int edgeBarrierSeedOffset = 89;

        [Header("Ocean Edge Trenches")]
        [SerializeField] private float oceanOffshoreWidthMeters = 160f;
        [SerializeField] private float oceanShoreShelfWidthMeters = 90f;
        [Tooltip("Dry beach face from shelf toe up to the dry shoulder (meters). Target ~50–100 m.")]
        [SerializeField] private float oceanBeachWidthMeters = 80f;
        [Tooltip("Dry-shoulder / hydrology beach band above sea. Hard coast declines from this shoulder to sea (no berm); soft falloff eases taller inland cliffs down to it.")]
        [SerializeField] private float oceanBeachMaxElevationMeters = 7f;
        [SerializeField] private float oceanTrenchDepthMeters = 80f;
        [SerializeField] private float oceanTrenchSteepness = 1.55f;
        [SerializeField] private float oceanTrenchNoiseAmplitudeMeters = 18f;
        [Tooltip("Water depth (m) at the trench/shelf join (outer shelf).")]
        [SerializeField] private float oceanShelfOuterDepthMeters = 8f;
        [Tooltip("Water depth (m) at the shelf/beach join (inner shelf). Must stay underwater.")]
        [SerializeField] private float oceanShelfInnerDepthMeters = 1f;
        [Tooltip("XZ wavelength for deepen-only shelf reef blotches (meters).")]
        [SerializeField] private float oceanShelfReefNoiseScaleMeters = 180f;
        [Tooltip("Max extra deepen (m) at reef-noise peaks on the shelf.")]
        [SerializeField] private float oceanShelfReefNoiseAmplitudeMeters = 6f;
        [Tooltip("Fraction of shelf cells eligible for reef deepen (higher = more blotches).")]
        [SerializeField, Range(0f, 1f)] private float oceanShelfReefCoverage = 0.32f;
        [Tooltip("How far noise can push the shoreline inland from the square map edge (meters).")]
        [SerializeField] private float oceanCoastErosionAmplitudeMeters = 1200f;
        [Tooltip("Along-coast meander wavelength (meters). Shorter = more visible bay/headland variance.")]
        [SerializeField] private float oceanCoastErosionScaleMeters = 3500f;
        [Tooltip("Lift inland lows to at least sea + this margin so mainland valleys stay dry. 0 disables. Does not raise ocean trench/beach core.")]
        [SerializeField] private float inlandDryFloorMetersAboveSea = 4f;
        [Tooltip("Soft blend distance inland of the ocean coast strip where the dry floor ramps in (meters).")]
        [SerializeField] private float inlandDryFloorBlendMeters = 180f;

        [Header("Islands / Archipelagos")]
        [Tooltip("World-space scale for island/archipelago ridged peaks (meters). Shorter than continentalness.")]
        [SerializeField] private float islandNoiseScaleMeters = 2800f;
        [Tooltip("Secondary archipelago cluster scale (meters).")]
        [SerializeField] private float archipelagoNoiseScaleMeters = 1400f;
        [SerializeField, Range(0.45f, 0.92f)] private float islandPeakThreshold = 0.58f;
        [SerializeField, Range(0.45f, 0.95f)] private float archipelagoPeakThreshold = 0.64f;

        [Header("Interior Relief / Orogen")]
        [SerializeField] private bool interiorMountainsEnabled = true;
        [SerializeField] private int interiorMountainRangeCount = 3;
        [SerializeField] private float interiorMountainPeakMeters = 1700f;
        [SerializeField] private float interiorMountainWidthMeters = 4000f;
        [SerializeField, Range(0f, 1f)] private float interiorMountainRuggedness = 0.9f;
        [SerializeField] private int interiorMountainSeedOffset = 101;
        [SerializeField] private float interiorRollingScaleMeters = 2200f;
        [SerializeField] private float interiorRollingAmplitudeMeters = 90f;
        [SerializeField] private float interiorReliefInsetMeters = 80f;
        [SerializeField] private float interiorHillsMultiplier = 1.65f;
        [Tooltip("Minimum InteriorLandformRelief mask required before interior mountain cliff/snow tiers paint.")]
        [SerializeField, Range(0f, 1f)] private float interiorMountainPaintMinMask = 0.55f;
        [Tooltip("When true, use legacy Sand/Sand01/Dirt beach blend instead of WetSand coastal blend.")]
        [SerializeField] private bool useLegacyCoastalBlend;
        [Tooltip("Bump when compose/orogen algebra changes so saves/rebuilds detect algorithm drift.")]
        [SerializeField] private int landformAlgorithmVersion = 7;
        [Tooltip("Residual ridged detail outside orogen cores (meters). Orogen SDF owns primary mountain height.")]
        [SerializeField] private float residualMountainAmplitudeMeters = 45f;
        [Tooltip("Piedmont skirt width beyond range core half-width (meters).")]
        [SerializeField] private float foothillSkirtMeters = 850f;
        [Tooltip("Cross-section power; higher = sharper crests.")]
        [SerializeField, Range(1.2f, 4f)] private float crestSharpness = 3.1f;
        [Tooltip("How deeply saddles cut along-crest height (0–1).")]
        [SerializeField, Range(0.15f, 0.85f)] private float saddleDepth = 0.55f;
        [Tooltip("Lateral warp of orogen mid-control point as a fraction of range length.")]
        [SerializeField, Range(0f, 0.35f)] private float orogenWarpFraction = 0.25f;
        [Tooltip("Structural valley deepen between ridge limbs (meters).")]
        [SerializeField] private float structuralValleyDepthMeters = 28f;
        [Tooltip("Micro FBM/ridged detail amplitude gated by orogen/foothill (meters).")]
        [SerializeField] private float microDetailAmplitudeMeters = 12f;

        [Header("Gameplay Land Budgets")]
        [SerializeField, Range(0.05f, 0.85f)] private float maxOrogenCoverageFraction = 0.4f;
        [SerializeField, Range(0.15f, 0.95f)] private float minLowlandSlope12Fraction = 0.32f;
        [SerializeField, Min(0)] private int minPassCountPerRange = 1;
        [SerializeField] private float highwayPassMaxSlopeDegrees = 8f;
        [SerializeField] private float passCorridorHalfWidthMeters = 120f;

        [Header("Thermal Erosion")]
        [SerializeField] private int thermalErosionIterations = 28;
        [SerializeField] private float thermalTalusAngleDegrees = 32f;
        [SerializeField] private float thermalTransferFraction = 0.32f;

        [Header("City Pad Grading")]
        [Tooltip("Bump when pad apron/cone algebra changes so fingerprints detect algorithm drift.")]
        [SerializeField] private int cityPadAlgorithmVersion = 10;
        [Tooltip("Meters beyond the arterial ring kept perfectly flat (planning-field freeze core).")]
        [SerializeField, Min(0f)] private float cityPadFlatMarginMeters = 30f;
        [Tooltip("Noisy eroded plateau escarpment width outside the flat core (meters).")]
        [SerializeField, Min(40f)] private float cityPadPlateauSlopeMeters = 180f;
        [SerializeField, Min(1f)] private float cityPadFalloffMinMeters = 160f;
        [SerializeField, Min(1f)] private float cityPadFalloffMaxMeters = 220f;
        [SerializeField, Range(0f, 0.45f)] private float cityPadCornerRadiusFraction = 0.2f;
        [SerializeField, Min(0f)] private float cityPadHydrologyPruneMarginMeters = 16f;
        [Tooltip("Pad escarpment talus angle (degrees). Higher = steeper natural plateau faces.")]
        [SerializeField, Range(1f, 35f)] private float cityPadApproachSlopeDegrees = 18f;
        [Tooltip("Reject sites / hard-cap cones when uncapped continuity at FalloffMax exceeds this.")]
        [SerializeField, Range(5f, 45f)] private float cityPadMaxContinuityDegrees = 25f;
        [Tooltip("When enabled, runs scoped thermal relax on pad aprons after flatten + highway re-carve.")]
        [SerializeField] private bool cityPadEdgeRelaxEnabled = true;
        [Tooltip("Hard cap on adaptive pad-edge talus iterations (0 = uncapped aside from internal max).")]
        [SerializeField, Min(0)] private int cityPadEdgeRelaxMaxIterations = 40;
        [Tooltip("Minimum pad plateau elevation above sea level (coastal reclaim floor).")]
        [SerializeField, Min(0f)] private float cityPadMinClearanceAboveSeaMeters = 2f;
        [Tooltip("Max water depth (m) that pads/sites may reclaim from Ocean/Lake/River.")]
        [SerializeField, Min(0f)] private float cityPadMaxReclaimDepthMeters = 8f;

        public int ContinentalnessSeedOffset => continentalnessSeedOffset;
        public int DomainWarpSeedOffset => domainWarpSeedOffset;
        public int HillsSeedOffset => hillsSeedOffset;
        public int MountainSeedOffset => mountainSeedOffset;

        public float ContinentalnessScale => continentalnessScale;
        public float DomainWarpScale => domainWarpScale;
        public float HillsScale => hillsScale;
        public float MountainScale => mountainScale;

        public float MainlandMaskLo => Mathf.Min(mainlandMaskLo, mainlandMaskHi - 0.02f);
        public float MainlandMaskHi => Mathf.Max(mainlandMaskHi, mainlandMaskLo + 0.02f);

        public float ContinentalnessAmplitude => continentalnessAmplitude;
        public float DomainWarpAmplitude => domainWarpAmplitude;
        public float HillsAmplitude => hillsAmplitude;
        public float MountainAmplitude => mountainAmplitude;

        public float PlainsBias => plainsBias;
        public float CoastFalloffWidthMeters => coastFalloffWidthMeters;
        public AnimationCurve MountainMaskCurve => mountainMaskCurve;

        public bool ForceOceanOnAllEdges => forceOceanOnAllEdges;
        public float OceanEdgeChance => oceanEdgeChance;
        public bool AllowLandlockedMaps => allowLandlockedMaps;
        public bool UseMacroBiomeRegions => useMacroBiomeRegions;
        public float BiomeForcedBoostScale => biomeForcedBoostScale;
        public float BiomeDominantThreshold => biomeDominantThreshold;

        public float EdgeBarrierDepthMeters => edgeBarrierDepthMeters;
        public float EdgeBarrierPeakMeters => edgeBarrierPeakMeters;
        public float EdgeBarrierFalloffMeters => edgeBarrierFalloffMeters;
        public float EdgeBarrierNoiseScaleMeters => edgeBarrierNoiseScaleMeters;
        public float EdgeBarrierNoiseAmplitudeMeters => edgeBarrierNoiseAmplitudeMeters;
        public int EdgeBarrierSeedOffset => edgeBarrierSeedOffset;

        public float OceanOffshoreWidthMeters => oceanOffshoreWidthMeters;
        public float OceanShoreShelfWidthMeters => oceanShoreShelfWidthMeters;
        public float OceanBeachWidthMeters => oceanBeachWidthMeters;
        public float OceanCoastStripWidthMeters =>
            oceanOffshoreWidthMeters + oceanShoreShelfWidthMeters + oceanBeachWidthMeters;
        public float OceanBeachMaxElevationMeters => oceanBeachMaxElevationMeters;
        public float OceanTrenchDepthMeters => oceanTrenchDepthMeters;
        public float OceanTrenchSteepness => oceanTrenchSteepness;
        public float OceanTrenchNoiseAmplitudeMeters => oceanTrenchNoiseAmplitudeMeters;
        public float OceanShelfOuterDepthMeters => Mathf.Max(0.5f, oceanShelfOuterDepthMeters);
        public float OceanShelfInnerDepthMeters =>
            Mathf.Clamp(oceanShelfInnerDepthMeters, 0.25f, OceanShelfOuterDepthMeters);
        public float OceanShelfReefNoiseScaleMeters => Mathf.Max(1f, oceanShelfReefNoiseScaleMeters);
        public float OceanShelfReefNoiseAmplitudeMeters => Mathf.Max(0f, oceanShelfReefNoiseAmplitudeMeters);
        public float OceanShelfReefCoverage => Mathf.Clamp01(oceanShelfReefCoverage);
        public float OceanCoastErosionAmplitudeMeters => oceanCoastErosionAmplitudeMeters;
        public float OceanCoastErosionScaleMeters => oceanCoastErosionScaleMeters;
        public float InlandDryFloorMetersAboveSea => inlandDryFloorMetersAboveSea;
        public float InlandDryFloorBlendMeters => inlandDryFloorBlendMeters;

        public float IslandNoiseScaleMeters => Mathf.Max(200f, islandNoiseScaleMeters);
        public float ArchipelagoNoiseScaleMeters => Mathf.Max(100f, archipelagoNoiseScaleMeters);
        public float IslandPeakThreshold => Mathf.Clamp(islandPeakThreshold, 0.45f, 0.92f);
        public float ArchipelagoPeakThreshold => Mathf.Clamp(archipelagoPeakThreshold, 0.45f, 0.95f);

        public bool InteriorMountainsEnabled => interiorMountainsEnabled;
        public int InteriorMountainRangeCount => interiorMountainRangeCount;
        public float InteriorMountainPeakMeters => interiorMountainPeakMeters;
        public float InteriorMountainWidthMeters => interiorMountainWidthMeters;
        public float InteriorMountainRuggedness => interiorMountainRuggedness;
        public int InteriorMountainSeedOffset => interiorMountainSeedOffset;
        public float InteriorRollingScaleMeters => interiorRollingScaleMeters;
        public float InteriorRollingAmplitudeMeters => interiorRollingAmplitudeMeters;
        public float InteriorReliefInsetMeters => interiorReliefInsetMeters;
        public float InteriorHillsMultiplier => interiorHillsMultiplier;
        public float InteriorMountainPaintMinMask => interiorMountainPaintMinMask;
        public bool UseLegacyCoastalBlend => useLegacyCoastalBlend;
        public int LandformAlgorithmVersion => landformAlgorithmVersion;
        public float ResidualMountainAmplitudeMeters => residualMountainAmplitudeMeters;
        public float FoothillSkirtMeters => foothillSkirtMeters;
        public float CrestSharpness => crestSharpness;
        public float SaddleDepth => saddleDepth;
        public float OrogenWarpFraction => orogenWarpFraction;
        public float StructuralValleyDepthMeters => structuralValleyDepthMeters;
        public float MicroDetailAmplitudeMeters => microDetailAmplitudeMeters;
        public float MaxOrogenCoverageFraction => maxOrogenCoverageFraction;
        public float MinLowlandSlope12Fraction => minLowlandSlope12Fraction;
        public int MinPassCountPerRange => minPassCountPerRange;
        public float HighwayPassMaxSlopeDegrees => highwayPassMaxSlopeDegrees;
        public float PassCorridorHalfWidthMeters => passCorridorHalfWidthMeters;

        public int ThermalErosionIterations => thermalErosionIterations;
        public float ThermalTalusAngleDegrees => thermalTalusAngleDegrees;
        public float ThermalTransferFraction => thermalTransferFraction;

        public int CityPadAlgorithmVersion => cityPadAlgorithmVersion;
        public float CityPadFlatMarginMeters => cityPadFlatMarginMeters;
        public float CityPadPlateauSlopeMeters => Mathf.Max(40f, cityPadPlateauSlopeMeters);
        public float CityPadFalloffMinMeters => cityPadFalloffMinMeters;
        public float CityPadFalloffMaxMeters => Mathf.Max(cityPadFalloffMinMeters, cityPadFalloffMaxMeters);
        public float CityPadCornerRadiusFraction => cityPadCornerRadiusFraction;
        public float CityPadHydrologyPruneMarginMeters => cityPadHydrologyPruneMarginMeters;
        public float CityPadApproachSlopeDegrees => cityPadApproachSlopeDegrees;
        public float CityPadMaxContinuityDegrees => cityPadMaxContinuityDegrees;
        public bool CityPadEdgeRelaxEnabled => cityPadEdgeRelaxEnabled;
        public int CityPadEdgeRelaxMaxIterations => cityPadEdgeRelaxMaxIterations;
        public float CityPadMinClearanceAboveSeaMeters => cityPadMinClearanceAboveSeaMeters;
        public float CityPadMaxReclaimDepthMeters => cityPadMaxReclaimDepthMeters;
    }
}
