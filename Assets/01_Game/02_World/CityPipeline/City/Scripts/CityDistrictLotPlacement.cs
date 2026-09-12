using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.City
{
    /// <summary>
    ///     Static lot-based placement logic for residential district buildings.
    ///     Extracted from <see cref="CityPrefabDistrictBuildingPlacer"/>.
    /// </summary>
    internal static partial class CityDistrictLotPlacement
    {
        public const string PlacedBuildingsContainerName = "PlacedBuildings";
#if UNITY_EDITOR
        internal struct DistrictLotBuildArgs
        {
            public Transform AreaTransform;
            public Transform LotsContainer;
            public CityNamedAreaMarker Marker;
            public List<CityAssembledBuildingCatalogEntry> Catalog;
            public CityNamedAreaBuildingLayoutSettings Settings;
            public System.Random Rng;
            public bool UseProxy;
            public IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> RoadMeshes;
            public IReadOnlyList<CityNamedArea> AllAreas;
            public RoadNetworkSettings RoadSettings;
            public IGeneratedBuildingStateSink StateSink;
            /// <summary>Optional world terrain query for water-aware lot placement.</summary>
            public IWorldTerrainQuery TerrainQuery;
            public float DeepWaterDepthMeters;
            public float MinDistanceToWaterMeters;
            public float MaxReclaimDepthMeters;
            public bool RequireWaterGate;
        }

        internal struct SingleLotArgs
        {
            public Transform LotTransform;
            public Rect BlockBounds;
            public float GroundY;
            public float Setback;
            public List<CityAssembledBuildingCatalogEntry> Candidates;
            public Transform Parent;
            public System.Random Rng;
            public bool UseProxy;
            public IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> RoadMeshes;
            /// <summary>World-XZ footprints of buildings already placed in this area pass.</summary>
            public List<Rect> PlacedFootprints;
            public IGeneratedBuildingStateSink StateSink;
        }

        private readonly struct DistrictLotPlacementContext
        {
            public readonly Rect LotRect;
            public readonly Rect BlockBounds;
            public readonly float GroundY;
            public readonly float Setback;
            public readonly BlockFace StreetFace;
            /// <summary>
            ///     World-space polygon of the lot (from its fill mesh). Non-null for
            ///     clipped corner/curved lots where the polygon is smaller than the
            ///     lot AABB — buildings must fit the polygon, not just its bounds.
            /// </summary>
            public readonly IReadOnlyList<Vector2> Polygon;

            public DistrictLotPlacementContext(
                Rect lotRect,
                Rect blockBounds,
                float groundY,
                float setback,
                BlockFace streetFace,
                IReadOnlyList<Vector2> polygon)
            {
                LotRect = lotRect;
                BlockBounds = blockBounds;
                GroundY = groundY;
                Setback = setback;
                StreetFace = streetFace;
                Polygon = polygon;
            }
        }

        private readonly struct DistrictLotPlacementRequest
        {
            public readonly CityAssembledBuildingCatalogEntry Entry;
            public readonly DistrictLotPlacementContext Lot;
            public readonly Transform LotTransform;
            public readonly Transform Parent;
            public readonly System.Random Rng;
            public readonly bool UseProxy;
            /// <summary>World-XZ footprints of buildings already placed in this area pass.</summary>
            public readonly List<Rect> PlacedFootprints;
            /// <summary>Optional slot rect override (commercial layouts place into slots).</summary>
            public readonly Rect? OverrideLotRect;
            public readonly float? OverrideSetback;
            public readonly BlockFace? OverrideFace;
            public readonly IGeneratedBuildingStateSink StateSink;
            public readonly int Ordinal;
            /// <summary>
            ///     When true, the building is scaled on XZ to match the slot/lot
            ///     rect exactly (commercial store lots / strip slots).
            /// </summary>
            public readonly bool FillLotExactly;

            public DistrictLotPlacementRequest(
                CityAssembledBuildingCatalogEntry entry,
                DistrictLotPlacementContext lot,
                Transform lotTransform,
                Transform parent,
                System.Random rng,
                bool useProxy,
                List<Rect> placedFootprints,
                IGeneratedBuildingStateSink stateSink,
                int ordinal,
                Rect? overrideLotRect = null,
                float? overrideSetback = null,
                BlockFace? overrideFace = null,
                bool fillLotExactly = false)
            {
                Entry = entry;
                Lot = lot;
                LotTransform = lotTransform;
                Parent = parent;
                Rng = rng;
                UseProxy = useProxy;
                PlacedFootprints = placedFootprints;
                StateSink = stateSink;
                Ordinal = ordinal;
                OverrideLotRect = overrideLotRect;
                OverrideSetback = overrideSetback;
                OverrideFace = overrideFace;
                FillLotExactly = fillLotExactly;
            }
        }

        public static Rect ExtractLotRect(Transform lotTransform)
        {
            if (lotTransform == null) return new Rect(0, 0, 0, 0);

            var mf = lotTransform.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return new Rect(0, 0, 0, 0);

            // Reconstruct the lot rect from the mesh's local bounds + world
            // position. This is correct for BOTH mesh types the lot pipeline
            // creates: the simple fill quad and clipped corner/curved-lot
            // polygons. The previous vertex-order assumption (verts[1].x =
            // half width, verts[2].z = half depth) only held for the simple
            // quad — for clipped meshes it produced degenerate rects (e.g.
            // 68 x 0 m strips) that painted long stray terrain strips
            // (concrete footpaths etc.) far outside the city.
            var localBounds = mf.sharedMesh.bounds;
            if (localBounds.size.x <= 0f || localBounds.size.z <= 0f)
                return new Rect(0, 0, 0, 0);

            var pos = lotTransform.position;
            return Rect.MinMaxRect(
                pos.x + localBounds.min.x, pos.z + localBounds.min.z,
                pos.x + localBounds.max.x, pos.z + localBounds.max.z);
        }

        /// <summary>
        ///     Reconstructs the lot's world-space polygon from its fill mesh vertices.
        ///     The clipped (corner/curved) lot mesh stores its polygon verts directly
        ///     in world coordinates, so this returns the exact fenced lot shape.
        /// </summary>
        private static List<Vector2> ExtractLotPolygonWorld(Transform lotTransform)
        {
            if (lotTransform == null) return null;

            var mf = lotTransform.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;

            var verts = mf.sharedMesh.vertices;
            if (verts == null || verts.Length < 4) return null;

            var pos = lotTransform.position;
            var polygon = new List<Vector2>(verts.Length);
            for (var i = 0; i < verts.Length; i++)
                polygon.Add(new Vector2(pos.x + verts[i].x, pos.z + verts[i].z));

            return polygon;
        }

        /// <summary>
        ///     Post-placement lot containment: the measured instance bounds must stay
        ///     inside the lot rect (small tolerance for float noise). Oversized prefabs
        ///     whose catalog dims understate their real footprint are rejected here so
        ///     the caller can try the next (smaller) candidate.
        /// </summary>
        private static bool FitsWithinLotWithTolerance(Rect bounds, Rect lotRect, float tolerance)
        {
            return bounds.xMin >= lotRect.xMin - tolerance &&
                   bounds.xMax <= lotRect.xMax + tolerance &&
                   bounds.yMin >= lotRect.yMin - tolerance &&
                   bounds.yMax <= lotRect.yMax + tolerance;
        }

        private static bool OverlapsAnyPlaced(Rect bounds, List<Rect> placed, float tolerance)
        {
            if (placed == null || placed.Count == 0) return false;

            var shrunk = new Rect(
                bounds.xMin + tolerance, bounds.yMin + tolerance,
                Mathf.Max(0f, bounds.width - tolerance * 2f),
                Mathf.Max(0f, bounds.height - tolerance * 2f));
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i].Overlaps(shrunk))
                    return true;
            }

            return false;
        }

        private static readonly List<Vector3> DoorSocketBuffer = new(8);

        /// <summary>
        ///     Records the placed building's footprint rect and street-facing door
        ///     anchor on its facing marker so the Lot Terrain step can paint the
        ///     slab to match the actual building instead of a lot-centred guess.
        /// </summary>
        private static void RecordPlacedBuildingAnchor(
            GameObject instance,
            CityBuildingStreetFacingMarker marker,
            CityBuildingRoadFacingUtility.LotPlacementResult placement,
            float footprintW,
            float footprintD,
            Rect? measuredFootprint = null)
        {
            DoorSocketBuffer.Clear();
            CityDoorAnchorUtility.CollectDoorPositions(instance.transform, DoorSocketBuffer);
            var door = CityDoorAnchorUtility.SelectStreetFacingDoor(
                DoorSocketBuffer, placement.WorldPosition, marker);
            if (door.HasValue)
            {
                marker.doorAnchorWorld = door.Value;
                marker.hasDoorAnchor = true;
            }

            // Prefer the bounds already measured at place-time — a second
            // GetComponentsInChildren walk per successful place was pure overhead.
            if (measuredFootprint.HasValue &&
                measuredFootprint.Value.width > 0.5f && measuredFootprint.Value.height > 0.5f)
            {
                marker.footprintXZ = measuredFootprint.Value;
                return;
            }

            if (!CityBuildingPrefabFootprintUtility.TryMeasureWorldBoundsXZ(instance, out var footprintXZ))
            {
                var swapped = Mathf.RoundToInt(placement.Rotation.eulerAngles.y / 90f) % 2 != 0;
                var extX = (swapped ? footprintD : footprintW) * 0.5f;
                var extZ = (swapped ? footprintW : footprintD) * 0.5f;
                var center = placement.WorldPosition;
                footprintXZ = Rect.MinMaxRect(
                    center.x - extX, center.z - extZ, center.x + extX, center.z + extZ);
            }

            marker.footprintXZ = footprintXZ;
        }

        /// <summary>
        ///     Prefers the lot's own marker face (resolved during the District Lots
        ///     step against the SUBDIVISION block). The placement-time block bounds
        ///     can differ (hub-shifted / region mode) and mislabel corner lots.
        /// </summary>
        private static BlockFace ResolveLotStreetFace(
            Transform lotTransform, Rect lotRect, Rect blockBounds, float groundY,
            IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> roadMeshes)
        {
            var lotMarker = lotTransform.GetComponent<CityLotFacingMarker>();
            return lotMarker != null
                ? lotMarker.streetFace
                : CityBuildingRoadFacingUtility.ResolveLotStreetFaceFromRoadMeshes(
                    lotRect, blockBounds, streetFrontX: blockBounds.width >= blockBounds.height,
                    groundY, roadMeshes);
        }

        public static bool TryPlaceSingleLot(SingleLotArgs a)
        {
            var lotRect = ExtractLotRect(a.LotTransform);
            if (lotRect.width < 8f || lotRect.height < 8f) return false;

            var streetFace = ResolveLotStreetFace(
                a.LotTransform, lotRect, a.BlockBounds, a.GroundY, a.RoadMeshes);

            var lotContext = new DistrictLotPlacementContext(
                lotRect, a.BlockBounds, a.GroundY, a.Setback, streetFace,
                ExtractLotPolygonWorld(a.LotTransform));

            for (var ci = 0; ci < a.Candidates.Count; ci++)
            {
                if (TryPlaceCandidate(new DistrictLotPlacementRequest(
                        a.Candidates[ci], lotContext, a.LotTransform, a.Parent, a.Rng,
                        a.UseProxy, a.PlacedFootprints, a.StateSink, 0)))
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Places a commercial layout plan (strip / corner L / U court / gas
        ///     station) into one lot, honouring the kind assigned at subdivision.
        ///     Returns the number of placed slots; 0 when the lot is Single (or
        ///     nothing fitted) so callers fall back to the legacy path.
        /// </summary>
        public static int TryPlaceCommercialPlan(
            DistrictLotBuildArgs a, Rect blockBounds, float setback,
            List<CityAssembledBuildingCatalogEntry> candidates, Transform lotContainer, float groundY,
            Transform lotTransform, Rect lotRect, List<Rect> placedFootprints)
        {
            var streetFace = ResolveLotStreetFace(lotTransform, lotRect, blockBounds, groundY, a.RoadMeshes);
            var lotMarker = lotTransform.GetComponent<CityLotFacingMarker>();
            var kind = lotMarker != null ? lotMarker.commercialKind : CommercialLotKind.Auto;
            var plan = CityCommercialLotLayoutPlanner.BuildPlan(
                lotRect, blockBounds, streetFace, kind, candidates, a.Rng);
            if (plan.Type == CommercialLayoutType.Single)
                return 0;

            var lotContext = new DistrictLotPlacementContext(
                lotRect, blockBounds, groundY, setback, streetFace,
                ExtractLotPolygonWorld(lotTransform));

            var placedCount = 0;
            var storeSetback = kind is CommercialLotKind.StoreLot or CommercialLotKind.StoreLotCorner
                ? 0.35f
                : 0f;
            var fillExact = kind is CommercialLotKind.StoreLot or CommercialLotKind.StoreLotCorner
                || plan.Type != CommercialLayoutType.Single;
            for (var i = 0; i < plan.Slots.Count; i++)
            {
                var slot = plan.Slots[i];
                if (slot.Entry == null)
                    continue;

                if (TryPlaceCandidate(new DistrictLotPlacementRequest(
                        slot.Entry, lotContext, lotTransform, lotContainer, a.Rng, a.UseProxy,
                        placedFootprints, a.StateSink, i,
                        slot.SlotRect, storeSetback, slot.StreetFace, fillExact)))
                    placedCount++;
            }

            return placedCount;
        }

        public static int PlaceDistrictLotBuildings(DistrictLotBuildArgs a)
        {
            // Region mode: lots live at world (hub-shifted) positions while BoundsXZ is
            // template-local — resolve the block bounds in world space so face-detection
            // fallbacks agree with the lot rects. Raw bounds (no corridor inset): the
            // inset belongs to footpath clearance, and face fallbacks compare lot edges
            // against these bounds directly.
            var blockBounds = a.Marker.GetHubShiftedBoundsXZ();
            if (blockBounds.width <= 0f || blockBounds.height <= 0f)
                blockBounds = a.Marker.BoundsXZ;
            // Full street setback — legacy placement pushed buildings deeper into
            // lots, and the door-aware terrain paint stretches the driveway to the
            // door regardless of depth.
            var setback = Mathf.Max(4f, a.Settings.streetSetbackMeters);

            // Strict category matching: a district only uses buildings from its own
            // top-level category folder (e.g. Residential/… for a residential district).
            // Mixed is the catch-all district and may draw from every category.
            var candidates = ResolveFilteredCandidates(a.Catalog, a.Marker.DistrictType);
            if (candidates.Count == 0 && a.Marker.DistrictType != CityDistrictType.Mixed)
            {
                Debug.LogWarning(
                    $"[CityDistrictLotPlacement] No '{a.Marker.DistrictType}' building prefabs in the catalog " +
                    $"for area '{a.Marker.name}' — its lots will stay empty.");
            }

            // Biggest-first: large lots take the large variants first so yard sizes
            // stay proportionate, and small lots fall through to smaller buildings.
            SortCandidatesByFootprintDesc(candidates, a.UseProxy);

            var groundY = a.AreaTransform.position.y;
            var lotContainer = new GameObject(PlacedBuildingsContainerName);
            lotContainer.transform.SetParent(a.AreaTransform, false);
            Undo.RegisterCreatedObjectUndo(lotContainer, "Place District Buildings");

            return PlaceLotsInContainer(a, blockBounds, setback, candidates, lotContainer.transform, groundY);
        }
#endif
    }
}
