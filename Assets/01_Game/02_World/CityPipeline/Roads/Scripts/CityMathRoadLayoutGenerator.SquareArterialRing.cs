using System.Collections.Generic;
using UnityEngine;

using Zombera.World.City;

namespace Zombera.World.Roads
{
    public static partial class CityMathRoadLayoutGenerator
    {
        internal readonly struct ArterialCornerArcSpec
        {
            public ArterialCornerArcSpec(
                float centerX,
                float centerZ,
                float radius,
                float startAngle,
                float endAngle)
            {
                CenterX = centerX;
                CenterZ = centerZ;
                Radius = radius;
                StartAngle = startAngle;
                EndAngle = endAngle;
            }

            public float CenterX { get; }
            public float CenterZ { get; }
            public float Radius { get; }
            public float StartAngle { get; }
            public float EndAngle { get; }
        }

        private readonly struct SquareRingAxisChainRequest
        {
            public SquareRingAxisChainRequest(
                RoadClass roadClass,
                float width,
                bool horizontal,
                float fixedAxis,
                float axisStart,
                float axisEnd,
                List<float> interiorKnots)
            {
                RoadClass = roadClass;
                Width = width;
                Horizontal = horizontal;
                FixedAxis = fixedAxis;
                AxisStart = axisStart;
                AxisEnd = axisEnd;
                InteriorKnots = interiorKnots;
            }

            public RoadClass RoadClass { get; }
            public float Width { get; }
            public bool Horizontal { get; }
            public float FixedAxis { get; }
            public float AxisStart { get; }
            public float AxisEnd { get; }
            public List<float> InteriorKnots { get; }
        }

        private readonly struct SquareArterialCornerCurvesRequest
        {
            public SquareArterialCornerCurvesRequest(
                float width,
                SquareRingBounds bounds,
                float radius,
                SquareRingJunctionExtents junctions)
            {
                Width = width;
                Bounds = bounds;
                Radius = radius;
                Junctions = junctions;
            }

            public float Width { get; }
            public SquareRingBounds Bounds { get; }
            public float Radius { get; }
            public SquareRingJunctionExtents Junctions { get; }
        }

        private readonly struct ArterialIntegratedCornerCurveRequest
        {
            public ArterialIntegratedCornerCurveRequest(
                float width,
                ArterialCornerArcSpec arc,
                Vector2 leadInKnot,
                Vector2 leadOutKnot)
            {
                Width = width;
                Arc = arc;
                LeadInKnot = leadInKnot;
                LeadOutKnot = leadOutKnot;
            }

            public float Width { get; }
            public ArterialCornerArcSpec Arc { get; }
            public Vector2 LeadInKnot { get; }
            public Vector2 LeadOutKnot { get; }
        }

        private readonly struct ClippedSquareArterialRingRequest
        {
            public ClippedSquareArterialRingRequest(
                CityMathRoadLayout layout,
                CityMathRoadLayoutResolved resolved,
                Vector2 center,
                float width,
                SquareRingBounds bounds)
            {
                Layout = layout;
                Resolved = resolved;
                Center = center;
                Width = width;
                Bounds = bounds;
            }

            public CityMathRoadLayout Layout { get; }
            public CityMathRoadLayoutResolved Resolved { get; }
            public Vector2 Center { get; }
            public float Width { get; }
            public SquareRingBounds Bounds { get; }
        }

        private readonly struct SquareRingCornerLayout
        {
            public SquareRingCornerLayout(
                SquareRingBounds bounds,
                float cornerRadius,
                float trimX0,
                float trimX1,
                float trimZ0,
                float trimZ1)
            {
                Bounds = bounds;
                CornerRadius = cornerRadius;
                TrimX0 = trimX0;
                TrimX1 = trimX1;
                TrimZ0 = trimZ0;
                TrimZ1 = trimZ1;
            }

            public SquareRingBounds Bounds { get; }
            public float CornerRadius { get; }
            public float TrimX0 { get; }
            public float TrimX1 { get; }
            public float TrimZ0 { get; }
            public float TrimZ1 { get; }
        }

        private readonly struct SquareArterialRingCornerArcRequest
        {
            public SquareArterialRingCornerArcRequest(
                float width,
                SquareRingCornerLayout layout,
                List<float> interiorCols,
                List<float> interiorRows)
            {
                Width = width;
                Layout = layout;
                InteriorCols = interiorCols;
                InteriorRows = interiorRows;
            }

            public float Width { get; }
            public SquareRingCornerLayout Layout { get; }
            public List<float> InteriorCols { get; }
            public List<float> InteriorRows { get; }
        }

