#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.Data;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        private const string PropsContainerName = "InteriorProps";

        /// <summary>
        ///     Global scale normalizer applied to all instantiated props.
        ///     Set to 1.0 when prefabs are already at correct world scale.
        ///     Use per-prop scaleOverride on <see cref="PropPlacement"/> for
        ///     fine-tuning individual props.
        /// </summary>
        private const float GlobalPropScaleNormalizer = 1f;

        /// <summary>
        ///     Places interior props into every room of the generated building, driven by
        ///     the <see cref="InteriorPropConfig"/> assigned on <see cref="RoomSettings"/>.
        ///     Called after perimeter walls and room content are placed but before the
        ///     prefab is saved.
        /// </summary>
        private static void PlaceInteriorProps(GenerationContext context, Transform root)
        {
            var config = context.Settings?.RoomSettings?.PropConfig;
            if (config == null)
            {
                Debug.LogWarning(
                    "[ModularSingleLevelHouseGeneratorTool] No InteriorPropConfig assigned to RoomSettings.PropConfig. " +
                    "Props won't be placed. Assign an InteriorPropConfig asset in RoomSettings.");
                return;
            }

            if (context.Plan.FloorRoomPlans.Count == 0)
                return;

            var totalProps = 0;
            foreach (var entry in config.roomEntries)
                totalProps += entry.props?.Length ?? 0;

            Debug.Log(
                $"[ModularSingleLevelHouseGeneratorTool] InteriorPropConfig: " +
                $"{config.roomEntries.Length} room entries, " +
                $"{totalProps} standalone props, " +
                $"fallback={config.fallbackProps.Length} props.");

            var propsRoot = new GameObject(PropsContainerName);
            propsRoot.transform.SetParent(root, false);

            for (var levelIdx = 0; levelIdx < context.Plan.FloorRoomPlans.Count; levelIdx++)
            {
                var floorPlan = context.Plan.FloorRoomPlans[levelIdx];
                if (floorPlan.Rooms == null || floorPlan.Rooms.Count == 0)
                    continue;

                var originOffset = GetFloorOriginOffset(context, levelIdx);
                var doorPositions = CollectDoorPositions(context, levelIdx);

                foreach (var room in floorPlan.Rooms)
                    PlacePropsInRoom(context, config, propsRoot, floorPlan, room, originOffset, doorPositions);
            }

            // Remove the container if nothing was placed.
            if (propsRoot.transform.childCount == 0)
            {
                Debug.LogWarning(
                    "[ModularSingleLevelHouseGeneratorTool] No props placed — " +
                    "check that arrangements have prefabs assigned and rooms are large enough.");
                Object.DestroyImmediate(propsRoot);
            }
            else
            {
                Debug.Log(
                    $"[ModularSingleLevelHouseGeneratorTool] Placed {propsRoot.transform.childCount} interior prop(s).");
            }
        }

        /// <summary>
        ///     Places props into a single room:
        ///     1. Guaranteed standalone props.
        ///     2. Weighted random fill from remaining spawn attempts.
        /// </summary>
        private static void PlacePropsInRoom(
            GenerationContext context,
            InteriorPropConfig config,
            GameObject propsRoot,
            FloorRoomPlan floorPlan,
            RoomRegion room,
            Vector3 originOffset,
            List<Vector3> doorPositions)
        {
            if (!TryResolveRoomEntry(config, room.RoomType,
                    out var props, out var fillAttempts))
            {
                Debug.LogWarning(
                    $"[Props] Room '{room.Name}' ({room.RoomType}): no props " +
                    $"in config entry and fallback is empty. Skipping.");
                return;
            }

            if (!TryBuildSpatialContext(floorPlan, room, originOffset, doorPositions,
                    config.doorClearanceRadius, out var spatial))
            {
                Debug.LogWarning(
                    $"[Props] Room '{room.Name}' ({room.RoomType}): bounds too small, skipping.");
                return;
            }

            Debug.Log(
                $"[Props] Room '{room.Name}' ({room.RoomType}): " +
                $"{props.Length} props, {fillAttempts} fill attempts, " +
                $"bounds=({spatial.Bounds.MinX:F1},{spatial.Bounds.MinZ:F1})-({spatial.Bounds.MaxX:F1},{spatial.Bounds.MaxZ:F1}), " +
                $"doors={doorPositions.Count}.");

            var propCounts = new Dictionary<PropPlacement, int>();
            var footprints = new List<PropArrangementLayout.PlacedFootprint>();

            PlaceGuaranteedProps(context, propsRoot, props, spatial, propCounts, footprints);

            var fillPlaced = 0;
            for (var attempt = 0; attempt < fillAttempts; attempt++)
            {
                if (TrySpawnOneWeightedProp(context, propsRoot, props, spatial,
                        propCounts, footprints))
                    fillPlaced++;
            }

            if (fillPlaced > 0)
                Debug.Log(
                    $"[Props] Room '{room.Name}': weighted fill placed {fillPlaced} prop(s).");
        }

        /// <summary>
        ///     Resolves the room entry from config, falling back to global fallback
        ///     if the room type has no explicit entry.
        /// </summary>
        private static bool TryResolveRoomEntry(
            InteriorPropConfig config,
            RoomType roomType,
            out PropPlacement[] props,
            out int fillAttempts)
        {
            var entry = config.GetEntryForRoom(roomType);
            props = entry?.props ?? System.Array.Empty<PropPlacement>();
            fillAttempts = entry?.spawnAttempts ?? 0;

            // Fallback: use config-level fallback if entry has nothing.
            if (props.Length == 0)
            {
                props = config.fallbackProps ?? System.Array.Empty<PropPlacement>();
                fillAttempts = config.fallbackSpawnAttempts;
            }

            return props.Length > 0;
        }

        /// <summary>
        ///     Builds the spatial context (bounds, touches, lot limits, door clearance)
        ///     for a single room. Returns false if the room bounds are invalid.
        /// </summary>
        private static bool TryBuildSpatialContext(
            FloorRoomPlan floorPlan,
            RoomRegion room,
            Vector3 originOffset,
            List<Vector3> doorPositions,
            float doorClearanceRadius,
            out PropSpatialCtx spatial)
        {
            spatial = default;

            if (!TryComputeRoomBounds(room, originOffset, out var bounds))
                return false;

            var planW = floorPlan.FloorPlanWidth;
            var planD = floorPlan.FloorPlanDepth;

            var touches = new RoomTouches(
                room.Ix0 == 0, room.Ix1 == planW,
                room.Iz0 == 0, room.Iz1 == planD);

            spatial = new PropSpatialCtx
            {
                FloorY = floorPlan.FloorY,
                Bounds = bounds,
                Touches = touches,
                LotMinX = originOffset.x - CellSize * 0.5f,
                LotMaxX = planW * CellSize - CellSize * 0.5f + originOffset.x,
                LotMinZ = originOffset.z - CellSize * 0.5f,
                LotMaxZ = planD * CellSize - CellSize * 0.5f + originOffset.z,
                DoorPositions = doorPositions,
                DoorClearanceSq = doorClearanceRadius * doorClearanceRadius,
            };

            return true;
        }

        // ── Phase 2 & 3: Standalone props ──────────────────────────────────

        /// <summary>
        ///     Phase 2: Places guaranteed standalone props that have guaranteedCount &gt; 0.
        /// </summary>
        private static void PlaceGuaranteedProps(
            GenerationContext context,
            GameObject propsRoot,
            PropPlacement[] props,
            in PropSpatialCtx spatial,
            Dictionary<PropPlacement, int> propCounts,
            List<PropArrangementLayout.PlacedFootprint> footprints)
        {
            var placed = 0;
            foreach (var placement in props)
            {
                if (placement.guaranteedCount <= 0 || placement.prefab == null)
                    continue;

                for (var g = 0; g < placement.guaranteedCount; g++)
                {
                    if (TrySpawnOneProp(context, propsRoot, placement, spatial,
                            propCounts, footprints))
                        placed++;
                    else
                        break;
                }
            }

            if (placed > 0)
                Debug.Log($"[Props] Guaranteed phase placed {placed} prop(s).");
        }

        private struct PropSpatialCtx
        {
            public float FloorY;
            public RoomWorldBounds Bounds;
            public RoomTouches Touches;
            public float LotMinX, LotMaxX, LotMinZ, LotMaxZ;
            public List<Vector3> DoorPositions;
            /// <summary>Squared door clearance radius — hard exclusion zone around each door.</summary>
            public float DoorClearanceSq;
        }

        private static bool TryComputeRoomBounds(RoomRegion room, Vector3 originOffset, out RoomWorldBounds bounds)
        {
            const float roomMargin = 0.4f;

            // Raw wall face positions (cell-aligned, no margin).
            var wallMinX = room.Ix0 * CellSize - CellSize * 0.5f + originOffset.x;
            var wallMaxX = room.Ix1 * CellSize - CellSize * 0.5f + originOffset.x;
            var wallMinZ = room.Iz0 * CellSize - CellSize * 0.5f + originOffset.z;
            var wallMaxZ = room.Iz1 * CellSize - CellSize * 0.5f + originOffset.z;

            // Interior safe zone (inset by margin for random prop placement).
            var minX = wallMinX + roomMargin;
            var maxX = wallMaxX - roomMargin;
            var minZ = wallMinZ + roomMargin;
            var maxZ = wallMaxZ - roomMargin;

            if (maxX <= minX || maxZ <= minZ)
            {
                bounds = default;
                return false;
            }

            bounds = new RoomWorldBounds
            {
                MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ,
                WallMinX = wallMinX, WallMaxX = wallMaxX,
                WallMinZ = wallMinZ, WallMaxZ = wallMaxZ,
            };
            return true;
        }

        // ── Prop placement helpers ────────────────────────────────────────

        /// <summary>
        ///     Attempts to place a single guaranteed prop (Phase 2).
        ///     Returns false if placement failed after all retries.
        /// </summary>
        private static bool TrySpawnOneProp(
            GenerationContext context,
            GameObject propsRoot,
            PropPlacement placement,
            in PropSpatialCtx spatial,
            Dictionary<PropPlacement, int> propCounts,
            List<PropArrangementLayout.PlacedFootprint> footprints)
        {
            if (placement.prefab == null)
                return false;

            if (!propCounts.TryGetValue(placement, out var placed))
                placed = 0;
            if (placed >= placement.maxPerRoom)
                return false;

            var ctx = BuildConstraintCtx(spatial);
            const int maxTries = 12;
            for (var trial = 0; trial < maxTries; trial++)
            {
                if (TryInstantiateProp(context, propsRoot, placement, spatial, ctx, footprints))
                {
                    propCounts[placement] = placed + 1;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Weighted pick + place (Phase 3). Selects a random prop from the weighted
        ///     list and attempts placement.
        /// </summary>
        private static bool TrySpawnOneWeightedProp(
            GenerationContext context,
            GameObject propsRoot,
            PropPlacement[] props,
            in PropSpatialCtx spatial,
            Dictionary<PropPlacement, int> propCounts,
            List<PropArrangementLayout.PlacedFootprint> footprints)
        {
            var placement = PickWeightedProp(props, context.Random);
            if (placement.prefab == null)
                return false;

            return TrySpawnOneProp(context, propsRoot, placement, spatial, propCounts, footprints);
        }

        /// <summary>
        ///     Generates a random or aligned position, validates it, and instantiates.
        /// </summary>
        private static bool TryInstantiateProp(
            GenerationContext context,
            GameObject propsRoot,
            PropPlacement placement,
            in PropSpatialCtx spatial,
            in PropConstraintCtx ctx,
            List<PropArrangementLayout.PlacedFootprint> footprints)
        {
            float cx, cz;

            if (placement.wallAlignment == WallAlignment.Centered)
            {
                cx = (spatial.Bounds.MinX + spatial.Bounds.MaxX) * 0.5f;
                cz = (spatial.Bounds.MinZ + spatial.Bounds.MaxZ) * 0.5f;
            }
            else
            {
                if (!PropArrangementLayout.TryPickAgainstWall(spatial.Bounds,
                        spatial.Touches, placement.prefab,
                        context.Random.ToSystemRandom(),
                        spatial.DoorPositions,
                        out var wallPos, out _))
                    return false;
                cx = wallPos.x;
                cz = wallPos.z;
            }

            if (!PassesAllChecks(cx, cz, placement, ctx, footprints))
                return false;

            var yaw = ResolveSnappedYaw(
                placement.randomiseYaw ? context.Random.Value01() * 360f : 0f,
                placement);

            InstantiateProp(propsRoot, placement,
                new Vector3(cx, spatial.FloorY + placement.yOffset, cz), yaw, null);

            if (placement.footprintRadius > 0f)
                PropArrangementLayout.RecordFootprint(
                    new Vector2(cx, cz), placement.footprintRadius, footprints);

            return true;
        }

        /// <summary>
        ///     Instantiates the prop prefab and sets name, rotation, and scale.
        /// </summary>
        private static void InstantiateProp(
            GameObject propsRoot,
            PropPlacement placement,
            Vector3 worldPos,
            float yaw,
            RoomRegion? room)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var go = Object.Instantiate(placement.prefab, worldPos, rot, propsRoot.transform);

            var roomName = room?.Name;
            go.name = roomName != null
                ? $"{placement.prefab.name}_{roomName}"
                : placement.prefab.name;

            var scale = GlobalPropScaleNormalizer * placement.scaleOverride;
            if (scale <= 0f) scale = 1f;
            go.transform.localScale = Vector3.one * scale;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        ///     Returns the floor origin offset for the given level index.
        ///     In skyscraper mode floors are centered within the base footprint.
        /// </summary>
        private static Vector3 GetFloorOriginOffset(GenerationContext context, int levelIdx)
        {
            if (context.Plan.SkyscraperMode &&
                context.Plan.FloorOffsets != null &&
                levelIdx < context.Plan.FloorOffsets.Length)
                return context.Plan.FloorOffsets[levelIdx];

            return Vector3.zero;
        }

        /// <summary>
        ///     Collects world-space door positions for the given level from the perimeter wall plan.
        /// </summary>
        private static List<Vector3> CollectDoorPositions(GenerationContext context, int levelIdx)
        {
            var result = new List<Vector3>();

            foreach (var wallPlan in context.Plan.PerimeterWallPlans)
            {
                if (wallPlan.Level != levelIdx)
                    continue;

                foreach (var seg in wallPlan.Segments)
                {
                    if (seg.IsDoor)
                        result.Add(seg.Segment.Position);
                }
            }

            return result;
        }

        /// <summary>
        ///     Picks a prop from the weighted list. Returns the prop (not null) but prefab may be null.
        /// </summary>
        private static PropPlacement PickWeightedProp(PropPlacement[] props, GenerationRandom random)
        {
            if (props == null || props.Length == 0)
                return default;

            var total = 0f;
            foreach (var p in props)
                total += Mathf.Max(0f, p.weight);

            if (total <= 0f)
                return props[random.RangeExclusive(0, props.Length)];

            var roll = random.Value01() * total;
            var acc = 0f;
            foreach (var p in props)
            {
                acc += Mathf.Max(0f, p.weight);
                if (roll <= acc)
                    return p;
            }

            return props[props.Length - 1];
        }

        // ── Constraint checking ────────────────────────────────────────────

        /// <summary>
        ///     Validates a candidate world-space position against all placement constraints.
        /// </summary>
        private struct PropConstraintCtx
        {
            public bool touchesLeft, touchesRight, touchesBottom, touchesTop;
            public float lotMinX, lotMaxX, lotMinZ, lotMaxZ;
            public List<Vector3> doorPositions;
            public float doorClearanceSq;
        }

        private static PropConstraintCtx BuildConstraintCtx(in PropSpatialCtx spatial)
        {
            return new PropConstraintCtx
            {
                touchesLeft = spatial.Touches.Left,
                touchesRight = spatial.Touches.Right,
                touchesBottom = spatial.Touches.Bottom,
                touchesTop = spatial.Touches.Top,
                lotMinX = spatial.LotMinX,
                lotMaxX = spatial.LotMaxX,
                lotMinZ = spatial.LotMinZ,
                lotMaxZ = spatial.LotMaxZ,
                doorPositions = spatial.DoorPositions,
                doorClearanceSq = spatial.DoorClearanceSq,
            };
        }

        /// <summary>Runs all placement checks: constraints, door zone, overlap, room bounds.</summary>
        private static bool PassesAllChecks(
            float cx, float cz, PropPlacement placement,
            in PropConstraintCtx ctx,
            List<PropArrangementLayout.PlacedFootprint> footprints)
        {
            if (!PassesConstraints(cx, cz, placement, ctx)) return false;
            if (!CheckDoorClearanceZone(cx, cz, ctx)) return false;
            if (PropArrangementLayout.HasOverlap(
                    new Vector2(cx, cz), placement.footprintRadius, footprints))
                return false;
            return true;
        }

        private static bool PassesConstraints(float cx, float cz, PropPlacement placement, in PropConstraintCtx ctx)
        {
            if (!CheckWallDistance(cx, cz, placement, ctx)) return false;
            if (!CheckDoorDistance(cx, cz, placement, ctx)) return false;
            if (cx < ctx.lotMinX || cx > ctx.lotMaxX || cz < ctx.lotMinZ || cz > ctx.lotMaxZ)
                return false;
            return true;
        }

        /// <summary>
        ///     Hard exclusion zone: a circle of radius sqrt(doorClearanceSq) around
        ///     each door position. No prop may be placed inside.
        /// </summary>
        private static bool CheckDoorClearanceZone(float cx, float cz, in PropConstraintCtx ctx)
        {
            if (ctx.doorClearanceSq <= 0f || ctx.doorPositions == null || ctx.doorPositions.Count == 0)
                return true;

            foreach (var doorPos in ctx.doorPositions)
            {
                var dx = cx - doorPos.x;
                var dz = cz - doorPos.z;
                if (dx * dx + dz * dz < ctx.doorClearanceSq)
                    return false;
            }

            return true;
        }

        private static bool CheckWallDistance(float cx, float cz, PropPlacement placement, in PropConstraintCtx ctx)
        {
            if (placement.minWallDistance <= 0f) return true;
            var nearestWallDist = float.MaxValue;
            if (ctx.touchesLeft)   nearestWallDist = Mathf.Min(nearestWallDist, cx - ctx.lotMinX);
            if (ctx.touchesRight)  nearestWallDist = Mathf.Min(nearestWallDist, ctx.lotMaxX - cx);
            if (ctx.touchesBottom) nearestWallDist = Mathf.Min(nearestWallDist, cz - ctx.lotMinZ);
            if (ctx.touchesTop)    nearestWallDist = Mathf.Min(nearestWallDist, ctx.lotMaxZ - cz);
            return nearestWallDist >= placement.minWallDistance;
        }

        private static bool CheckDoorDistance(float cx, float cz, PropPlacement placement, in PropConstraintCtx ctx)
        {
            if (placement.minDoorDistance <= 0f || ctx.doorPositions.Count == 0) return true;
            var minDistSq = placement.minDoorDistance * placement.minDoorDistance;
            var nearestDoorSq = float.MaxValue;
            foreach (var doorPos in ctx.doorPositions)
            {
                var dx = cx - doorPos.x;
                var dz = cz - doorPos.z;
                var distSq = dx * dx + dz * dz;
                if (distSq < nearestDoorSq) nearestDoorSq = distSq;
            }
            return nearestDoorSq >= minDistSq;
        }

        // ── Yaw snapping ──────────────────────────────────────────────────

        /// <summary>
        ///     Snaps a yaw angle to the nearest multiple of yawSnapDegrees when configured.
        ///     Falls back to the raw yaw if snapping is disabled or yawSnapDegrees ≤ 0.
        /// </summary>
        private static float ResolveSnappedYaw(float rawYaw, PropPlacement placement)
        {
            if (!placement.randomiseYaw)
                return 0f;

            if (placement.yawSnapDegrees <= 0f)
                return rawYaw;

            var snap = placement.yawSnapDegrees;
            var steps = Mathf.RoundToInt(360f / snap);
            var snappedIndex = Mathf.RoundToInt(rawYaw / snap) % steps;
            if (snappedIndex < 0) snappedIndex += steps;
            return snappedIndex * snap;
        }

        /// <summary>Returns true if the XZ point is within the room bounds.</summary>
        private static bool IsInRoomBounds(float x, float z, in RoomWorldBounds bounds)
        {
            return x >= bounds.MinX && x <= bounds.MaxX
                && z >= bounds.MinZ && z <= bounds.MaxZ;
        }

        // ── Per-room floor materials ────────────────────────────────────────

        /// <summary>
        ///     After the main reskin pass, applies per-room-type floor materials from
        ///     the <see cref="RoomFloorSkinConfig"/> to floor renderers that fall
        ///     within each room's world-space bounds.
        /// </summary>
        /// <returns>true if any floor materials were changed.</returns>
        private static bool ApplyRoomFloorMaterials(GameObject root, GenerationContext context, RoomFloorSkinConfig config)
        {
            if (config == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return false;

            var roomBounds = BuildRoomFloorBounds(context);
            if (roomBounds.Count == 0)
                return false;

            var roomTypeMaterials = PrePickRoomFloorMaterials(config, roomBounds);
            if (roomTypeMaterials.Count == 0)
                return false;

            var applied = 0;
            foreach (var r in renderers)
            {
                if (!IsFloorRenderer(r))
                    continue;

                if (TryApplyFloorMaterial(r, roomBounds, roomTypeMaterials))
                    applied++;
            }

            if (applied > 0)
                Debug.Log(
                    $"[ModularSingleLevelHouseGeneratorTool] Applied per-room floor materials to {applied} floor renderer(s).");

            return applied > 0;
        }

        /// <summary>
        ///     Builds a list of (world-space XZ rect, room type name, floor Y) for every
        ///     room across all floors, used to match floor renderers to their containing room.
        ///     Stairwell rooms are included naturally since they are proper <see cref="RoomRegion"/> entries.
        /// </summary>
        private static List<(Rect xz, string roomTypeName, float floorY)> BuildRoomFloorBounds(GenerationContext context)
        {
            var roomBounds = new List<(Rect xz, string roomTypeName, float floorY)>();

            for (var levelIdx = 0; levelIdx < context.Plan.FloorRoomPlans.Count; levelIdx++)
            {
                var floorPlan = context.Plan.FloorRoomPlans[levelIdx];
                if (floorPlan.Rooms == null)
                    continue;

                var originOffset = GetFloorOriginOffset(context, levelIdx);

                foreach (var room in floorPlan.Rooms)
                {
                    var minX = room.Ix0 * CellSize - CellSize * 0.5f + originOffset.x;
                    var maxX = room.Ix1 * CellSize - CellSize * 0.5f + originOffset.x;
                    var minZ = room.Iz0 * CellSize - CellSize * 0.5f + originOffset.z;
                    var maxZ = room.Iz1 * CellSize - CellSize * 0.5f + originOffset.z;

                    var typeName = Zombera.Data.RoomFloorSkinConfig.RoomTypeNameFromIndex((int)room.RoomType);
                    roomBounds.Add((
                        Rect.MinMaxRect(minX, minZ, maxX, maxZ),
                        typeName,
                        floorPlan.FloorY));
                }
            }

            return roomBounds;
        }

        /// <summary>
        ///     Pre-picks one material per room type name for consistent per-room results.
        /// </summary>
        private static Dictionary<string, Material> PrePickRoomFloorMaterials(
            RoomFloorSkinConfig config,
            List<(Rect xz, string roomTypeName, float floorY)> roomBounds)
        {
            var materials = new Dictionary<string, Material>();
            foreach (var (_, roomTypeName, _) in roomBounds)
            {
                if (string.IsNullOrEmpty(roomTypeName) || materials.ContainsKey(roomTypeName))
                    continue;

                var mat = config.PickFloorMaterial(roomTypeName);
                if (mat != null)
                    materials[roomTypeName] = mat;
            }

            return materials;
        }

        /// <summary>
        ///     Detects floor renderers by name (own or parent contains "floor").
        /// </summary>
        private static bool IsFloorRenderer(Renderer r)
        {
            if (r.name.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return r.transform.parent != null
                   && r.transform.parent.name.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        ///     Tries to match a renderer to a room and apply the room-type's floor material.
        ///     Returns true when a material was applied.
        /// </summary>
        private static bool TryApplyFloorMaterial(
            Renderer r,
            List<(Rect xz, string roomTypeName, float floorY)> roomBounds,
            Dictionary<string, Material> roomTypeMaterials)
        {
            var pos = r.transform.position;

            foreach (var (xz, roomTypeName, floorY) in roomBounds)
            {
                if (string.IsNullOrEmpty(roomTypeName))
                    continue;

                if (Mathf.Abs(pos.y - floorY) > WallHeight * 0.5f)
                    continue;

                if (!xz.Contains(new Vector2(pos.x, pos.z)))
                    continue;

                if (!roomTypeMaterials.TryGetValue(roomTypeName, out var mat) || mat == r.sharedMaterial)
                    return false;

                Undo.RecordObject(r, "Apply Room Floor Material");
                r.sharedMaterial = mat;
                EditorUtility.SetDirty(r);
                return true;
            }

            return false;
        }

        // ── Per-room interior wall paint materials ──────────────────────────

        /// <summary>
        ///     After the main reskin pass, applies per-room-type interior wall paint
        ///     materials from the <see cref="RoomFloorSkinConfig"/> to interior wall
        ///     renderers (<c>IntWall_*</c>) that fall within each room's world-space bounds.
        /// </summary>
        /// <returns>true if any interior wall materials were changed.</returns>
        private static bool ApplyRoomInteriorWallMaterials(GameObject root, GenerationContext context, RoomFloorSkinConfig config)
        {
            if (config == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return false;

            var roomBounds = BuildRoomFloorBounds(context);
            if (roomBounds.Count == 0)
                return false;

            var roomTypeMaterials = PrePickRoomWallMaterials(config, roomBounds);
            if (roomTypeMaterials.Count == 0)
                return false;

            var applied = 0;
            foreach (var r in renderers)
            {
                if (!IsInteriorWallRenderer(r))
                    continue;

                if (TryApplyWallMaterial(r, roomBounds, roomTypeMaterials))
                    applied++;
            }

            if (applied > 0)
                Debug.Log(
                    $"[ModularSingleLevelHouseGeneratorTool] Applied per-room interior wall paint to {applied} wall renderer(s).");

            return applied > 0;
        }

        /// <summary>
        ///     Pre-picks one wall paint material per room type name for consistent per-room results.
        /// </summary>
        private static Dictionary<string, Material> PrePickRoomWallMaterials(
            RoomFloorSkinConfig config,
            List<(Rect xz, string roomTypeName, float floorY)> roomBounds)
        {
            var materials = new Dictionary<string, Material>();
            foreach (var (_, roomTypeName, _) in roomBounds)
            {
                if (string.IsNullOrEmpty(roomTypeName) || materials.ContainsKey(roomTypeName))
                    continue;

                var mat = config.PickWallPaintMaterial(roomTypeName);
                if (mat != null)
                    materials[roomTypeName] = mat;
            }

            return materials;
        }

        /// <summary>
        ///     Returns true when the renderer or its parent is an interior wall piece
        ///     (name contains "IntWall" or "InteriorWall").
        /// </summary>
        private static bool IsInteriorWallRenderer(Renderer r)
        {
            if (r.name.IndexOf("IntWall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (r.name.IndexOf("InteriorWall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (r.transform.parent != null)
            {
                var parentName = r.transform.parent.name;
                if (parentName.IndexOf("IntWall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (parentName.IndexOf("InteriorWall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Tries to match a renderer to a room and apply the room-type's wall paint material.
        ///     Returns true when a material was applied.
        /// </summary>
        private static bool TryApplyWallMaterial(
            Renderer r,
            List<(Rect xz, string roomTypeName, float floorY)> roomBounds,
            Dictionary<string, Material> roomTypeMaterials)
        {
            var pos = r.transform.position;

            foreach (var (xz, roomTypeName, floorY) in roomBounds)
            {
                if (string.IsNullOrEmpty(roomTypeName))
                    continue;

                if (Mathf.Abs(pos.y - floorY) > WallHeight * 0.5f)
                    continue;

                if (!xz.Contains(new Vector2(pos.x, pos.z)))
                    continue;

                if (!roomTypeMaterials.TryGetValue(roomTypeName, out var mat) || mat == r.sharedMaterial)
                    return false;

                Undo.RecordObject(r, "Apply Room Wall Material");
                r.sharedMaterial = mat;
                EditorUtility.SetDirty(r);
                return true;
            }

            return false;
        }

        // ── Per-room ceiling materials ──────────────────────────────────────

        /// <summary>
        ///     After the main reskin pass, applies per-room-type ceiling materials
        ///     from the <see cref="RoomFloorSkinConfig"/> to ceiling renderers that
        ///     fall within each room's world-space bounds.
        /// </summary>
        /// <returns>true if any ceiling materials were changed.</returns>
        private static bool ApplyRoomCeilingMaterials(GameObject root, GenerationContext context, RoomFloorSkinConfig config)
        {
            if (config == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return false;

            var roomBounds = BuildRoomFloorBounds(context);
            if (roomBounds.Count == 0)
                return false;

            var roomTypeMaterials = PrePickRoomCeilingMaterials(config, roomBounds);
            if (roomTypeMaterials.Count == 0)
                return false;

            var applied = 0;
            foreach (var r in renderers)
            {
                if (!IsCeilingRenderer(r))
                    continue;

                if (TryApplyCeilingMaterial(r, roomBounds, roomTypeMaterials))
                    applied++;
            }

            if (applied > 0)
                Debug.Log(
                    $"[ModularSingleLevelHouseGeneratorTool] Applied per-room ceiling materials to {applied} ceiling renderer(s).");

            return applied > 0;
        }

        /// <summary>
        ///     Pre-picks one ceiling material per room type name for consistent per-room results.
        /// </summary>
        private static Dictionary<string, Material> PrePickRoomCeilingMaterials(
            RoomFloorSkinConfig config,
            List<(Rect xz, string roomTypeName, float floorY)> roomBounds)
        {
            var materials = new Dictionary<string, Material>();
            foreach (var (_, roomTypeName, _) in roomBounds)
            {
                if (string.IsNullOrEmpty(roomTypeName) || materials.ContainsKey(roomTypeName))
                    continue;

                var mat = config.PickCeilingMaterial(roomTypeName);
                if (mat != null)
                    materials[roomTypeName] = mat;
            }

            return materials;
        }

        /// <summary>
        ///     Returns true when the renderer or its parent is a ceiling piece
        ///     (name contains "Ceiling").
        /// </summary>
        private static bool IsCeilingRenderer(Renderer r)
        {
            if (r.name.IndexOf("Ceiling", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return r.transform.parent != null
                   && r.transform.parent.name.IndexOf("Ceiling", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        ///     Tries to match a renderer to a room and apply the room-type's ceiling material.
        ///     Returns true when a material was applied.
        /// </summary>
        private static bool TryApplyCeilingMaterial(
            Renderer r,
            List<(Rect xz, string roomTypeName, float floorY)> roomBounds,
            Dictionary<string, Material> roomTypeMaterials)
        {
            var pos = r.transform.position;

            foreach (var (xz, roomTypeName, floorY) in roomBounds)
            {
                if (string.IsNullOrEmpty(roomTypeName))
                    continue;

                if (Mathf.Abs(pos.y - floorY) > WallHeight * 0.5f)
                    continue;

                if (!xz.Contains(new Vector2(pos.x, pos.z)))
                    continue;

                if (!roomTypeMaterials.TryGetValue(roomTypeName, out var mat) || mat == r.sharedMaterial)
                    return false;

                Undo.RecordObject(r, "Apply Room Ceiling Material");
                r.sharedMaterial = mat;
                EditorUtility.SetDirty(r);
                return true;
            }

            return false;
        }
    }

    internal readonly struct RoomTouches
    {
        public readonly bool Left, Right, Bottom, Top;
        public RoomTouches(bool left, bool right, bool bottom, bool top)
        {
            Left = left; Right = right; Bottom = bottom; Top = top;
        }
    }

    internal struct RoomWorldBounds
    {
        /// <summary>Interior bounds with margin (safe zone for random prop placement).</summary>
        public float MinX, MaxX, MinZ, MaxZ;

        /// <summary>Actual wall face positions (no margin) — use for wall-aligned placement.</summary>
        public float WallMinX, WallMaxX, WallMinZ, WallMaxZ;
    }

    /// <summary>
    ///     Spatial placement logic for props — wall-aligned positioning, centered positioning,
    ///     prefab edge-distance computation, and footprint overlap detection.
    /// </summary>
    internal static class PropArrangementLayout
    {
        private const float OverlapSpacing = 0.3f;

        /// <summary>Per-prefab cache of distances from local origin to cardinal renderer edges.</summary>
        private static readonly Dictionary<GameObject, (float plusX, float minusX, float plusZ, float minusZ)>
            s_PrefabEdgeCache = new();

        /// <summary>
        ///     Computes the distance from the prefab root pivot to each of the four cardinal
        ///     edges (+X, -X, +Z, -Z) of the combined renderer bounding box in local space.
        ///     Falls back to a 0.5 m default disc when the prefab has no renderers.
        /// </summary>
        private static (float plusX, float minusX, float plusZ, float minusZ) GetPivotToEdgeDistances(
            GameObject prefab)
        {
            if (prefab == null)
                return (0.5f, 0.5f, 0.5f, 0.5f);

            if (s_PrefabEdgeCache.TryGetValue(prefab, out var cached))
                return cached;

            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path))
            {
                s_PrefabEdgeCache[prefab] = (0.5f, 0.5f, 0.5f, 0.5f);
                return s_PrefabEdgeCache[prefab];
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var extents = ComputeEdgeExtentsFromRoot(contents);
                s_PrefabEdgeCache[prefab] = extents;
                return extents;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        ///     Walks all renderers in the prefab hierarchy and returns the local-space
        ///     bounding-box distances from the root transform origin to each cardinal edge.
        /// </summary>
        private static (float plusX, float minusX, float plusZ, float minusZ) ComputeEdgeExtentsFromRoot(
            GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
                return (0.5f, 0.5f, 0.5f, 0.5f);

            Bounds? combined = null;
            foreach (var r in renderers)
            {
                // Transform world-space renderer bounds back to root local space.
                var localMin = root.transform.InverseTransformPoint(r.bounds.min);
                var localMax = root.transform.InverseTransformPoint(r.bounds.max);
                var localCenter = (localMin + localMax) * 0.5f;
                var localSize = localMax - localMin;
                var localBounds = new Bounds(localCenter, localSize);

                if (combined == null)
                    combined = localBounds;
                else
                    combined.Value.Encapsulate(localBounds);
            }

            if (combined == null)
                return (0.5f, 0.5f, 0.5f, 0.5f);

            var b = combined.Value;
            return (
                plusX: Mathf.Max(0f, b.max.x),
                minusX: Mathf.Max(0f, -b.min.x),
                plusZ: Mathf.Max(0f, b.max.z),
                minusZ: Mathf.Max(0f, -b.min.z)
            );
        }

        private enum WallSide { Left, Right, Bottom, Top }

        /// <summary>
        ///     Picks a position against a room wall using a realistic placement
        ///     strategy: prefers exterior walls without doors, biases toward corners
        ///     rather than dead-center, and returns different positions on successive
        ///     calls so retries explore the room.
        /// </summary>
        public static bool TryPickAgainstWall(
            in RoomWorldBounds bounds,
            in RoomTouches touches,
            GameObject prefab,
            System.Random random,
            List<Vector3> doorPositions,
            out Vector3 anchorPos,
            out float anchorYaw)
        {
            const float wallSurfaceOffset = 0.05f;

            var (_, _, plusZ, _) = GetPivotToEdgeDistances(prefab);
            var walls = BuildWallCandidates(bounds, touches, plusZ,
                wallSurfaceOffset, doorPositions);

            return TryPickFromWallCandidates(walls, bounds, random,
                out anchorPos, out anchorYaw);
        }

        private static List<WallCandidate> BuildWallCandidates(
            in RoomWorldBounds bounds, in RoomTouches touches,
            float plusZ, float wallSurfaceOffset, List<Vector3> doorPositions)
        {
            var walls = new List<WallCandidate>(4);

            // WallCoord uses the UNMARGINED wall face position so the prop
            // sits flush against the actual wall. AlongMin/AlongMax use the
            // margined interior bounds to keep props away from corners.
            walls.Add(new WallCandidate
            {
                Side = WallSide.Left, Length = bounds.MaxZ - bounds.MinZ,
                IsExterior = touches.Left,
                WallCoordX = bounds.WallMinX + plusZ + wallSurfaceOffset,
                AlongMin = bounds.MinZ, AlongMax = bounds.MaxZ, Yaw = 270f,
                DoorCount = CountDoorsOnWall(WallSide.Left, bounds, doorPositions),
            });
            walls.Add(new WallCandidate
            {
                Side = WallSide.Right, Length = bounds.MaxZ - bounds.MinZ,
                IsExterior = touches.Right,
                WallCoordX = bounds.WallMaxX - plusZ - wallSurfaceOffset,
                AlongMin = bounds.MinZ, AlongMax = bounds.MaxZ, Yaw = 90f,
                DoorCount = CountDoorsOnWall(WallSide.Right, bounds, doorPositions),
            });
            walls.Add(new WallCandidate
            {
                Side = WallSide.Bottom, Length = bounds.MaxX - bounds.MinX,
                IsExterior = touches.Bottom,
                WallCoordZ = bounds.WallMinZ + plusZ + wallSurfaceOffset,
                AlongMin = bounds.MinX, AlongMax = bounds.MaxX, Yaw = 180f,
                DoorCount = CountDoorsOnWall(WallSide.Bottom, bounds, doorPositions),
            });
            walls.Add(new WallCandidate
            {
                Side = WallSide.Top, Length = bounds.MaxX - bounds.MinX,
                IsExterior = touches.Top,
                WallCoordZ = bounds.WallMaxZ - plusZ - wallSurfaceOffset,
                AlongMin = bounds.MinX, AlongMax = bounds.MaxX, Yaw = 0f,
                DoorCount = CountDoorsOnWall(WallSide.Top, bounds, doorPositions),
            });

            // Sort: exterior first, then fewest doors, then longest.
            walls.Sort((a, b) =>
            {
                var extCmp = b.IsExterior.CompareTo(a.IsExterior);
                if (extCmp != 0) return extCmp;
                var doorCmp = a.DoorCount.CompareTo(b.DoorCount);
                if (doorCmp != 0) return doorCmp;
                return b.Length.CompareTo(a.Length);
            });

            return walls;
        }

        private static bool TryPickFromWallCandidates(
            List<WallCandidate> walls, in RoomWorldBounds bounds,
            System.Random random, out Vector3 anchorPos, out float anchorYaw)
        {
            const float cornerMargin = 0.3f;
            const int maxWallCandidates = 3;
            const int positionsPerWall = 5;

            var candidateCount = Mathf.Min(walls.Count, maxWallCandidates);
            ShuffleTopWalls(walls, candidateCount, random);

            for (var wi = 0; wi < candidateCount; wi++)
            {
                var wall = walls[wi];
                for (var attempt = 0; attempt < positionsPerWall; attempt++)
                {
                    var along = PickPositionAlongWall(wall.AlongMin, wall.AlongMax,
                        cornerMargin, random, attempt, positionsPerWall);

                    float ax = wall.Side is WallSide.Left or WallSide.Right
                        ? wall.WallCoordX : along;
                    float az = wall.Side is WallSide.Left or WallSide.Right
                        ? along : wall.WallCoordZ;

                    if (ax < bounds.MinX || ax > bounds.MaxX ||
                        az < bounds.MinZ || az > bounds.MaxZ)
                        continue;

                    anchorPos = new Vector3(ax, 0f, az);
                    anchorYaw = wall.Yaw;
                    return true;
                }
            }

            anchorPos = Vector3.zero;
            anchorYaw = 0f;
            return false;
        }

        /// <summary>
        ///     Counts how many doors lie on (or very close to) the given wall face.
        ///     Uses the unmargined wall positions for accurate door-on-wall detection.
        /// </summary>
        private static int CountDoorsOnWall(
            WallSide side, in RoomWorldBounds bounds, List<Vector3> doorPositions)
        {
            const float threshold = 0.6f;

            if (doorPositions == null || doorPositions.Count == 0)
                return 0;

            var count = 0;
            foreach (var door in doorPositions)
            {
                var onWall = side switch
                {
                    WallSide.Left => Mathf.Abs(door.x - bounds.WallMinX) < threshold,
                    WallSide.Right => Mathf.Abs(door.x - bounds.WallMaxX) < threshold,
                    WallSide.Bottom => Mathf.Abs(door.z - bounds.WallMinZ) < threshold,
                    WallSide.Top => Mathf.Abs(door.z - bounds.WallMaxZ) < threshold,
                    _ => false,
                };
                if (onWall) count++;
            }
            return count;
        }

        /// <summary>
        ///     Picks a position along a wall that tends toward corners rather than
        ///     exact center. Early attempts go to the edges (corners); later attempts
        ///     fill in toward the center as a fallback.
        /// </summary>
        private static float PickPositionAlongWall(
            float alongMin, float alongMax, float cornerMargin,
            System.Random random, int attempt, int totalAttempts)
        {
            var span = alongMax - alongMin;
            if (span <= cornerMargin * 2f)
                return (alongMin + alongMax) * 0.5f;

            var usableMin = alongMin + cornerMargin;
            var usableMax = alongMax - cornerMargin;

            // First half of attempts: bias toward corners (both ends).
            if (attempt < totalAttempts / 2)
            {
                // Pick left corner or right corner with equal probability.
                var useLeftCorner = random.Next(2) == 0;
                // Corner zone: first 35% of usable range.
                var cornerRange = (usableMax - usableMin) * 0.35f;
                if (useLeftCorner)
                    return usableMin + (float)random.NextDouble() * cornerRange;
                else
                    return usableMax - (float)random.NextDouble() * cornerRange;
            }

            // Second half: anywhere in the usable range.
            return usableMin + (float)random.NextDouble() * (usableMax - usableMin);
        }

        /// <summary>
        ///     Fisher-Yates shuffle on the first N wall candidates so repeated calls
        ///     try different walls.
        /// </summary>
        private static void ShuffleTopWalls(List<WallCandidate> walls, int count, System.Random random)
        {
            for (var i = count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (walls[i], walls[j]) = (walls[j], walls[i]);
            }
        }

        private struct WallCandidate
        {
            public WallSide Side;
            public float Length;
            public bool IsExterior;
            public float WallCoordX, WallCoordZ;
            public float AlongMin, AlongMax;
            public float Yaw;
            public int DoorCount;
        }

        public static bool TryPickCentered(
            in RoomWorldBounds bounds,
            System.Random random,
            out Vector3 anchorPos,
            out float anchorYaw)
        {
            var cx = (bounds.MinX + bounds.MaxX) * 0.5f;
            var cz = (bounds.MinZ + bounds.MaxZ) * 0.5f;
            anchorPos = new Vector3(cx, 0f, cz);

            var dirs = new[] { 0f, 90f, 180f, 270f };
            anchorYaw = dirs[random.Next(dirs.Length)];
            return true;
        }

        internal struct PlacedFootprint
        {
            public Vector2 pos;
            public float radius;
        }

        public static bool HasOverlap(
            Vector2 candidate, float candidateRadius, List<PlacedFootprint> placed)
        {
            if (candidateRadius <= 0f || placed.Count == 0)
                return false;

            foreach (var fp in placed)
            {
                var combined = candidateRadius + fp.radius + OverlapSpacing;
                var dx = candidate.x - fp.pos.x;
                var dz = candidate.y - fp.pos.y;
                if (dx * dx + dz * dz < combined * combined)
                    return true;
            }

            return false;
        }

        public static void RecordFootprint(
            Vector2 pos, float radius, List<PlacedFootprint> placed)
        {
            if (radius > 0f)
                placed.Add(new PlacedFootprint { pos = pos, radius = radius });
        }
    }
}
#endif
