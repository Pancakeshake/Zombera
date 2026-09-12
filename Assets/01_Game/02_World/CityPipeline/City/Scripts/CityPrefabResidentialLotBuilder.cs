using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.City
{
    /// <summary>
    ///     Static residential lot subdivision and fence placement for city blocks.
    ///     Extracted from <see cref="CityPrefabDistrictBuilder"/>.
    /// </summary>
    internal static partial class CityPrefabResidentialLotBuilder
    {
        public struct Config
        {
            public GameObject FencePrefab;

            /// <summary>Working lot side bounds — resolved per area from the district ranges below.</summary>
            public float LotWidthMinMeters;
            public float LotWidthMaxMeters;
            public float LotDepthMinMeters;
            public float LotDepthMaxMeters;

            /// <summary>Per-district lot size ranges (from CityBuildConfig). Single source of truth for lot sizing.</summary>
            public LotSizeRange ResidentialLotSize;
            public LotSizeRange CommercialLotSize;
            public LotSizeRange IndustrialLotSize;
            public LotSizeRange CityCoreLotSize;

            public bool CreateEditorFloorVisuals;
            public float FloorVisualHeight;
            public int Seed;
            public Transform RoadObjectsRoot;
            /// <summary>
            ///     Curved outline polygon from <see cref="CityNamedAreaMarker.OutlineXZ"/>.
            ///     When non-null and ≥3 verts with rounded corners, lots are clipped to this shape.
            /// </summary>
            public IReadOnlyList<Vector2> Outline;
            public bool IsCurved;
            /// <summary>False for CityCore and Commercial — lot fills only, no fences.</summary>
            public bool PlaceFences;
            /// <summary>Fence every lot edge, road-facing sides included (industrial full enclosure).</summary>
            public bool FenceAllEdges;
            /// <summary>District color for lot fill visuals (set per area).</summary>
            public Color DistrictColor;
            /// <summary>Max width:depth ratio before splitting (default 2.5).</summary>
            public float MaxAspectRatio;
            public IReadOnlyList<CityNamedArea> AllAreas;
            /// <summary>
            ///     Optional per-site child filter. When non-null, only these child
            ///     transforms of the areas root are processed — region mode uses it
            ///     to subdivide each city with its own seed in one pass.
            /// </summary>
            public IReadOnlyList<Transform> AreaFilter;
            public RoadNetworkSettings RoadSettings;

            /// <summary>Chance (0–1) that an eligible commercial lot becomes a gas station.</summary>
            public float GasStationChance;
        }

        internal struct PopulateArgs
        {
            public List<LotPlacement> Lots;
            public Transform LotsRoot;
            public Rect Block;
            public bool StreetFrontX;
            public GameObject ResolvedFence;
            public Config Config;
            public HashSet<string> PlacedEdges;
            /// <summary>Collected ONCE for the whole pass — per-area collection was the District Lots hot path.</summary>
            public IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> RoadMeshes;
            /// <summary>District the lots belong to (drives commercial kind assignment).</summary>
            public CityDistrictType DistrictType;
            /// <summary>Area rng — shared with subdivision so kind assignment stays seeded.</summary>
            public System.Random Rng;
        }

        internal struct SubdivideArgs
        {
            public Rect Block;
            public bool StreetFrontX;
            public float LotWidthMin, LotWidthMax;
            public float LotDepthMin, LotDepthMax;
            public System.Random Rng;
            public IReadOnlyList<Vector2> Outline;
            public bool IsCurved;
            public CityBlockCornerMask Corners;
            public float FilletRadius;
        }

        internal struct OversizedSplitArgs
        {
            public List<LotPlacement> Lots;
            public int Index;
            public Rect Block;
            public float MinWidth, MinDepth, MaxWidth, MaxDepth;
            public System.Random Rng;
            public float Snap;
        }

        internal const string DistrictLotsContainerName = "DistrictLots";
#if UNITY_EDITOR
        private const string FencePrefabPath =
            "Assets/02_Shared/Prefabs/Fences/SM_Fence_A.prefab";
#endif

        internal struct LotPlacement
        {
            public Rect Bounds;
            public bool IsCornerLot;
            /// <summary>Clipped polygon verts for corner/curved lots (world-space XZ).</summary>
            public List<Vector2> ClippedOutline;
            public bool IsCurvedLot;
            /// <summary>
            ///     Sub-divided terrain zones within this lot (driveway, front yard, backyard, etc.).
            ///     Null until <see cref="GenerateDistrictLotTerrain"/> subdivides the lot.
            ///     When non-null, these replace the single Lot_XX fill quad.
            /// </summary>
            public List<LotSubZone> SubZones;

            /// <summary>Block-layout store lots: explicit parking-facing side (skips road-mesh facing).</summary>
            public BlockFace OverrideFace;
            /// <summary>Block-layout store lots: explicit commercial kind (Auto = classify as usual).</summary>
            public CommercialLotKind OverrideKind;
        }

        private sealed class LotPhaseTimings
        {
            public long RoadCollectMs;
            public long SubdivideMs;
            public long PopulateMs;
            public long VisualMs;
            public long FacingMs;
        }

#if UNITY_EDITOR
        public static int GenerateResidentialLots(Transform areasRoot, Config config)
        {
            if (areasRoot == null) return 0;

            var resolvedFence = ResolveResidentialFencePrefab(config.FencePrefab);
            var rng = new System.Random(config.Seed);
            var lotsCreated = 0;
            var placedEdges = new HashSet<string>();
            var timings = new LotPhaseTimings();

            // Collect the road-mesh acceleration data ONCE for the whole pass —
            // re-collecting per area re-read every road mesh's vertices for
            // every district block.
            var roadWatch = System.Diagnostics.Stopwatch.StartNew();
            var roadMeshes = config.RoadObjectsRoot != null
                ? CityRoadMeshQuery.CollectRoadMeshes(config.RoadObjectsRoot)
                : null;
            timings.RoadCollectMs = roadWatch.ElapsedMilliseconds;

            for (var a = 0; a < areasRoot.childCount; a++)
            {
                var child = areasRoot.GetChild(a);
                if (config.AreaFilter != null && !AreaFilterContains(config.AreaFilter, child))
                    continue;
                ProcessResidentialArea(child, resolvedFence, config, roadMeshes, rng, placedEdges, timings, ref lotsCreated);
            }

            // Terrain paint belongs to the Lot Terrain step — District Lots only
            // subdivides lots / editor floor visuals.

            Debug.Log(
                "[CityPrefabResidentialLotBuilder] Residential lots: " + lotsCreated +
                " — roadCollect=" + timings.RoadCollectMs + "ms subdivide=" + timings.SubdivideMs +
                "ms populate=" + timings.PopulateMs + "ms (visual=" + timings.VisualMs +
                "ms facing=" + timings.FacingMs + "ms).");
            return lotsCreated;
        }

        private static bool AreaFilterContains(IReadOnlyList<Transform> filter, Transform child)
        {
            for (var i = 0; i < filter.Count; i++)
            {
                if (filter[i] == child)
                    return true;
            }

            return false;
        }

        private static void ProcessResidentialArea(
            Transform areaTransform, GameObject resolvedFence, Config config,
            IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> roadMeshes,
            System.Random rng, HashSet<string> placedEdges,
            LotPhaseTimings timings, ref int lotsCreated)
        {
            var subdivideWatch = System.Diagnostics.Stopwatch.StartNew();

            var marker = areaTransform.GetComponent<CityNamedAreaMarker>();
            if (marker == null || !SupportsDistrictLots(marker.DistrictType))
                return;

            // Clear old and legacy containers (no Undo — District Lots is a bulk
            // regenerate step; per-object Undo dominated populate time).
            var existingLots = areaTransform.Find(DistrictLotsContainerName);
            if (existingLots != null) UnityEngine.Object.DestroyImmediate(existingLots.gameObject);
            existingLots = areaTransform.Find("ResidentialLots");
            if (existingLots != null) UnityEngine.Object.DestroyImmediate(existingLots.gameObject);

            var lotsRoot = new GameObject(DistrictLotsContainerName);
            lotsRoot.transform.SetParent(areaTransform, false);

            DisableDistrictFillRenderer(areaTransform);

            // CityCore and Commercial get lot fills only, never fences. Keep the caller's
            // fence choice, and turn fences off for those districts even when enabled.
            config.PlaceFences = config.PlaceFences
                && marker.DistrictType != CityDistrictType.CityCore
                && marker.DistrictType != CityDistrictType.Commercial;
            // Industrial encloses the whole district: every edge fenced, road-facing
            // sides included (outer lots form the perimeter, interior edges the
            // between-lot fences).
            config.FenceAllEdges = marker.DistrictType == CityDistrictType.Industrial;
            config.DistrictColor = CityNamedAreaMarker.DistrictColor(marker.DistrictType);
            config.MaxAspectRatio = marker.DistrictType == CityDistrictType.Industrial ? 3f : 2.5f;
            var size = ResolveLotSizeForDistrict(config, marker.DistrictType);
            config.LotWidthMinMeters = size.min;
            config.LotWidthMaxMeters = size.max;
            config.LotDepthMinMeters = size.min;
            config.LotDepthMaxMeters = size.max;

            // Named area bounds already account for road + sidewalk via
            // ResolveDistrictBlockInsetMeters — no additional corridor inset needed.
            var block = marker.BoundsXZ;
            if (block.width < 20f || block.height < 20f)
                return;

            // Resolve curved outline — fillet detected by arc vertices (>4), not corner mask.
            var outline = CityNamedAreaPolygonUtility.ResolveOutlineXZ(marker);
            var isCurved = outline.Count > 4;
            config.Outline = outline;
            config.IsCurved = isCurved;

            // Fillet radius derived from outline (arterial radius not stored on marker).
            var filletR = isCurved ? EstimateFilletRadiusFromOutline(outline, block) : 0f;

            var streetFrontX = block.width >= block.height;

            // Commercial blocks use block-level store layouts (U / L / strip) with
            // parking in front of the stores. Curved (filleted) blocks keep the
            // legacy per-lot path so lots can clip to the outline.
            var lots = new List<LotPlacement>();
            CityCommercialBlockLayout blockLayout = null;
            if (marker.DistrictType == CityDistrictType.Commercial && !isCurved)
                blockLayout = TryBuildCommercialBlockLayout(marker, block, rng, lotsRoot, lots);

            if (blockLayout == null)
            {
                lots = SubdivideBlockIntoLots(new SubdivideArgs
                {
                    Block = block, StreetFrontX = streetFrontX,
                    LotWidthMin = config.LotWidthMinMeters, LotWidthMax = config.LotWidthMaxMeters,
                    LotDepthMin = config.LotDepthMinMeters, LotDepthMax = config.LotDepthMaxMeters,
                    Rng = rng, Outline = outline, IsCurved = isCurved,
                    Corners = marker.RoundedCorners, FilletRadius = filletR
                });

                // Layer 1 — row-wise fold: spread interior into same-row neighbours first.
                FoldInteriorCellsIntoColumns(lots, block);

                // Layer 2 — absorb landlocked without max cap; road access always wins.
                AbsorbLandlockedLots(lots, block);

                // Split elongated strips before sliver/oversized processing.
                var aspectLimit = config.MaxAspectRatio > 1f ? config.MaxAspectRatio : 2.5f;
                SplitElongatedLots(lots, block,
                    config.LotWidthMinMeters, config.LotDepthMinMeters, aspectLimit);
                AbsorbLandlockedLots(lots, block);

                // Sliver merge with district max cap.
                MergeSliverLotsIntoNeighbors(lots, block, config.LotWidthMinMeters, config.LotDepthMinMeters,
                    config.LotWidthMaxMeters, config.LotDepthMaxMeters);

                // Split any mega-lots back to district max (then re-absorb if split created interior).
                SplitOversizedLots(lots, block,
                    config.LotWidthMinMeters, config.LotDepthMinMeters,
                    config.LotWidthMaxMeters, config.LotDepthMaxMeters, rng);
                AbsorbLandlockedLots(lots, block);

                // Layer 3 — final gate: no landlocked lots may reach Populate.
                EnsureAllLotsRoadAccessible(lots, block);

                // Assign clipped outlines for any lot intersecting the fillet.
                if (isCurved)
                    AssignClippedOutlines(lots, outline, block);
            }

            timings.SubdivideMs += subdivideWatch.ElapsedMilliseconds;

            var populateWatch = System.Diagnostics.Stopwatch.StartNew();
            PopulateResidentialLots(new PopulateArgs
            {
                Lots = lots, LotsRoot = lotsRoot.transform, Block = block,
                StreetFrontX = streetFrontX, ResolvedFence = resolvedFence,
                Config = config, PlacedEdges = placedEdges, RoadMeshes = roadMeshes,
                DistrictType = marker.DistrictType, Rng = rng
            }, timings, ref lotsCreated);
            timings.PopulateMs += populateWatch.ElapsedMilliseconds;
        }

        private static LotSizeRange ResolveLotSizeForDistrict(Config config, CityDistrictType t)
        {
            var range = t switch
            {
                CityDistrictType.Commercial => config.CommercialLotSize,
                CityDistrictType.Industrial => config.IndustrialLotSize,
                CityDistrictType.CityCore   => config.CityCoreLotSize,
                _                           => config.ResidentialLotSize,
            };

            if (range.min > 0f && range.max > 0f)
                return range;

            // Legacy fallback for callers that don't populate the district ranges.
            return t switch
            {
                CityDistrictType.Commercial => new LotSizeRange(28, 56),
                CityDistrictType.Industrial => new LotSizeRange(32, 64),
                CityDistrictType.CityCore   => new LotSizeRange(48, 96),
                _                           => new LotSizeRange(16, 32),
            };
        }

        private static bool SupportsDistrictLots(CityDistrictType t) =>
            t is CityDistrictType.Residential or CityDistrictType.Commercial
                or CityDistrictType.Industrial or CityDistrictType.CityCore;

        private static void DisableDistrictFillRenderer(Transform areaTransform)
        {
            var districtFill = areaTransform.Find("DistrictFill");
            if (districtFill == null) return;

            var fillRenderer = districtFill.GetComponent<MeshRenderer>();
            if (fillRenderer != null) fillRenderer.enabled = false;
        }

        /// <summary>
        ///     Computes clipped outline polygons for any lot on a curved block
        ///     whose rect intersects the fillet arc.  Places clipped polygons in
        ///     <see cref="LotPlacement.ClippedOutline"/> and sets IsCurvedLot.
        /// </summary>
        private static void AssignClippedOutlines(List<LotPlacement> lots, IReadOnlyList<Vector2> outline, Rect block)
        {
            // Single-lot curved block: one lot = entire outline shape.
            if (lots.Count == 1)
            {
                var solo = lots[0];
                solo.ClippedOutline = new List<Vector2>(outline);
                solo.Bounds = block;
                solo.IsCurvedLot = true;
                lots[0] = solo;
                return;
            }

            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];

                // Expand clip rect to block boundary on every side the lot touches.
                var clipRect = ExpandClipRectToBlockEdges(lot.Bounds, block, 0.05f);

                var clipped = new List<Vector2>();
                if (!CityNamedAreaPolygonUtility.TryComputeLotClipPolygon(
                        outline, clipRect, clipped, 4f)) continue;
                if (clipped.Count < 3) continue;

                // Store clipped outline whenever clip area differs from lot rect area.
                var clipArea = CityNamedAreaPolygonUtility.ComputePolygonArea(clipped);
                var rectArea = clipRect.width * clipRect.height;
                if (clipArea < rectArea * 0.99f || clipped.Count > 4)
                {
                    lot.IsCurvedLot = true; // ensures polygon fence + mesh paths
                    lot.IsCornerLot = true;  // size-cap exemption
                    lot.ClippedOutline = clipped;
                    lot.Bounds = CityNamedAreaPolygonUtility.ComputeBounds(clipped);
                }

                lots[i] = lot;
            }
        }

        /// <summary>
        ///     Estimates the fillet radius from the outline by measuring the shortest
        ///     distance from any corner of the bounding rect to the nearest arc vertex.
        /// </summary>
        private static float EstimateFilletRadiusFromOutline(IReadOnlyList<Vector2> outline, Rect block)
        {
            if (outline == null || outline.Count < 5) return 0f;
            var best = float.MaxValue;
            var corners = new Vector2[]
            {
                new(block.xMin, block.yMin), new(block.xMax, block.yMin),
                new(block.xMax, block.yMax), new(block.xMin, block.yMax)
            };
            for (var c = 0; c < corners.Length; c++)
            {
                for (var i = 0; i < outline.Count; i++)
                {
                    var d = Vector2.Distance(corners[c], outline[i]);
                    if (d > 0.5f && d < best) best = d;
                }
            }

            return best < float.MaxValue ? Mathf.Max(2f, best) : 8f;
        }

        /// <summary>
        ///     Expands <paramref name="lot"/> to <paramref name="block"/> boundaries on
        ///     every side the lot touches, so the clip rect captures the full road frontage
        ///     (straight sides) while the curve comes from the outline clip.
        /// </summary>
        private static Rect ExpandClipRectToBlockEdges(Rect lot, Rect block, float snap)
        {
            var r = lot;
            if (Mathf.Abs(lot.xMin - block.xMin) < snap) r.xMin = block.xMin;
            if (Mathf.Abs(lot.xMax - block.xMax) < snap) r.xMax = block.xMax;
            if (Mathf.Abs(lot.yMin - block.yMin) < snap) r.yMin = block.yMin;
            if (Mathf.Abs(lot.yMax - block.yMax) < snap) r.yMax = block.yMax;
            return r;
        }

#endif
    }
}