        private readonly struct SquareArterialRingChainRequest
        {
            public SquareArterialRingChainRequest(
                float width,
                SquareRingBounds bounds,
                List<float> interiorCols,
                List<float> interiorRows)
            {
                Width = width;
                Bounds = bounds;
                InteriorCols = interiorCols;
                InteriorRows = interiorRows;
            }

            public float Width { get; }
            public SquareRingBounds Bounds { get; }
            public List<float> InteriorCols { get; }
            public List<float> InteriorRows { get; }
        }

        private static void AddSquareArterialRing(
            RoadNetworkRuntime network,
            CityMathRoadLayout layout,
            RoadNetworkSettings settings,
            Vector2 center,
            CityMathRoadLayoutResolved resolved)
        {
            var width = ResolveUnifiedCityRoadWidthMeters(settings);
            ResolveEffectiveSquareArterialRingBounds(
                layout,
                resolved,
                center,
                out var x0,
                out var x1,
                out var z0,
                out var z1);

            if (layout.clipLocalStreetsToArterialRing && layout.generateStreetGrid
                && TryEmitClippedSquareArterialRing(
                    network,
                    new ClippedSquareArterialRingRequest(
                        layout, resolved, center, width, new SquareRingBounds(x0, x1, z0, z1))))
                return;

            EmitSimpleSquareArterialRing(network, width, x0, x1, z0, z1);
        }

        private static bool TryEmitClippedSquareArterialRing(
            RoadNetworkRuntime network,
            ClippedSquareArterialRingRequest request)
        {
            var bounds = request.Bounds;
            var center = request.Center;
            var resolved = request.Resolved;
            var layout = request.Layout;
            var cityXMin = center.x - resolved.HalfWidthMeters;
            var cityXMax = center.x + resolved.HalfWidthMeters;
            var cityZMin = center.y - resolved.HalfDepthMeters;
            var cityZMax = center.y + resolved.HalfDepthMeters;
            var rowLines = BuildAxisStreetLines(cityZMin, cityZMax, layout, resolved.Seed, 101);
            var colLines = BuildAxisStreetLines(cityXMin, cityXMax, layout, resolved.Seed, 303);
            var interiorCols = FilterOpenInterval(colLines, bounds.X0, bounds.X1);
            var interiorRows = FilterOpenInterval(rowLines, bounds.Z0, bounds.Z1);

            var cornerRadius = ResolveArterialCornerRadius(
                layout, resolved, bounds.X0, bounds.X1, bounds.Z0, bounds.Z1);
            var trimX0 = bounds.X0 + cornerRadius;
            var trimX1 = bounds.X1 - cornerRadius;
            var trimZ0 = bounds.Z0 + cornerRadius;
            var trimZ1 = bounds.Z1 - cornerRadius;
            var useCornerArcs = cornerRadius >= 2f && trimX1 - trimX0 >= 4f && trimZ1 - trimZ0 >= 4f;

            var id = 100;
            if (useCornerArcs)
            {
                EmitSquareArterialRingWithCornerArcs(
                    network,
                    ref id,
                    new SquareArterialRingCornerArcRequest(
                        request.Width,
                        new SquareRingCornerLayout(bounds, cornerRadius, trimX0, trimX1, trimZ0, trimZ1),
                        interiorCols,
                        interiorRows));
            }
            else
            {
                EmitSquareArterialRingWithoutCornerArcs(
                    network,
                    ref id,
                    new SquareArterialRingChainRequest(request.Width, bounds, interiorCols, interiorRows));
            }

            return true;
        }

