using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Height and traversal-cost grid sampled from active terrains for road A*.
    /// </summary>
    public sealed partial class TerrainRoadCostField
    {
        private readonly Terrain[] _terrains;
        private readonly float _cellSize;
        private readonly Vector2 _originXZ;
        private readonly int _width;
        private readonly int _height;
        private readonly float[] _heights;
        private readonly float[] _localRelief;
        private readonly bool[] _hasSample;
        private readonly float _maxRelief;
        private readonly float[] _softAdditive;
        private readonly float[] _softMultiplier;
        private readonly float _heuristicStepScale;
        private bool[] _pinOverrides;
        private bool _heightRangeCached;
        private float _cachedMinHeight;
        private float _cachedMaxHeight;

        /// <summary>Soft preference floor used by A* heuristic (must stay ≤ real soft mul).</summary>
        internal const float SoftPreferenceMulMin = 0.55f;
        /// <summary>Soft preference ceiling — biome/orogen bias, not Ocean×99 hard walls.</summary>
        internal const float SoftPreferenceMulMax = 2.25f;
        /// <summary>Max soft additive expressed as multiples of cell size.</summary>
        internal const float SoftPreferenceAddMaxCells = 1.25f;

        public float CellSize => _cellSize;
        public Vector2 OriginXZ => _originXZ;
        public int Width => _width;
        public int Height => _height;
        public bool IsValid => _width > 1 && _height > 1;
        /// <summary>Minimum soft step scale across the field (1 when soft arrays absent).</summary>
        public float HeuristicStepScale => _heuristicStepScale;

        private TerrainRoadCostField(
            Terrain[] terrains,
            float cellSize,
            Vector2 originXZ,
            int width,
            int height,
            float[] heights,
            float[] localRelief,
            bool[] hasSample,
            float maxRelief,
            float[] softAdditive = null,
            float[] softMultiplier = null)
        {
            _terrains = terrains;
            _cellSize = cellSize;
            _originXZ = originXZ;
            _width = width;
            _height = height;
            _heights = heights;
            _localRelief = localRelief;
            _hasSample = hasSample;
            _maxRelief = maxRelief;
            _softAdditive = softAdditive;
            _softMultiplier = softMultiplier;
            _heuristicStepScale = ResolveHeuristicStepScale(softMultiplier, SoftPreferenceMulMin);
        }

        private static float ResolveHeuristicStepScale(float[] softMultiplier, float floor)
        {
            if (softMultiplier == null || softMultiplier.Length == 0)
                return 1f;

            var min = float.MaxValue;
            for (var i = 0; i < softMultiplier.Length; i++)
            {
                var m = softMultiplier[i];
                if (m < min)
                    min = m;
            }

            if (min >= float.MaxValue * 0.5f)
                return 1f;

            return Mathf.Clamp(min, floor, 1f);
        }

        public static TerrainRoadCostField TryBuild(
            Rect worldBoundsXZ,
            Terrain[] terrains,
            RoadNetworkSettings settings,
            float? cellSizeOverride = null)
        {
            if (terrains == null || terrains.Length == 0 || settings == null) return null;

            var cellSize = cellSizeOverride ?? Mathf.Max(4f, settings.pathfindingCellSizeMeters);
            var width = Mathf.Max(2, Mathf.CeilToInt(worldBoundsXZ.width / cellSize));
            var height = Mathf.Max(2, Mathf.CeilToInt(worldBoundsXZ.height / cellSize));

            var heights = new float[width * height];
            var localRelief = new float[width * height];
            var hasSample = new bool[width * height];
            var origin = new Vector2(worldBoundsXZ.xMin, worldBoundsXZ.yMin);
            var reliefRadius = Mathf.Max(cellSize, settings.localReliefSampleRadiusMeters);
            var reliefCells = Mathf.Max(1, Mathf.CeilToInt(reliefRadius / cellSize));
            var maxRelief = 0f;

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var world = origin + new Vector2((x + 0.5f) * cellSize, (z + 0.5f) * cellSize);
                    if (TrySampleTerrainHeight(terrains, new Vector3(world.x, 0f, world.y), out var h))
                    {
                        heights[z * width + x] = h;
                        hasSample[z * width + x] = true;
                    }
                }
            }

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = z * width + x;
                    if (!hasSample[index]) continue;

                    var centerHeight = heights[index];
                    var localMin = centerHeight;
                    for (var dz = -reliefCells; dz <= reliefCells; dz++)
                    {
                        for (var dx = -reliefCells; dx <= reliefCells; dx++)
                        {
                            if (!TryGetHeightInternal(x + dx, z + dz, heights, hasSample, width, height, out var sampleHeight))
                                continue;

                            localMin = Mathf.Min(localMin, sampleHeight);
                        }
                    }

                    var relief = Mathf.Max(0f, centerHeight - localMin);
                    localRelief[index] = relief;
                    maxRelief = Mathf.Max(maxRelief, relief);
                }
            }

            return new TerrainRoadCostField(
                terrains, cellSize, origin, width, height, heights, localRelief, hasSample, maxRelief,
                softAdditive: null, softMultiplier: null);
        }

        private static bool TryGetHeightInternal(
            int cellX,
            int cellZ,
            float[] heights,
            bool[] hasSample,
            int width,
            int height,
            out float sampleHeight)
        {
            sampleHeight = 0f;
            if (cellX < 0 || cellZ < 0 || cellX >= width || cellZ >= height) return false;
            var index = cellZ * width + cellX;
            if (!hasSample[index]) return false;
            sampleHeight = heights[index];
            return true;
        }

        public static Rect ComputeBounds(RoadNetworkRuntime network, float marginMeters)
        {
            var margin = Mathf.Max(0f, marginMeters);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var found = false;

            if (network?.Roads != null)
            {
                for (var r = 0; r < network.Roads.Count; r++)
                {
                    var road = network.Roads[r];
                    if (road?.pointsXZ == null) continue;

                    for (var i = 0; i < road.pointsXZ.Count; i++)
                    {
                        var p = road.pointsXZ[i];
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                        found = true;
                    }
                }
            }

            if (!found)
                return default;

            return Rect.MinMaxRect(min.x - margin, min.y - margin, max.x + margin, max.y + margin);
        }

        public bool Contains(Vector2 worldXZ)
        {
            return TryWorldToCell(worldXZ, out var x, out var z) && IsTraversable(x, z);
        }

        public bool TryWorldToCell(Vector2 worldXZ, out int cellX, out int cellZ)
        {
            var local = worldXZ - _originXZ;
            cellX = Mathf.FloorToInt(local.x / _cellSize);
            cellZ = Mathf.FloorToInt(local.y / _cellSize);
            return cellX >= 0 && cellZ >= 0 && cellX < _width && cellZ < _height;
        }

        public Vector2 CellCenterWorld(int cellX, int cellZ)
        {
            return _originXZ + new Vector2((cellX + 0.5f) * _cellSize, (cellZ + 0.5f) * _cellSize);
        }

        public int ToIndex(int cellX, int cellZ) => cellZ * _width + cellX;

        public bool IsTraversable(int cellX, int cellZ)
        {
            if (cellX < 0 || cellZ < 0 || cellX >= _width || cellZ >= _height) return false;
            var index = ToIndex(cellX, cellZ);
            if (_pinOverrides != null && _pinOverrides[index])
                return true;
            return _hasSample[index];
        }

        public bool TryGetHeight(int cellX, int cellZ, out float height)
        {
            height = 0f;
            if (cellX < 0 || cellZ < 0 || cellX >= _width || cellZ >= _height)
                return false;
            var index = ToIndex(cellX, cellZ);
            var pinned = _pinOverrides != null && _pinOverrides[index];
            if (!_hasSample[index] && !pinned)
                return false;
            height = _heights[index];
            return true;
        }

        /// <summary>
        ///     Highway-plan-only: mark endpoint cells (and inward rim) traversable without
        ///     changing global FromWorldCostField soft-block semantics.
        /// </summary>
        public void ClearPinnedCells()
        {
            InvalidateHeightRangeCache();
            if (_pinOverrides == null)
                return;
            for (var i = 0; i < _pinOverrides.Length; i++)
                _pinOverrides[i] = false;
        }

        private void InvalidateHeightRangeCache()
        {
            _heightRangeCached = false;
        }

        public bool TryPinEndpoint(Vector2 worldXZ, Vector2 inwardTowardCenter, int inwardCells = 2)
        {
            if (!TryWorldToCell(worldXZ, out var x, out var z))
                return false;

            EnsurePinOverrides();
            PinCell(x, z);

            var dir = inwardTowardCenter - worldXZ;
            var stepX = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
                ? (dir.x > 0f ? 1 : dir.x < 0f ? -1 : 0)
                : 0;
            var stepZ = stepX == 0
                ? (dir.y > 0f ? 1 : dir.y < 0f ? -1 : 0)
                : 0;
            if (stepX == 0 && stepZ == 0)
                return true;

            var cx = x;
            var cz = z;
            var steps = Mathf.Max(0, inwardCells);
            for (var i = 0; i < steps; i++)
            {
                cx += stepX;
                cz += stepZ;
                if (cx < 0 || cz < 0 || cx >= _width || cz >= _height)
                    break;
                PinCell(cx, cz);
            }

            return true;
        }

        private void EnsurePinOverrides()
        {
            if (_pinOverrides != null)
                return;
            _pinOverrides = new bool[_width * _height];
        }

        private void PinCell(int cellX, int cellZ)
        {
            var index = ToIndex(cellX, cellZ);
            _pinOverrides[index] = true;
            if (_hasSample[index])
                return;

            // Heights are retained when FromWorldCostField soft-blocks a cell.
            _hasSample[index] = true;
            InvalidateHeightRangeCache();
            if (!HasNeighborHeight(cellX, cellZ) && _heights[index] == 0f)
                FillPinnedHeightFromNeighbors(cellX, cellZ, index);
        }

        private bool HasNeighborHeight(int cellX, int cellZ)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var nx = cellX + dx;
                    var nz = cellZ + dz;
                    if (nx < 0 || nz < 0 || nx >= _width || nz >= _height) continue;
                    if (_hasSample[ToIndex(nx, nz)] ||
                        (_pinOverrides != null && _pinOverrides[ToIndex(nx, nz)]))
                        return true;
                }
            }

            return false;
        }

        private void FillPinnedHeightFromNeighbors(int cellX, int cellZ, int index)
        {
            var sum = 0f;
            var count = 0;
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var nx = cellX + dx;
                    var nz = cellZ + dz;
                    if (nx < 0 || nz < 0 || nx >= _width || nz >= _height) continue;
                    var ni = ToIndex(nx, nz);
                    if (!_hasSample[ni]) continue;
                    sum += _heights[ni];
                    count++;
                }
            }

            if (count > 0)
                _heights[index] = sum / count;
        }

        public float GetSlopeRatio(int cellX, int cellZ)
        {
            if (!TryGetHeight(cellX, cellZ, out var center)) return float.MaxValue;

            var maxSlope = 0f;
            SampleNeighborSlope(cellX + 1, cellZ, center, ref maxSlope);
            SampleNeighborSlope(cellX - 1, cellZ, center, ref maxSlope);
            SampleNeighborSlope(cellX, cellZ + 1, center, ref maxSlope);
            SampleNeighborSlope(cellX, cellZ - 1, center, ref maxSlope);
            SampleNeighborSlope(cellX + 1, cellZ + 1, center, ref maxSlope, diagonal: true);
            SampleNeighborSlope(cellX - 1, cellZ + 1, center, ref maxSlope, diagonal: true);
            SampleNeighborSlope(cellX + 1, cellZ - 1, center, ref maxSlope, diagonal: true);
            SampleNeighborSlope(cellX - 1, cellZ - 1, center, ref maxSlope, diagonal: true);
            return maxSlope;
        }

        public bool IsPinned(int cellX, int cellZ)
        {
            if (_pinOverrides == null) return false;
            if (cellX < 0 || cellZ < 0 || cellX >= _width || cellZ >= _height) return false;
            return _pinOverrides[ToIndex(cellX, cellZ)];
        }

        public float GetTraversalCost(
            int fromX,
            int fromZ,
            int toX,
            int toZ,
            float maxSlopeRatio,
            float slopeCostWeight,
            float elevationCostWeight,
            float mountainProximityCostWeight,
            float minHeight,
            float heightRange)
        {
            if (!IsTraversable(toX, toZ)) return float.PositiveInfinity;

            var step = toX != fromX && toZ != fromZ ? _cellSize * 1.4142135f : _cellSize;
            if (!TryGetHeight(fromX, fromZ, out var fromH) || !TryGetHeight(toX, toZ, out var toH))
                return float.PositiveInfinity;

            var edgeSlope = Mathf.Abs(toH - fromH) / Mathf.Max(0.01f, step);
            var pinTouch = IsPinned(fromX, fromZ) || IsPinned(toX, toZ);
            if (!pinTouch && edgeSlope > maxSlopeRatio) return float.PositiveInfinity;

            var cellSlope = GetSlopeRatio(toX, toZ);
            if (!pinTouch && cellSlope > maxSlopeRatio) return float.PositiveInfinity;

            var slopeTerm = maxSlopeRatio > 0.0001f
                ? slopeCostWeight * Mathf.Pow(Mathf.Min(cellSlope, maxSlopeRatio * 3f) / maxSlopeRatio, 2f)
                : 0f;

            var elevNorm = heightRange > 0.01f
                ? (toH - minHeight) / heightRange
                : 0f;
            var elevTerm = elevationCostWeight * elevNorm;

            var relief = _localRelief[ToIndex(toX, toZ)];
            var reliefNorm = _maxRelief > 0.01f ? relief / _maxRelief : 0f;
            var mountainTerm = mountainProximityCostWeight * reliefNorm;

            var toIndex = ToIndex(toX, toZ);
            var softAdd = _softAdditive != null ? _softAdditive[toIndex] : 0f;
            var softMul = _softMultiplier != null ? _softMultiplier[toIndex] : 1f;

            // Geometric core stays admissible vs geometric heuristic; soft is a bounded preference bias.
            var geometric = step + slopeTerm + elevTerm + mountainTerm;
            var softBias = softAdd + step * (softMul - 1f);
            var maxSoftBias = step * (SoftPreferenceMulMax - 1f) + SoftPreferenceAddMaxCells * _cellSize;
            var minSoftBias = step * (SoftPreferenceMulMin - 1f);
            softBias = Mathf.Clamp(softBias, minSoftBias, maxSoftBias);
            return geometric + softBias;
        }

        public void ComputeHeightRange(out float minHeight, out float maxHeight)
        {
            if (_heightRangeCached)
            {
                minHeight = _cachedMinHeight;
                maxHeight = _cachedMaxHeight;
                return;
            }

            minHeight = float.MaxValue;
            maxHeight = float.MinValue;
            for (var i = 0; i < _heights.Length; i++)
            {
                if (!_hasSample[i]) continue;
                minHeight = Mathf.Min(minHeight, _heights[i]);
                maxHeight = Mathf.Max(maxHeight, _heights[i]);
            }

            if (minHeight > maxHeight)
            {
                minHeight = 0f;
                maxHeight = 0f;
            }

            _cachedMinHeight = minHeight;
            _cachedMaxHeight = maxHeight;
            _heightRangeCached = true;
        }

        private void SampleNeighborSlope(int cellX, int cellZ, float centerHeight, ref float maxSlope, bool diagonal = false)
        {
            if (!TryGetHeight(cellX, cellZ, out var neighborHeight)) return;

            var dist = diagonal ? _cellSize * 1.4142135f : _cellSize;
            maxSlope = Mathf.Max(maxSlope, Mathf.Abs(neighborHeight - centerHeight) / dist);
        }

        private static bool TrySampleTerrainHeight(Terrain[] terrains, Vector3 worldPos, out float height)
        {
            height = 0f;
            foreach (var terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;

                var tPos = terrain.transform.position;
                var tSize = terrain.terrainData.size;
                if (worldPos.x < tPos.x || worldPos.x > tPos.x + tSize.x ||
                    worldPos.z < tPos.z || worldPos.z > tPos.z + tSize.z)
                    continue;

                height = terrain.SampleHeight(worldPos);
                return true;
            }

            return false;
        }
    }
}
