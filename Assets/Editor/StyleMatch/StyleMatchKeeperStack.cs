#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>Re-applies scene + profile values from the keeper stack after full pipeline resets.</summary>
    public static class StyleMatchKeeperStack
    {
        private const string LandformPath =
            "Assets/02_Shared/ScriptableObjects/World/Profiles/LandformProfile.asset";

        public static void Apply()
        {
            StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 350f, 250f, 500f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 550f, 450f, 700f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 180f, 80f, 400f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 380f, 120f, 500f);
            StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 500f, 400f, 700f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 750f, 600f, 950f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_peakExclusiveMinElevationMeters", 780f, 700f, 900f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_peakExclusiveFullElevationMeters", 980f, 850f, 1200f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 55f, 8f, 80f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_beachMaxElevationAboveSea", 12f, 2f, 16f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_snowLineNoiseAmplitudeMeters", 50f, 0f, 120f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.88f, 0.55f, 0.95f);
            StyleMatchLoopRunner.SetSurfacePainterFloat("_rockExposureNoiseScaleMeters", 55f, 8f, 120f);

            var profile = AssetDatabase.LoadAssetAtPath<LandformProfile>(LandformPath);
            if (profile == null)
                return;

            var so = new SerializedObject(profile);
            so.FindProperty("biomeForcedBoostScale").floatValue = 2.6f;
            so.FindProperty("biomeDominantThreshold").floatValue = 0.62f;
            so.FindProperty("useMacroBiomeRegions").boolValue = true;
            so.FindProperty("interiorMountainPeakMeters").floatValue = 1700f;
            so.FindProperty("interiorRollingAmplitudeMeters").floatValue = 200f;
            so.FindProperty("plainsBias").floatValue = 0.08f;
            so.FindProperty("edgeBarrierPeakMeters").floatValue = 820f;
            so.FindProperty("interiorMountainRuggedness").floatValue = 0.82f;
            so.FindProperty("interiorHillsMultiplier").floatValue = 1.8f;
            so.FindProperty("mountainAmplitude").floatValue = 780f;
            var widthProp = so.FindProperty("interiorMountainWidthMeters");
            if (widthProp != null)
                widthProp.floatValue = 4800f;
            var residualProp = so.FindProperty("residualMountainAmplitudeMeters");
            if (residualProp != null)
                residualProp.floatValue = 85f;
            var skirtProp = so.FindProperty("foothillSkirtMeters");
            if (skirtProp != null)
                skirtProp.floatValue = 1900f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
