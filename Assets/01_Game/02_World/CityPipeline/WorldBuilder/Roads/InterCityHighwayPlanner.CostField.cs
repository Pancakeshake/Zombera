using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Corridor-scoped cost fields for inter-city highway planning.</summary>
    public sealed partial class InterCityHighwayPlanner
    {
        public static TerrainRoadCostField BuildCostFieldForLinks(
            WorldMapSession session,
            WorldGenerationProfile profile,
            IWorldTerrainQuery terrainQuery,
            RoadNetworkSettings settings,
            OrogenPlan orogen,
            IReadOnlyList<WorldCitySite> cities,
            IReadOnlyList<(int IndexA, int IndexB)> links)
        {
            if (settings == null || cities == null || links == null || links.Count == 0)
                return BuildCostField(session, profile, terrainQuery, settings, orogen);

            var margin = Mathf.Max(64f, settings.pathfindingMarginMeters);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var found = false;

            for (var i = 0; i < links.Count; i++)
            {
                var a = cities[links[i].IndexA];
                var b = cities[links[i].IndexB];
                if (a == null || b == null)
                    continue;
                ExpandBounds(ref min, ref max, a.CenterXZ);
                ExpandBounds(ref min, ref max, b.CenterXZ);
                found = true;
            }

            if (!found)
                return BuildCostField(session, profile, terrainQuery, settings, orogen);

            var bounds = Rect.MinMaxRect(min.x - margin, min.y - margin, max.x + margin, max.y + margin);
            bounds = QuantizeBounds(bounds, ResolveHighwayCellSize(settings), session.WorldBoundsXZ);
            bounds = IntersectRects(bounds, session.WorldBoundsXZ);
            if (bounds.width < 8f || bounds.height < 8f)
                bounds = session.WorldBoundsXZ;

            return BuildCostFieldOnBounds(bounds, profile, terrainQuery, settings, orogen);
        }

        public static TerrainRoadCostField BuildCostFieldOnBounds(
            Rect boundsXZ,
            WorldGenerationProfile profile,
            IWorldTerrainQuery terrainQuery,
            RoadNetworkSettings settings,
            OrogenPlan orogen)
        {
            if (terrainQuery == null || settings == null)
                return null;

            var options = new WorldCostFieldOptions
            {
                CellSizeMeters = ResolveHighwayCellSize(settings),
                RoadSettings = settings,
                MaxTraversableWaterDepthMeters = profile?.Hydrology != null
                    ? profile.Hydrology.FordMaxDepthMeters
                    : 0.3f,
                MaxBridgeableWaterDepthMeters = profile?.Hydrology != null
                    ? profile.Hydrology.BridgeCorridorMaxDepthMeters
                    : 4f,
                WaterSoftCostPerMeterDepth = profile?.Hydrology != null
                    ? profile.Hydrology.WaterSoftCostPerMeterDepth
                    : 24f,
                Orogen = orogen,
                PassAttractHalfWidthMeters = profile?.Landforms != null
                    ? Mathf.Max(40f, profile.Landforms.PassCorridorHalfWidthMeters)
                    : 120f
            };

            if (!terrainQuery.TryBuildCostField(boundsXZ, options, out var worldField) ||
                worldField == null)
                return null;

            return TerrainRoadCostField.FromWorldCostField(worldField);
        }

        private static float ResolveHighwayCellSize(RoadNetworkSettings settings)
        {
            var highway = settings.highwayPathfindingCellSizeMeters;
            if (highway >= 4f)
                return highway;
            return Mathf.Max(4f, settings.pathfindingCellSizeMeters);
        }

        private static void ExpandBounds(ref Vector2 min, ref Vector2 max, Vector2 p)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        private static Rect QuantizeBounds(Rect bounds, float cellSize, Rect worldBounds)
        {
            var cell = Mathf.Max(4f, cellSize);
            var origin = new Vector2(worldBounds.xMin, worldBounds.yMin);
            var x0 = origin.x + Mathf.Floor((bounds.xMin - origin.x) / cell) * cell;
            var y0 = origin.y + Mathf.Floor((bounds.yMin - origin.y) / cell) * cell;
            var x1 = origin.x + Mathf.Ceil((bounds.xMax - origin.x) / cell) * cell;
            var y1 = origin.y + Mathf.Ceil((bounds.yMax - origin.y) / cell) * cell;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        private static Rect IntersectRects(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return b;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
