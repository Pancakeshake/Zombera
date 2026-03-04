using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.AI.Sensors
{
    /// <summary>
    /// Detects recently reported sound events in hearing range.
    /// Noise is intentionally decoupled from specific gameplay systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseSensor : MonoBehaviour
    {
        [SerializeField] private float hearingRadius = 10f;
        [SerializeField] private float noiseMemorySeconds = 4f;
        [SerializeField] private bool drawHearingRadius;

        private static readonly List<NoiseSample> ActiveNoiseSamples = new List<NoiseSample>();

        public bool HasRecentNoise { get; private set; }
        public Vector3 LastNoisePosition { get; private set; }
        public float LastNoiseAge { get; private set; } = float.PositiveInfinity;
        public float HearingRadius => hearingRadius;

        /// <summary>
        /// Global entry point for any system to publish an audible event.
        /// </summary>
        public static void ReportNoise(Vector3 worldPosition, float radius, GameObject source = null, float intensity = 1f)
        {
            ActiveNoiseSamples.Add(new NoiseSample
            {
                Position = worldPosition,
                Radius = Mathf.Max(0f, radius),
                Intensity = Mathf.Max(0.01f, intensity),
                TimeCreated = Time.time,
                Source = source
            });
        }

        public void Sense(Unit self)
        {
            HasRecentNoise = false;
            LastNoisePosition = Vector3.zero;
            LastNoiseAge = float.PositiveInfinity;

            if (self == null)
            {
                return;
            }

            CleanupExpiredNoise();

            float bestScore = float.NegativeInfinity;
            Vector3 listenerPosition = self.transform.position;

            for (int i = 0; i < ActiveNoiseSamples.Count; i++)
            {
                NoiseSample sample = ActiveNoiseSamples[i];
                float age = Mathf.Max(0f, Time.time - sample.TimeCreated);
                float effectiveRadius = hearingRadius + (sample.Radius * sample.Intensity);
                float distance = Vector3.Distance(listenerPosition, sample.Position);

                if (distance > effectiveRadius)
                {
                    continue;
                }

                float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, effectiveRadius));
                float freshness = 1f - Mathf.Clamp01(age / Mathf.Max(0.01f, noiseMemorySeconds));
                float score = closeness * 0.7f + freshness * 0.3f;

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                HasRecentNoise = true;
                LastNoisePosition = sample.Position;
                LastNoiseAge = age;
            }

            // TODO: Add occlusion/dampening against walls and elevation.
            // TODO: Add noise categories (combat, footsteps, alarms) for behavior weighting.
        }

        private void CleanupExpiredNoise()
        {
            for (int i = ActiveNoiseSamples.Count - 1; i >= 0; i--)
            {
                if (Time.time - ActiveNoiseSamples[i].TimeCreated > noiseMemorySeconds)
                {
                    ActiveNoiseSamples.RemoveAt(i);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawHearingRadius)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, hearingRadius);
        }

        private struct NoiseSample
        {
            public Vector3 Position;
            public float Radius;
            public float Intensity;
            public float TimeCreated;
            public GameObject Source;
        }
    }
}