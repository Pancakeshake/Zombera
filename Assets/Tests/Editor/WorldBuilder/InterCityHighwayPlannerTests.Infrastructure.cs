#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Tests.Editor.WorldBuilder
{
    /// <summary>Soft-cost adapter + infrastructure polyline stitch coverage for highway planner.</summary>
    public sealed partial class InterCityHighwayPlannerTests
    {
        [Test]
        public void Planner_WithFlatCostField_RoutesPreserveWorldPathHighway()
        {
            var settings = ScriptableObject.CreateInstance<RoadNetworkSettings>();
            settings.connectCitiesWithHighways = true;
            settings.fallbackToStraightPathWhenPathfindingFails = false;
            settings.enableTerrainValidatedInfrastructureFallback = false;
            settings.highwayWidth = 12f;
            settings.maxHighwayRoadSlopeDegrees = 45f;
            settings.pathfindingMaxExpandedNodes = 50000;
            settings.highwayPathfindingCellSizeMeters = 50f;

            var siteA = new WorldCitySite
            {
                CenterXZ = new Vector2(200f, 200f),
                HalfWidthMeters = 80f,
                HalfDepthMeters = 80f
            };
            var siteB = new WorldCitySite
            {
                CenterXZ = new Vector2(800f, 200f),
                HalfWidthMeters = 80f,
                HalfDepthMeters = 80f
            };

            var worldField = new FlatTestWorldCostField(
                Rect.MinMaxRect(0f, 0f, 1000f, 400f),
                cellSizeMeters: 25f,
                heightWorldY: 10f);
            var costField = TerrainRoadCostField.FromWorldCostField(worldField);
            Assert.IsNotNull(costField);
            Assert.IsTrue(costField.IsValid);

            var ok = InterCityHighwayPlanner.TryValidateEdge(
                siteA, siteB, settings, costField, terrainQuery: null, landforms: null);
            Assert.IsTrue(ok, "Flat synthetic cost field should route a highway edge.");

            var road = new RoadPolyline
            {
                id = InterCityHighwayPlanner.HighwayIdBase,
                roadClass = RoadClass.Highway,
                widthMeters = settings.highwayWidth,
                preserveWorldPath = true,
                pointsXZ = new List<Vector2> { siteA.CenterXZ, siteB.CenterXZ }
            };
            Assert.IsTrue(road.preserveWorldPath);
            Assert.AreEqual(RoadClass.Highway, road.roadClass);
        }

        [Test]
        public void FromWorldCostField_BakesSoftCostFactorsIntoTraversal()
        {
            var worldField = new FlatTestWorldCostField(
                Rect.MinMaxRect(0f, 0f, 100f, 100f),
                cellSizeMeters: 10f,
                heightWorldY: 5f,
                softAdditive: 12f,
                softMultiplier: 2f);
            var costField = TerrainRoadCostField.FromWorldCostField(worldField);
            Assert.IsNotNull(costField);

            var withSoft = costField.GetTraversalCost(
                1, 1, 2, 1,
                maxSlopeRatio: 10f,
                slopeCostWeight: 0f,
                elevationCostWeight: 0f,
                mountainProximityCostWeight: 0f,
                minHeight: 0f,
                heightRange: 1f);
            // Soft is a bounded additive preference, not a multiplicative hard wall.
            Assert.Greater(withSoft, costField.CellSize);
            // SoftPreferenceMulMax=2.25, SoftPreferenceAddMaxCells=1.25
            var maxSoftBias = costField.CellSize * (2.25f - 1f) + 1.25f * costField.CellSize;
            Assert.Less(withSoft, costField.CellSize + maxSoftBias + 0.01f);
        }

        [Test]
        public void FromWorldCostField_ClampsExtremeSoftFactors()
        {
            var worldField = new FlatTestWorldCostField(
                Rect.MinMaxRect(0f, 0f, 100f, 100f),
                cellSizeMeters: 10f,
                heightWorldY: 5f,
                softAdditive: 1000f,
                softMultiplier: 99f);
            var costField = TerrainRoadCostField.FromWorldCostField(worldField);
            Assert.IsNotNull(costField);

            var cost = costField.GetTraversalCost(
                1, 1, 2, 1,
                maxSlopeRatio: 10f,
                slopeCostWeight: 0f,
                elevationCostWeight: 0f,
                mountainProximityCostWeight: 0f,
                minHeight: 0f,
                heightRange: 1f);
            var maxSoftBias = costField.CellSize * (2.25f - 1f) + 1.25f * costField.CellSize;
            Assert.LessOrEqual(cost, costField.CellSize + maxSoftBias + 0.01f);
            // Unclamped Ocean×99 would be ~100k; preference clamp keeps cost near one step.
            Assert.Less(cost, costField.CellSize * 5f);
        }

        [Test]
        public void InfrastructurePolyline_InsertsTunnelAndBridgeBoundaries()
        {
            var network = new RoadNetworkRuntime(1);
            network.AddRoad(new RoadPolyline
            {
                id = 200001,
                roadClass = RoadClass.Highway,
                widthMeters = 12f,
                preserveWorldPath = true,
                pointsXZ = new List<Vector2>
                {
                    new(0f, 0f),
                    new(100f, 0f)
                }
            });

            WaterCrossingBuildCache.Set(new List<WaterCrossing>
            {
                new(
                    1UL,
                    200001,
                    new Vector2(40f, 0f),
                    new Vector2(60f, 0f),
                    widthMeters: 20f,
                    maximumDepthMeters: 2f,
                    deckWorldY: 0f,
                    roadClass: RoadClass.Highway,
                    policy: WaterCrossingPolicy.Bridge)
            });
            MountainTunnelBuildCache.Set(new List<MountainTunnel>
            {
                new(
                    2UL,
                    200001,
                    new Vector2(10f, 0f),
                    new Vector2(30f, 0f),
                    0f,
                    0f,
                    20f,
                    25f,
                    8f)
            });

            try
            {
                var inserted = HighwayInfrastructurePolylineUtility.InsertBoundaryPoints(network);
                Assert.GreaterOrEqual(inserted, 4);
                var points = network.Roads[0].pointsXZ;
                Assert.That(points, Has.Some.EqualTo(new Vector2(10f, 0f)));
                Assert.That(points, Has.Some.EqualTo(new Vector2(30f, 0f)));
                Assert.That(points, Has.Some.EqualTo(new Vector2(40f, 0f)));
                Assert.That(points, Has.Some.EqualTo(new Vector2(60f, 0f)));
            }
            finally
            {
                MountainTunnelBuildCache.Clear();
                WaterCrossingBuildCache.Set(null);
            }
        }

        private sealed class FlatTestWorldCostField : IWorldCostField
        {
            private readonly float _heightWorldY;
            private readonly float _softAdditive;
            private readonly float _softMultiplier;

            public FlatTestWorldCostField(
                Rect boundsXZ,
                float cellSizeMeters,
                float heightWorldY,
                float softAdditive = 0f,
                float softMultiplier = 1f)
            {
                BoundsXZ = boundsXZ;
                CellSizeMeters = Mathf.Max(0.01f, cellSizeMeters);
                Width = Mathf.Max(2, Mathf.CeilToInt(boundsXZ.width / CellSizeMeters));
                Height = Mathf.Max(2, Mathf.CeilToInt(boundsXZ.height / CellSizeMeters));
                _heightWorldY = heightWorldY;
                _softAdditive = softAdditive;
                _softMultiplier = Mathf.Max(0.01f, softMultiplier);
            }

            public Rect BoundsXZ { get; }
            public float CellSizeMeters { get; }
            public int Width { get; }
            public int Height { get; }

            public bool HasSample(int x, int z) =>
                x >= 0 && z >= 0 && x < Width && z < Height;

            public bool IsTraversable(int x, int z, RoadClass roadClass) => HasSample(x, z);

            public float GetHeight(int x, int z) => _heightWorldY;

            public float GetTraversalCost(int fromX, int fromZ, int toX, int toZ, RoadClass roadClass)
            {
                if (!IsTraversable(toX, toZ, roadClass))
                    return float.PositiveInfinity;
                var step = CellSizeMeters;
                if (fromX != toX && fromZ != toZ)
                    step *= 1.41421356f;
                GetSoftCostFactors(toX, toZ, roadClass, out var additive, out var mul);
                return (step + additive) * mul;
            }

            public void GetSoftCostFactors(
                int x,
                int z,
                RoadClass roadClass,
                out float additive,
                out float multiplier)
            {
                additive = _softAdditive;
                multiplier = _softMultiplier;
            }
        }
    }
}
#endif
