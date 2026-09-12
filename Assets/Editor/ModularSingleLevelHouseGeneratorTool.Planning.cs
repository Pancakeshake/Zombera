#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        /// <summary>World Y of the floor slab surface for story index <paramref name="level"/> (0 = ground).</summary>
        private static float LevelFloorSurfaceY(int level)
        {
            return FloorSurfaceY + level * WallHeight;
        }

        private static int ResolveAxisCells(int fixedCellsOrZero, int min, int max, GenerationRandom random)
        {
            min = Mathf.Clamp(min, MinFootprintCells, MaxFootprintCells);
            max = Mathf.Clamp(max, MinFootprintCells, MaxFootprintCells);
            if (max < min)
                max = min;

            if (fixedCellsOrZero > 0)
                return Mathf.Clamp(fixedCellsOrZero, MinFootprintCells, MaxFootprintCells);

            return random.RangeInclusive(min, max);
        }

        private static HashSet<int> PickUniqueIndices(int count, int pick, GenerationRandom random)
        {
            pick = Mathf.Clamp(pick, 0, count);
            var set = new HashSet<int>();
            if (pick == 0 || count <= 0)
                return set;

            var guard = 0;
            while (set.Count < pick && guard++ < 4096)
                set.Add(random.RangeExclusive(0, count));

            return set;
        }

        /// <summary>
        /// Plans stair transitions so every stair chains to the next: each stair's forward
        /// neighbor becomes the next stair's base cell, creating a continuous traversable path.
        /// When the grid is smaller than the transition count, cells are reused with a 2-level
        /// gap (alternating shafts) so stairs can stack vertically.
        /// </summary>
        private static bool TryPlanStairLocations(int width, int depth, int transitionCount, GenerationRandom random,
            out StairTransition[] result)
        {
            result = Array.Empty<StairTransition>();
            if (transitionCount <= 0)
                return true;

            var totalCells = width * depth;
            var allowReuse = transitionCount > totalCells;

            var lastUsedAtLevel = new Dictionary<Vector2Int, int>();
            var holesAtLevel = new List<HashSet<Vector2Int>>(transitionCount + 1);
            for (var i = 0; i <= transitionCount; i++)
                holesAtLevel.Add(new HashSet<Vector2Int>());

            result = new StairTransition[transitionCount];

            // Track the previous stair's forward neighbor so the next stair can chain from it
            var chainTarget = new Vector2Int(-1, -1);
            var hasChainTarget = false;

            for (var t = 0; t < transitionCount; t++)
            {
                var cellCtx = new StairCellContext(width, depth, lastUsedAtLevel, holesAtLevel[t], t, allowReuse);
                if (!TryResolveStairCell(cellCtx, random,
                        ref chainTarget, ref hasChainTarget, out var ix, out var iz))
                    return false;

                lastUsedAtLevel[new Vector2Int(ix, iz)] = t;

                var isLast = t == transitionCount - 1;
                var rot = PickStairRotation(ix, iz, width, depth, isLast, random);

                result[t] = new StairTransition(t, ix, iz, rot);
                holesAtLevel[t + 1].Add(new Vector2Int(ix, iz));

                UpdateChainTarget(ix, iz, rot, width, depth, ref chainTarget, ref hasChainTarget);
            }

            return true;
        }

        private static bool TryResolveStairCell(StairCellContext ctx, GenerationRandom random,
            ref Vector2Int chainTarget, ref bool hasChainTarget, out int ix, out int iz)
        {
            ix = 0;
            iz = 0;

            if (hasChainTarget
                && chainTarget.x >= 0 && chainTarget.x < ctx.Width
                && chainTarget.y >= 0 && chainTarget.y < ctx.Depth)
            {
                var chainCell = new Vector2Int(chainTarget.x, chainTarget.y);
                var canChain = ctx.AllowReuse
                    ? !ctx.LastUsedAtLevel.TryGetValue(chainCell, out var lastLvl) || ctx.CurrentLevel - lastLvl >= 2
                    : !ctx.LastUsedAtLevel.ContainsKey(chainCell);

                if (canChain)
                {
                    ix = chainTarget.x;
                    iz = chainTarget.y;
                    return true;
                }
            }

            return TryPickStairCell(ctx, random, out ix, out iz);
        }

        private static void UpdateChainTarget(int ix, int iz, float rot, int width, int depth,
            ref Vector2Int chainTarget, ref bool hasChainTarget)
        {
            if (TryGetForwardNeighborCell(ix, iz, rot, width, depth, out var fx, out var fz))
            {
                chainTarget = new Vector2Int(fx, fz);
                hasChainTarget = true;
            }
            else
            {
                hasChainTarget = false;
            }
        }

        private readonly struct StairCellContext
        {
            public readonly int Width;
            public readonly int Depth;
            public readonly Dictionary<Vector2Int, int> LastUsedAtLevel;
            public readonly HashSet<Vector2Int> HoleCellsOnThisLevel;
            public readonly int CurrentLevel;
            public readonly bool AllowReuse;

            public StairCellContext(int width, int depth,
                Dictionary<Vector2Int, int> lastUsedAtLevel,
                HashSet<Vector2Int> holeCellsOnThisLevel, int currentLevel, bool allowReuse)
            {
                Width = width;
                Depth = depth;
                LastUsedAtLevel = lastUsedAtLevel;
                HoleCellsOnThisLevel = holeCellsOnThisLevel;
                CurrentLevel = currentLevel;
                AllowReuse = allowReuse;
            }
        }

        private static bool IsCellAvailable(int ix, int iz, StairCellContext ctx)
        {
            if (ix < 0 || ix >= ctx.Width || iz < 0 || iz >= ctx.Depth)
                return false;

            var cell = new Vector2Int(ix, iz);
            if (ctx.HoleCellsOnThisLevel.Contains(cell))
                return false;

            if (ctx.AllowReuse)
            {
                if (ctx.LastUsedAtLevel.TryGetValue(cell, out var lastLevel)
                    && ctx.CurrentLevel - lastLevel < 2)
                    return false;
            }
            else if (ctx.LastUsedAtLevel.ContainsKey(cell))
            {
                return false;
            }

            return true;
        }
        private static bool TryPickStairCell(StairCellContext ctx,
            GenerationRandom random, out int ix, out int iz)
        {
            foreach (var preferInterior in new[] { true, false })
            {
                var interiorOnly = preferInterior && ctx.Width >= 3 && ctx.Depth >= 3;
                var candidates = EnumerateGridCells(ctx.Width, ctx.Depth, interiorOnly);
                var pool = new List<Vector2Int>();
                foreach (var candidate in candidates)
                {
                    if (IsCellAvailable(candidate.x, candidate.y, ctx))
                        pool.Add(candidate);
                }

                if (pool.Count <= 0)
                    continue;

                var pick = pool[random.RangeExclusive(0, pool.Count)];
                ix = pick.x;
                iz = pick.y;
                return true;
            }

            ix = 0;
            iz = 0;
            return false;
        }

        /// <summary>
        /// Picks a rotation (0/90/180/270) for a stair. For the last stair (<paramref name="requireClearance"/>),
        /// the forward neighbor must have clearance from the grid edge so the player walks onto open floor.
        /// For intermediate stairs the forward neighbor becomes the next stair's base, so any valid
        /// direction works.
        /// </summary>
        private static float PickStairRotation(int ix, int iz, int width, int depth, bool requireClearance,
            GenerationRandom random)
        {
            var preferred = new List<int>();
            var fallback = new List<int>();

            for (var r = 0; r < 4; r++)
            {
                var rot = r * 90f;
                if (!TryGetForwardNeighborCell(ix, iz, rot, width, depth, out var fx, out var fz))
                    continue;

                if (HasForwardClearance(fx, fz, rot, width, depth))
                    preferred.Add(r);
                else
                    fallback.Add(r);
            }

            int chosen;
            if (preferred.Count > 0)
                chosen = preferred[random.RangeExclusive(0, preferred.Count)];
            else if (!requireClearance && fallback.Count > 0)
                chosen = fallback[random.RangeExclusive(0, fallback.Count)];
            else if (fallback.Count > 0)
                // Last stair with no clearance available — pick the least-bad direction
                chosen = fallback[random.RangeExclusive(0, fallback.Count)];
            else
                chosen = random.RangeExclusive(0, 4);

            return chosen * 90f;
        }

        /// <summary>
        /// Returns true if there is at least one more cell in the forward direction beyond (fx,fz)
        /// before hitting the grid boundary.
        /// </summary>
        private static bool HasForwardClearance(int fx, int fz, float rotationY, int width, int depth)
        {
            var forward = Quaternion.Euler(0f, rotationY, 0f) * Vector3.forward;
            if (Mathf.Abs(forward.x) >= Mathf.Abs(forward.z))
            {
                var step = forward.x >= 0f ? 1 : -1;
                var nextX = fx + step;
                return nextX >= 0 && nextX < width;
            }
            else
            {
                var step = forward.z >= 0f ? 1 : -1;
                var nextZ = fz + step;
                return nextZ >= 0 && nextZ < depth;
            }
        }

        /// <summary>Neighbor cell in the direction the stair faces (Unity forward after Y rotation).</summary>
        private static bool TryGetForwardNeighborCell(int ix, int iz, float rotationY, int width, int depth,
            out int fx, out int fz)
        {
            var forward = Quaternion.Euler(0f, rotationY, 0f) * Vector3.forward;
            fx = ix;
            fz = iz;
            if (Mathf.Abs(forward.x) >= Mathf.Abs(forward.z))
            {
                var step = forward.x >= 0f ? 1 : -1;
                fx = ix + step;
            }
            else
            {
                var step = forward.z >= 0f ? 1 : -1;
                fz = iz + step;
            }

            if (fx < 0 || fx >= width || fz < 0 || fz >= depth)
                return false;

            return true;
        }

        private static List<Vector2Int> EnumerateGridCells(int width, int depth, bool interiorOnly)
        {
            if (interiorOnly && (width < 3 || depth < 3))
                return EnumerateGridCells(width, depth, interiorOnly: false);

            var list = new List<Vector2Int>();
            var xMin = interiorOnly ? 1 : 0;
            var xMax = interiorOnly ? width - 2 : width - 1;
            var zMin = interiorOnly ? 1 : 0;
            var zMax = interiorOnly ? depth - 2 : depth - 1;

            for (var x = xMin; x <= xMax; x++)
            {
                for (var z = zMin; z <= zMax; z++)
                    list.Add(new Vector2Int(x, z));
            }

            return list;
        }

        /// <summary>
        /// Creates floor holes only at the stair landing cell (where the player arrives).
        /// The forward neighbor is left as solid floor since the next stair sits there.
        /// When skyscraper mode is active, validates skip cells against per-floor dimensions.
        /// </summary>
        private static HashSet<FloorCell> BuildStairUpperLandingSkipSet(StairTransition[] transitions,
            GenerationPlan plan)
        {
            var set = new HashSet<FloorCell>();
            foreach (var transition in transitions)
            {
                var upper = transition.LowerLevel + 1;
                var upperW = plan.SkyscraperMode ? plan.FloorWidths[upper] : plan.Width;
                var upperD = plan.SkyscraperMode ? plan.FloorDepths[upper] : plan.Depth;

                // Only the landing cell gets a hole — forward neighbor is solid floor for the next stair
                if (transition.Ix >= 0 && transition.Ix < upperW && transition.Iz >= 0 && transition.Iz < upperD)
                    set.Add(new FloorCell(upper, transition.Ix, transition.Iz));
            }

            return set;
        }

        private static List<WallSegment> BuildPerimeterSegments(int width, int depth, float wallY, Vector3 originOffset = default)
        {
            var list = new List<WallSegment>();
            // wallInset moved to class-level WallInset constant

            for (var ix = 0; ix < width; ix++)
            {
                var cx = ix * CellSize + originOffset.x;
                var cz = originOffset.z;
                list.Add(new WallSegment(
                    new Vector3(cx, wallY, cz - CellSize * 0.5f + WallInset),
                    Quaternion.identity));
            }

            for (var ix = 0; ix < width; ix++)
            {
                var cx = ix * CellSize + originOffset.x;
                var cz = (depth - 1) * CellSize + originOffset.z;
                list.Add(new WallSegment(
                    new Vector3(cx, wallY, cz + CellSize * 0.5f - WallInset),
                    Quaternion.Euler(0f, 180f, 0f)));
            }

            for (var iz = 0; iz < depth; iz++)
            {
                var cx = originOffset.x;
                var cz = iz * CellSize + originOffset.z;
                list.Add(new WallSegment(
                    new Vector3(cx - CellSize * 0.5f + WallInset, wallY, cz),
                    Quaternion.Euler(0f, 90f, 0f)));
            }

            for (var iz = 0; iz < depth; iz++)
            {
                var cx = (width - 1) * CellSize + originOffset.x;
                var cz = iz * CellSize + originOffset.z;
                list.Add(new WallSegment(
                    new Vector3(cx + CellSize * 0.5f - WallInset, wallY, cz),
                    Quaternion.Euler(0f, -90f, 0f)));
            }

            return list;
        }

        private static void PlanPerimeterWalls(GenerationContext context)
        {
            context.Plan.PerimeterWallPlans.Clear();
            var plan = context.Plan;

            for (var level = 0; level < plan.FloorCount; level++)
            {
                var levelWidth = plan.SkyscraperMode ? plan.FloorWidths[level] : plan.Width;
                var levelDepth = plan.SkyscraperMode ? plan.FloorDepths[level] : plan.Depth;
                var levelOffset = plan.SkyscraperMode ? plan.FloorOffsets[level] : Vector3.zero;
                var wallY = LevelFloorSurfaceY(level);
                var perimeter = BuildPerimeterSegments(levelWidth, levelDepth, wallY, levelOffset);
                var levelPlan = new LevelWallPlan { Level = level, WallY = wallY };

                for (var i = 0; i < perimeter.Count; i++)
                {
                    var isDoor = level == 0 && plan.GroundDoorIndices.Contains(i);
                    // Snapping commercial types keep windows off the side walls.
                    var sideWindowAllowed = !IsCommercialSnapType(context) || i < 2 * levelWidth;
                    var isWindow = !isDoor
                                   && sideWindowAllowed
                                   && context.Prefabs.Window != null
                                   && context.Settings.WindowChance > 0f
                                   && context.Random.Value01() < context.Settings.WindowChance;
                    levelPlan.Segments.Add(new PlannedPerimeterSegment(perimeter[i], i, isDoor, isWindow));
                }

                if (level == 0 && plan.PrimaryDoorSegmentIndex < 0)
                {
                    for (var s = 0; s < levelPlan.Segments.Count; s++)
                    {
                        if (!levelPlan.Segments[s].IsDoor)
                            continue;
                        plan.PrimaryDoorSegmentIndex = levelPlan.Segments[s].SegmentIndex;
                        break;
                    }
                }

                context.Plan.PerimeterWallPlans.Add(levelPlan);
            }
        }



        private static void PlanFloorRooms(GenerationContext context)
        {
            context.Plan.FloorRoomPlans.Clear();

            // Commercial interiors are deterministic: front sales floor + one back room.
            if (context.BuildingCategory == CityDistrictType.Commercial)
            {
                PlanCommercialRooms(context);
                return;
            }

            if (context.ActiveTemplate != null)
            {
                PlanTemplateRooms(context);
                if (TryHandleTemplateFallback(context))
                    return;
            }

            PlanLegacyRooms(context);
        }

        /// <summary>Returns true when template rooms were produced and legacy should be skipped.</summary>
        private static bool TryHandleTemplateFallback(GenerationContext context)
        {
            if (context.Plan.FloorRoomPlans.Count > 0)
            {
                Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Template '{context.ActiveTemplate.DisplayName}' generated {context.Plan.FloorRoomPlans.Count} floor(s) of rooms.");
                return true;
            }

            Debug.LogWarning(
                $"[ModularSingleLevelHouseGeneratorTool] Template '{context.ActiveTemplate.DisplayName}' produced no rooms. " +
                (context.Settings.MaxRoomsPerFloor > 0
                    ? "Falling back to legacy MaxRoomsPerFloor slider."
                    : "No legacy fallback configured — building will have no interior rooms or walls."));
            return false;
        }

        /// <summary>
        ///     Commercial interiors: a SalesFloor strip at the front and, when the back
        ///     strip is large enough, one back-of-house room. Back room type comes from
        ///     the active template's required/optional pool, else a default weighted pool.
        /// </summary>
        private static void PlanCommercialRooms(GenerationContext context)
        {
            var plan = context.Plan;
            if (plan.FloorCount <= 0)
                return;

            var width = plan.Width;
            var depth = plan.Depth;
            if (width <= 1 || depth <= 1)
                return;

            var frontDepth = Mathf.Max(1, Mathf.RoundToInt(depth * 0.6f));
            var backDepth = depth - frontDepth;

            var backType = ResolveCommercialBackRoomType(context);

            // Kitchen shops seat customers in front of the kitchen; other shops
            // use a sales floor in front of their back-of-house room.
            var kitchenFront = backType == RoomType.CommercialKitchen;
            var frontType = kitchenFront ? RoomType.Dining : RoomType.SalesFloor;
            var frontName = kitchenFront ? "Dining Area" : "Sales Floor";

            var rooms = new List<RoomRegion>
            {
                new RoomRegion(0, 0, width, frontDepth, frontName, frontType)
            };

            if (backDepth >= 2 && width >= 2)
            {
                var backName = backType == RoomType.CommercialKitchen ? "Kitchen" : "Back Room";
                rooms.Add(new RoomRegion(0, frontDepth, width, depth, backName, backType));
            }

            var roomAt = BuildRoomAtGrid(rooms, width, depth);
            var interiorWalls = PlanInteriorWalls(
                rooms, roomAt, width, depth,
                CollectDoorwayBlockedCellsForLevel(
                    plan.StairTransitions, plan.SkipUpperLandingFloors, 0, width, depth),
                context.Prefabs.Doorway != null,
                context.Random);

            plan.FloorRoomPlans.Add(new FloorRoomPlan
            {
                Level = 0,
                FloorY = LevelFloorSurfaceY(0),
                FloorPlanWidth = width,
                FloorPlanDepth = depth,
                Rooms = rooms,
                RoomAt = roomAt,
                InteriorWalls = interiorWalls
            });

            Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Commercial rooms: {rooms.Count} room(s) on {width}x{depth} (frontDepth={frontDepth}).");
        }

        private static RoomType ResolveCommercialBackRoomType(GenerationContext context)
        {
            var pool = new List<RoomType>();
            var template = context.ActiveTemplate;

            if (template != null)
            {
                if (template.RequiredRooms != null)
                    for (var i = 0; i < template.RequiredRooms.Length; i++)
                        if (template.RequiredRooms[i].RoomType != RoomType.SalesFloor)
                            pool.Add(template.RequiredRooms[i].RoomType);

                if (pool.Count == 0 && template.OptionalRooms != null)
                    for (var i = 0; i < template.OptionalRooms.Length; i++)
                        if (template.OptionalRooms[i].RoomType != RoomType.SalesFloor)
                            pool.Add(template.OptionalRooms[i].RoomType);
            }

            // Stockroom is the most common back room for shops.
            if (pool.Count > 0 && pool.Contains(RoomType.Stockroom))
                pool.Add(RoomType.Stockroom);

            if (pool.Count == 0)
            {
                pool.Add(RoomType.Stockroom);
                pool.Add(RoomType.Stockroom);
                pool.Add(RoomType.CommercialKitchen);
                pool.Add(RoomType.Office);
                pool.Add(RoomType.StaffRoom);
            }

            return pool[context.Random.RangeExclusive(0, pool.Count)];
        }

        private static int[,] BuildRoomAtGrid(List<RoomRegion> rooms, int width, int depth)
        {
            var roomAt = new int[width, depth];
            for (var x = 0; x < width; x++)
                for (var z = 0; z < depth; z++)
                    roomAt[x, z] = -1;

            for (var ri = 0; ri < rooms.Count; ri++)
            {
                var room = rooms[ri];
                for (var x = room.Ix0; x < room.Ix1 && x < width; x++)
                    for (var z = room.Iz0; z < room.Iz1 && z < depth; z++)
                        roomAt[x, z] = ri;
            }

            return roomAt;
        }

        private static bool IsCommercialSnapType(GenerationContext context)
        {
            if (context.BuildingCategory != CityDistrictType.Commercial)
                return false;
            return context.ActiveTemplate == null || !context.ActiveTemplate.AllowSideWindows;
        }

        private static void PlanLegacyRooms(GenerationContext context)
        {
            if (context.Settings.MaxRoomsPerFloor <= 0)
                return;

            var plan = context.Plan;

            for (var level = 0; level < plan.FloorCount; level++)
            {
                var levelWidth = plan.SkyscraperMode ? plan.FloorWidths[level] : plan.Width;
                var levelDepth = plan.SkyscraperMode ? plan.FloorDepths[level] : plan.Depth;

                if (levelWidth * levelDepth <= 9)
                    continue;

                var targetRoomCount = PickRoomCountForFloor(
                    context.Settings.MinRoomsPerFloor,
                    context.Settings.MaxRoomsPerFloor,
                    context.Random);
                if (targetRoomCount <= 0)
                    continue;

                var blockedDoorwayCells = CollectDoorwayBlockedCellsForLevel(
                    context.Plan.StairTransitions,
                    context.Plan.SkipUpperLandingFloors,
                    level,
                    levelWidth,
                    levelDepth);

                var rooms = PartitionIntoRooms(levelWidth, levelDepth, targetRoomCount, context.Random);
                if (rooms.Count == 0)
                    continue;

                InjectStairwellRooms(context, rooms, null, level, levelWidth, levelDepth);

                var interiorWalls = PlanInteriorWalls(
                    rooms, null,
                    levelWidth,
                    levelDepth,
                    blockedDoorwayCells,
                    context.Prefabs.Doorway != null,
                    context.Random);

                RemoveStairwellEntranceWalls(context, interiorWalls, rooms, levelWidth, levelDepth);

                context.Plan.FloorRoomPlans.Add(new FloorRoomPlan
                {
                    Level = level,
                    FloorY = LevelFloorSurfaceY(level),
                    FloorPlanWidth = levelWidth,
                    FloorPlanDepth = levelDepth,
                    Rooms = rooms,
                    InteriorWalls = interiorWalls
                });
            }
        }

        /// <summary>
        ///     Template-driven room planning. Reads RequiredRooms + OptionalRooms from the active
        ///     template, applies sizing constraints, adjacency rules, and open-plan merging.
        /// </summary>
        private static void PlanTemplateRooms(GenerationContext context)
        {
            var template = context.ActiveTemplate;
            if (template == null)
                return;

            // Build a flat list of concrete room slots from the template.
            var roomSlots = CollectTemplateRoomSlots(template, context.Random, context.Settings.RoomSettings);
            if (roomSlots.Count == 0)
                return;

            var plan = context.Plan;

            for (var level = 0; level < plan.FloorCount; level++)
            {
                var levelWidth = plan.SkyscraperMode ? plan.FloorWidths[level] : plan.Width;
                var levelDepth = plan.SkyscraperMode ? plan.FloorDepths[level] : plan.Depth;

                var blockedDoorwayCells = CollectDoorwayBlockedCellsForLevel(
                    plan.StairTransitions,
                    plan.SkipUpperLandingFloors,
                    level,
                    levelWidth,
                    levelDepth);

                var totalCells = levelWidth * levelDepth;
                if (totalCells <= 0)
                    continue;

                // Place rooms using percentage-based allocation.
                var (rooms, roomAt) = PlaceTemplateRooms(
                    roomSlots, levelWidth, levelDepth,
                    blockedDoorwayCells,
                    context.Settings.RoomSettings,
                    template.PreferOpenPlan, context.Random);

                if (rooms.Count == 0)
                    continue;

                InjectStairwellRooms(context, rooms, roomAt, level, levelWidth, levelDepth);

                var interiorWalls = PlanInteriorWalls(
                    rooms, roomAt,
                    levelWidth,
                    levelDepth,
                    blockedDoorwayCells,
                    context.Prefabs.Doorway != null,
                    context.Random);

                RemoveStairwellEntranceWalls(context, interiorWalls, rooms, levelWidth, levelDepth);

                context.Plan.FloorRoomPlans.Add(new FloorRoomPlan
                {
                    Level = level,
                    FloorY = LevelFloorSurfaceY(level),
                    FloorPlanWidth = levelWidth,
                    FloorPlanDepth = levelDepth,
                    Rooms = rooms,
                    RoomAt = roomAt,
                    InteriorWalls = interiorWalls
                });
            }
        }

        /// <summary>
        ///     Builds a concrete list of room slots from the template's RequiredRooms and
        ///     OptionalRooms. Each slot carries sizing constraints and a target floor percentage.
        ///     Optional rooms may be randomly excluded; required rooms are always included.
        /// </summary>
        private static void AddSlotEntries(RoomTemplateEntry entry,
            List<RoomSlot> slots, GenerationRandom random, bool allowSkip, RoomSettings roomSettings)
        {
            var count = entry.ResolveCount(new System.Random(random.RangeExclusive(1, int.MaxValue)));
            if (allowSkip && count <= 0)
                return;
            var cfg = roomSettings != null ? roomSettings.GetConfig(entry.RoomType) : RoomTypeConfig.DefaultFor(entry.RoomType);
            var pct = ResolvePctFromConfig(cfg, random);
            for (var i = 0; i < count; i++)
                slots.Add(new RoomSlot(entry.RoomType, pct));
        }

        private static int ResolvePctFromConfig(RoomTypeConfig cfg, GenerationRandom random)
        {
            var min = Mathf.Max(0, cfg.FloorPercentageMin);
            var max = Mathf.Max(min, cfg.FloorPercentageMax);
            if (min >= max) return min;
            return min + new System.Random(random.RangeExclusive(1, int.MaxValue)).Next(0, max - min + 1);
        }

        private static List<RoomSlot> CollectTemplateRoomSlots(
            ResidentialHouseTemplate template,
            GenerationRandom random,
            RoomSettings roomSettings)
        {
            var slots = new List<RoomSlot>();

            foreach (var entry in template.RequiredRooms)
                AddSlotEntries(entry, slots, random, allowSkip: false, roomSettings);

            foreach (var entry in template.OptionalRooms)
                AddSlotEntries(entry, slots, random, allowSkip: true, roomSettings);

            Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Template '{template.DisplayName}': collected {slots.Count} room slot(s) " +
                      $"(required={template.RequiredRooms.Length}, optional={template.OptionalRooms.Length}).");
            return slots;
        }

        /// <summary>
        ///     Places template room slots into a grid using percentage-based allocation.
        ///     Each room's FloorPercentage determines its share of the total available area.
        ///     Rooms are placed smallest-first, then expanded to fill the grid completely.
        /// </summary>
        private static (List<RoomRegion> regions, int[,] roomAt) PlaceTemplateRooms(
            List<RoomSlot> slots,
            int gridWidth,
            int gridDepth,
            HashSet<Vector2Int> blockedCells,
            RoomSettings roomSettings,
            bool preferOpenPlan,
            GenerationRandom random)
        {
            var regions = new List<RoomRegion>();
            if (slots.Count == 0 || gridWidth <= 0 || gridDepth <= 0)
                return (regions, null);

            var occupied = new bool[gridWidth, gridDepth];
            var blockedInBounds = 0;
            foreach (var blocked in blockedCells)
                if (blocked.x >= 0 && blocked.x < gridWidth && blocked.y >= 0 && blocked.y < gridDepth)
                {
                    occupied[blocked.x, blocked.y] = true;
                    blockedInBounds++;
                }

            var availableCells = gridWidth * gridDepth - blockedInBounds;

            var ordered = new List<RoomSlot>(slots);
            ordered = MergeOpenPlanSlots(ordered, roomSettings, preferOpenPlan);

            var roomTargets = ComputeRoomTargets(ordered, availableCells);

            var placedIndices = PlaceRoomSlots(regions, ordered, roomTargets, occupied, gridWidth, gridDepth);

            var ctx = new RoomFloodFillContext(regions, placedIndices, roomTargets, occupied, (gridWidth, gridDepth), random);
            FloodFillRemaining(ctx);
            FillOrphanedCells(ctx);

            // Rebuild bounding boxes from cell ownership so room shapes match actual cell assignments.
            RebuildRegionsFromCells(ctx);

            // Square-only: clamp each region to its smaller dimension after flood-fill expansion.
            MakeRegionsSquare(ctx.Regions, gridWidth, gridDepth);

            ClampRegionsToGrid(regions, gridWidth, gridDepth);

            Debug.Log($"[ModularSingleLevelHouseGeneratorTool] PlaceTemplateRooms: grid={gridWidth}x{gridDepth}, slots={slots.Count}, " +
                      $"availableCells={availableCells}, placed={placedIndices.Count}/{ordered.Count}, regions={regions.Count}.");
            return (regions, ctx.RoomAt);
        }

        private static int[] ComputeRoomTargets(List<RoomSlot> ordered, int availableCells)
        {
            var totalPct = 0;
            foreach (var s in ordered) totalPct += s.TargetPct;

            var roomTargets = new int[ordered.Count];
            var sum = 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                var pct = totalPct > 0 ? (float)ordered[i].TargetPct / totalPct : 1f / ordered.Count;
                var target = Mathf.Max(1, Mathf.RoundToInt(pct * availableCells));
                roomTargets[i] = target;
                sum += target;
            }

            // Scale targets so they sum to exactly availableCells.
            if (sum > 0 && sum != availableCells)
            {
                var scale = (float)availableCells / sum;
                sum = 0;
                for (var i = 0; i < roomTargets.Length; i++)
                {
                    roomTargets[i] = Mathf.Max(1, Mathf.RoundToInt(roomTargets[i] * scale));
                    sum += roomTargets[i];
                }
            }

            return roomTargets;
        }

        private static List<int> PlaceRoomSlots(
            List<RoomRegion> regions,
            List<RoomSlot> ordered,
            int[] roomTargets,
            bool[,] occupied,
            int gridWidth,
            int gridDepth)
        {
            var placeOrder = new List<int>();
            for (var i = 0; i < ordered.Count; i++)
                placeOrder.Add(i);
            placeOrder.Sort((a, b) => roomTargets[a].CompareTo(roomTargets[b]));

            var placedIndices = new List<int>();
            foreach (var idx in placeOrder)
            {
                var targetArea = roomTargets[idx];
                var dim = BestRectForArea(targetArea, gridWidth, gridDepth);
                var placed = TryPlaceRect(occupied, gridWidth, gridDepth, dim.w, dim.d, out var px, out var pz)
                          || TryPlaceRect(occupied, gridWidth, gridDepth, 1, 1, out px, out pz);

                if (placed)
                {
                    var rw = Mathf.Min(dim.w, gridWidth - px);
                    var rd = Mathf.Min(dim.d, gridDepth - pz);
                    regions.Add(new RoomRegion(px, pz, px + rw, pz + rd, ordered[idx].Type.ToString(), ordered[idx].Type));
                    placedIndices.Add(idx);
                }
            }

            return placedIndices;
        }

        private static void ClampRegionsToGrid(List<RoomRegion> regions, int gridWidth, int gridDepth)
        {
            for (var i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                var clampedIx0 = Mathf.Clamp(r.Ix0, 0, gridWidth);
                var clampedIz0 = Mathf.Clamp(r.Iz0, 0, gridDepth);
                var clampedIx1 = Mathf.Clamp(r.Ix1, clampedIx0, gridWidth);
                var clampedIz1 = Mathf.Clamp(r.Iz1, clampedIz0, gridDepth);
                if (clampedIx0 != r.Ix0 || clampedIz0 != r.Iz0 || clampedIx1 != r.Ix1 || clampedIz1 != r.Iz1)
                    regions[i] = new RoomRegion(clampedIx0, clampedIz0, clampedIx1, clampedIz1, r.Name, r.RoomType);
            }
        }

        /// <summary>
        ///     Picks width×depth dimensions for a target cell count that fit within the grid.
        ///     Square-only: always returns equal sides.
        /// </summary>
        private static (int w, int d) BestRectForArea(int targetArea, int gridW, int gridD)
        {
            var side = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(targetArea)), 1, Mathf.Min(gridW, gridD));
            return (side, side);
        }

        /// <summary>
        ///     Flood-fills remaining free cells by assigning each to the adjacent room
        ///     that still has remaining capacity versus its percentage-derived target area.
        ///     Also respects per-room max dimension constraints.
        /// </summary>
        private sealed class RoomFloodFillContext
        {
            internal readonly List<RoomRegion> Regions;
            internal readonly int[] RoomTargets;
            internal readonly bool[,] Occupied;
            internal readonly GenerationRandom Random;
            internal readonly Dictionary<int, int> RoomOfIdx;
            /// <summary>Per-cell room ownership: -1 = unowned, otherwise the region index.</summary>
            internal readonly int[,] RoomAt;

            internal int GridW => _gridSize.w;
            internal int GridD => _gridSize.d;
            private readonly (int w, int d) _gridSize;

            internal RoomFloodFillContext(
                List<RoomRegion> regions, List<int> placedIndices,
                int[] roomTargets, bool[,] occupied, (int w, int d) gridSize, GenerationRandom random)
            {
                Regions = regions;
                RoomTargets = roomTargets;
                Occupied = occupied;
                _gridSize = gridSize;
                Random = random;
                RoomOfIdx = new Dictionary<int, int>();
                RoomAt = new int[gridSize.w, gridSize.d];
                for (var ix = 0; ix < gridSize.w; ix++)
                    for (var iz = 0; iz < gridSize.d; iz++)
                        RoomAt[ix, iz] = -1;
                for (var i = 0; i < placedIndices.Count; i++)
                    RoomOfIdx[placedIndices[i]] = i;
                // Fill RoomAt from initial region placements
                for (var ri = 0; ri < regions.Count; ri++)
                {
                    var r = regions[ri];
                    for (var ix = r.Ix0; ix < r.Ix1; ix++)
                        for (var iz = r.Iz0; iz < r.Iz1; iz++)
                            if (ix < gridSize.w && iz < gridSize.d)
                                RoomAt[ix, iz] = ri;
                }
            }
        }

        private static void FloodFillRemaining(RoomFloodFillContext ctx)
        {
            var changed = true;
            var maxIterations = ctx.GridW * ctx.GridD * 2;
            var iterations = 0;
            while (changed && iterations++ < maxIterations)
            {
                changed = false;
                for (var ix = 0; ix < ctx.GridW; ix++)
                {
                    for (var iz = 0; iz < ctx.GridD; iz++)
                    {
                        if (TryFloodFillCell(ctx, ix, iz))
                            changed = true;
                    }
                }
            }
        }

        private static bool TryFloodFillCell(RoomFloodFillContext ctx, int ix, int iz)
        {
            if (ctx.Occupied[ix, iz]) return false;

            var neighbors = FindNeighboringRooms(ctx, ix, iz);
            if (neighbors.Count == 0) return false;

            var bestRi = PickBestNeighbor(ctx, neighbors);
            if (bestRi < 0) return false;

            return TryExpandRoom(ctx, ix, iz, bestRi);
        }

        private static List<int> FindNeighboringRooms(RoomFloodFillContext ctx, int ix, int iz)
        {
            var neighbors = new List<int>();
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nx = ix + dx;
                var nz = iz + dz;
                if (nx < 0 || nx >= ctx.GridW || nz < 0 || nz >= ctx.GridD) continue;
                if (!ctx.Occupied[nx, nz]) continue;
                for (var ri = 0; ri < ctx.Regions.Count; ri++)
                {
                    var r = ctx.Regions[ri];
                    if (nx >= r.Ix0 && nx < r.Ix1 && nz >= r.Iz0 && nz < r.Iz1)
                    {
                        if (!neighbors.Contains(ri))
                            neighbors.Add(ri);
                        break;
                    }
                }
            }
            return neighbors;
        }

        private static int PickBestNeighbor(RoomFloodFillContext ctx, List<int> neighbors)
        {
            var bestRi = -1;
            var bestCap = -1;
            Shuffle(neighbors, ctx.Random);
            foreach (var ri in neighbors)
            {
                var slotIdx = ResolveSlotIndex(ctx, ri);
                if (slotIdx < 0) continue;

                var r = ctx.Regions[ri];
                var curArea = (r.Ix1 - r.Ix0) * (r.Iz1 - r.Iz0);
                var cap = ctx.RoomTargets[slotIdx] - curArea;
                if (cap > bestCap)
                {
                    bestCap = cap;
                    bestRi = ri;
                }
            }
            return bestCap > 0 ? bestRi : -1;
        }

        private static int ResolveSlotIndex(RoomFloodFillContext ctx, int regionIndex)
        {
            foreach (var kv in ctx.RoomOfIdx)
                if (kv.Value == regionIndex)
                    return kv.Key;
            return -1;
        }

        private static bool TryExpandRoom(RoomFloodFillContext ctx, int ix, int iz, int bestRi)
        {
            var bestR = ctx.Regions[bestRi];
            var newIx0 = Mathf.Max(0, Mathf.Min(bestR.Ix0, ix));
            var newIz0 = Mathf.Max(0, Mathf.Min(bestR.Iz0, iz));
            var newIx1 = Mathf.Min(ctx.GridW, Mathf.Max(bestR.Ix1, ix + 1));
            var newIz1 = Mathf.Min(ctx.GridD, Mathf.Max(bestR.Iz1, iz + 1));

            ctx.Regions[bestRi] = new RoomRegion(newIx0, newIz0, newIx1, newIz1, bestR.Name, bestR.RoomType);
            ctx.Occupied[ix, iz] = true;
            ctx.RoomAt[ix, iz] = bestRi;
            return true;
        }

        /// <summary>
        ///     Iteratively flood-fills remaining free cells using adjacency only (no long-distance jumps).
        ///     Ignores capacity limits — every cell gets claimed. Keeps rooms contiguous.
        /// </summary>
        private static void FillOrphanedCells(RoomFloodFillContext ctx)
        {
            var changed = true;
            var maxIterations = ctx.GridW * ctx.GridD * 2;
            var iterations = 0;
            while (changed && iterations++ < maxIterations)
                changed = FillOrphanPass(ctx);

            ForceClaimRemainingCells(ctx);
        }

        private static bool FillOrphanPass(RoomFloodFillContext ctx)
        {
            var changed = false;
            for (var ix = 0; ix < ctx.GridW; ix++)
                for (var iz = 0; iz < ctx.GridD; iz++)
                    if (TryClaimOrphanedCell(ctx, ix, iz))
                        changed = true;
            return changed;
        }

        private static bool TryClaimOrphanedCell(RoomFloodFillContext ctx, int ix, int iz)
        {
            if (ctx.Occupied[ix, iz]) return false;
            var ri = FindAdjacentRoom(ix, iz, ctx);
            if (ri < 0) return false;
            ctx.Occupied[ix, iz] = true;
            ctx.RoomAt[ix, iz] = ri;
            return true;
        }

        private static void ForceClaimRemainingCells(RoomFloodFillContext ctx)
        {
            if (ctx.Regions.Count == 0) return;
            for (var ix = 0; ix < ctx.GridW; ix++)
                for (var iz = 0; iz < ctx.GridD; iz++)
                {
                    if (ctx.Occupied[ix, iz]) continue;
                    ctx.Occupied[ix, iz] = true;
                    ctx.RoomAt[ix, iz] = 0;
                }
        }

        /// <summary>
        ///     Finds the room adjacent to (ix,iz) that has the most remaining capacity.
        ///     Only considers the four cardinal neighbours.
        /// </summary>
        private static int FindAdjacentRoom(int ix, int iz, RoomFloodFillContext ctx)
        {
            var bestRi = -1;
            var bestCap = int.MinValue;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nx = ix + dx;
                var nz = iz + dz;
                if (nx < 0 || nx >= ctx.GridW || nz < 0 || nz >= ctx.GridD) continue;
                if (!ctx.Occupied[nx, nz]) continue;
                var ri = ctx.RoomAt[nx, nz];
                if (ri < 0) continue;

                var slotIdx = ResolveSlotIndex(ctx, ri);
                var cap = slotIdx >= 0 ? ctx.RoomTargets[slotIdx] : int.MaxValue;
                // Prefer expanding smaller rooms first to balance
                var r = ctx.Regions[ri];
                var curArea = (r.Ix1 - r.Ix0) * (r.Iz1 - r.Iz0);
                var remaining = cap - curArea;
                if (remaining > bestCap || (remaining == bestCap && ctx.Random.RangeExclusive(0, 2) == 0))
                {
                    bestCap = remaining;
                    bestRi = ri;
                }
            }
            return bestRi;
        }

        /// <summary>
        ///     Rebuilds <see cref="RoomFloodFillContext.Regions"/> bounding boxes from the per-cell
        ///     ownership grid so room shapes match actual cell assignments.
        /// </summary>
        private static void RebuildRegionsFromCells(RoomFloodFillContext ctx)
        {
            for (var ri = 0; ri < ctx.Regions.Count; ri++)
                RebuildSingleRegion(ctx, ri);
        }

        private static void RebuildSingleRegion(RoomFloodFillContext ctx, int ri)
        {
            var r = ctx.Regions[ri];
            var ix0 = ctx.GridW; var iz0 = ctx.GridD;
            var ix1 = 0; var iz1 = 0;
            var any = false;
            for (var ix = 0; ix < ctx.GridW; ix++)
                for (var iz = 0; iz < ctx.GridD; iz++)
                {
                    if (ctx.RoomAt[ix, iz] != ri) continue;
                    any = true;
                    ix0 = Mathf.Min(ix0, ix);
                    iz0 = Mathf.Min(iz0, iz);
                    ix1 = Mathf.Max(ix1, ix + 1);
                    iz1 = Mathf.Max(iz1, iz + 1);
                }
            if (any)
                ctx.Regions[ri] = new RoomRegion(ix0, iz0, ix1, iz1, r.Name, r.RoomType);
        }

        /// <summary>
        ///     Merges the classic open-plan triad (LivingRoom + Kitchen + Dining) into a single
        ///     combined slot when at least two are present and marked open-plan in RoomSettings.
        ///     Only ONE instance of each eligible type is merged — additional same-type rooms
        ///     (e.g. a second Kitchen) remain as separate, independent rooms.
        ///     Hallway is NEVER merged — it stays as a separate connector room.
        /// </summary>
        private static List<RoomSlot> MergeOpenPlanSlots(
            List<RoomSlot> ordered,
            RoomSettings roomSettings,
            bool preferOpenPlan)
        {
            if (!preferOpenPlan) return ordered;

            var mergeTypes = new HashSet<RoomType> { RoomType.LivingRoom, RoomType.Kitchen, RoomType.Dining };

            // Only take the FIRST instance of each eligible type so that
            // multiple same-type rooms (e.g. 4 Kitchens) are not collapsed into one.
            var openSlots = new List<int>();
            var seenTypes = new HashSet<RoomType>();
            for (var i = 0; i < ordered.Count; i++)
                if (mergeTypes.Contains(ordered[i].Type)
                    && roomSettings != null
                    && roomSettings.CanBeOpenPlan(ordered[i].Type)
                    && seenTypes.Add(ordered[i].Type))
                    openSlots.Add(i);

            if (openSlots.Count < 2)
                return ordered;

            var mergedPct = 0;
            for (var i = openSlots.Count - 1; i >= 0; i--)
            {
                var idx = openSlots[i];
                mergedPct += ordered[idx].TargetPct;
                ordered.RemoveAt(idx);
            }

            ordered.Insert(0, new RoomSlot(RoomType.LivingRoom, mergedPct));
            return ordered;
        }

        /// <summary>
        ///     Tries to find a free rectangle of size w×d in the occupancy grid.
        /// </summary>
        private static bool TryPlaceRect(bool[,] occupied, int gridW, int gridD,
            int w, int d, out int px, out int pz)
        {
            px = 0;
            pz = 0;
            if (w <= 0 || d <= 0 || w > gridW || d > gridD)
                return false;

            for (var ix = 0; ix <= gridW - w; ix++)
            {
                for (var iz = 0; iz <= gridD - d; iz++)
                {
                    if (IsRectFree(occupied, ix, iz, w, d, gridW, gridD))
                    {
                        MarkRectOccupied(occupied, ix, iz, w, d);
                        px = ix;
                        pz = iz;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        ///     Greedily expands each placed room into adjacent free cells, respecting
        ///     the per-room max dimension limits.
        /// </summary>
        /// <summary>
        ///     Returns true if the rectangle [ix0,ix0+w) × [iz0,iz0+d) is entirely within
        ///     the grid bounds and has no occupied cells.
        /// </summary>
        private static void MarkRectOccupied(bool[,] occupied, int ix0, int iz0, int w, int d)
        {
            for (var rx = ix0; rx < ix0 + w; rx++)
            {
                for (var rz = iz0; rz < iz0 + d; rz++)
                    occupied[rx, rz] = true;
            }
        }

        private static bool IsRectFree(bool[,] occupied, int ix0, int iz0, int w, int d, int gridW, int gridD)
        {
            if (ix0 < 0 || iz0 < 0 || ix0 + w > gridW || iz0 + d > gridD)
                return false;
            for (var x = ix0; x < ix0 + w; x++)
            {
                for (var z = iz0; z < iz0 + d; z++)
                {
                    if (occupied[x, z])
                        return false;
                }
            }
            return true;
        }

        private static List<RoomRegion> PartitionIntoRooms(int width, int depth, int targetCount,
            GenerationRandom random)
        {
            var regions = new List<RoomRegion>();
            var names = new Queue<string>(s_DefaultRoomNames);
            var clampedTargetCount = Mathf.Clamp(targetCount, 1, width * depth);
            BSPSplitRooms(new BspSplitContext((0, 0), (width, depth), clampedTargetCount, random, regions, names));
            // Square-only: clamp each BSP region to its smaller dimension.
            MakeRegionsSquare(regions, width, depth);
            return regions;
        }

        private readonly struct BspSplitContext
        {
            public readonly (int x, int z) Min, Max;
            public int Ix0 => Min.x; public int Iz0 => Min.z;
            public int Ix1 => Max.x; public int Iz1 => Max.z;
            public readonly int Count;
            public readonly GenerationRandom Random;
            public readonly List<RoomRegion> Regions;
            public readonly Queue<string> Names;

            public BspSplitContext((int x, int z) min, (int x, int z) max, int count,
                GenerationRandom random, List<RoomRegion> regions, Queue<string> names)
            {
                Min = min; Max = max;
                Count = count; Random = random; Regions = regions; Names = names;
            }

            public BspSplitContext With(int ix0, int iz0, int ix1, int iz1, int count) =>
                new((ix0, iz0), (ix1, iz1), count, Random, Regions, Names);
        }

        private static void BSPSplitRooms(BspSplitContext ctx)
        {
            var w = ctx.Ix1 - ctx.Ix0;
            var d = ctx.Iz1 - ctx.Iz0;
            if (ctx.Count <= 1 || (w < 2 && d < 2))
            {
                var name = ctx.Names.Count > 0 ? ctx.Names.Dequeue() : $"Room_{ctx.Regions.Count + 1}";
                ctx.Regions.Add(new RoomRegion(ctx.Ix0, ctx.Iz0, ctx.Ix1, ctx.Iz1, name));
                return;
            }

            if (w > 1 && (d <= 1 || w >= d))
            {
                var split = ctx.Ix0 + ctx.Random.RangeExclusive(1, w);
                var leftCount = Mathf.Max(1, ctx.Count / 2);
                BSPSplitRooms(ctx.With(ctx.Ix0, ctx.Iz0, split, ctx.Iz1, leftCount));
                BSPSplitRooms(ctx.With(split, ctx.Iz0, ctx.Ix1, ctx.Iz1, ctx.Count - leftCount));
            }
            else
            {
                var split = ctx.Iz0 + ctx.Random.RangeExclusive(1, d);
                var bottomCount = Mathf.Max(1, ctx.Count / 2);
                BSPSplitRooms(ctx.With(ctx.Ix0, ctx.Iz0, ctx.Ix1, split, bottomCount));
                BSPSplitRooms(ctx.With(ctx.Ix0, split, ctx.Ix1, ctx.Iz1, ctx.Count - bottomCount));
            }
        }

        private static List<PlannedInteriorWallSegment> PlanInteriorWalls(List<RoomRegion> rooms,
            int[,] roomAt, int width, int depth,
            HashSet<Vector2Int> blockedDoorwayCells, bool canUseDoorwayPrefab, GenerationRandom random)
        {
            var cellRoom = BuildCellRoomLookup(rooms, roomAt, width, depth);
            var boundaries = DiscoverRoomBoundaries(cellRoom, width, depth, blockedDoorwayCells);
            return AssignDoorwaysToBoundaries(boundaries, canUseDoorwayPrefab, random);
        }

        private static Dictionary<Vector2Int, int> BuildCellRoomLookup(List<RoomRegion> rooms,
            int[,] roomAt, int width, int depth)
        {
            var cellRoom = new Dictionary<Vector2Int, int>(width * depth);
            if (roomAt != null && roomAt.GetLength(0) == width && roomAt.GetLength(1) == depth)
                FillCellRoomFromGrid(cellRoom, rooms, roomAt, width, depth);
            else
                FillCellRoomFromBoundingBoxes(cellRoom, rooms);
            return cellRoom;
        }

        private static void FillCellRoomFromGrid(
            Dictionary<Vector2Int, int> cellRoom,
            List<RoomRegion> rooms,
            int[,] roomAt,
            int width,
            int depth)
        {
            for (var ix = 0; ix < width; ix++)
                for (var iz = 0; iz < depth; iz++)
                {
                    var ri = roomAt[ix, iz];
                    if (ri >= 0 && ri < rooms.Count)
                        cellRoom[new Vector2Int(ix, iz)] = ri;
                }
        }

        private static void FillCellRoomFromBoundingBoxes(
            Dictionary<Vector2Int, int> cellRoom,
            List<RoomRegion> rooms)
        {
            for (var r = 0; r < rooms.Count; r++)
                for (var ix = rooms[r].Ix0; ix < rooms[r].Ix1; ix++)
                    for (var iz = rooms[r].Iz0; iz < rooms[r].Iz1; iz++)
                        cellRoom[new Vector2Int(ix, iz)] = r;
        }

        private static Dictionary<(int, int), List<InteriorWallSegment>> DiscoverRoomBoundaries(
            Dictionary<Vector2Int, int> cellRoom,
            int width, int depth,
            HashSet<Vector2Int> blockedDoorwayCells)
        {
            var boundaries = new Dictionary<(int, int), List<InteriorWallSegment>>();
            for (var ix = 0; ix < width; ix++)
            {
                for (var iz = 0; iz < depth; iz++)
                {
                    if (!cellRoom.TryGetValue(new Vector2Int(ix, iz), out var rA))
                        continue;

                    TryAddEastBoundary(boundaries, cellRoom, ix, iz, rA, width, blockedDoorwayCells);
                    TryAddNorthBoundary(boundaries, cellRoom, ix, iz, rA, depth, blockedDoorwayCells);
                }
            }
            return boundaries;
        }

        private static void TryAddEastBoundary(
            Dictionary<(int, int), List<InteriorWallSegment>> boundaries,
            Dictionary<Vector2Int, int> cellRoom,
            int ix, int iz, int rA, int width,
            HashSet<Vector2Int> blockedDoorwayCells)
        {
            if (ix + 1 >= width || !cellRoom.TryGetValue(new Vector2Int(ix + 1, iz), out var rB) || rA == rB)
                return;

            var key = rA < rB ? (rA, rB) : (rB, rA);
            if (!boundaries.TryGetValue(key, out var list))
                boundaries[key] = list = new List<InteriorWallSegment>();

            var allowDoorway = CanPlaceDoorwayBetweenCells(
                new Vector2Int(ix, iz), new Vector2Int(ix + 1, iz), blockedDoorwayCells);

            list.Add(new InteriorWallSegment(
                new WallSegment(
                    new Vector3(ix * CellSize + CellSize * 0.5f, 0f, iz * CellSize),
                    Quaternion.Euler(0f, -90f, 0f)),
                allowDoorway));
        }

        private static void TryAddNorthBoundary(
            Dictionary<(int, int), List<InteriorWallSegment>> boundaries,
            Dictionary<Vector2Int, int> cellRoom,
            int ix, int iz, int rA, int depth,
            HashSet<Vector2Int> blockedDoorwayCells)
        {
            if (iz + 1 >= depth || !cellRoom.TryGetValue(new Vector2Int(ix, iz + 1), out var rC) || rA == rC)
                return;

            var key = rA < rC ? (rA, rC) : (rC, rA);
            if (!boundaries.TryGetValue(key, out var list))
                boundaries[key] = list = new List<InteriorWallSegment>();

            var allowDoorway = CanPlaceDoorwayBetweenCells(
                new Vector2Int(ix, iz), new Vector2Int(ix, iz + 1), blockedDoorwayCells);

            list.Add(new InteriorWallSegment(
                new WallSegment(
                    new Vector3(ix * CellSize, 0f, iz * CellSize + CellSize * 0.5f),
                    Quaternion.identity),
                allowDoorway));
        }

        private static List<PlannedInteriorWallSegment> AssignDoorwaysToBoundaries(
            Dictionary<(int, int), List<InteriorWallSegment>> boundaries,
            bool canUseDoorwayPrefab,
            GenerationRandom random)
        {
            var planned = new List<PlannedInteriorWallSegment>();
            foreach (var kv in boundaries)
            {
                var segments = kv.Value;
                var doorIdx = canUseDoorwayPrefab ? PickDoorwayIndex(segments, random) : -1;

                for (var i = 0; i < segments.Count; i++)
                {
                    var interior = segments[i];
                    planned.Add(new PlannedInteriorWallSegment(interior.Segment, kv.Key.Item1, kv.Key.Item2, i,
                        i == doorIdx));
                }
            }
            return planned;
        }

        private static int PickDoorwayIndex(List<InteriorWallSegment> segments, GenerationRandom random)
        {
            var doorwayCandidates = new List<int>();
            for (var i = 0; i < segments.Count; i++)
            {
                if (segments[i].AllowDoorway)
                    doorwayCandidates.Add(i);
            }

            if (doorwayCandidates.Count > 0)
                return doorwayCandidates[random.RangeExclusive(0, doorwayCandidates.Count)];

            return -1;
        }

        private static bool CanPlaceDoorwayBetweenCells(Vector2Int a, Vector2Int b,
            HashSet<Vector2Int> blockedDoorwayCells)
        {
            if (blockedDoorwayCells == null || blockedDoorwayCells.Count == 0)
                return true;

            return !blockedDoorwayCells.Contains(a) && !blockedDoorwayCells.Contains(b);
        }

        private static HashSet<Vector2Int> CollectDoorwayBlockedCellsForLevel(
            StairTransition[] stairTransitions,
            HashSet<FloorCell> skipUpperLandingFloors,
            int level,
            int width,
            int depth)
        {
            var blocked = new HashSet<Vector2Int>();

            if (stairTransitions != null)
            {
                foreach (var tr in stairTransitions)
                {
                    if (tr.LowerLevel != level)
                        continue;

                    blocked.Add(new Vector2Int(tr.Ix, tr.Iz));
                    if (TryGetForwardNeighborCell(tr.Ix, tr.Iz, tr.RotationY, width, depth, out var fx, out var fz))
                        blocked.Add(new Vector2Int(fx, fz));
                }
            }

            if (skipUpperLandingFloors != null)
            {
                foreach (var cell in skipUpperLandingFloors)
                    if (cell.Level == level)
                        blocked.Add(new Vector2Int(cell.Ix, cell.Iz));
            }

            return blocked;
        }

        /// <summary>
        ///     Square-only: clamps each region to its smaller dimension (width vs depth),
        ///     centering the square within the original bounding box and clamping to grid bounds.
        /// </summary>
        private static void MakeRegionsSquare(List<RoomRegion> regions, int gridW, int gridD)
        {
            for (var i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                var w = r.Ix1 - r.Ix0;
                var d = r.Iz1 - r.Iz0;
                if (w <= 0 || d <= 0 || w == d)
                    continue;

                var side = Mathf.Min(w, d);
                // Centre the square within the original rectangle, then clamp to grid.
                var cx = (r.Ix0 + r.Ix1) / 2;
                var cz = (r.Iz0 + r.Iz1) / 2;
                var half = side / 2;
                var newIx0 = Mathf.Max(0, cx - half);
                var newIz0 = Mathf.Max(0, cz - half);
                var newIx1 = Mathf.Min(gridW, newIx0 + side);
                var newIz1 = Mathf.Min(gridD, newIz0 + side);

                // Push back if we hit the far boundary.
                if (newIx1 - newIx0 < side)
                    newIx0 = Mathf.Max(0, newIx1 - side);
                if (newIz1 - newIz0 < side)
                    newIz0 = Mathf.Max(0, newIz1 - side);

                regions[i] = new RoomRegion(newIx0, newIz0, newIx1, newIz1, r.Name, r.RoomType);
            }
        }

        private static int PickRoomCountForFloor(int minRooms, int maxRooms, GenerationRandom random)
        {
            if (maxRooms <= 0)
                return 0;

            minRooms = Mathf.Max(0, minRooms);
            maxRooms = Mathf.Max(minRooms, maxRooms);
            if (minRooms == maxRooms)
                return minRooms;

            return random.RangeInclusive(minRooms, maxRooms);
        }

        private static void Shuffle(List<int> list, GenerationRandom random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.RangeExclusive(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        ///     Injects 1×1 <see cref="RoomType.Stairwell"/> rooms into the room list and
        ///     roomAt grid at each stair transition cell. Called after initial room placement
        ///     and before interior wall planning so walls naturally enclose the stairwell.
        /// </summary>
        private static void InjectStairwellRooms(
            GenerationContext context,
            List<RoomRegion> rooms,
            int[,] roomAt,
            int level,
            int levelWidth,
            int levelDepth)
        {
            if (context.Plan.StairTransitions.Length == 0)
                return;

            // Only inject on floors that have stairs (levels where a stair exists as lower or upper level).
            if (!HasStairOnLevel(context, level))
                return;

            foreach (var tr in context.Plan.StairTransitions)
            {
                // Stairwell exists on both the lower and upper level of each transition.
                if (tr.LowerLevel != level && tr.LowerLevel + 1 != level)
                    continue;

                if (tr.Ix < 0 || tr.Ix >= levelWidth || tr.Iz < 0 || tr.Iz >= levelDepth)
                    continue;

                // Check if this cell is already claimed by another room (shouldn't be, but guard).
                if (roomAt != null && roomAt[tr.Ix, tr.Iz] >= 0)
                    continue;

                var name = $"Stairwell_L{tr.LowerLevel}_to_L{tr.LowerLevel + 1}";
                var stairwellRoom = new RoomRegion(tr.Ix, tr.Iz, tr.Ix + 1, tr.Iz + 1, name, RoomType.Stairwell);
                var newIndex = rooms.Count;
                rooms.Add(stairwellRoom);

                if (roomAt != null)
                    roomAt[tr.Ix, tr.Iz] = newIndex;
            }
        }

        private static bool HasStairOnLevel(GenerationContext context, int level)
        {
            foreach (var tr in context.Plan.StairTransitions)
            {
                if (tr.LowerLevel == level || tr.LowerLevel + 1 == level)
                    return true;
            }
            return false;
        }

        /// <summary>
        ///     Removes interior walls on the stair entrance (lower level) and exit (upper level)
        ///     sides of stairwell rooms so the stairway is accessible from the adjacent room.
        /// </summary>
        private static void RemoveStairwellEntranceWalls(
            GenerationContext context,
            List<PlannedInteriorWallSegment> interiorWalls,
            List<RoomRegion> rooms,
            int levelWidth,
            int levelDepth)
        {
            if (context.Plan.StairTransitions.Length == 0 || interiorWalls.Count == 0)
                return;

            // Find stairwell room indices on this level.
            var stairwellRoomIndices = CollectStairwellRoomIndices(rooms);
            if (stairwellRoomIndices.Count == 0)
                return;

            // For each stair transition, determine the open side based on stair direction.
            // Open side on lower level = entrance (stair faces this way).
            // Open side on upper level = exit (opposite direction).
            var openEdges = new HashSet<(int ix, int iz, bool isNorth)>();
            foreach (var tr in context.Plan.StairTransitions)
                AddTransitionOpenEdges(tr, levelWidth, levelDepth, openEdges);

            if (openEdges.Count == 0)
                return;

            // Remove interior walls that sit on open edges and involve a stairwell room.
            for (var i = interiorWalls.Count - 1; i >= 0; i--)
            {
                if (TryRemoveWallOnOpenEdge(interiorWalls[i], stairwellRoomIndices, openEdges))
                    interiorWalls.RemoveAt(i);
            }
        }

        private static HashSet<int> CollectStairwellRoomIndices(List<RoomRegion> rooms)
        {
            var indices = new HashSet<int>();
            for (var i = 0; i < rooms.Count; i++)
                if (rooms[i].RoomType == RoomType.Stairwell)
                    indices.Add(i);
            return indices;
        }

        private static void AddTransitionOpenEdges(
            StairTransition tr, int levelWidth, int levelDepth,
            HashSet<(int ix, int iz, bool isNorth)> openEdges)
        {
            if (tr.Ix < 0 || tr.Ix >= levelWidth || tr.Iz < 0 || tr.Iz >= levelDepth)
                return;

            var rot = NormalizeRotationY(tr.RotationY);

            // Stairwell cell at lower level: open on entrance side.
            AddOpenEdge(tr.Ix, tr.Iz, rot, isLower: true, openEdges);

            // Stairwell cell at upper level: open on exit side (opposite entrance).
            AddOpenEdge(tr.Ix, tr.Iz, rot, isLower: false, openEdges);
        }

        private static int NormalizeRotationY(float rotationY)
        {
            var rot = Mathf.RoundToInt(rotationY / 90f) * 90;
            return ((rot % 360) + 360) % 360; // Normalize to [0, 270].
        }

        private static bool TryRemoveWallOnOpenEdge(
            PlannedInteriorWallSegment wall,
            HashSet<int> stairwellRoomIndices,
            HashSet<(int ix, int iz, bool isNorth)> openEdges)
        {
            if (!stairwellRoomIndices.Contains(wall.RoomA) && !stairwellRoomIndices.Contains(wall.RoomB))
                return false;

            // Determine which grid cell edge this wall sits on.
            var pos = wall.Segment.Position;
            var ix = Mathf.RoundToInt(pos.x / CellSize);
            var iz = Mathf.RoundToInt(pos.z / CellSize);

            // North walls are at the north edge of cell (ix, iz) facing +Z.
            // East walls are at the east edge of cell (ix, iz) facing +X.
            var rotY = NormalizeRotationY(wall.Segment.Rotation.eulerAngles.y);

            // East wall (rot ≈ 270 or -90) is on the +X edge of cell (ix, iz).
            // North wall (rot ≈ 0) is on the +Z edge of cell (ix, iz).
            if (rotY == 0)
                return openEdges.Contains((ix, iz, true));
            if (rotY == 270 || rotY == 90)
                return openEdges.Contains((ix, iz, false));
            return false;
        }

        private static void AddOpenEdge(int ix, int iz, int stairRot, bool isLower,
            HashSet<(int ix, int iz, bool isNorth)> openEdges)
        {
            // Entrance (lower level): open on the side the stair faces.
            // Exit (upper level): open on the opposite side.
            var dir = isLower ? stairRot : (stairRot + 180) % 360;
            switch (dir)
            {
                case 0:   openEdges.Add((ix, iz, true)); break;   // +Z edge
                case 180: openEdges.Add((ix, iz - 1, true)); break; // -Z edge (cell to south)
                case 90:  openEdges.Add((ix, iz, false)); break;   // +X edge
                case 270: openEdges.Add((ix - 1, iz, false)); break; // -X edge (cell to west)
            }
        }

    }
}
#endif
