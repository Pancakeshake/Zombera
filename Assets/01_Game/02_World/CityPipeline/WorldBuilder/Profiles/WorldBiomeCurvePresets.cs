using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Reusable biome classification curve presets for palette defaults and tests.</summary>
    public static class WorldBiomeCurvePresets
    {
        public static AnimationCurve Bell(float center, float width, float peak = 1f)
        {
            var half = Mathf.Max(0.02f, width * 0.5f);
            return new AnimationCurve(
                new Keyframe(Mathf.Clamp01(center - half), 0f),
                new Keyframe(center, peak),
                new Keyframe(Mathf.Clamp01(center + half), 0f));
        }

        public static AnimationCurve Range(float min, float max, float peak = 1f) =>
            new AnimationCurve(
                new Keyframe(0f, min <= 0f ? peak : 0f),
                new Keyframe(min, 0f),
                new Keyframe(max, peak),
                new Keyframe(1f, max >= 1f ? peak : 0f));

        public static AnimationCurve HighAbove(float threshold, float peak = 1f) =>
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(threshold, 0f),
                new Keyframe(Mathf.Clamp01(threshold + 0.08f), peak),
                new Keyframe(1f, peak));

        public static AnimationCurve LowBelow(float threshold, float peak = 1f) =>
            new AnimationCurve(
                new Keyframe(0f, peak),
                new Keyframe(threshold, peak),
                new Keyframe(Mathf.Clamp01(threshold + 0.08f), 0f),
                new Keyframe(1f, 0f));

        public static AnimationCurve SlopeLowToMid(float peak = 1f) =>
            new AnimationCurve(
                new Keyframe(0f, peak),
                new Keyframe(0.35f, peak * 0.85f),
                new Keyframe(0.65f, peak * 0.35f),
                new Keyframe(1f, 0f));

        public static AnimationCurve SlopeMidToHigh(float peak = 1f) =>
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.35f, 0f),
                new Keyframe(0.55f, peak * 0.45f),
                new Keyframe(1f, peak));
    }
}
