using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Shared static helpers for building road-facing orientation.
    ///     Single source of truth for yaw conventions, lot-face detection,
    ///     door-socket resolution, world-extent computation, and lot placement.
    /// </summary>
    public static class CityBuildingRoadFacingUtility
    {
        // ------------------------------------------------------------------
        // Face detection
        // ------------------------------------------------------------------

        /// <summary>
        ///     Returns the <see cref="BlockFace"/> of the lot edge that coincides
        ///     with the block boundary (the open / street-facing side).
        ///     Delegates to <see cref="ResolveLotOpenStreetFace"/> using the same
        ///     street-front axis as the fence builder.
        /// </summary>
        public static BlockFace ResolveLotStreetFace(Rect lot, Rect blockBounds)
        {
            var streetFrontX = blockBounds.width >= blockBounds.height;
            return ResolveLotOpenStreetFace(lot, blockBounds, streetFrontX);
        }

        /// <summary>
        ///     Single structural rule: "no fence = road." Returns the <see cref="BlockFace"/>
        ///     of the lot edge guaranteed to be open (no fence), using the same street-front
        ///     axis that <see cref="CityPrefabResidentialLotBuilder"/> uses when placing fences.
        ///     Deterministic tie-break: South when <paramref name="streetFrontX"/>, else West.
        /// </summary>
        public static BlockFace ResolveLotOpenStreetFace(Rect lot, Rect blockBounds, bool streetFrontX)
        {
            const float snap = 0.05f;

            // Any edge snapping to the block boundary is open — all four road sides.
            var openBottom = Mathf.Abs(lot.yMin - blockBounds.yMin) < snap;
            var openTop    = Mathf.Abs(lot.yMax - blockBounds.yMax) < snap;
            var openLeft   = Mathf.Abs(lot.xMin - blockBounds.xMin) < snap;
            var openRight  = Mathf.Abs(lot.xMax - blockBounds.xMax) < snap;

            // Single open edge: unambiguous.
            if (openBottom && !openTop)     return BlockFace.South;
            if (openTop    && !openBottom)  return BlockFace.North;
            if (openLeft   && !openRight)   return BlockFace.West;
            if (openRight  && !openLeft)    return BlockFace.East;

            // Through-lot: both edges on the open axis touch the block. Nearest wins.
            if (openBottom && openTop)
            {
                var dS = Mathf.Abs(lot.yMin - blockBounds.yMin);
                var dN = Mathf.Abs(lot.yMax - blockBounds.yMax);
                return dS <= dN ? BlockFace.South : BlockFace.North;
            }
            if (openLeft && openRight)
            {
                var dW = Mathf.Abs(lot.xMin - blockBounds.xMin);
                var dE = Mathf.Abs(lot.xMax - blockBounds.xMax);
                return dW <= dE ? BlockFace.West : BlockFace.East;
            }

            // Zero open edges: stale bounds, FP drift, or hub moved after generation.
            // Use lot centre vs block centre on the depth axis — never hard-default to
            // a single face for all failed lots.
            Debug.LogWarning(
                "[CityBuildingRoadFacingUtility] Lot " + lot + " has no open (street-facing) edge " +
                "touching block bounds " + blockBounds + " (streetFrontX=" + streetFrontX + "). " +
                "Hub may have moved — regenerate areas and lots.");
            if (streetFrontX)
                return lot.center.y <= blockBounds.center.y ? BlockFace.South : BlockFace.North;
            else
                return lot.center.x <= blockBounds.center.x ? BlockFace.West : BlockFace.East;
        }

        /// <summary>
        ///     Returns every <see cref="BlockFace"/> whose corresponding edge is open
        ///     (no fence), using the same booleans as PlaceFencesForLot.
        /// </summary>
        public static bool TryGetLotOpenFaces(Rect lot, Rect blockBounds, bool streetFrontX, List<BlockFace> results,
            IReadOnlyList<Vector2> outline = null)
        {
            if (results == null) return false;
            results.Clear();

            const float snap = 0.05f;
            const float outlineSnap = 0.15f;
            // Any edge snapping to the block boundary is open (no fence) — all four road sides.
            var openBottom = Mathf.Abs(lot.yMin - blockBounds.yMin) < snap;
            var openTop    = Mathf.Abs(lot.yMax - blockBounds.yMax) < snap;
            var openLeft   = Mathf.Abs(lot.xMin - blockBounds.xMin) < snap;
            var openRight  = Mathf.Abs(lot.xMax - blockBounds.xMax) < snap;

            // For curved blocks, an edge on the outer outline is also open.
            if (outline != null && outline.Count >= 3)
            {
                if (!openBottom) openBottom = IsEdgeOnPolygon(lot.xMin, lot.yMin, lot.xMax, lot.yMin, outline, outlineSnap);
                if (!openTop)    openTop    = IsEdgeOnPolygon(lot.xMin, lot.yMax, lot.xMax, lot.yMax, outline, outlineSnap);
                if (!openLeft)   openLeft   = IsEdgeOnPolygon(lot.xMin, lot.yMin, lot.xMin, lot.yMax, outline, outlineSnap);
                if (!openRight)  openRight  = IsEdgeOnPolygon(lot.xMax, lot.yMin, lot.xMax, lot.yMax, outline, outlineSnap);
            }

            if (openBottom) results.Add(BlockFace.South);
            if (openTop)    results.Add(BlockFace.North);
            if (openLeft)   results.Add(BlockFace.West);
            if (openRight)  results.Add(BlockFace.East);

            return results.Count > 0;
        }

        private static bool IsEdgeOnPolygon(float x0, float z0, float x1, float z1,
            IReadOnlyList<Vector2> polygon, float tolerance)
        {
            var segments = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, z0), new Vector2(x1, z1)) / 2f));
            for (var s = 0; s <= segments; s++)
            {
                var t = s / (float)segments;
                var px = x0 + (x1 - x0) * t;
                var pz = z0 + (z1 - z0) * t;
                var onPoly = false;
                for (var i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    var abx = b.x - a.x;
                    var abz = b.y - a.y;
                    var apx = px - a.x;
                    var apz = pz - a.y;
                    var proj = Mathf.Clamp01((apx * abx + apz * abz) / Mathf.Max(0.0001f, abx * abx + abz * abz));
                    var cx = a.x + proj * abx;
                    var cz = a.y + proj * abz;
                    if ((px - cx) * (px - cx) + (pz - cz) * (pz - cz) <= tolerance * tolerance)
                    {
                        onPoly = true;
                        break;
                    }
                }

                if (!onPoly) return false;
            }

            return true;
        }

        /// <summary>
        ///     Picks the open lot edge closest to any generated road mesh.
        ///     Single source of truth: "nearest unfenced road."
        ///     Falls back to <see cref="ResolveLotOpenStreetFace"/> when no road meshes
        ///     are available or no open edges exist.
        /// </summary>
        public static BlockFace ResolveLotStreetFaceFromRoadMeshes(
            Rect lot,
            Rect blockBounds,
            bool streetFrontX,
            float groundY,
            IReadOnlyList<CityRoadMeshQuery.RoadMeshInfo> roadMeshes,
            IReadOnlyList<Vector2> outline = null)
        {
            var openFaces = new List<BlockFace>(4);
            if (!TryGetLotOpenFaces(lot, blockBounds, streetFrontX, openFaces, outline))
                return ResolveLotOpenStreetFace(lot, blockBounds, streetFrontX);

            // Single open edge: no road query needed.
            if (openFaces.Count == 1)
                return openFaces[0];

            // Multiple open edges (through-lot, corner): pick closest to road mesh.
            if (roadMeshes != null && roadMeshes.Count > 0)
            {
                var bestFace = openFaces[0];
                var bestDist = float.MaxValue;
                for (var i = 0; i < openFaces.Count; i++)
                {
                    var mid = GetOpenEdgeMidpoint(lot, openFaces[i], groundY);
                    var dSq = CityRoadMeshQuery.GetMinRoadDistanceSq(roadMeshes, mid);
                    if (dSq < bestDist)
                    {
                        bestDist = dSq;
                        bestFace = openFaces[i];
                    }
                }

                return bestFace;
            }

            // Fallback: open-edge-only heuristic (no road mesh data).
            return ResolveLotOpenStreetFace(lot, blockBounds, streetFrontX);
        }

        private static Vector3 GetOpenEdgeMidpoint(Rect lot, BlockFace face, float groundY)
        {
            return face switch
            {
                BlockFace.South => new Vector3(lot.center.x, groundY, lot.yMin),
                BlockFace.North => new Vector3(lot.center.x, groundY, lot.yMax),
                BlockFace.West  => new Vector3(lot.xMin, groundY, lot.center.y),
                _               => new Vector3(lot.xMax, groundY, lot.center.y), // East
            };
        }

        // ------------------------------------------------------------------
        // Yaw conventions (match ResolveFaceAxes exactly)
        // ------------------------------------------------------------------

        /// <summary>South = 180, North = 0, West = 270, East = 90.</summary>
        public static float GetRoadFacingYaw(BlockFace face)
        {
            return face switch
            {
                BlockFace.South => 180f,
                BlockFace.North => 0f,
                BlockFace.West  => 270f,
                BlockFace.East  => 90f,
                _ => 0f
            };
        }

        /// <summary>World-space unit vector pointing from the lot toward the road.</summary>
        public static Vector3 GetRoadNormal(BlockFace face)
        {
            return face switch
            {
                BlockFace.South => Vector3.back,
                BlockFace.North => Vector3.forward,
                BlockFace.West  => Vector3.left,
                BlockFace.East  => Vector3.right,
                _ => Vector3.forward
            };
        }

        // ------------------------------------------------------------------
        // Door / socket orientation
        // ------------------------------------------------------------------

        /// <summary>
        ///     Returns the yaw offset that must be added to the road-facing yaw
        ///     so the building's door (<c>StairSocket</c> or fallback) points
        ///     toward the road.
        ///     Priority: <paramref name="catalogYawOffset"/> (explicit override)
        ///     > StairSocket child > Door / Entrance child.
        ///     Default (no socket) returns 0 (assumes +Z = front).
        /// </summary>
        /// <summary>
        ///     Simple dominant-axis heuristic: finds StairSocket/Door/Entrance child,
        ///     returns yaw offset based on which local axis dominates.
        ///     +Z→0, -Z→180, +X→270, -X→90. Returns 0 if no socket found.
        /// </summary>
        private static float GetSimpleDoorYawOffset(GameObject prefab)
        {
            if (prefab == null) return 0f;
            var socket = FindMarkerChild(prefab.transform);
            if (socket == null) return 0f;
            var lp = socket.localPosition;
            if (Mathf.Abs(lp.x) > Mathf.Abs(lp.z))
                return lp.x > 0f ? 270f : 90f;
            else
                return lp.z > 0f ? 0f : 180f;
        }

        public static float GetDoorYawOffset(GameObject prefab, float catalogYawOffset)
        {
            CityBuildingPrefabFootprintUtility.TryMeasureFootprint(prefab, out var fp);
            return GetDoorYawOffset(prefab, catalogYawOffset, fp);
        }

        /// <summary>
        ///     Resolves door yaw from a pre-measured footprint, avoiding a throwaway
        ///     prefab instantiation inside TryMeasureFootprint.
        /// </summary>
        public static float GetDoorYawOffset(GameObject prefab, float catalogYawOffset, BuildingFootprintInfo cachedFootprint)
        {
            if (Mathf.Abs(catalogYawOffset) > 0.01f)
                return catalogYawOffset;

            if (prefab == null) return 0f;

            // Authoritative: the generator stamps the main door's outward yaw on
            // the prefab root — prefer it over socket-order guessing.
            var stamped = prefab.GetComponent<BuildingDoorAnchor>();
            if (stamped != null && stamped.hasDoor)
                return stamped.doorYawOffset;

            var socket = FindMarkerChild(prefab.transform);
            if (socket == null) return 0f;

            // Footprint-relative door face: which cardinal side of the bounding box
            // the socket sits on.  Essential for large prefabs where StairSocket is
            // in a corner and raw localPosition dominant axis lies.
            if (cachedFootprint.IsValid)
            {
                var rel = new Vector2(
                    socket.localPosition.x - cachedFootprint.CenterOffsetXZ.x,
                    socket.localPosition.z - cachedFootprint.CenterOffsetXZ.y);
                var hw = cachedFootprint.WidthMeters  * 0.5f;
                var hd = cachedFootprint.DepthMeters  * 0.5f;

                var dPZ = Mathf.Abs(rel.y - hd); // distance to +Z face
                var dNZ = Mathf.Abs(rel.y + hd); // distance to -Z face
                var dPX = Mathf.Abs(rel.x - hw); // distance to +X face
                var dNX = Mathf.Abs(rel.x + hw); // distance to -X face

                var minD = Mathf.Min(dPZ, dNZ, dPX, dNX);
                if (Mathf.Approximately(minD, dPZ)) return 0f;    // +Z
                if (Mathf.Approximately(minD, dNZ)) return 180f;  // -Z
                if (Mathf.Approximately(minD, dPX)) return 270f;  // +X
                return 90f;                                        // -X
            }

            // Fallback: dominant-axis heuristic on raw localPosition.
            var lp = socket.localPosition;
            if (Mathf.Abs(lp.x) > Mathf.Abs(lp.z))
                return lp.x > 0f ? 270f : 90f;
            else
                return lp.z > 0f ? 0f : 180f;
        }

        private static Transform FindMarkerChild(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var n = child.name;
                if (n.StartsWith("StairSocket", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Door",        System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Entrance",    System.StringComparison.OrdinalIgnoreCase))
                    return child;

                var found = FindMarkerChild(child);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // World extents (lifted from CityNamedAreaBuildingLayout)
        // ------------------------------------------------------------------

        /// <summary>
        ///     Given a local footprint (width X, depth Z) and a Y-axis yaw,
        ///     returns the axis-aligned world extents (extentX, extentZ) that
        ///     the rotated building occupies.
        /// </summary>
        public static void GetWorldExtents(
            float localWidth, float localDepth, float yawDegrees,
            out float extentX, out float extentZ)
        {
            yawDegrees = ((yawDegrees % 360f) + 360f) % 360f;
            if (Mathf.Abs(yawDegrees - 90f)  < 0.1f ||
                Mathf.Abs(yawDegrees - 270f) < 0.1f)
            {
                extentX = localDepth;
                extentZ = localWidth;
            }
            else
            {
                extentX = localWidth;
                extentZ = localDepth;
            }
        }

        // ------------------------------------------------------------------
        // Lot placement
        // ------------------------------------------------------------------

        /// <summary>
        ///     Result of computing a building placement inside a residential lot.
        /// </summary>
        public readonly struct LotPlacementResult
        {
            public readonly Vector3 WorldPosition;
            public readonly Quaternion Rotation;
            public readonly float ExtentX;
            public readonly float ExtentZ;
            public readonly bool Fits;

            public LotPlacementResult(Vector3 pos, Quaternion rot, float ex, float ez, bool fits)
            {
                WorldPosition = pos; Rotation = rot; ExtentX = ex; ExtentZ = ez; Fits = fits;
            }

            public static readonly LotPlacementResult Failed = default;
        }

        /// <summary>
        ///     Computes a building placement for a single residential lot.
        ///     Returns position, rotation, world extents, and a fit flag.
        ///     The caller validates footprint containment inside the lot.
        /// </summary>
        public static LotPlacementResult TryComputeLotPlacement(
            Rect lotRect,
            Rect blockBounds,
            float groundY,
            float setbackMeters,
            float localWidth,
            float localDepth,
            GameObject prefab,
            float catalogYawOffset,
            float marginMeters = 0.3f,
            float extraBackMeters = 0f,
            BlockFace? resolvedFace = null)
        {
            var face = resolvedFace ?? ResolveLotStreetFace(lotRect, blockBounds);

            // Resolve door offset: catalog override or footprint-relative detection.
            float doorOffset;
            if (Mathf.Abs(catalogYawOffset) > 0.01f)
                doorOffset = catalogYawOffset;
            else
                doorOffset = GetDoorYawOffset(prefab, 0f);

            // Emulates the pre-refactor directional logic (CityNamedAreaBuildingLayout):
            // base yaw from the street face (South=180, North=0, West=270, East=90)
            // plus the prefab's measured door offset — the door always points at the
            // lot's street side, matching the fence-open-edge rule.
            var finalYaw = (GetRoadFacingYaw(face) + doorOffset) % 360f;

            if (finalYaw < 0f) finalYaw += 360f;

            GetWorldExtents(localWidth, localDepth, finalYaw, out var extX, out var extZ);

            // Position: centred horizontally, setback + half-extent + extraBack from the open edge.
            // Adaptive inset: shallow lots automatically reduce the front setback so the
            // building still fits (leaving ≥ 1 m rear clearance) — the full street
            // setback only applies when the lot is deep enough.
            var deepSpan = face is BlockFace.South or BlockFace.North
                ? Mathf.Abs(lotRect.height)
                : Mathf.Abs(lotRect.width);
            // Inward extent is extZ for north/south-facing lots and extX for east/west —
            // the old code always used extZ, which over-allowed inset for rotated
            // (east/west-facing) footprints.
            var inwardExtent = face is BlockFace.South or BlockFace.North ? extZ : extX;
            var maxInset = Mathf.Max(1f, deepSpan - inwardExtent - marginMeters * 2f);
            var totalInset = Mathf.Min(setbackMeters + extraBackMeters, maxInset);
            Vector3 pos;
            switch (face)
            {
                case BlockFace.South:
                    pos = new Vector3(lotRect.center.x, groundY, lotRect.yMin + totalInset + extZ * 0.5f);
                    break;
                case BlockFace.North:
                    pos = new Vector3(lotRect.center.x, groundY, lotRect.yMax - totalInset - extZ * 0.5f);
                    break;
                case BlockFace.West:
                    pos = new Vector3(lotRect.xMin + totalInset + extX * 0.5f, groundY, lotRect.center.y);
                    break;
                default: // East
                    pos = new Vector3(lotRect.xMax - totalInset - extX * 0.5f, groundY, lotRect.center.y);
                    break;
            }

            // Axis-aligned containment.
            var hx = extX * 0.5f;
            var hz = extZ * 0.5f;
            var fits = pos.x - hx >= lotRect.xMin + marginMeters &&
                       pos.x + hx <= lotRect.xMax - marginMeters &&
                       pos.z - hz >= lotRect.yMin + marginMeters &&
                       pos.z + hz <= lotRect.yMax - marginMeters;

            var rot = Quaternion.Euler(0f, finalYaw, 0f);
            return new LotPlacementResult(pos, rot, extX, extZ, fits);
        }
    }
}