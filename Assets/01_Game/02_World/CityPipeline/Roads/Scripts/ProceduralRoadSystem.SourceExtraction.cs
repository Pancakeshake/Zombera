using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.Roads
{
    public sealed partial class ProceduralRoadSystem
    {
        private readonly HashSet<(int x, int z)> _loggedMissingSplineTiles = new();

        private IReadOnlyList<RoadPolyline> ResolveSourceRoadPolylinesForTile(
            WorldTileInfo tile,
            RoadGenerationDiagnostics diagnostics)
        {
            var preferMapMagicSplines = settings != null && settings.UsesMapMagicSplineLayout;

            if (preferMapMagicSplines)
            {
                var splineRoads = new List<RoadPolyline>(64);
                if (TryExtractMapMagicSplineRoads(tile, splineRoads))
                {
                    diagnostics.SourceMode = "MapMagicSpline";
                    diagnostics.SourcePolylineCount = splineRoads.Count;
                    return splineRoads;
                }

                diagnostics.SourceMode = "MapMagicSplineMissing";
                LogMapMagicSplineMissingOnce(tile);

                if (settings != null && !settings.fallbackToDeterministicWhenMissing)
                {
                    diagnostics.SourcePolylineCount = 0;
                    return Array.Empty<RoadPolyline>();
                }
            }

            EnsureGlobalRoadNetwork();

            if (_globalNetwork == null || _globalNetwork.Roads == null)
            {
                diagnostics.SourceMode = preferMapMagicSplines
                    ? "MapMagicSplineMissing->WorldMapUnavailable"
                    : "WorldMapUnavailable";
                diagnostics.SourcePolylineCount = 0;
                return Array.Empty<RoadPolyline>();
            }

            diagnostics.SourceMode = preferMapMagicSplines
                ? "MapMagicSplineMissing->WorldMap"
                : "WorldMap";
            diagnostics.SourcePolylineCount = _globalNetwork.Roads.Count;
            return _globalNetwork.Roads;
        }

        private bool TryExtractMapMagicSplineRoads(WorldTileInfo tile, List<RoadPolyline> output)
        {
            if (output == null) return false;
            output.Clear();

            var planSource = WorldTileInfoUtility.FindRoadPlanSource();
            if (planSource == null) return false;

            var scratch = new List<Vector3>(64);
            var roadsOut = new List<IReadOnlyList<Vector3>>(16);
            if (!planSource.TryGetRoadPlan(tile.WorldRectXZ, scratch, roadsOut))
                return false;

            var width = settings != null ? Mathf.Max(0.5f, settings.mapMagicSplineWidthMeters) : 4f;
            var roadClass = ResolveRoadClassFromWidth(width);
            var idBase = unchecked(tile.Coord.X * 73856093) ^ unchecked(tile.Coord.Z * 19349663);

            for (var i = 0; i < roadsOut.Count; i++)
            {
                var src = roadsOut[i];
                if (src == null || src.Count < 2) continue;

                var road = new RoadPolyline
                {
                    id = idBase + i + 1,
                    roadClass = roadClass,
                    widthMeters = width,
                    preserveWorldPath = true
                };

                for (var p = 0; p < src.Count; p++)
                {
                    var pt = src[p];
                    road.pointsXZ.Add(new Vector2(pt.x, pt.z));
                }

                if (road.pointsXZ.Count >= 2)
                    output.Add(road);
            }

            return output.Count > 0;
        }

        private static RoadClass ResolveRoadClassFromWidth(float widthMeters)
        {
            if (widthMeters >= 10f) return RoadClass.Highway;
            if (widthMeters >= 6f) return RoadClass.Arterial;
            return RoadClass.Local;
        }

        private void LogMapMagicSplineMissingOnce(WorldTileInfo tile)
        {
            var key = (tile.Coord.X, tile.Coord.Z);
            if (!_loggedMissingSplineTiles.Add(key)) return;

            Debug.LogWarning(
                "[ProceduralRoadSystem] MapMagic spline plan missing for tile (" +
                key.X + "," + key.Z + ").",
                this);
        }
    }
}
