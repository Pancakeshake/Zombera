using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Indexes river polylines and hydrology masks for crossing / depth queries.</summary>
    public sealed class WorldHydrologyQueryService : IWorldHydrologyQuery
    {
        private HydrologyPlan _plan;

        public void Bind(HydrologyPlan plan) => _plan = plan;

        public bool TryGetNearestRiver(Vector2 worldXZ, out WorldRiverSample sample)
        {
            sample = default;
            if (_plan?.Rivers == null || _plan.Rivers.Length == 0) return false;

            var bestDist = float.PositiveInfinity;
            WorldRiverSample best = default;
            var found = false;

            for (var r = 0; r < _plan.Rivers.Length; r++)
            {
                var river = _plan.Rivers[r];
                if (river?.PointsXZ == null || river.PointsXZ.Length == 0) continue;

                for (var p = 0; p < river.PointsXZ.Length; p++)
                {
                    var point = river.PointsXZ[p];
                    var dist = Vector2.Distance(worldXZ, point);
                    if (dist >= bestDist) continue;

                    bestDist = dist;
                    var width = p < river.WidthMeters.Length ? river.WidthMeters[p] : 0f;
                    var depth = p < river.DepthMeters.Length ? river.DepthMeters[p] : 0f;
                    best = new WorldRiverSample(river.StableId, point, dist, width, depth);
                    found = true;
                }
            }

            if (!found) return false;
            sample = best;
            return true;
        }

        public float SampleWaterDepth(Vector2 worldXZ)
        {
            if (!TryGetCell(worldXZ, out var x, out var z)) return 0f;
            return _plan.DepthMeters[_plan.Index(x, z)];
        }

        public float SampleDistanceToWater(Vector2 worldXZ)
        {
            if (!TryGetCell(worldXZ, out var x, out var z)) return float.PositiveInfinity;
            return _plan.DistanceToWaterMeters[_plan.Index(x, z)];
        }

        public bool TrySampleWater(Vector2 worldXZ, out WorldWaterSample sample)
        {
            sample = default;
            if (!TryGetCell(worldXZ, out var x, out var z)) return false;
            return _plan.TrySampleCell(x, z, out sample);
        }

        public bool TryFindCrossing(Vector2 from, Vector2 to, out WaterCrossing crossing)
        {
            crossing = default;
            if (_plan?.Crossings == null || _plan.Crossings.Count == 0) return false;

            var mid = (from + to) * 0.5f;
            var bestDist = float.PositiveInfinity;
            var found = false;
            WaterCrossing best = default;

            for (var i = 0; i < _plan.Crossings.Count; i++)
            {
                var candidate = _plan.Crossings[i];
                var dist = Vector2.Distance(mid, candidate.PositionXZ);
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = candidate;
                found = true;
            }

            if (!found || bestDist > 64f) return false;
            crossing = best;
            return true;
        }

        private bool TryGetCell(Vector2 worldXZ, out int x, out int z)
        {
            x = 0;
            z = 0;
            if (_plan == null) return false;
            x = Mathf.FloorToInt((worldXZ.x - _plan.OriginXZ.x) / _plan.CellSizeMeters);
            z = Mathf.FloorToInt((worldXZ.y - _plan.OriginXZ.y) / _plan.CellSizeMeters);
            return x >= 0 && z >= 0 && x < _plan.Width && z < _plan.Height;
        }
    }
}