        private static void EmitSquareArterialRingWithCornerArcs(
            RoadNetworkRuntime network,
            ref int id,
            SquareArterialRingCornerArcRequest request)
        {
            var layout = request.Layout;
            var bounds = layout.Bounds;
            var trimX0 = layout.TrimX0;
            var trimX1 = layout.TrimX1;
            var trimZ0 = layout.TrimZ0;
            var trimZ1 = layout.TrimZ1;
            var trimmedCols = FilterOpenInterval(
                request.InteriorCols, trimX0 + CornerJunctionClearanceMeters, trimX1 - CornerJunctionClearanceMeters);
            var trimmedRows = FilterOpenInterval(
                request.InteriorRows, trimZ0 + CornerJunctionClearanceMeters, trimZ1 - CornerJunctionClearanceMeters);

            var colFirst = trimmedCols.Count > 0 ? trimmedCols[0] : (trimX0 + trimX1) * 0.5f;
            var colLast = trimmedCols.Count > 0 ? trimmedCols[trimmedCols.Count - 1] : (trimX0 + trimX1) * 0.5f;
            var rowFirst = trimmedRows.Count > 0 ? trimmedRows[0] : (trimZ0 + trimZ1) * 0.5f;
            var rowLast = trimmedRows.Count > 0 ? trimmedRows[trimmedRows.Count - 1] : (trimZ0 + trimZ1) * 0.5f;
            var chainCols = trimmedCols.Count > 2 ? trimmedCols.GetRange(1, trimmedCols.Count - 2) : new List<float>();
            var chainRows = trimmedRows.Count > 2 ? trimmedRows.GetRange(1, trimmedRows.Count - 2) : new List<float>();

            EmitSquareRingAxisChain(network, ref id, new SquareRingAxisChainRequest(
                RoadClass.Arterial, request.Width, horizontal: true, fixedAxis: bounds.Z0, colFirst, colLast, chainCols));
            EmitSquareRingAxisChain(network, ref id, new SquareRingAxisChainRequest(
                RoadClass.Arterial, request.Width, horizontal: true, fixedAxis: bounds.Z1, colFirst, colLast, chainCols));
            EmitSquareRingAxisChain(network, ref id, new SquareRingAxisChainRequest(
                RoadClass.Arterial, request.Width, horizontal: false, fixedAxis: bounds.X1, rowFirst, rowLast, chainRows));
            EmitSquareRingAxisChain(network, ref id, new SquareRingAxisChainRequest(
                RoadClass.Arterial, request.Width, horizontal: false, fixedAxis: bounds.X0, rowFirst, rowLast, chainRows));
            AddSquareArterialIntegratedCornerCurves(
                network,
                ref id,
                new SquareArterialCornerCurvesRequest(
                    request.Width,
                    bounds,
                    layout.CornerRadius,
                    new SquareRingJunctionExtents(colFirst, colLast, rowFirst, rowLast)));
        }

        private static void EmitSquareArterialRingWithoutCornerArcs(
            RoadNetworkRuntime network,
            ref int id,
            SquareArterialRingChainRequest request)
        {
            var bounds = request.Bounds;
            EmitSquareRingAxisChain(network, ref id, new SquareRingAxisChainRequest(
                RoadClass.Arterial, request.Width, horizontal: true, fixedAxis: bounds.Z0, bounds.X0, bounds.X1, request.InteriorCols));
            EmitSquareRingAxisChain(network, ref id, new SquareRingAxisChainRequest(
                RoadClass.Arterial, request.Width, horizontal: true, fixedAxis: bounds.Z1, bounds.X0, bounds.X1, request.InteriorCols));
            EmitSquareRingAxisChain(network, ref id, new SquareRingAxisChainRequest(
                RoadClass.Arterial, request.Width, horizontal: false, fixedAxis: bounds.X1, bounds.Z0, bounds.Z1, request.InteriorRows));
            EmitSquareRingAxisChain(network, ref id, new SquareRingAxisChainRequest(
                RoadClass.Arterial, request.Width, horizontal: false, fixedAxis: bounds.X0, bounds.Z0, bounds.Z1, request.InteriorRows));
        }

        private static void EmitSimpleSquareArterialRing(
            RoadNetworkRuntime network,
            float width,
            float x0,
            float x1,
            float z0,
            float z1)
        {
            AddAxisAlignedSegment(network, 100, RoadClass.Arterial, width, new Vector2(x0, z0), new Vector2(x1, z0));
            AddAxisAlignedSegment(network, 101, RoadClass.Arterial, width, new Vector2(x1, z0), new Vector2(x1, z1));
            AddAxisAlignedSegment(network, 102, RoadClass.Arterial, width, new Vector2(x1, z1), new Vector2(x0, z1));
            AddAxisAlignedSegment(network, 103, RoadClass.Arterial, width, new Vector2(x0, z1), new Vector2(x0, z0));
        }

