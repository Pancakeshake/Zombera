using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Grid cost field built from terrain query samples for road pathfinding.</summary>
    public sealed class WorldCostField : IWorldCostField
    {
        private readonly float[] _heights;
        private readonly float[] _slopes;
        private readonly float[] _buildability;
        private readonly float[] _noBuild;
        private readonly float[] _waterDepth;
        private readonly float[] _biomeRoadMultiplier;
        private readonly bool[] _hasSample;
        private readonly WorldCostFieldOptions _options;
        private readonly int _width;
        private readonly int _height;

        public Rect BoundsXZ { get; }
        public float CellSizeMeters { get; }
        public int Width => _width;
        public int Height => _height;

        public bool HasSample(int x, int z) =>
            InBounds(x, z) && _hasSample[Index(x, z)];

        public WorldCostField(
            Rect boundsXZ,
            float cellSizeMeters,
            int width,
            int height,
            float[] heights,
            float[] slopes,
            float[] buildability,
            float[] noBuild,
            float[] waterDepth,
            float[] biomeRoadMultiplier,
            bool[] hasSample,
            WorldCostFieldOptions options)
        {
            BoundsXZ = boundsXZ;
            CellSizeMeters = cellSizeMeters;
            _width = width;
            _height = height;
            _heights = heights;
            _slopes = slopes;
            _buildability = buildability;
            _noBuild = noBuild;
            _waterDepth = waterDepth;
            _biomeRoadMultiplier = biomeRoadMultiplier;
            _hasSample = hasSample;
            _options = options ?? new WorldCostFieldOptions();
        }

        public bool IsTraversable(int x, int z, RoadClass roadClass)
        {
            if (!InBounds(x, z) || !_hasSample[Index(x, z)]) return false;

            var i = Index(x, z);
            if (_noBuild[i] >= _options.MaxNoBuildMask) return false;
            if (_waterDepth[i] > ResolveMaxWaterDepth(roadClass)) return false;

            var maxSlope = ResolveMaxSlope(roadClass);
            return maxSlope <= 0f || _slopes[i] <= maxSlope;
        }

        public float GetHeight(int x, int z)
        {
            if (!InBounds(x, z)) return 0f;
            return _heights[Index(x, z)];
        }

        public float GetTraversalCost(int fromX, int fromZ, int toX, int toZ, RoadClass roadClass)
        {
            if (!IsTraversable(toX, toZ, roadClass)) return float.PositiveInfinity;

            var from = Index(fromX, fromZ);
            var to = Index(toX, toZ);
            var step = CellSizeMeters;
            if (fromX != toX && fromZ != toZ)
                step *= 1.41421356f;

            var slope = _slopes[to];
            var relief = Mathf.Abs(_heights[to] - _heights[from]);
            GetSoftCostFactors(toX, toZ, roadClass, out var softAdditive, out var softMultiplier);
            return (step + slope * 0.35f + relief * 0.2f + softAdditive) * softMultiplier;
        }

        public void GetSoftCostFactors(int x, int z, RoadClass roadClass, out float additive, out float multiplier)
        {
            _ = roadClass;
            additive = 0f;
            multiplier = 1f;
            if (!InBounds(x, z) || !_hasSample[Index(x, z)])
                return;

            var i = Index(x, z);
            var waterMul = Mathf.Max(0f, _options.WaterSoftCostPerMeterDepth);
            additive = _waterDepth[i] * waterMul + (1f - Mathf.Clamp01(_buildability[i])) * 2f;
            var biomeMul = Mathf.Max(0.01f, _biomeRoadMultiplier[i]);
            multiplier = biomeMul * ResolveOrogenCostMultiplier(x, z);
        }

        private float ResolveMaxWaterDepth(RoadClass roadClass)
        {
            if (roadClass == RoadClass.Highway || roadClass == RoadClass.Arterial)
                return Mathf.Max(
                    _options.MaxTraversableWaterDepthMeters,
                    _options.MaxBridgeableWaterDepthMeters);

            return _options.MaxTraversableWaterDepthMeters;
        }

        private float ResolveOrogenCostMultiplier(int x, int z)
        {
            var orogen = _options.Orogen;
            if (orogen == null)
                return 1f;

            var world = CellCenterXZ(x, z);
            var pass = orogen.SamplePassAttract(world.x, world.y, _options.PassAttractHalfWidthMeters);
            if (pass > 0.05f)
                return Mathf.Lerp(1f, Mathf.Max(0.2f, _options.PassAttractCostMultiplier), pass);

            var core = orogen.SampleOrogenCoreMask(world.x, world.y);
            if (core > 0.2f)
                return Mathf.Lerp(1f, Mathf.Max(1f, _options.OrogenCoreCostMultiplier), core);

            return 1f;
        }

        private Vector2 CellCenterXZ(int x, int z)
        {
            return new Vector2(
                BoundsXZ.xMin + (x + 0.5f) * CellSizeMeters,
                BoundsXZ.yMin + (z + 0.5f) * CellSizeMeters);
        }

        private float ResolveMaxSlope(RoadClass roadClass)
        {
            var settings = _options.RoadSettings;
            if (settings == null) return 90f;
            if (!settings.usePerRoadClassSlopeLimits)
                return settings.maxCityRoadSlopeDegrees;

            return roadClass switch
            {
                RoadClass.Highway => settings.ResolveHighwayPathfindingMaxSlopeDegrees(),
                RoadClass.Arterial => settings.maxArterialRoadSlopeDegrees,
                _ => settings.maxLocalRoadSlopeDegrees
            };
        }

        private bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < _width && z < _height;
        private int Index(int x, int z) => z * _width + x;
    }
}
