using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Footpath span flush / mesh emit for perimeter builder.</summary>
    internal static partial class CityPerimeterFootpathBuilder
    {
        private static int FlushRun(
            Transform footpathsParent,
            int blockId,
            int stripIndex,
            List<Vector2> spanRun,
            Vector2 centroid,
            IReadOnlyList<Vector2> highwayAnchors,
            float exitSetback,
            float footpathWidth,
            Material material,
            Func<Vector2, float> sampleY,
            float uvTile,
            ProceduralLayerMeshAccumulator accumulator)
        {
            if (spanRun.Count < 2)
            {
                spanRun.Clear();
                return 0;
            }

            DedupeConsecutive(spanRun);
            var spans = SplitPolylineExcludingAnchors(spanRun, highwayAnchors, exitSetback);
            spanRun.Clear();
            var placed = 0;

            for (var s = 0; s < spans.Count; s++)
            {
                var span = spans[s];
                if (span == null || span.Count < 2 || PolylineLength(span) < MinEdgeSpanMeters)
                    continue;

                if (!BuildOutwardBand(
                        footpathsParent,
                        $"PerimeterFootpath_{blockId}_{stripIndex}_{s}",
                        span,
                        centroid,
                        footpathWidth,
                        material,
                        sampleY,
                        uvTile,
                        accumulator))
                    continue;

                placed++;
            }

            return placed;
        }

        private static bool BuildOutwardBand(
            Transform footpathsParent,
            string objectName,
            IReadOnlyList<Vector2> span,
            Vector2 centroid,
            float footpathWidth,
            Material material,
            Func<Vector2, float> sampleY,
            float uvTile,
            ProceduralLayerMeshAccumulator accumulator)
        {
            var inner = new List<Vector2>(span.Count);
            var outer = new List<Vector2>(span.Count);
            for (var i = 0; i < span.Count; i++)
            {
                var prev = span[Mathf.Max(0, i - 1)];
                var next = span[Mathf.Min(span.Count - 1, i + 1)];
                var outward = ResolveOutwardNormal(prev, next, centroid);
                inner.Add(span[i] + outward * FootpathOutsetMeters);
                outer.Add(span[i] + outward * (FootpathOutsetMeters + footpathWidth));
            }

            var mesh = RoadMeshBuilder.BuildBandMeshBetweenPolylines(outer, inner, sampleY, uvTile);
            if (mesh == null)
                return false;

            if (accumulator != null)
            {
                material.renderQueue = 2002;
                return ProceduralMeshEmitUtility.Emit(
                    footpathsParent,
                    ProceduralRoadNetworkNames.Footpaths,
                    objectName,
                    mesh,
                    material,
                    accumulator);
            }

            var go = new GameObject(objectName);
            go.transform.SetParent(footpathsParent, false);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Perimeter Footpath");
#endif
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = material;
            mr.sharedMaterial.renderQueue = 2002;
            return true;
        }
    }
}
