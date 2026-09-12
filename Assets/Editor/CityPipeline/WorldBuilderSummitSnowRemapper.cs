#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor
{
    /// <summary>
    /// Async in-place summit snow remap for an already-generated World Builder grid.
    /// Processes a few terrains per editor update so Unity stays responsive.
    /// </summary>
    public static class WorldBuilderSummitSnowRemapper
    {
        private const float ElevMarginMeters = 20f;
        private const int TerrainsPerTick = 2;

        private static bool _running;
        private static int _index;
        private static Terrain[] _terrains;
        private static WorldSurfacePainter _painter;
        private static MethodInfo _eval;
        private static int _snowIdx;
        private static int _snowRockIdx;
        private static int _cliffDarkIdx;
        private static float _snowMin;
        private static float _snowFull;
        private static float _rockMin;
        private static float _rockFull;
        private static float _peakMin;
        private static float _peakFull;
        private static int _cells;
        private static int _touched;
        private static double _beforeSum;
        private static double _afterSum;

        [MenuItem("Tools/World/Remap Summit Snow (In Place)")]
        public static void RemapFromMenu()
        {
            if (!StartRemap(out var error))
                Debug.LogError("[SummitSnowRemapper] " + error);
        }

        public static bool StartRemap(out string error)
        {
            error = null;
            if (_running)
            {
                error = "Summit snow remap already running.";
                return false;
            }

            _painter = Object.FindFirstObjectByType<WorldSurfacePainter>(FindObjectsInactive.Include);
            if (_painter == null)
            {
                error = "WorldSurfacePainter not found.";
                return false;
            }

            _terrains = Terrain.activeTerrains;
            if (_terrains == null || _terrains.Length == 0)
            {
                error = "No active terrains.";
                return false;
            }

            EnsurePainterBands(_painter);
            if (!TryResolveLayers(_terrains[0], out _snowIdx, out _snowRockIdx, out _cliffDarkIdx))
            {
                error = "Could not resolve Snow / SnowRock layers.";
                return false;
            }

            var so = new SerializedObject(_painter);
            _snowMin = so.FindProperty("_snowMinElevationMeters").floatValue;
            _snowFull = so.FindProperty("_snowFullElevationMeters").floatValue;
            _rockMin = so.FindProperty("_rockMinElevationMeters").floatValue;
            _rockFull = so.FindProperty("_rockFullElevationMeters").floatValue;
            _peakMin = so.FindProperty("_peakExclusiveMinElevationMeters").floatValue;
            _peakFull = so.FindProperty("_peakExclusiveFullElevationMeters").floatValue;
            _eval = typeof(WorldSurfacePainter).GetMethod(
                "EvaluateMountainSurfaceWeights",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_eval == null)
            {
                error = "EvaluateMountainSurfaceWeights missing.";
                return false;
            }

            _index = 0;
            _cells = 0;
            _touched = 0;
            _beforeSum = 0d;
            _afterSum = 0d;
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.LogWarning(
                "[SummitSnowRemapper] started terrains=" + _terrains.Length +
                " snow=" + _snowMin + "-" + _snowFull);
            return true;
        }

        private static void Tick()
        {
            if (!_running || _terrains == null)
            {
                Stop();
                return;
            }

            var budget = TerrainsPerTick;
            while (budget-- > 0 && _index < _terrains.Length)
            {
                var terrain = _terrains[_index++];
                if (terrain == null || terrain.terrainData == null)
                    continue;
                if (RemapTerrain(terrain))
                    _touched++;
            }

            EditorUtility.DisplayProgressBar(
                "Summit Snow Remap",
                "Terrains " + _index + "/" + _terrains.Length,
                (float)_index / _terrains.Length);

            if (_index < _terrains.Length)
                return;

            EditorUtility.SetDirty(_painter);
            var avgBefore = _cells > 0 ? _beforeSum / _cells : 0d;
            var avgAfter = _cells > 0 ? _afterSum / _cells : 0d;
            Debug.LogWarning(
                "[SummitSnowRemapper] done terrains=" + _touched +
                " cells=" + _cells +
                " avgSnow " + avgBefore.ToString("F3") + " -> " + avgAfter.ToString("F3"));
            Stop();
        }

        private static void Stop()
        {
            _running = false;
            EditorApplication.update -= Tick;
            EditorUtility.ClearProgressBar();
            _terrains = null;
            _painter = null;
            _eval = null;
        }

        private static void EnsurePainterBands(WorldSurfacePainter painter)
        {
            var so = new SerializedObject(painter);
            so.FindProperty("_rockMinElevationMeters").floatValue = 350f;
            so.FindProperty("_rockFullElevationMeters").floatValue = 550f;
            so.FindProperty("_snowMinElevationMeters").floatValue = 480f;
            so.FindProperty("_snowFullElevationMeters").floatValue = 720f;
            so.FindProperty("_peakExclusiveMinElevationMeters").floatValue = 580f;
            so.FindProperty("_peakExclusiveFullElevationMeters").floatValue = 880f;
            so.FindProperty("_cliffSnowMaxWeight").floatValue = 0.88f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(painter);
        }

        private static bool TryResolveLayers(
            Terrain terrain,
            out int snowIdx,
            out int snowRockIdx,
            out int cliffDarkIdx)
        {
            snowIdx = -1;
            snowRockIdx = -1;
            cliffDarkIdx = -1;
            var layers = terrain.terrainData.terrainLayers;
            if (layers == null)
                return false;
            for (var i = 0; i < layers.Length; i++)
            {
                var n = layers[i] != null ? layers[i].name : string.Empty;
                if (snowIdx < 0 &&
                    n.IndexOf("Snow", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) < 0)
                    snowIdx = i;
                if (n.IndexOf("dark_rock_with_snow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("SnowRock", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    snowRockIdx = i;
                if (n.IndexOf("grey_rocky_cliff", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("CliffDark", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    cliffDarkIdx = i;
            }

            if (snowIdx < 0) snowIdx = 10;
            if (snowRockIdx < 0) snowRockIdx = 24;
            return snowIdx >= 0;
        }

        /// <summary>Per-terrain scratch so the alphamap resample loop stays flat.</summary>
        private readonly struct TerrainRemapContext
        {
            public readonly TerrainData Data;
            public readonly float[,,] Map;
            public readonly Vector3 Position;
            public readonly Vector3 Size;
            public readonly float InvAw;
            public readonly float InvAh;
            public readonly int HmRes;
            public readonly float[,] Heights;
            public readonly int LayerCount;

            public TerrainRemapContext(
                TerrainData data,
                float[,,] map,
                Vector3 position,
                Vector3 size,
                float invAw,
                float invAh,
                int hmRes,
                float[,] heights,
                int layerCount)
            {
                Data = data;
                Map = map;
                Position = position;
                Size = size;
                InvAw = invAw;
                InvAh = invAh;
                HmRes = hmRes;
                Heights = heights;
                LayerCount = layerCount;
            }
        }

        private static bool RemapTerrain(Terrain terrain)
        {
            var td = terrain.terrainData;
            var aW = td.alphamapWidth;
            var aH = td.alphamapHeight;
            var aL = td.alphamapLayers;
            if (aL <= 0 || _snowIdx >= aL)
                return false;

            var hmRes = td.heightmapResolution;
            var context = new TerrainRemapContext(
                td,
                td.GetAlphamaps(0, 0, aW, aH),
                terrain.transform.position,
                td.size,
                1f / Mathf.Max(1, aW - 1),
                1f / Mathf.Max(1, aH - 1),
                hmRes,
                td.GetHeights(0, 0, hmRes, hmRes),
                aL);

            var dirty = false;
            for (var z = 0; z < aH; z++)
            {
                var nz = z * context.InvAh;
                var hz = Mathf.Clamp(Mathf.RoundToInt(nz * (hmRes - 1)), 0, hmRes - 1);
                for (var x = 0; x < aW; x++)
                {
                    if (ApplySnowAt(in context, x, z, x * context.InvAw, nz, hz))
                        dirty = true;
                }
            }

            if (!dirty)
                return false;
            td.SetAlphamaps(0, 0, context.Map);
            EditorUtility.SetDirty(td);
            return true;
        }

        /// <summary>Blends snow/rock weights into one alphamap cell; returns true when the cell changed.</summary>
        private static bool ApplySnowAt(
            in TerrainRemapContext context,
            int x,
            int z,
            float nx,
            float nz,
            int hz)
        {
            var hx = Mathf.Clamp(Mathf.RoundToInt(nx * (context.HmRes - 1)), 0, context.HmRes - 1);
            var y = context.Position.y + context.Heights[hz, hx] * context.Size.y;
            if (y < _snowMin - ElevMarginMeters)
                return false;

            _cells++;
            var worldX = context.Position.x + nx * context.Size.x;
            var worldZ = context.Position.z + nz * context.Size.z;
            var slope = context.Data.GetSteepness(nx, nz);
            var blend = (Vector3)_eval.Invoke(
                _painter,
                new object[]
                {
                    worldX, worldZ, slope, y, _rockMin, _rockFull, _snowMin, _snowFull
                });
            if (blend.z <= 0.001f && blend.y <= 0.001f)
                return false;

            var exclusiveT = Mathf.Clamp01(Mathf.InverseLerp(_peakMin, _peakFull, y));
            var mix = Mathf.Lerp(0.35f, 1f, exclusiveT);
            _beforeSum += context.Map[z, x, _snowIdx];

            ApplySnowBlend(in context, x, z, blend, mix);
            _afterSum += context.Map[z, x, _snowIdx];
            return true;
        }

        private static void ApplySnowBlend(
            in TerrainRemapContext context,
            int x,
            int z,
            Vector3 blend,
            float mix)
        {
            var keep = 1f - mix;
            for (var l = 0; l < context.LayerCount; l++)
                context.Map[z, x, l] *= keep;
            context.Map[z, x, _snowIdx] += blend.z * mix;
            if (_snowRockIdx >= 0 && _snowRockIdx < context.LayerCount)
                context.Map[z, x, _snowRockIdx] += blend.y * mix;
            if (_cliffDarkIdx >= 0 && _cliffDarkIdx < context.LayerCount)
                context.Map[z, x, _cliffDarkIdx] += blend.x * mix;

            NormalizeCell(in context, x, z);
        }

        /// <summary>Renormalizes one alphamap cell back to a layer-sum of 1 after blending.</summary>
        private static void NormalizeCell(in TerrainRemapContext context, int x, int z)
        {
            var sum = 0f;
            for (var l = 0; l < context.LayerCount; l++)
                sum += context.Map[z, x, l];
            if (sum <= 1e-5f)
                return;

            var inv = 1f / sum;
            for (var l = 0; l < context.LayerCount; l++)
                context.Map[z, x, l] *= inv;
        }
    }
}
#endif
