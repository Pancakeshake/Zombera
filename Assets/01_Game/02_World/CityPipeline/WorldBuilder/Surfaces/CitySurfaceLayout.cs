using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Surface categories a runtime city can stamp into the terrain splat.</summary>
    public enum CitySurfaceType
    {
        Asphalt,
        Concrete,
        Paver,
        Garden,
        Gravel
    }

    /// <summary>
    ///     Seed-deterministic geometry knobs shared by the MapMagic city surface graph node
    ///     and the runtime pin service.
    /// </summary>
    [Serializable]
    public struct CitySurfaceParams
    {
        [Min(8f)] public float blockSize;
        [Min(2f)] public float streetWidth;
        [Min(0f)] public float sidewalkWidth;
        [Min(0f)] public float backyardDepth;
        [Min(0f)] public float blendMeters;
        [Range(0f, 1f)] public float parkChance;

        public static CitySurfaceParams Default => new CitySurfaceParams
        {
            blockSize = 80f,
            streetWidth = 12f,
            sidewalkWidth = 2f,
            backyardDepth = 6f,
            blendMeters = 1.5f,
            parkChance = 0.15f
        };
    }

    /// <summary>One rectangular paint zone in world XZ space (Rect.y maps to world Z).</summary>
    [Serializable]
    public struct CitySurfaceZone
    {
        public Rect bounds;
        public CitySurfaceType type;
        public float blendMeters;

        public CitySurfaceZone(Rect bounds, CitySurfaceType type, float blendMeters)
        {
            this.bounds = bounds;
            this.type = type;
            this.blendMeters = blendMeters;
        }
    }

    /// <summary>Pure data result of planning a city's surfaces. Safe to read on worker threads.</summary>
    public sealed class CitySurfaceLayout
    {
        public Vector2 center;
        public float radius;
        public int seed;
        public readonly List<CitySurfaceZone> zones = new List<CitySurfaceZone>();
    }

    /// <summary>
    ///     Plans a deterministic grid of city blocks (concrete slabs, paver sidewalks,
    ///     garden backyards, occasional gravel aprons) around a center. No scene access
    ///     and no UnityEngine.Random — the same (center, radius, seed, params) always
    ///     yields the same layout, so the MapMagic graph node and runtime builders agree.
    /// </summary>
    public static class CitySurfacePlanner
    {
        public static CitySurfaceLayout Generate(Vector2 center, float radius, int seed, CitySurfaceParams p)
        {
            var layout = new CitySurfaceLayout { center = center, radius = radius, seed = seed };
            if (radius <= 0f || p.blockSize < 8f)
                return layout;

            var inset = p.streetWidth * 0.5f + p.sidewalkWidth;
            var count = Mathf.CeilToInt(radius / p.blockSize) + 1;

            for (var bx = -count; bx <= count; bx++)
            {
                for (var bz = -count; bz <= count; bz++)
                {
                    var blockCenter = center + new Vector2((bx + 0.5f) * p.blockSize, (bz + 0.5f) * p.blockSize);
                    if (Vector2.Distance(blockCenter, center) > radius)
                        continue;

                    var rng = new System.Random(HashBlock(seed, bx, bz));

                    // Park block — leave the graph ground untouched.
                    if (rng.NextDouble() < p.parkChance)
                        continue;

                    AppendBlock(layout, center, bx, bz, inset, p, rng);
                }
            }

            return layout;
        }

        private static void AppendBlock(
            CitySurfaceLayout layout, Vector2 center, int bx, int bz,
            float inset, CitySurfaceParams p, System.Random rng)
        {
            var x0 = center.x + bx * p.blockSize;
            var z0 = center.y + bz * p.blockSize;

            var ix0 = x0 + inset;
            var iz0 = z0 + inset;
            var iw = Mathf.Max(1f, p.blockSize - inset * 2f);
            var ih = Mathf.Max(1f, p.blockSize - inset * 2f);

            // Garden strip along the back edge.
            var gardenDepth = Mathf.Min(p.backyardDepth, ih * 0.4f);
            if (gardenDepth > 0.5f)
            {
                layout.zones.Add(new CitySurfaceZone(
                    new Rect(ix0, iz0 + ih - gardenDepth, iw, gardenDepth),
                    CitySurfaceType.Garden, p.blendMeters));
            }

            // Concrete slab: the buildable lot surface in front of the garden.
            var slabH = ih - gardenDepth;
            if (slabH > 1f)
            {
                layout.zones.Add(new CitySurfaceZone(
                    new Rect(ix0, iz0, iw, slabH),
                    CitySurfaceType.Concrete, p.blendMeters));
            }

            // Paver sidewalk ring hugging the street edge.
            var sw = p.sidewalkWidth;
            if (sw > 0.05f)
            {
                var f0 = x0 + p.streetWidth * 0.5f;
                var f1 = x0 + p.blockSize - p.streetWidth * 0.5f;
                var g0 = z0 + p.streetWidth * 0.5f;
                var g1 = z0 + p.blockSize - p.streetWidth * 0.5f;
                var fw = Mathf.Max(0.1f, f1 - f0);
                var fh = Mathf.Max(0.1f, g1 - g0);

                var blend = Mathf.Min(p.blendMeters, sw * 0.5f);
                layout.zones.Add(new CitySurfaceZone(new Rect(f0, g0, fw, sw), CitySurfaceType.Paver, blend));
                layout.zones.Add(new CitySurfaceZone(new Rect(f0, g1 - sw, fw, sw), CitySurfaceType.Paver, blend));
                layout.zones.Add(new CitySurfaceZone(new Rect(f0, g0 + sw, sw, Mathf.Max(0.1f, fh - sw * 2f)), CitySurfaceType.Paver, blend));
                layout.zones.Add(new CitySurfaceZone(new Rect(f1 - sw, g0 + sw, sw, Mathf.Max(0.1f, fh - sw * 2f)), CitySurfaceType.Paver, blend));
            }

            // Occasional gravel apron right at the lot front for variety.
            if (rng.NextDouble() < 0.25f)
            {
                var apronW = Mathf.Min(5f, iw * 0.6f);
                var apronH = Mathf.Min(4f, ih * 0.3f);
                layout.zones.Add(new CitySurfaceZone(
                    new Rect(ix0 + (iw - apronW) * 0.5f, iz0 + 1f, apronW, apronH),
                    CitySurfaceType.Gravel, p.blendMeters * 0.6f));
            }
        }

        private static int HashBlock(int seed, int bx, int bz)
        {
            unchecked
            {
                return seed * 73856093 ^ bx * 19349663 ^ bz * 83492791;
            }
        }
    }
}
