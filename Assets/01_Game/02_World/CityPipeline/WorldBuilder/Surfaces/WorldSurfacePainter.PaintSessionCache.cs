using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Session-level coast/shore grids for <see cref="WorldSurfacePainter"/>.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private LandformField _paintSessionLandforms;
        private HydrologyPlan _paintSessionWater;
        private BiomeField _paintSessionBiomes;
        private bool[] _nearOceanCoastGrid;
        private float[] _shoreWeightGrid;
        private float[] _edgeDistGrid;
        private float[] _interiorMaskGrid;
        private int _shoreBiomeIndex = -1;
        private bool _paintSessionGridsReady;

        public void PreparePaintSession(
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes)
        {
            if (!TryBeginPaintSession(landforms, water, biomes))
                return;

            _paintSessionLandforms = landforms;
            _paintSessionWater = water;
            _paintSessionBiomes = biomes;
            EnsureNaturalRecordIndices(biomes, _cachedNaturalRecords ?? CollectNaturalBiomeRecords(_biomePalette));
            _shoreBiomeIndex = ResolveBiomeIndex(biomes, "Shore");

            var cellCount = landforms.Width * landforms.Height;
            EnsureSessionGridCapacity(cellCount);

            _paintLandforms = landforms;
            _paintLandformOrigin = landforms.OriginXZ;
            _paintInvLandformCellSize = 1f / landforms.CellSize;

            FillSessionCoastGrids(landforms, water, biomes);
            _paintSessionGridsReady = true;
            ClearCellSnapshots();
        }

        private bool TryBeginPaintSession(
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes)
        {
            if (landforms == null || biomes == null)
            {
                _paintSessionGridsReady = false;
                return false;
            }

            if (_paintSessionGridsReady &&
                landforms == _paintSessionLandforms &&
                water == _paintSessionWater &&
                biomes == _paintSessionBiomes)
                return false;

            if (!ValidatePaintSessionFieldSizes(landforms, water, biomes))
            {
                _paintSessionGridsReady = false;
                return false;
            }

            return true;
        }

        private bool ValidatePaintSessionFieldSizes(
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes)
        {
            if (biomes.Width != landforms.Width || biomes.Height != landforms.Height)
            {
                Debug.LogError(
                    "[WorldSurfacePainter] BiomeField size (" + biomes.Width + "x" + biomes.Height +
                    ") does not match LandformField (" + landforms.Width + "x" + landforms.Height + ").",
                    this);
                return false;
            }

            if (water != null && (water.Width != landforms.Width || water.Height != landforms.Height))
            {
                Debug.LogError(
                    "[WorldSurfacePainter] HydrologyPlan size (" + water.Width + "x" + water.Height +
                    ") does not match LandformField (" + landforms.Width + "x" + landforms.Height + ").",
                    this);
                return false;
            }

            return true;
        }

        private void FillSessionCoastGrids(
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes)
        {
            var bounds = _paintContextReady ? _paintSession.WorldBoundsXZ : default;
            var layout = _paintBoundaryLayout;
            var canSampleBoundary = _paintContextReady && _paintBoundaryReady && bounds.width > 1f;
            var sampling = new SessionCoastSampling(bounds, layout, canSampleBoundary);

            for (var z = 0; z < landforms.Height; z++)
            {
                for (var x = 0; x < landforms.Width; x++)
                    FillSessionCoastCell(landforms, water, biomes, x, z, in sampling);
            }
        }

        private readonly struct SessionCoastSampling
        {
            public readonly Rect Bounds;
            public readonly WorldMapBoundaryLayout Layout;
            public readonly bool CanSampleBoundary;

            public SessionCoastSampling(Rect bounds, WorldMapBoundaryLayout layout, bool canSampleBoundary)
            {
                Bounds = bounds;
                Layout = layout;
                CanSampleBoundary = canSampleBoundary;
            }
        }

        private void FillSessionCoastCell(
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes,
            int x,
            int z,
            in SessionCoastSampling sampling)
        {
            var cell = landforms.Index(x, z);
            _nearOceanCoastGrid[cell] = IsNearOceanCoast(water, landforms, x, z);
            _shoreWeightGrid[cell] = _shoreBiomeIndex >= 0
                ? biomes.Weights[biomes.WeightIndex(cell, _shoreBiomeIndex)]
                : 0f;

            var center = landforms.CellCenterXZ(x, z);
            _edgeDistGrid[cell] = SampleSessionEdgeDistance(
                center, sampling.Bounds, sampling.Layout, sampling.CanSampleBoundary);
            _interiorMaskGrid[cell] = SampleSessionInteriorMask(
                center, sampling.Bounds, sampling.Layout, sampling.CanSampleBoundary);
        }

        private static float SampleSessionEdgeDistance(
            Vector2 center,
            Rect bounds,
            WorldMapBoundaryLayout layout,
            bool canSampleBoundary)
        {
            if (canSampleBoundary &&
                WorldMapBoundaryUtility.TryGetSideAwareOceanEdgeDistance(
                    center.x, center.y, bounds, layout, out var edgeDist))
                return edgeDist;

            return 9999f;
        }

        private float SampleSessionInteriorMask(
            Vector2 center,
            Rect bounds,
            WorldMapBoundaryLayout layout,
            bool canSampleBoundary)
        {
            if (canSampleBoundary && _paintLandformProfile != null)
            {
                return InteriorLandformRelief.EvaluateInteriorMask(
                    center.x, center.y, bounds, layout, _paintLandformProfile);
            }

            return 1f;
        }

        private void EnsureSessionGridCapacity(int cellCount)
        {
            if (_nearOceanCoastGrid == null || _nearOceanCoastGrid.Length != cellCount)
                _nearOceanCoastGrid = new bool[cellCount];
            if (_shoreWeightGrid == null || _shoreWeightGrid.Length != cellCount)
                _shoreWeightGrid = new float[cellCount];
            if (_edgeDistGrid == null || _edgeDistGrid.Length != cellCount)
                _edgeDistGrid = new float[cellCount];
            if (_interiorMaskGrid == null || _interiorMaskGrid.Length != cellCount)
                _interiorMaskGrid = new float[cellCount];
        }

        private void TryApplySessionCoastData(ref PaintCellSnapshot snapshot, int cell)
        {
            if (!_paintSessionGridsReady || cell < 0)
                return;

            if (cell >= _nearOceanCoastGrid.Length)
                return;

            snapshot.NearOceanCoast = _nearOceanCoastGrid[cell];
            snapshot.ShoreWeight = _shoreWeightGrid[cell];
            snapshot.EdgeDistMeters = _edgeDistGrid[cell];
            snapshot.InteriorMask = _interiorMaskGrid[cell];

            var barrierDepth = _paintLandformProfile != null
                ? _paintLandformProfile.EdgeBarrierDepthMeters
                : 650f;
            snapshot.InOceanBarrierStrip = snapshot.EdgeDistMeters < barrierDepth;
        }
    }
}
