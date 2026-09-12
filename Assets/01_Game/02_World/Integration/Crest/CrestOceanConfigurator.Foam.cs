using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Pipeline foam: calm SimSettingsFoam + material appearance from WorldWaterProfile.
    /// </summary>
    internal static partial class CrestOceanConfigurator
    {
        private const string FoamKeyword = "_FOAM_ON";
        private const string CalmFoamSettingsPath =
            "Assets/02_Shared/ScriptableObjects/World/Profiles/CrestSimSettingsFoamCalm.asset";

        // Baselines at FoamStrength == 1. Default profile strength ~0.2 → quiet whitecaps.
        private const float BaseWaveFoamStrength = 0.6f;
        private const float BaseWaveFoamCoverage = 0.22f;
        private const float BaseShorelineFoamStrength = 1f;
        private const float BaseFoamFadeRate = 1.4f;
        private const float BaseShorelineFoamMinDepth = 0.15f;
        private const float BaseWhiteFoamAlpha = 0.6f;
        private const float BaseBubblesCoverage = 1f;
        private const float BaseLightScale = 1.75f;
        private const float ShoreWidthToMaxDepth = 0.025f;

        /// <summary>
        /// Enables Crest flow and applies quiet foam from optional water profile.
        /// Called every Hub ocean/inland build after the ocean prefab is spawned.
        /// </summary>
        public static void EnsureFlowAndFoam(OceanRenderer ocean, WorldWaterProfile water = null)
        {
            if (ocean == null)
                return;

            SetPrivateField(ocean, "_createFlowSim", true);

            var enableFoam = water == null || water.EnableFoam;
            SetPrivateField(ocean, "_createFoamSim", enableFoam);

            var material = ocean.OceanMaterial;
            if (material == null)
                return;

            material.EnableKeyword("_FLOW_ON");
            if (material.HasProperty("_Flow"))
                material.SetFloat("_Flow", 1f);

            if (!enableFoam)
            {
                DisableFoamOnMaterial(material);
                return;
            }

            ApplyCalmFoamSim(ocean, water);
            ApplyCalmShoreFoam(material, water);
        }

        /// <summary>
        /// Softens Crest foam appearance. <paramref name="water"/> scales intensity when present.
        /// </summary>
        public static void ApplyCalmShoreFoam(Material material, WorldWaterProfile water = null)
        {
            if (material == null)
                return;

            var strength = ResolveFoamStrength(water);
            material.EnableKeyword(FoamKeyword);
            if (material.HasProperty("_Foam"))
                material.SetFloat("_Foam", 1f);

            if (material.HasProperty("_ShorelineFoamMinDepth"))
                material.SetFloat("_ShorelineFoamMinDepth", BaseShorelineFoamMinDepth);

            if (material.HasProperty("_WaveFoamBubblesCoverage"))
                material.SetFloat("_WaveFoamBubblesCoverage", BaseBubblesCoverage * strength);

            if (material.HasProperty("_WaveFoamLightScale"))
                material.SetFloat("_WaveFoamLightScale", BaseLightScale * strength);

            if (!material.HasProperty("_FoamWhiteColor"))
                return;

            var white = material.GetColor("_FoamWhiteColor");
            white.a = BaseWhiteFoamAlpha * strength;
            material.SetColor("_FoamWhiteColor", white);
        }

        private static void ApplyCalmFoamSim(OceanRenderer ocean, WorldWaterProfile water)
        {
            var settings = EnsureRuntimeFoamSettings(ocean);
            var strength = ResolveFoamStrength(water);

            settings._prewarm = true;
            settings._foamFadeRate = BaseFoamFadeRate;
            settings._waveFoamStrength = BaseWaveFoamStrength * strength;
            settings._waveFoamCoverage = BaseWaveFoamCoverage;
            settings._shorelineFoamStrength = BaseShorelineFoamStrength * strength;
            settings._shorelineFoamMaxDepth = ResolveShorelineMaxDepth(water);
            settings._simulationFrequency = 30f;
        }

        private static SimSettingsFoam EnsureRuntimeFoamSettings(OceanRenderer ocean)
        {
            var existing = ocean._simSettingsFoam;
            if (existing != null && (existing.hideFlags & HideFlags.HideAndDontSave) != 0)
                return existing;

            var runtime = ScriptableObject.CreateInstance<SimSettingsFoam>();
            runtime.hideFlags = HideFlags.HideAndDontSave;
            runtime.name = "CrestSimSettingsFoamCalm_Runtime";

            // Seed from project calm asset when present (inspector baseline).
#if UNITY_EDITOR
            var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<SimSettingsFoam>(CalmFoamSettingsPath);
            if (loaded != null)
            {
                runtime._prewarm = loaded._prewarm;
                runtime._foamFadeRate = loaded._foamFadeRate;
                runtime._waveFoamStrength = loaded._waveFoamStrength;
                runtime._waveFoamCoverage = loaded._waveFoamCoverage;
                runtime._shorelineFoamStrength = loaded._shorelineFoamStrength;
                runtime._shorelineFoamMaxDepth = loaded._shorelineFoamMaxDepth;
                runtime._simulationFrequency = loaded._simulationFrequency;
            }
#endif

            ocean._simSettingsFoam = runtime;
            return runtime;
        }

        private static float ResolveFoamStrength(WorldWaterProfile water)
        {
            if (water == null)
                return 0.2f;
            return Mathf.Clamp01(water.FoamStrength);
        }

        private static float ResolveShorelineMaxDepth(WorldWaterProfile water)
        {
            var shoreMeters = water != null ? water.FoamShoreWidthMeters : 12f;
            return Mathf.Clamp(shoreMeters * ShoreWidthToMaxDepth, 0.05f, 1.5f);
        }

        private static void DisableFoamOnMaterial(Material material)
        {
            material.DisableKeyword(FoamKeyword);
            if (material.HasProperty("_Foam"))
                material.SetFloat("_Foam", 0f);
        }
    }
}
