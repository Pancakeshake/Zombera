#if UNITY_EDITOR
using System;
using JBooth.MicroSplat;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor.StyleMatch
{
    public sealed partial class StyleMatchLoopRunner
    {
        private delegate bool IterationMutator(int iter, ref string summary);

        private static readonly IterationMutator[] IterationPlan =
        {
            Iter00_EnviroSilentSetup,
            Iter01_MicroSplatInterpolationContrast,
            Iter02_MicroSplatLayerContrast,
            Iter03_CompileMicroSplatArrays,
            Iter04_BeachInlandBlend,
            Iter05_MainGrassNoiseScale,
            Iter06_SnowElevationBand,
            Iter07_CliffSnowWeight,
            Iter08_RiverBedWiden,
            Iter09_MicroSplatTriplanarContrast,
            Iter10_NatureDensityBoost,
            Iter11_BeachElevationCap,
            Iter12_MicroSplatBrightness,
            Iter13_EnviroClearWeather,
            Iter14_MicroSplatHeightContrast,
            Iter15_NatureForestDensity,
            Iter16_GrassYellowBoost,
            Iter17_MicroSplatCompileRefresh,
            Iter18_BeachNoiseScale,
            Iter19_FinalPolishContrast,
        };

        private static bool Iter00_EnviroSilentSetup(int iter, ref string summary)
        {
            _ = iter;
            summary = "Wire Enviro config + silent scene setup";
            StyleMatchEnviroSetup.EnsureInActiveScene();
            StyleMatchProfileMutations.WireEnvironmentProfile();
            return true;
        }

        private static bool Iter01_MicroSplatInterpolationContrast(int iter, ref string summary)
        {
            var scale = CycleScale(iter);
            summary = "MicroSplat InterpolationContrast +" + (0.12f * scale).ToString("F3");
            return StyleMatchMicroSplatMutations.BumpFloat(
                MicroSplatPropData.PerTexFloat.InterpolationContrast, 0.12f * scale, 0f, 2f);
        }

        private static bool Iter02_MicroSplatLayerContrast(int iter, ref string summary)
        {
            var scale = CycleScale(iter);
            summary = "MicroSplat Contrast +" + (0.08f * scale).ToString("F3") + " layers 0-12";
            return StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                MicroSplatPropData.PerTexFloat.Contrast, 0.08f * scale, 0, 12, 0f, 2f);
        }

        private static bool Iter03_CompileMicroSplatArrays(int iter, ref string summary)
        {
            _ = iter;
            summary = "Compile Microsplat_World texture arrays";
            return StyleMatchMicroSplatMutations.CompileWorldConfig();
        }

        private static bool Iter04_BeachInlandBlend(int iter, ref string summary)
        {
            _ = iter;
            summary = "WorldSurfacePainter beach inland blend +5m";
            return StyleMatchPainterMutations.AddFloat("_beachInlandBlendMeters", 5f, 8f, 48f);
        }

        private static bool Iter05_MainGrassNoiseScale(int iter, ref string summary)
        {
            _ = iter;
            summary = "WorldSurfacePainter main grass noise scale -8m";
            return StyleMatchPainterMutations.AddFloat("_mainGrassNoiseScaleMeters", -8f, 24f, 120f);
        }

        private static bool Iter06_SnowElevationBand(int iter, ref string summary)
        {
            _ = iter;
            summary = "Lower snow min elevation -35m";
            return StyleMatchPainterMutations.AddFloat("_snowMinElevationMeters", -35f, 400f, 700f);
        }

        private static bool Iter07_CliffSnowWeight(int iter, ref string summary)
        {
            _ = iter;
            summary = "Cliff snow max weight +0.08";
            return StyleMatchPainterMutations.AddFloat("_cliffSnowMaxWeight", 0.08f, 0.2f, 0.85f);
        }

        private static bool Iter08_RiverBedWiden(int iter, ref string summary)
        {
            _ = iter;
            summary = "River bed max distance +2m";
            return StyleMatchPainterMutations.AddFloat("_riverBedMaxDistanceMeters", 2f, 4f, 24f);
        }

        private static bool Iter09_MicroSplatTriplanarContrast(int iter, ref string summary)
        {
            _ = iter;
            summary = "MicroSplat triplanar contrast +0.1 cliffs 5-10";
            return StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                MicroSplatPropData.PerTexFloat.TriplanarContrast, 0.1f, 5, 10, 0f, 2f);
        }

        private static bool Iter10_NatureDensityBoost(int iter, ref string summary)
        {
            _ = iter;
            summary = "WorldNatureProfile density ×1.2";
            return StyleMatchProfileMutations.ScaleNatureDensity(1.2f);
        }

        private static bool Iter11_BeachElevationCap(int iter, ref string summary)
        {
            _ = iter;
            summary = "Beach max elevation above sea +1.2m";
            return StyleMatchPainterMutations.AddFloat("_beachMaxElevationAboveSea", 1.2f, 2f, 12f);
        }

        private static bool Iter12_MicroSplatBrightness(int iter, ref string summary)
        {
            _ = iter;
            summary = "MicroSplat brightness +0.05 grass/sand layers";
            return StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                MicroSplatPropData.PerTexFloat.Brightness, 0.05f, 0, 6, 0f, 2f);
        }

        private static bool Iter13_EnviroClearWeather(int iter, ref string summary)
        {
            _ = iter;
            summary = "Enviro clear weather + midday";
            return StyleMatchEnviroSetup.SetClearMidday();
        }

        private static bool Iter14_MicroSplatHeightContrast(int iter, ref string summary)
        {
            _ = iter;
            summary = "MicroSplat height contrast +0.1 rock layers";
            return StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                MicroSplatPropData.PerTexFloat.HeightContrast, 0.1f, 5, 10, 0f, 2f);
        }

        private static bool Iter15_NatureForestDensity(int iter, ref string summary)
        {
            _ = iter;
            summary = "Forest/Hills tree density +25%";
            return StyleMatchProfileMutations.BoostBiomeNatureDensity(
                new[] { "Forest", "Hills", "Plains" }, 1.25f);
        }

        private static bool Iter16_GrassYellowBoost(int iter, ref string summary)
        {
            _ = iter;
            summary = "Beach detail noise scale -4m (sharper shore)";
            return StyleMatchPainterMutations.AddFloat("_beachDetailNoiseScaleMeters", -4f, 8f, 64f);
        }

        private static bool Iter17_MicroSplatCompileRefresh(int iter, ref string summary)
        {
            _ = iter;
            summary = "Recompile MicroSplat.mat shader";
            return StyleMatchMicroSplatMutations.CompileTemplateMaterial();
        }

        private static bool Iter18_BeachNoiseScale(int iter, ref string summary)
        {
            _ = iter;
            summary = "Beach macro noise scale -6m";
            return StyleMatchPainterMutations.AddFloat("_beachNoiseScaleMeters", -6f, 20f, 120f);
        }

        private static bool Iter19_FinalPolishContrast(int iter, ref string summary)
        {
            _ = iter;
            summary = "Final interpolation contrast +0.06";
            return StyleMatchMicroSplatMutations.BumpFloat(
                MicroSplatPropData.PerTexFloat.InterpolationContrast, 0.06f, 0f, 2.5f);
        }

        private const int PlanSlotCount = 20;

        private static float CycleScale(int planIndex) =>
            1f + (planIndex / PlanSlotCount) * 0.25f;

        private static bool ApplyIterationMutation(int planIndex, int sessionIter, ref string summary)
        {
            var slot = planIndex % PlanSlotCount;
            if (slot < 0 || slot >= IterationPlan.Length)
                return false;

            try
            {
                var ok = IterationPlan[slot](sessionIter, ref summary);
                var cycle = planIndex / PlanSlotCount;
                if (cycle > 0)
                    summary += " (cycle " + cycle + ", scale " + CycleScale(planIndex).ToString("F2") + ")";
                return ok;
            }
            catch (Exception ex)
            {
                Debug.LogError("[StyleMatch] Mutation failed iter " + sessionIter + ": " + ex.Message);
                summary = "FAILED: " + ex.Message;
                return false;
            }
        }
    }
}
#endif
