using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    public static partial class ProceduralCityRoadBuilder
    {
        private static Material _cachedDefaultAsphalt;

        private struct AsphaltBuildStats
        {
            public int StripCount;
            public int SkippedPolyline;
            public int SkippedMaterial;
            public int SkippedMesh;
        }

        private struct AsphaltStripContext
        {
            public Transform Root;
            public RoadPolyline Road;
            public Material Material;
            public float Width;
            public Func<Vector2, float> ResolveHeight;
            public RoadNetworkSettings Settings;
            public int PolylineIndex;
            public int PolylineCount;
            public ProceduralLayerMeshAccumulator Accumulator;
        }

        private static AsphaltBuildStats BuildAsphaltStrips(
            Transform networkRoot,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            Func<Vector2, float> resolveHeight,
            ProceduralCityRoadBuildOptions options)
        {
            var asphaltRoot = EnsureChildFolder(networkRoot, ProceduralRoadNetworkNames.Asphalt);
            var stats = new AsphaltBuildStats();

            for (var i = 0; i < roads.Count; i++)
                AppendAsphaltForRoad(asphaltRoot, roads[i], settings, resolveHeight, options, ref stats);

            if (stats.SkippedPolyline > 0 || stats.SkippedMaterial > 0 || stats.SkippedMesh > 0)
            {
                Debug.LogWarning(
                    "[ProceduralCityRoadBuilder] Asphalt skips: polyline=" + stats.SkippedPolyline +
                    " material=" + stats.SkippedMaterial +
                    " mesh=" + stats.SkippedMesh +
                    " strips=" + stats.StripCount + ".",
                    networkRoot);
            }

            return stats;
        }

        private static void AppendAsphaltForRoad(
            Transform asphaltRoot,
            RoadPolyline road,
            RoadNetworkSettings settings,
            Func<Vector2, float> resolveHeight,
            ProceduralCityRoadBuildOptions options,
            ref AsphaltBuildStats stats)
        {
            if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
            {
                stats.SkippedPolyline++;
                return;
            }

            var width = road.widthMeters > 0f
                ? road.widthMeters
                : settings.ResolveWidthMeters(road.roadClass);
            var material = ResolveAsphaltMaterial(road, settings);
            if (material == null)
            {
                stats.SkippedMaterial++;
                return;
            }

            var polylines = BuildRoadPolylines(road.pointsXZ, options);
            var ctx = new AsphaltStripContext
            {
                Root = asphaltRoot,
                Road = road,
                Material = material,
                Width = width,
                ResolveHeight = resolveHeight,
                Settings = settings,
                PolylineCount = polylines.Count,
                Accumulator = options.Accumulator
            };

            for (var p = 0; p < polylines.Count; p++)
            {
                var polyline = polylines[p];
                if (polyline.Count < 2)
                {
                    stats.SkippedPolyline++;
                    continue;
                }

                ctx.PolylineIndex = p;
                AppendAsphaltParts(ctx, polyline, ref stats);
            }
        }

        private static void AppendAsphaltParts(
            in AsphaltStripContext ctx,
            List<Vector2> polyline,
            ref AsphaltBuildStats stats)
        {
            var lateralSlop = ctx.Width * 0.5f + 4f;
            var roadBounds = ComputePolylineBounds(polyline, lateralSlop);
            var infrastructure = HasActiveInfrastructureSpans() && IsFiniteRect(roadBounds)
                ? new AsphaltInfrastructureSkipSampler(ctx.Road.id, roadBounds, lateralSlop)
                : null;
            var parts = SplitExcludingInfrastructureSpans(polyline, infrastructure, lateralSlop);
            var terrainHeight = ctx.ResolveHeight;
            Func<Vector2, float> roadBedHeight = terrainHeight;
            if (infrastructure != null && infrastructure.HasTunnels)
            {
                var approach = Mathf.Clamp(ctx.Settings.tunnelHoleApproachMeters, 2f, 12f);
                roadBedHeight = p => infrastructure.ResolveTunnelApproachHeight(
                    p,
                    terrainHeight(p),
                    approach);
            }

            var surfaceLift = Mathf.Max(0f, ctx.Settings.roadSurfaceLiftMeters);
            var sampledRoadBed = roadBedHeight;
            Func<Vector2, float> liftedSurface = surfaceLift > 0f
                ? p => sampledRoadBed(p) + surfaceLift
                : sampledRoadBed;
            Func<Vector2, float> resolveHeight = liftedSurface;
            if (infrastructure != null && infrastructure.HasBridgeApproaches)
            {
                var approach = Mathf.Max(2f, ctx.Settings.bridgeApproachMeters);
                resolveHeight = p => infrastructure.ResolveBridgeApproachHeight(
                    p,
                    liftedSurface(p),
                    approach);
            }
            var resolveWidth = BuildTunnelApproachWidthSampler(ctx, infrastructure);

            for (var h = 0; h < parts.Count; h++)
            {
                var part = parts[h];
                if (part.Count < 2)
                {
                    stats.SkippedPolyline++;
                    continue;
                }

                var mesh = BuildAsphaltMesh(ctx, part, resolveHeight, resolveWidth);
                if (!TryValidateAsphaltMesh(mesh))
                {
                    stats.SkippedMesh++;
                    continue;
                }

                var goName = ctx.PolylineCount == 1 && parts.Count == 1
                    ? $"Road_{ctx.Road.id}"
                    : $"Road_{ctx.Road.id}_Part_{ctx.PolylineIndex}_{h}";
                EmitAsphaltStrip(ctx, goName, mesh);
                stats.StripCount++;
            }
        }

        private static Func<Vector2, float> BuildTunnelApproachWidthSampler(
            in AsphaltStripContext ctx,
            AsphaltInfrastructureSkipSampler infrastructure)
        {
            if (infrastructure == null || !infrastructure.HasTunnels)
                return null;
            var boreSource = ctx.Settings.tunnelMidPrefab != null
                ? ctx.Settings.tunnelMidPrefab
                : ctx.Settings.tunnelPortalPrefab;
            if (!InfrastructureKitSockets.TryGetBoreClearHalfWidth(boreSource, out var clearHalfWidth))
                return null;

            var roadWidth = ctx.Width;
            var apertureWidth = clearHalfWidth * 2f;
            var approach = Mathf.Max(12f, ctx.Settings.tunnelHoleApproachMeters);
            return point => infrastructure.ResolveTunnelApproachWidth(
                point,
                roadWidth,
                apertureWidth,
                approach);
        }

        private static Mesh BuildAsphaltMesh(
            in AsphaltStripContext ctx,
            List<Vector2> points,
            Func<Vector2, float> resolveHeight,
            Func<Vector2, float> resolveWidth)
        {
            var uvTile = Mathf.Max(0.25f, ctx.Settings.roadUvWorldUnitsPerTile);
            return resolveWidth != null
                ? RoadMeshBuilder.BuildVariableWidthStripMeshWithHeightSampler(
                    points, resolveWidth, resolveHeight, uvTile)
                : RoadMeshBuilder.BuildStripMeshWithHeightSampler(
                    points, ctx.Width, resolveHeight, uvTile);
        }

        private static void EmitAsphaltStrip(in AsphaltStripContext ctx, string objectName, Mesh mesh)
        {
            if (ctx.Accumulator != null)
            {
                ctx.Accumulator.AppendMesh(ProceduralRoadNetworkNames.Asphalt, ctx.Material, mesh);
                return;
            }

            CreateAsphaltObject(ctx.Root, objectName, mesh, ctx.Material);
        }

        private static List<List<Vector2>> SplitExcludingInfrastructureSpans(
            IReadOnlyList<Vector2> polyline,
            AsphaltInfrastructureSkipSampler skipSampler,
            float lateralSlopMeters)
        {
            var results = new List<List<Vector2>>(2);
            if (polyline == null || polyline.Count < 2)
                return results;

            if (skipSampler == null || !skipSampler.HasSpans)
            {
                results.Add(CopyPolyline(polyline));
                return results;
            }

            // Infrastructure boundaries can be consecutive vertices (mouthA→mouthB).
            // Sample segments before classification so the bore/deck interior cannot
            // survive as one long asphalt edge merely because both endpoints are kept.
            var samples = RoadMeshBuilder.ResamplePolyline(
                polyline,
                Mathf.Clamp(lateralSlopMeters * 0.5f, 2f, 6f));
            List<Vector2> current = null;
            for (var i = 0; i < samples.Count; i++)
            {
                var p = samples[i];
                if (skipSampler.ShouldSkip(p))
                {
                    if (current != null && current.Count >= 2)
                        results.Add(current);
                    current = null;
                    continue;
                }

                current ??= new List<Vector2>(8);
                current.Add(p);
            }

            if (current != null && current.Count >= 2)
                results.Add(current);

            return results;
        }

        private static Rect ComputePolylineBounds(IReadOnlyList<Vector2> polyline, float padMeters)
        {
            var min = polyline[0];
            var max = polyline[0];
            for (var i = 1; i < polyline.Count; i++)
            {
                min = Vector2.Min(min, polyline[i]);
                max = Vector2.Max(max, polyline[i]);
            }

            var pad = Mathf.Max(0f, padMeters);
            return Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
        }

        private static bool IsFiniteRect(Rect rect) =>
            !(float.IsNaN(rect.xMin) || float.IsNaN(rect.yMin) || float.IsNaN(rect.xMax) || float.IsNaN(rect.yMax) ||
              float.IsInfinity(rect.xMin) || float.IsInfinity(rect.yMin) || float.IsInfinity(rect.xMax) ||
              float.IsInfinity(rect.yMax) ||
              rect.xMax < rect.xMin || rect.yMax < rect.yMin);

        private static bool HasActiveInfrastructureSpans()
        {
            var hasTunnels = MountainTunnelBuildCache.Active != null && MountainTunnelBuildCache.Active.Count > 0;
            var hasBridges = WaterCrossingBuildCache.Active != null && WaterCrossingBuildCache.Active.Count > 0;
            return hasTunnels || hasBridges;
        }

        private static List<Vector2> CopyPolyline(IReadOnlyList<Vector2> polyline)
        {
            var copy = new List<Vector2>(polyline.Count);
            for (var i = 0; i < polyline.Count; i++)
                copy.Add(polyline[i]);
            return copy;
        }

        private static bool TryValidateAsphaltMesh(Mesh mesh)
        {
            if (mesh == null)
                return false;

            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                return false;
            }

            if (bounds.extents.x < 0f || bounds.extents.y < 0f || bounds.extents.z < 0f)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                return false;
            }

            return true;
        }

        private static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        private static List<List<Vector2>> BuildRoadPolylines(
            IReadOnlyList<Vector2> sourcePoints,
            ProceduralCityRoadBuildOptions options)
        {
            if (options.ClipRect.HasValue)
            {
                var clipped = RoadMeshBuilder.ExtractOverlappingSubPolylines(sourcePoints, options.ClipRect.Value);
                for (var i = 0; i < clipped.Count; i++)
                    ExtendPolylineEnds(clipped[i], options.JunctionOverlapMeters);
                return clipped;
            }

            var polyline = CopyPolyline(sourcePoints);
            ExtendPolylineEnds(polyline, options.JunctionOverlapMeters);
            return new List<List<Vector2>> { polyline };
        }

        private static void ExtendPolylineEnds(List<Vector2> pts, float pad)
        {
            if (pts == null || pts.Count < 2 || pad <= 0f)
                return;

            var a0 = pts[0];
            var a1 = pts[1];
            var dirA = a0 - a1;
            if (dirA.sqrMagnitude > 0.0001f)
                pts[0] = a0 + dirA.normalized * pad;

            var b0 = pts[^1];
            var b1 = pts[^2];
            var dirB = b0 - b1;
            if (dirB.sqrMagnitude > 0.0001f)
                pts[^1] = b0 + dirB.normalized * pad;
        }

        private static Material ResolveAsphaltMaterial(RoadPolyline road, RoadNetworkSettings settings)
        {
            var material = RoadMeshBuilder.ResolveMaterial(road, settings);
            if (material != null)
                return material;

#if UNITY_EDITOR
            const string defaultAsphaltGuid = "0262a5c7a165ade479a671c507e01434";
            material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                RoadKitPrefabs.DefaultRoadAsphaltMaterialPath);
            if (material != null)
                return material;

            var guidPath = UnityEditor.AssetDatabase.GUIDToAssetPath(defaultAsphaltGuid);
            if (!string.IsNullOrEmpty(guidPath))
            {
                material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(guidPath);
                if (material != null)
                    return material;
            }
#endif
            if (_cachedDefaultAsphalt == null)
                _cachedDefaultAsphalt = Resources.Load<Material>("clean_asphalt_diff_2k");

            return _cachedDefaultAsphalt;
        }

        private static void CreateAsphaltObject(
            Transform asphaltRoot,
            string objectName,
            Mesh mesh,
            Material material)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(asphaltRoot, false);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = material;
        }

        private static Transform EnsureChildFolder(Transform parent, string folderName)
        {
            var existing = parent.Find(folderName);
            if (existing != null)
                return existing;

            var folder = new GameObject(folderName);
            folder.transform.SetParent(parent, false);
            return folder.transform;
        }
    }
}
