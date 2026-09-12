using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Deterministic 2D value / FBM / ridged / domain-warp noise.</summary>
    public sealed class DeterministicNoise2D
    {
        private readonly int _seed;

        public DeterministicNoise2D(int seed)
        {
            _seed = seed;
        }

        public DeterministicNoise2D(DeterministicRng rng)
        {
            _seed = rng != null ? rng.NextInt() : 0;
        }

        public float Sample(float x, float z) => ValueNoise(x, z);

        public float Fbm(float x, float z, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            var sum = 0f;
            var amp = 1f;
            var freq = 1f;
            var max = 0f;
            var count = Mathf.Max(1, octaves);

            for (var i = 0; i < count; i++)
            {
                sum += ValueNoise(x * freq, z * freq) * amp;
                max += amp;
                amp *= gain;
                freq *= lacunarity;
            }

            return max > 0f ? sum / max : 0f;
        }

        public float Ridged(float x, float z, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            var sum = 0f;
            var amp = 0.5f;
            var freq = 1f;
            var count = Mathf.Max(1, octaves);

            for (var i = 0; i < count; i++)
            {
                var n = 1f - Mathf.Abs(ValueNoise(x * freq, z * freq) * 2f - 1f);
                n *= n;
                sum += n * amp;
                amp *= gain;
                freq *= lacunarity;
            }

            return Mathf.Clamp01(sum);
        }

        public float DomainWarp(float x, float z, float warpAmplitude, float warpScale)
        {
            WarpCoordinates(x, z, warpAmplitude, warpScale, out var wx, out var wz);
            return Sample(wx, wz);
        }

        /// <summary>Two independent low-frequency warp offsets applied to sample coordinates.</summary>
        public void WarpCoordinates(
            float x,
            float z,
            float warpAmplitude,
            float warpScale,
            out float warpedX,
            out float warpedZ)
        {
            if (warpScale <= 0.0001f || warpAmplitude <= 0f)
            {
                warpedX = x;
                warpedZ = z;
                return;
            }

            var ox = Fbm(x / warpScale, z / warpScale, 3) * 2f - 1f;
            var oz = Fbm((x + 19.1f) / warpScale, (z + 67.3f) / warpScale, 3) * 2f - 1f;
            warpedX = x + ox * warpAmplitude;
            warpedZ = z + oz * warpAmplitude;
        }

        private float ValueNoise(float x, float z)
        {
            var x0 = Mathf.FloorToInt(x);
            var z0 = Mathf.FloorToInt(z);
            var tx = x - x0;
            var tz = z - z0;
            tx = tx * tx * (3f - 2f * tx);
            tz = tz * tz * (3f - 2f * tz);

            var n00 = Hash01(x0, z0);
            var n10 = Hash01(x0 + 1, z0);
            var n01 = Hash01(x0, z0 + 1);
            var n11 = Hash01(x0 + 1, z0 + 1);

            var nx0 = Mathf.Lerp(n00, n10, tx);
            var nx1 = Mathf.Lerp(n01, n11, tx);
            return Mathf.Lerp(nx0, nx1, tz);
        }

        private float Hash01(int x, int z)
        {
            unchecked
            {
                var h = (uint)_seed;
                h ^= (uint)x * 374761393u;
                h ^= (uint)z * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }
    }
}
