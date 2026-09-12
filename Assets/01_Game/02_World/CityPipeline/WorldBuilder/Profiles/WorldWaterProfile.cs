using System;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Crest ocean / foam / lake material settings for WorldBuilder water presentation.</summary>
    [CreateAssetMenu(
        fileName = "WorldWaterProfile",
        menuName = "Zombera/World/World Water Profile")]
    public sealed class WorldWaterProfile : ScriptableObject
    {
        /// <summary>
        /// Named Crest spline preset. Inland generation reads only these values (plus the flow
        /// window and spectra below), so runtime water never depends on the development scene.
        /// </summary>
        [Serializable]
        public readonly struct CrestSplinePreset
        {
            public readonly float SplineRadius;
            public readonly int SplineSubdivisions;
            public readonly float HeightRadius;
            public readonly int HeightSubdivisions;
            public readonly int WaveResolution;
            public readonly float WaveTurbulence;
            public readonly float PointWaveWeight;
            public readonly float PointFlowVelocity;

            public CrestSplinePreset(
                float splineRadius,
                int splineSubdivisions,
                float heightRadius,
                int heightSubdivisions,
                int waveResolution,
                float waveTurbulence,
                float pointWaveWeight,
                float pointFlowVelocity)
            {
                SplineRadius = splineRadius;
                SplineSubdivisions = splineSubdivisions;
                HeightRadius = heightRadius;
                HeightSubdivisions = heightSubdivisions;
                WaveResolution = waveResolution;
                WaveTurbulence = waveTurbulence;
                PointWaveWeight = pointWaveWeight;
                PointFlowVelocity = pointFlowVelocity;
            }

            /// <summary>Authored Crest river reference (radius 25, height 18.92/8, FFT 256).</summary>
            public static CrestSplinePreset RiverDefault => new(25f, 1, 18.92f, 8, 256, 0.5f, 1f, 2f);

            /// <summary>Authored Crest lake reference (radius 20, height 25/4, FFT 128).</summary>
            public static CrestSplinePreset LakeDefault => new(20f, 1, 25f, 4, 128, 0.145f, 1f, 2f);
        }
        [Header("Crest")]
        [SerializeField] private GameObject oceanRendererPrefab;
        [SerializeField] private GameObject waterBodyPrefab;
        [SerializeField] private UnityEngine.Object crestOceanMaterial;
        [Tooltip(
            "Optional lake WaterBody material override. Crest docs: do not use with bordering ocean " +
            "bodies while Water Body Culling is enabled — currently unused by spawn paths.")]
        [SerializeField] private Material crestLakeMaterial;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Lakes whose surface differs from sea level get a Crest RegisterHeightInput spline (docs lakes).")]
        private float crestLakeSeaLevelToleranceMeters = 0.05f;
        [SerializeField] private float seaLevelWorldY;
        [SerializeField] private bool enableFoam = true;
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Scales Crest wave + shoreline foam generation and white-foam appearance (0 = none, 1 = calm baseline).")]
        private float foamStrength = 0.35f;
        [SerializeField] private float foamShoreWidthMeters = 20f;

        [Header("Crest Rivers")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Minimum downstream Crest flow velocity assigned to river spline points (meters/second).")]
        private float riverMinimumFlowSpeedMetersPerSecond = 0.75f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Maximum downstream Crest flow velocity assigned to river spline points (meters/second).")]
        private float riverMaximumFlowSpeedMetersPerSecond = 4f;

        [Header("Crest Inland Splines")]
        [SerializeField, Min(0f)]
        [Tooltip("Authored Crest Spline radius for generated river centerlines (meters).")]
        private float riverSplineRadius = 25f;
        [SerializeField, Min(1)]
        [Tooltip("Authored Crest Spline subdivisions for generated rivers.")]
        private int riverSplineSubdivisions = 1;
        [SerializeField, Min(0f)]
        [Tooltip("Authored Register Height Input radius for generated rivers (meters).")]
        private float riverHeightRadius = 18.92f;
        [SerializeField, Min(1)]
        [Tooltip("Register Height Input subdivisions for generated rivers.")]
        private int riverHeightSubdivisions = 8;
        [SerializeField, Min(1)]
        [Tooltip("Shape FFT resolution for generated rivers.")]
        private int riverWaveResolution = 256;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Shape FFT wind turbulence for generated rivers.")]
        private float riverWaveTurbulence = 0.5f;
        [SerializeField, Min(0f)]
        [Tooltip("Authored Crest Spline radius for generated lakes (meters).")]
        private float lakeSplineRadius = 20f;
        [SerializeField, Min(1)]
        [Tooltip("Authored Crest Spline subdivisions for generated lakes.")]
        private int lakeSplineSubdivisions = 1;
        [SerializeField, Min(0f)]
        [Tooltip("Authored Register Height Input radius for generated lakes (meters).")]
        private float lakeHeightRadius = 25f;
        [SerializeField, Min(1)]
        [Tooltip("Register Height Input subdivisions for generated lakes.")]
        private int lakeHeightSubdivisions = 4;
        [SerializeField, Min(1)]
        [Tooltip("Shape FFT resolution for generated lakes.")]
        private int lakeWaveResolution = 128;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Authored Shape FFT wind turbulence for generated lakes.")]
        private float lakeWaveTurbulence = 0.145f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Authored Shape FFT weight assigned to generated spline points.")]
        private float pointWaveWeight = 1f;
        [SerializeField, Min(0f)]
        [Tooltip("Authored flow velocity assigned to generated spline points (meters/second).")]
        private float pointFlowVelocity = 2f;

        [Header("Crest Inland Footprint")]
        [SerializeField, Min(0f)]
        [Tooltip("Maximum bank-height deviation accepted when validating a water footprint (meters).")]
        private float bankHeightTolerance = 0.25f;
        [SerializeField, Min(0f)]
        [Tooltip("Maximum allowed footprint-edge fitting error (meters).")]
        private float maximumEdgeError = 8f;
        [SerializeField, Min(0f)]
        [Tooltip("Minimum vertical clearance maintained between the water surface and carved bed (meters).")]
        private float minimumBedClearance = 0.5f;
        [SerializeField, Min(0)]
        [Tooltip("Maximum number of footprint repair passes.")]
        private int repairPassLimit = 2;
        [SerializeField, Min(0f)]
        [Tooltip("Minimum spacing between generated spline points (meters).")]
        private float minPointSpacing = 16f;
        [SerializeField, Min(0f)]
        [Tooltip("Maximum spacing between generated spline points (meters).")]
        private float maxPointSpacing = 128f;

        [Header("Crest Waves")]
        [SerializeField]
        [Tooltip("Crest Ocean Wave Spectrum for the global / outer ocean (typically WavesModerate).")]
        private ScriptableObject outerSpectrum;
        [SerializeField]
        [Tooltip("Crest Ocean Wave Spectrum for the coastal calm Blend override (typically WavesCalm).")]
        private ScriptableObject coastalSpectrum;
        [SerializeField]
        [Range(0f, 1f)]
        private float outerWaveWeight = 1f;
        [SerializeField]
        [Min(0f)]
        private float outerWindSpeedKph = 28f;
        [SerializeField]
        [Range(0f, 1f)]
        private float coastalWaveWeight = 0.35f;
        [SerializeField]
        [Min(0f)]
        private float coastalWindSpeedKph = 8f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Expands the calm coastal Blend mesh past core bounds toward the ocean ring (meters).")]
        private float coastalCalmPaddingMeters = 400f;

        [Header("Crest Sea Floor")]
        [SerializeField]
        [Tooltip("Layers rendered into OceanDepthCache (Default is always unioned with live Terrain layers).")]
        private LayerMask depthCacheLayers = 1; // Default
        [SerializeField]
        [Tooltip(
            "Optional MeshRenderer layers that get RegisterSeaFloorDepthInput (dynamic/carved geometry). " +
            "Terrains use OceanDepthCache instead (Crest docs).")]
        private LayerMask seaFloorGeometryLayers;

        [Header("Crest Inland Spectra")]
        [SerializeField]
        [Tooltip("ShapeFFT spectrum for elevated lakes (LakesAndRivers lake spectrum by default).")]
        private ScriptableObject lakeSpectrum;
        [SerializeField]
        [Tooltip("ShapeFFT spectrum for river splines (LakesAndRivers river spectrum by default).")]
        private ScriptableObject riverSpectrum;

        public GameObject OceanRendererPrefab => oceanRendererPrefab;
        public GameObject WaterBodyPrefab => waterBodyPrefab;
        public UnityEngine.Object CrestOceanMaterial => crestOceanMaterial;
        public Material CrestLakeMaterial => crestLakeMaterial;
        public float CrestLakeSeaLevelToleranceMeters => crestLakeSeaLevelToleranceMeters;
        public float SeaLevelWorldY => seaLevelWorldY;
        public bool EnableFoam => enableFoam;
        public float FoamStrength => foamStrength;
        public float FoamShoreWidthMeters => foamShoreWidthMeters;
        public float RiverMinimumFlowSpeedMetersPerSecond => riverMinimumFlowSpeedMetersPerSecond;
        public float RiverMaximumFlowSpeedMetersPerSecond => riverMaximumFlowSpeedMetersPerSecond;
        public float RiverSplineRadius => riverSplineRadius;
        public int RiverSplineSubdivisions => riverSplineSubdivisions;
        public float RiverHeightRadius => riverHeightRadius;
        public int RiverHeightSubdivisions => riverHeightSubdivisions;
        public int RiverWaveResolution => riverWaveResolution;
        public float RiverWaveTurbulence => riverWaveTurbulence;
        public float LakeSplineRadius => lakeSplineRadius;
        public int LakeSplineSubdivisions => lakeSplineSubdivisions;
        public float LakeHeightRadius => lakeHeightRadius;
        public int LakeHeightSubdivisions => lakeHeightSubdivisions;
        public int LakeWaveResolution => lakeWaveResolution;
        public float LakeWaveTurbulence => lakeWaveTurbulence;
        public float PointWaveWeight => pointWaveWeight;
        public float PointFlowVelocity => pointFlowVelocity;

        /// <summary>Named Crest preset used for generated river splines.</summary>
        public CrestSplinePreset RiverPreset => new(
            riverSplineRadius,
            riverSplineSubdivisions,
            riverHeightRadius,
            riverHeightSubdivisions,
            riverWaveResolution,
            riverWaveTurbulence,
            pointWaveWeight,
            pointFlowVelocity);

        /// <summary>Named Crest preset used for generated lake splines.</summary>
        public CrestSplinePreset LakePreset => new(
            lakeSplineRadius,
            lakeSplineSubdivisions,
            lakeHeightRadius,
            lakeHeightSubdivisions,
            lakeWaveResolution,
            lakeWaveTurbulence,
            pointWaveWeight,
            pointFlowVelocity);

        /// <summary>Carve/fit/repair options derived from this profile.</summary>
        public InlandWaterFootprintOptions FootprintOptions => InlandWaterFootprintOptions.FromWater(this);

        /// <summary>Crest ribbon settings derived from this profile, for validation.</summary>
        public CrestRibbonValidationSettings RibbonSettings => CrestRibbonValidationSettings.FromWater(this);
        public float BankHeightTolerance => bankHeightTolerance;
        public float MaximumEdgeError => maximumEdgeError;
        public float MinimumBedClearance => minimumBedClearance;
        public int RepairPassLimit => repairPassLimit;
        public float MinPointSpacing => minPointSpacing;
        public float MaxPointSpacing => maxPointSpacing;

        public ScriptableObject OuterSpectrum => outerSpectrum;
        public ScriptableObject CoastalSpectrum => coastalSpectrum;
        public float OuterWaveWeight => outerWaveWeight;
        public float OuterWindSpeedKph => outerWindSpeedKph;
        public float CoastalWaveWeight => coastalWaveWeight;
        public float CoastalWindSpeedKph => coastalWindSpeedKph;
        public float CoastalCalmPaddingMeters => coastalCalmPaddingMeters;

        public LayerMask DepthCacheLayers => depthCacheLayers;
        public LayerMask SeaFloorGeometryLayers => seaFloorGeometryLayers;
        public ScriptableObject LakeSpectrum => lakeSpectrum;
        public ScriptableObject RiverSpectrum => riverSpectrum;
    }
}
