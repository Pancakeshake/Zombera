#region

using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Zombera.Environment
{
    internal struct DayNightRenderContext
    {
        public Light Sun;
        public Gradient SunColor;
        public AnimationCurve SunIntensity;
        public Gradient AmbientSkyColor;
        public Gradient AmbientEquatorColor;
        public Gradient AmbientGroundColor;
        public Gradient FogColor;
        public AnimationCurve FogDensity;
        public Material SkyboxMaterial;
        public AnimationCurve SkyboxAtmosphere;
        public AnimationCurve SkyboxExposure;
        public int AtmosphereThicknessShaderId;
        public int ExposureShaderId;
    }

    internal static class DayNightRenderApplier
    {
        public static void Apply(in DayNightRenderContext context, float normalizedTime, bool forceGi)
        {
            RotateSun(context.Sun, normalizedTime);

            if (context.Sun != null)
            {
                context.Sun.color = EvaluateGradient(context.SunColor, normalizedTime, Color.white);
                context.Sun.intensity = EvaluateCurve(context.SunIntensity, normalizedTime, 1f);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = EvaluateGradient(context.AmbientSkyColor, normalizedTime, RenderSettings.ambientSkyColor);
            RenderSettings.ambientEquatorColor = EvaluateGradient(context.AmbientEquatorColor, normalizedTime,
                RenderSettings.ambientEquatorColor);
            RenderSettings.ambientGroundColor = EvaluateGradient(context.AmbientGroundColor, normalizedTime,
                RenderSettings.ambientGroundColor);

            if (RenderSettings.fog)
            {
                RenderSettings.fogColor = EvaluateGradient(context.FogColor, normalizedTime, RenderSettings.fogColor);
                RenderSettings.fogDensity = EvaluateCurve(context.FogDensity, normalizedTime, RenderSettings.fogDensity);
            }

            if (context.SkyboxMaterial != null)
            {
                if (context.SkyboxMaterial.HasProperty(context.AtmosphereThicknessShaderId))
                {
                    var atmosphere = EvaluateCurve(context.SkyboxAtmosphere, normalizedTime,
                        context.SkyboxMaterial.GetFloat(context.AtmosphereThicknessShaderId));
                    context.SkyboxMaterial.SetFloat(context.AtmosphereThicknessShaderId, atmosphere);
                }

                if (context.SkyboxMaterial.HasProperty(context.ExposureShaderId))
                {
                    var exposure = EvaluateCurve(context.SkyboxExposure, normalizedTime,
                        context.SkyboxMaterial.GetFloat(context.ExposureShaderId));
                    context.SkyboxMaterial.SetFloat(context.ExposureShaderId, exposure);
                }
            }

            if (forceGi) DynamicGI.UpdateEnvironment();
        }

        private static void RotateSun(Light sun, float normalizedTime)
        {
            if (sun == null) return;

            var xAngle = normalizedTime * 360f - 90f;
            sun.transform.rotation = Quaternion.Euler(xAngle, -30f, 0f);
        }

        private static Color EvaluateGradient(Gradient gradient, float t, Color fallback)
        {
            return gradient == null ? fallback : gradient.Evaluate(t);
        }

        private static float EvaluateCurve(AnimationCurve curve, float t, float fallback)
        {
            return curve == null ? fallback : curve.Evaluate(t);
        }
    }
}
