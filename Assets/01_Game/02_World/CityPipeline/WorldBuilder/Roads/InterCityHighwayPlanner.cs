using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Zombera.World.Roads;
using Debug = UnityEngine.Debug;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public sealed class InterCityHighwayEdgeDiagnostic
    {
        public int SiteIndexA;
        public int SiteIndexB;
        public bool PathfindingSucceeded;
        public bool IsMstEdge;
        public float EdgeLengthMeters;
        public float EndAToPlateauMeters;
        public float EndBToPlateauMeters;
        public string FailReason;
    }

    public sealed class InterCityHighwayPlanResult
    {
        public RoadNetworkRuntime Network { get; set; }
        public List<InterCityHighwayEdgeDiagnostic> Edges { get; } = new(8);
        public int FailedEdgeCount { get; set; }
        public int FailedMstEdgeCount { get; set; }
        public int FailedExtraEdgeCount { get; set; }
        public int MstEdgeCount { get; set; }
        public int RoutedMstEdgeCount { get; set; }
        public long CostFieldMs { get; set; }
        public long AstarMs { get; set; }
        public int RoutedEdgeCount { get; set; }
    }

    /// <summary>Plans MST inter-city highways with plateau-edge pins and terrain A*.</summary>
    public sealed partial class InterCityHighwayPlanner
    {
        public const int HighwayIdBase = 200000;
        internal const float ApproachGradeMeters = 80f;
        internal const int MaxFaceRetries = 4;

        public InterCityHighwayPlanResult Plan(
            WorldMapSession session,
            IReadOnlyList<WorldCitySite> cities,
            WorldGenerationProfile profile,
            IWorldTerrainQuery terrainQuery,
            bool assignSiteEntries = true,
            OrogenPlan orogen = null,
            LandformField landformField = null,
            IWorldHydrologyQuery hydrologyQuery = null)
        {
            var result = new InterCityHighwayPlanResult
            {
                Network = new RoadNetworkRuntime(session.Seed)
            };

            var settings = profile?.RoadNetworkSettings;
            if (settings == null || cities == null || cities.Count < 2 ||
                !settings.connectCitiesWithHighways)
                return result;

            var positions = BuildPositions(cities);
            var connected = InterCityHighwayTopology.CollectConnectedIndices(cities.Count, _ => true);
            if (connected.Count < 2)
                return result;

            var minLink = Mathf.Max(50f, settings.highwayMinLinkDistanceMeters);
            var links = InterCityHighwayTopology.BuildMinimumSpanningTreeWithExtraLinks(
                positions,
                connected,
                minLink,
                settings.highwayExtraLoopChance,
                session.Seed,
                out var mstEdgeCount);
            result.MstEdgeCount = mstEdgeCount;
            var forceChordKeys = new HashSet<ulong>();
            AppendSnowOrogenTunnelLinks(
                links, cities, terrainQuery, orogen, settings, forceChordKeys);
            if (links.Count == 0)
                return result;

            var fieldSw = Stopwatch.StartNew();
            var costField = BuildCostFieldForLinks(
                session, profile, terrainQuery, settings, orogen, cities, links);
            result.CostFieldMs = fieldSw.ElapsedMilliseconds;
            var roadId = HighwayIdBase;
            var landforms = profile?.Landforms;
            var astarSw = Stopwatch.StartNew();

            for (var l = 0; l < links.Count; l++)
            {
                var indexA = links[l].IndexA;
                var indexB = links[l].IndexB;
                var isMst = l < mstEdgeCount;
                var siteA = cities[indexA];
                var siteB = cities[indexB];
                if (siteA == null || siteB == null)
                    continue;

                Vector2 start = default;
                Vector2 end = default;
                List<Vector2> routed = null;
                string failReason = null;
                var forcedChord = ShouldForceInfrastructureChord(forceChordKeys, indexA, indexB) &&
                    TryRouteTerrainValidatedInfrastructure(
                        siteA, siteB, profile, terrainQuery, landforms, landformField, orogen,
                        hydrologyQuery,
                        out start, out end, out routed, out failReason);
                if (!forcedChord &&
                    !TryRoutePinnedHighway(
                        siteA, siteB, settings, costField, terrainQuery, landforms,
                        out start, out end, out routed, out failReason))
                {
                    var astarFailReason = failReason;
                    if (TryRouteTerrainValidatedInfrastructure(
                            siteA, siteB, profile, terrainQuery, landforms, landformField, orogen,
                            hydrologyQuery,
                            out start, out end, out routed, out var fallbackFailReason))
                    {
                        failReason = null;
                    }
                    else
                    {
                        failReason = astarFailReason + "/" + fallbackFailReason;
                    }
                }

                if (routed == null || routed.Count < 2)
                {
                    result.FailedEdgeCount++;
                    if (isMst)
                        result.FailedMstEdgeCount++;
                    else
                        result.FailedExtraEdgeCount++;
                    result.Edges.Add(new InterCityHighwayEdgeDiagnostic
                    {
                        SiteIndexA = indexA,
                        SiteIndexB = indexB,
                        PathfindingSucceeded = false,
                        IsMstEdge = isMst,
                        EdgeLengthMeters = Vector2.Distance(
                            ResolveSiteAnchor(siteA, siteB.CenterXZ, landforms, terrainQuery, 0),
                            ResolveSiteAnchor(siteB, siteA.CenterXZ, landforms, terrainQuery, 0)),
                        FailReason = failReason
                    });
                    continue;
                }

                var edgeLength = PolylineLength(routed);
                result.Network.AddRoad(new RoadPolyline
                {
                    id = roadId++,
                    roadClass = RoadClass.Highway,
                    widthMeters = settings.highwayWidth,
                    preserveWorldPath = true,
                    pointsXZ = routed
                });
                result.RoutedEdgeCount++;
                if (isMst)
                    result.RoutedMstEdgeCount++;

                ResolvePlateauRect(siteA, landforms, out var plateauA);
                ResolvePlateauRect(siteB, landforms, out var plateauB);
                result.Edges.Add(new InterCityHighwayEdgeDiagnostic
                {
                    SiteIndexA = indexA,
                    SiteIndexB = indexB,
                    PathfindingSucceeded = true,
                    IsMstEdge = isMst,
                    EdgeLengthMeters = edgeLength,
                    EndAToPlateauMeters = InterCitySiteFootprintUtility.DistanceToRectEdge(start, plateauA),
                    EndBToPlateauMeters = InterCitySiteFootprintUtility.DistanceToRectEdge(end, plateauB)
                });

                if (!assignSiteEntries)
                    continue;

                AssignHighwayEntry(siteA, start, SampleLandformHeight(terrainQuery, start), edgeLength);
                AssignHighwayEntry(siteB, end, SampleLandformHeight(terrainQuery, end), edgeLength);
            }

            result.AstarMs = astarSw.ElapsedMilliseconds;
            LogPlanDiagnostics(result, costField);
            return result;
        }

        public static bool TryValidateEdge(
            WorldCitySite siteA,
            WorldCitySite siteB,
            RoadNetworkSettings settings,
            TerrainRoadCostField costField,
            IWorldTerrainQuery terrainQuery = null,
            LandformProfile landforms = null)
        {
            if (siteA == null || siteB == null || settings == null)
                return false;

            return TryRoutePinnedHighway(
                siteA, siteB, settings, costField, terrainQuery, landforms,
                out _, out _, out _, out _);
        }

        public static TerrainRoadCostField BuildCostField(
            WorldMapSession session,
            WorldGenerationProfile profile,
            IWorldTerrainQuery terrainQuery,
            RoadNetworkSettings settings,
            OrogenPlan orogen = null)
        {
            return BuildCostFieldOnBounds(
                session.WorldBoundsXZ, profile, terrainQuery, settings, orogen);
        }

        public static List<(int IndexA, int IndexB)> BuildLinks(
            IReadOnlyList<WorldCitySite> cities,
            RoadNetworkSettings settings,
            int seed)
        {
            if (cities == null || cities.Count < 2 || settings == null)
                return new List<(int, int)>();

            var positions = BuildPositions(cities);
            var connected = InterCityHighwayTopology.CollectConnectedIndices(cities.Count, _ => true);
            var minLink = Mathf.Max(50f, settings.highwayMinLinkDistanceMeters);
            return InterCityHighwayTopology.BuildMinimumSpanningTreeWithExtraLinks(
                positions,
                connected,
                minLink,
                settings.highwayExtraLoopChance,
                seed,
                out _);
        }

        private static Vector2[] BuildPositions(IReadOnlyList<WorldCitySite> cities)
        {
            var positions = new Vector2[cities.Count];
            for (var i = 0; i < cities.Count; i++)
                positions[i] = cities[i] != null ? cities[i].CenterXZ : Vector2.zero;
            return positions;
        }

        private static void LogPlanDiagnostics(
            InterCityHighwayPlanResult result,
            TerrainRoadCostField costField)
        {
            if (result == null)
                return;

            var grid = costField != null && costField.IsValid
                ? costField.Width + "x" + costField.Height + "@" + costField.CellSize.ToString("F0") + "m"
                : "none";
            Debug.Log(
                "[InterCityHighwayPlanner] highways=" + result.RoutedEdgeCount +
                " failed=" + result.FailedEdgeCount +
                " failedMst=" + result.FailedMstEdgeCount +
                " costField=" + result.CostFieldMs + "ms astar=" + result.AstarMs + "ms grid=" + grid);

            for (var i = 0; i < result.Edges.Count; i++)
            {
                var edge = result.Edges[i];
                if (edge == null)
                    continue;
                if (!edge.PathfindingSucceeded)
                {
                    Debug.Log(
                        "[InterCityHighwayPlanner:diag] fail " + edge.SiteIndexA + "->" + edge.SiteIndexB +
                        " mst=" + edge.IsMstEdge +
                        " reason=" + edge.FailReason);
                    continue;
                }

                if (edge.EndAToPlateauMeters > 1.5f || edge.EndBToPlateauMeters > 1.5f)
                    Debug.LogWarning(
                        "[InterCityHighwayPlanner:diag] edge " + edge.SiteIndexA + "->" + edge.SiteIndexB +
                        " off-plateau A=" + edge.EndAToPlateauMeters.ToString("F1") +
                        "m B=" + edge.EndBToPlateauMeters.ToString("F1") + "m");
            }
        }

        private static float PolylineLength(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2)
                return 0f;

            var length = 0f;
            for (var i = 1; i < points.Count; i++)
                length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        private static float SampleLandformHeight(IWorldTerrainQuery terrainQuery, Vector2 xz)
        {
            if (terrainQuery != null && terrainQuery.TrySample(xz, out var sample))
                return sample.HeightWorldY;
            return 0f;
        }

        private static void AssignHighwayEntry(
            WorldCitySite site,
            Vector2 anchor,
            float height,
            float edgeLength)
        {
            if (site == null)
                return;

            if (!site.HasHighwayEntry || edgeLength > site.HighwayEntryEdgeLengthMeters)
            {
                site.HighwayEntryXZ = anchor;
                site.HighwayEntryHeightWorldY = height;
                site.HighwayEntryEdgeLengthMeters = edgeLength;
            }
        }
    }
}