        /// <summary>
        ///     Each corner is a single road from the outermost junction knot on one edge, around the
        ///     rounded corner, to the outermost junction knot on the adjacent edge. The road carries
        ///     curvedMarkers=true so EasyRoads splines through the arc samples with constant width
        ///     instead of mitering tightly spaced StraightXZ markers (which fans the mesh wider).
        /// </summary>
        private static void AddSquareArterialIntegratedCornerCurves(
            RoadNetworkRuntime network,
            ref int id,
            SquareArterialCornerCurvesRequest request)
        {
            var bounds = request.Bounds;
            var junctions = request.Junctions;
            var radius = Mathf.Max(0.5f, request.Radius);

            AddArterialIntegratedCornerCurve(
                network,
                ref id,
                new ArterialIntegratedCornerCurveRequest(
                    request.Width,
                    new ArterialCornerArcSpec(
                        bounds.X0 + radius,
                        bounds.Z0 + radius,
                        radius,
                        Mathf.PI * 1.5f,
                        Mathf.PI),
                    new Vector2(junctions.ColFirst, bounds.Z0),
                    new Vector2(bounds.X0, junctions.RowFirst)));
            AddArterialIntegratedCornerCurve(
                network,
                ref id,
                new ArterialIntegratedCornerCurveRequest(
                    request.Width,
                    new ArterialCornerArcSpec(
                        bounds.X1 - radius,
                        bounds.Z0 + radius,
                        radius,
                        Mathf.PI * 1.5f,
                        Mathf.PI * 2f),
                    new Vector2(junctions.ColLast, bounds.Z0),
                    new Vector2(bounds.X1, junctions.RowFirst)));
            AddArterialIntegratedCornerCurve(
                network,
                ref id,
                new ArterialIntegratedCornerCurveRequest(
                    request.Width,
                    new ArterialCornerArcSpec(
                        bounds.X1 - radius,
                        bounds.Z1 - radius,
                        radius,
                        0f,
                        Mathf.PI * 0.5f),
                    new Vector2(bounds.X1, junctions.RowLast),
                    new Vector2(junctions.ColLast, bounds.Z1)));
            AddArterialIntegratedCornerCurve(
                network,
                ref id,
                new ArterialIntegratedCornerCurveRequest(
                    request.Width,
                    new ArterialCornerArcSpec(
                        bounds.X0 + radius,
                        bounds.Z1 - radius,
                        radius,
                        Mathf.PI * 0.5f,
                        Mathf.PI),
                    new Vector2(junctions.ColFirst, bounds.Z1),
                    new Vector2(bounds.X0, junctions.RowLast)));
        }

        private static void AddArterialIntegratedCornerCurve(
            RoadNetworkRuntime network,
            ref int id,
            ArterialIntegratedCornerCurveRequest request)
        {
            var road = new RoadPolyline
            {
                id = id++,
                roadClass = RoadClass.Arterial,
                widthMeters = request.Width,
                curvedMarkers = true
            };

            var arc = request.Arc;
            var arcStart = new Vector2(
                arc.CenterX + Mathf.Cos(arc.StartAngle) * arc.Radius,
                arc.CenterZ + Mathf.Sin(arc.StartAngle) * arc.Radius);
            var arcEnd = new Vector2(
                arc.CenterX + Mathf.Cos(arc.EndAngle) * arc.Radius,
                arc.CenterZ + Mathf.Sin(arc.EndAngle) * arc.Radius);

            AppendStraightLeadMarkers(road.pointsXZ, request.LeadInKnot, arcStart);

            var arcLength = arc.Radius * Mathf.Abs(arc.EndAngle - arc.StartAngle);
            var segments = CityNamedAreaOutlineBuilder.ResolveArcSegmentCount(arcLength);
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = Mathf.Lerp(arc.StartAngle, arc.EndAngle, t);
                AppendDistinctPointXZ(road.pointsXZ, new Vector2(
                    arc.CenterX + Mathf.Cos(angle) * arc.Radius,
                    arc.CenterZ + Mathf.Sin(angle) * arc.Radius));
            }

            AppendStraightLeadMarkers(road.pointsXZ, arcEnd, request.LeadOutKnot, appendFromSecond: true);

            if (road.pointsXZ.Count >= 2)
                network.AddRoad(road);
        }

        private static void EmitSquareRingAxisChain(
            RoadNetworkRuntime network,
            ref int id,
            SquareRingAxisChainRequest request)
        {
            var knots = new List<float>(request.InteriorKnots.Count + 2) { request.AxisStart };
            knots.AddRange(request.InteriorKnots);
            knots.Add(request.AxisEnd);

            for (var i = 0; i < knots.Count - 1; i++)
            {
                if (knots[i + 1] - knots[i] < 1f)
                    continue;

                if (request.Horizontal)
                {
                    AddAxisAlignedSegment(
                        network,
                        id++,
                        request.RoadClass,
                        request.Width,
                        new Vector2(knots[i], request.FixedAxis),
                        new Vector2(knots[i + 1], request.FixedAxis));
                }
                else
                {
                    AddAxisAlignedSegment(
                        network,
                        id++,
                        request.RoadClass,
                        request.Width,
                        new Vector2(request.FixedAxis, knots[i]),
                        new Vector2(request.FixedAxis, knots[i + 1]));
                }
            }
        }
    }
}
