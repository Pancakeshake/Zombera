#region

using UnityEngine;

#endregion

namespace Zombera.Environment
{
    [CreateAssetMenu(fileName = "DayNightDefaultsProfile", menuName = "Zombera/Environment/Day Night Defaults Profile")]
    public sealed class DayNightDefaultsProfile : ScriptableObject
    {
        [Header("Sun")] [SerializeField] private Gradient sunColor = new();
        [SerializeField] private AnimationCurve sunIntensity = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Ambient")] [SerializeField] private Gradient ambientSkyColor = new();
        [SerializeField] private Gradient ambientEquatorColor = new();
        [SerializeField] private Gradient ambientGroundColor = new();

        [Header("Fog")] [SerializeField] private Gradient fogColor = new();
        [SerializeField] private AnimationCurve fogDensity = AnimationCurve.Linear(0f, 0.003f, 1f, 0.003f);

        [Header("Skybox")] [SerializeField] private AnimationCurve skyboxAtmosphere = AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f);
        [SerializeField] private AnimationCurve skyboxExposure = AnimationCurve.Linear(0f, 0.2f, 1f, 0.2f);

        public Gradient SunColor => sunColor;
        public AnimationCurve SunIntensity => sunIntensity;
        public Gradient AmbientSkyColor => ambientSkyColor;
        public Gradient AmbientEquatorColor => ambientEquatorColor;
        public Gradient AmbientGroundColor => ambientGroundColor;
        public Gradient FogColor => fogColor;
        public AnimationCurve FogDensity => fogDensity;
        public AnimationCurve SkyboxAtmosphere => skyboxAtmosphere;
        public AnimationCurve SkyboxExposure => skyboxExposure;

#if UNITY_EDITOR
        internal static DayNightDefaultsProfile CreateZomberaSurvivalProfile()
        {
            var profile = CreateInstance<DayNightDefaultsProfile>();

            profile.sunIntensity = new AnimationCurve(
                new Keyframe(0.00f, 0.00f, 0f, 0f),
                new Keyframe(0.22f, 0.00f, 0f, 0f),
                new Keyframe(0.27f, 0.30f, 4f, 4f),
                new Keyframe(0.50f, 1.00f, 0f, 0f),
                new Keyframe(0.73f, 0.30f, -4f, -4f),
                new Keyframe(0.78f, 0.00f, 0f, 0f),
                new Keyframe(1.00f, 0.00f, 0f, 0f)
            );

            profile.fogDensity = new AnimationCurve(
                new Keyframe(0.00f, 0.006f),
                new Keyframe(0.22f, 0.006f),
                new Keyframe(0.32f, 0.002f),
                new Keyframe(0.68f, 0.002f),
                new Keyframe(0.78f, 0.006f),
                new Keyframe(1.00f, 0.006f)
            );

            profile.skyboxAtmosphere = new AnimationCurve(
                new Keyframe(0.00f, 0.40f),
                new Keyframe(0.25f, 0.75f),
                new Keyframe(0.50f, 1.00f),
                new Keyframe(0.75f, 0.75f),
                new Keyframe(1.00f, 0.40f)
            );

            profile.skyboxExposure = new AnimationCurve(
                new Keyframe(0.00f, 0.18f),
                new Keyframe(0.25f, 0.65f),
                new Keyframe(0.50f, 1.30f),
                new Keyframe(0.75f, 0.65f),
                new Keyframe(1.00f, 0.18f)
            );

            profile.sunColor = BuildSunColorGradient();
            profile.ambientSkyColor = BuildAmbientSkyGradient();
            profile.ambientEquatorColor = BuildAmbientEquatorGradient();
            profile.ambientGroundColor = BuildAmbientGroundGradient();
            profile.fogColor = BuildFogGradient();

            return profile;
        }

        private static Gradient BuildSunColorGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.50f, 0.56f, 0.78f), 0.00f),
                    new GradientColorKey(new Color(1.00f, 0.48f, 0.19f), 0.25f),
                    new GradientColorKey(new Color(1.00f, 0.91f, 0.69f), 0.33f),
                    new GradientColorKey(new Color(0.96f, 0.91f, 0.82f), 0.50f),
                    new GradientColorKey(new Color(0.94f, 0.82f, 0.50f), 0.65f),
                    new GradientColorKey(new Color(1.00f, 0.31f, 0.06f), 0.75f),
                    new GradientColorKey(new Color(0.44f, 0.13f, 0.31f), 0.82f),
                    new GradientColorKey(new Color(0.50f, 0.56f, 0.78f), 1.00f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private static Gradient BuildAmbientSkyGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.04f, 0.07f, 0.14f), 0.00f),
                    new GradientColorKey(new Color(0.78f, 0.31f, 0.19f), 0.25f),
                    new GradientColorKey(new Color(0.53f, 0.69f, 0.88f), 0.33f),
                    new GradientColorKey(new Color(0.38f, 0.56f, 0.75f), 0.50f),
                    new GradientColorKey(new Color(0.47f, 0.61f, 0.77f), 0.65f),
                    new GradientColorKey(new Color(0.75f, 0.25f, 0.06f), 0.75f),
                    new GradientColorKey(new Color(0.19f, 0.08f, 0.13f), 0.82f),
                    new GradientColorKey(new Color(0.04f, 0.07f, 0.14f), 1.00f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private static Gradient BuildAmbientEquatorGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.03f, 0.04f, 0.08f), 0.00f),
                    new GradientColorKey(new Color(0.38f, 0.19f, 0.10f), 0.25f),
                    new GradientColorKey(new Color(0.25f, 0.31f, 0.38f), 0.40f),
                    new GradientColorKey(new Color(0.19f, 0.22f, 0.27f), 0.50f),
                    new GradientColorKey(new Color(0.25f, 0.22f, 0.17f), 0.65f),
                    new GradientColorKey(new Color(0.38f, 0.15f, 0.05f), 0.75f),
                    new GradientColorKey(new Color(0.09f, 0.04f, 0.06f), 0.82f),
                    new GradientColorKey(new Color(0.03f, 0.04f, 0.08f), 1.00f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private static Gradient BuildAmbientGroundGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.02f, 0.02f, 0.03f), 0.00f),
                    new GradientColorKey(new Color(0.13f, 0.08f, 0.04f), 0.25f),
                    new GradientColorKey(new Color(0.09f, 0.10f, 0.07f), 0.40f),
                    new GradientColorKey(new Color(0.07f, 0.08f, 0.06f), 0.50f),
                    new GradientColorKey(new Color(0.09f, 0.09f, 0.06f), 0.65f),
                    new GradientColorKey(new Color(0.13f, 0.06f, 0.02f), 0.75f),
                    new GradientColorKey(new Color(0.04f, 0.02f, 0.02f), 0.82f),
                    new GradientColorKey(new Color(0.02f, 0.02f, 0.03f), 1.00f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private static Gradient BuildFogGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.06f, 0.08f, 0.12f), 0.00f),
                    new GradientColorKey(new Color(0.43f, 0.31f, 0.24f), 0.25f),
                    new GradientColorKey(new Color(0.60f, 0.67f, 0.73f), 0.40f),
                    new GradientColorKey(new Color(0.56f, 0.63f, 0.69f), 0.50f),
                    new GradientColorKey(new Color(0.63f, 0.53f, 0.44f), 0.70f),
                    new GradientColorKey(new Color(0.62f, 0.40f, 0.33f), 0.75f),
                    new GradientColorKey(new Color(0.13f, 0.08f, 0.12f), 0.82f),
                    new GradientColorKey(new Color(0.06f, 0.08f, 0.12f), 1.00f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }
#endif
    }
}
