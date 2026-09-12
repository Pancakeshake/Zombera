#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zombera.Data;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        private static readonly List<Mesh> _bakedMeshes = new();
        private static readonly List<Material> _bakedMaterials = new();

        /// <summary>Returns a human-readable folder name for a floor level (0 = "Ground Floor", 1 = "1st Floor", etc.).</summary>
        private static string LevelFolderName(int level)
        {
            return level switch
            {
                0 => "Ground Floor",
                1 => "1st Floor",
                2 => "2nd Floor",
                3 => "3rd Floor",
                _ => $"{level}th Floor"
            };
        }

        private static string BuildHierarchyAndSavePrefab(GenerationContext context)
        {
            var root = new GameObject(context.AssetFileName);
            var floorsParent = new GameObject("Floors");
            var ceilingsParent = new GameObject("Ceilings");
            var wallsParent = new GameObject("Walls");
            var stairsParent = new GameObject("Stairs");
            var roofParent = new GameObject("Roof");

            // ── Sub-parents ──────────────────────────────────────────
            // Floor level sub-groups
            var floorLevelParents = new Transform[context.Plan.FloorCount];
            var ceilingLevelParents = new Transform[context.Plan.FloorCount];
            for (var level = 0; level < context.Plan.FloorCount; level++)
            {
                var floorLabel = LevelFolderName(level);
                var floorLevel = new GameObject(floorLabel);
                floorLevel.transform.SetParent(floorsParent.transform, false);
                floorLevelParents[level] = floorLevel.transform;

                var ceilingLevel = new GameObject(floorLabel);
                ceilingLevel.transform.SetParent(ceilingsParent.transform, false);
                ceilingLevelParents[level] = ceilingLevel.transform;
            }

            // Wall sub-groups
            var externalWallsParent = new GameObject("External");
            externalWallsParent.transform.SetParent(wallsParent.transform, false);
            var internalWallsParent = new GameObject("Internal");
            internalWallsParent.transform.SetParent(wallsParent.transform, false);

            floorsParent.transform.SetParent(root.transform, false);
            ceilingsParent.transform.SetParent(root.transform, false);
            wallsParent.transform.SetParent(root.transform, false);
            stairsParent.transform.SetParent(root.transform, false);
            roofParent.transform.SetParent(root.transform, false);

            try
            {
                PlaceFoundationsAndFloors(context, floorLevelParents);
                PlaceStairs(context, stairsParent.transform);
                PlacePerimeterWalls(context, externalWallsParent.transform, stairsParent.transform);
                PlaceFuseBox(context, externalWallsParent.transform);
                PlacePlannedRoomContent(context, root.transform, internalWallsParent.transform);
                PlaceInteriorProps(context, root.transform);

                // Authoritative door-facing stamp — placement uses this instead of
                // guessing which StairSocket is the front door.
                StampMainDoorAnchor(root, context.Plan);

                // Ceilings always close the interior volume (with stairwell cutouts).
                // Only the exterior roof assembly, parapet, and guttering are skip-controlled.
                PlaceRoomCeilings(context, ceilingLevelParents);

                if (!context.Settings.SkipRoofAndCeiling)
                {
                    PlaceParapetWalls(context, externalWallsParent.transform);

                    // Roofs: top cap + step-back roofs at each shrink level
                    PlaceSkyscraperRoofs(context, roofParent.transform);

                    // Guttering and downpipes along roof eaves
                    PlaceGuttering(context, roofParent.transform);
                }
                else
                {
                    Debug.Log("[ModularSingleLevelHouseGeneratorTool] Skipping roof assembly, parapet & guttering (SkipRoofAndCeiling override is enabled).");
                }

                // Save the prefab first so the main asset exists,
                // then attach cloned meshes as sub-assets and re-save.
                PrefabUtility.SaveAsPrefabAsset(root, context.AssetPath);
                foreach (var mesh in _bakedMeshes)
                {
                    mesh.hideFlags = HideFlags.None;
                    AssetDatabase.AddObjectToAsset(mesh, context.AssetPath);
                }
                foreach (var mat in _bakedMaterials)
                {
                    mat.hideFlags = HideFlags.None;
                    AssetDatabase.AddObjectToAsset(mat, context.AssetPath);
                }
                PrefabUtility.SaveAsPrefabAsset(root, context.AssetPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(context.AssetPath);
                EditorGUIUtility.PingObject(Selection.activeObject);

                var stairNote = context.Plan.NeedsStairs
                    ? $", {context.Plan.StairTransitions.Length} stair run(s)"
                    : string.Empty;
                var ok =
                    $"Saved '{context.AssetPath}' ({context.BuildingCategory}, footprint {context.Plan.Width}x{context.Plan.Depth} cells, {context.Plan.FloorCount} floor(s){stairNote}, {CellSize}m grid / {WallHeight}m story height).";
                Debug.Log($"[ModularSingleLevelHouseGeneratorTool] {ok}");
                return context.AssetPath;
            }
            finally
            {
                Object.DestroyImmediate(root);
                _bakedMeshes.Clear();
                _bakedMaterials.Clear();
            }
        }

        private static void PlacePerimeterWalls(GenerationContext context, Transform wallsParent, Transform stairsParent)
        {
            foreach (var levelPlan in context.Plan.PerimeterWallPlans)
            {
                // Commercial ground floors swap the storefront for shop glass.
                var glassBySegment = ResolveShopGlassSegments(context, levelPlan);

                foreach (var plannedSegment in levelPlan.Segments)
                {
                    glassBySegment.TryGetValue(plannedSegment.SegmentIndex, out var glassPiece);
                    PlacePerimeterWallSegment(context, wallsParent, stairsParent, levelPlan.Level, plannedSegment, glassPiece);
                }
            }
        }

        /// <summary>
        ///     Maps ground-floor perimeter segments on the door side to shop-glass pieces
        ///     (caps at building corners, full pieces between). Commercial buildings only.
        /// </summary>
        private static Dictionary<int, GameObject> ResolveShopGlassSegments(GenerationContext context, LevelWallPlan levelPlan)
        {
            var map = new Dictionary<int, GameObject>();
            if (levelPlan.Level != 0 || context.BuildingCategory != CityDistrictType.Commercial)
                return map;
            if (context.Prefabs.ShopGlassFull == null)
                return map;

            var doorIndex = context.Plan.PrimaryDoorSegmentIndex;
            if (doorIndex < 0 || levelPlan.Segments.Count == 0)
                return map;

            var width = context.Plan.Width;
            var depth = context.Plan.Depth;
            int sideStart, sideLength;
            if (doorIndex < width) { sideStart = 0; sideLength = width; }
            else if (doorIndex < 2 * width) { sideStart = width; sideLength = width; }
            else if (doorIndex < 2 * width + depth) { sideStart = 2 * width; sideLength = depth; }
            else { sideStart = 2 * width + depth; sideLength = depth; }

            // Runs are always ordered along the side: rows run along +X, columns along +Z.
            var sideDir = sideStart < 2 * width ? Vector3.right : Vector3.forward;

            ComposeGlassRunsOnSide(context, levelPlan, sideStart, sideLength, sideDir, map);

            // Corner shops continue the storefront around one corner onto the adjacent
            // side column — the two runs meet at 90° with their cap pieces.
            if (context.Plan.CornerStorefront)
            {
                var cornerStart = context.Plan.CornerSideIsLeft ? 2 * width : 2 * width + depth;
                ComposeGlassRunsOnSide(context, levelPlan, cornerStart, depth, Vector3.forward, map);
            }

            return map;
        }

        /// <summary>
        ///     Composes shop-glass runs along one perimeter side, splitting around any
        ///     door segments so glass never replaces a doorway.
        /// </summary>
        private static void ComposeGlassRunsOnSide(
            GenerationContext context, LevelWallPlan levelPlan,
            int sideStart, int sideLength, Vector3 sideDir,
            Dictionary<int, GameObject> map)
        {
            var sideEnd = sideStart + sideLength;

            var runStart = -1;
            for (var i = sideStart; i < sideEnd; i++)
            {
                var isGlass = !levelPlan.Segments[i].IsDoor;
                if (isGlass && runStart < 0)
                    runStart = i;

                if (!isGlass && runStart >= 0)
                {
                    AssignShopGlassRun(context, levelPlan, runStart, i - 1, sideDir, map);
                    runStart = -1;
                }
            }

            if (runStart >= 0)
                AssignShopGlassRun(context, levelPlan, runStart, sideEnd - 1, sideDir, map);
        }

        /// <summary>
        ///     Composes a glass run: 1-wide runs get the double-capped piece, longer runs
        ///     get a cap at each end with full pieces between (e.g. left cap, fulls, right cap).
        /// </summary>
        private static void AssignShopGlassRun(
            GenerationContext context, LevelWallPlan levelPlan,
            int runStart, int runEnd, Vector3 sideDir,
            Dictionary<int, GameObject> map)
        {
            var length = runEnd - runStart + 1;

            if (length == 1)
            {
                map[runStart] = context.Prefabs.ShopGlassCapBoth ?? context.Prefabs.ShopGlassFull;
                return;
            }

            var startCap = ResolveShopGlassCap(context, levelPlan, runStart, -sideDir);
            var endCap = ResolveShopGlassCap(context, levelPlan, runEnd, sideDir);
            for (var i = runStart; i <= runEnd; i++)
            {
                if (i == runStart)
                    map[i] = startCap;
                else if (i == runEnd)
                    map[i] = endCap;
                else
                    map[i] = context.Prefabs.ShopGlassFull;
            }
        }

        private static GameObject ResolveShopGlassCap(
            GenerationContext context, LevelWallPlan levelPlan, int segmentIndex, Vector3 pillarSide)
        {
            var segment = levelPlan.Segments[segmentIndex];
            var leftWorld = segment.Segment.Rotation * Vector3.left;
            var cap = Vector3.Dot(leftWorld, pillarSide) > 0.9f
                ? context.Prefabs.ShopGlassCapLeft
                : context.Prefabs.ShopGlassCapRight;
            return cap ?? context.Prefabs.ShopGlassFull;
        }

        private static void PlacePerimeterWallSegment(
            GenerationContext context,
            Transform wallsParent,
            Transform stairsParent,
            int level,
            PlannedPerimeterSegment plannedSegment,
            GameObject glassOverride)
        {
            var prefab = glassOverride != null ? glassOverride : ResolvePerimeterWallPrefab(context, plannedSegment);
            if (prefab == null)
                return;

            // Doors from external packs don't include wall surround — place kit doorway behind them
            var isOverrideDoor = plannedSegment.IsDoor && prefab != context.Prefabs.Doorway;
            if (isOverrideDoor && context.Prefabs.Doorway != null)
            {
                var doorFrame = PrefabUtility.InstantiatePrefab(context.Prefabs.Doorway, wallsParent) as GameObject;
                if (doorFrame != null)
                {
                    doorFrame.name = $"Wall_L{level}_{plannedSegment.SegmentIndex:D2}_DoorFrame";
                    doorFrame.transform.localPosition = plannedSegment.Segment.Position;
                    doorFrame.transform.localRotation = plannedSegment.Segment.Rotation;
                    SetFrameMaterials(doorFrame, context);
                }
            }

            var go = PrefabUtility.InstantiatePrefab(prefab, wallsParent) as GameObject;
            if (go == null)
                return;

            go.name = $"Wall_L{level}_{plannedSegment.SegmentIndex:D2}_{ResolvePerimeterWallTag(plannedSegment, prefab)}";
            go.transform.localPosition = plannedSegment.Segment.Position;
            go.transform.localRotation = plannedSegment.Segment.Rotation;

            // Apply door-specific adjustments for non-kit door prefabs
            if (isOverrideDoor)
            {
                Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Applying door adapter to {go.name}, prefab={prefab.name}");
                ApplyDoorAdapter(go, context, isExterior: true);
            }

            if (plannedSegment.IsDoor)
                TryPlaceExteriorStairsForDoor(context, stairsParent, plannedSegment);

            // Shop-glass segments are their own window wall — don't nest the old
            // closed-window mesh inside them.
            if (plannedSegment.IsWindow && glassOverride == null)
                PlaceWindowClosedMesh(context, go);
        }

        private static void PlaceParapetWalls(GenerationContext context, Transform wallsParent)
        {
            var config = context.ActiveRoofType;
            if (config == null || config.Style != RoofStyle.Flat || config.ParapetYScale <= 0f)
                return;

            var prefs = context.Prefabs;
            if (prefs.Wall == null)
                return;

            var topLevel = context.Plan.FloorCount - 1;

            foreach (var levelPlan in context.Plan.PerimeterWallPlans)
            {
                if (levelPlan.Level != topLevel)
                    continue;

                foreach (var seg in levelPlan.Segments)
                {
                    var go = PrefabUtility.InstantiatePrefab(prefs.Wall, wallsParent) as GameObject;
                    if (go == null) continue;

                    go.name = $"Parapet_L{topLevel}_{seg.SegmentIndex:D2}";
                    go.transform.localPosition = seg.Segment.Position + new Vector3(0f, WallHeight, 0f);
                    go.transform.localRotation = seg.Segment.Rotation;
                    go.transform.localScale = new Vector3(1f, config.ParapetYScale, config.ParapetZScale);
                    // Push inward along the wall's forward axis
                    go.transform.localPosition += go.transform.forward * config.ParapetInset;
                    BakeMeshScale(go);
                }
                break;
            }
        }

        private static void PlaceWindowClosedMesh(GenerationContext context, GameObject windowGo)
        {
            if (context.Prefabs.WindowClosed == null) return;

            var closed = PrefabUtility.InstantiatePrefab(context.Prefabs.WindowClosed, windowGo.transform) as GameObject;
            if (closed == null) return;

            closed.name = context.Prefabs.WindowClosed.name;
            closed.transform.localPosition = Vector3.zero;
            closed.transform.localRotation = Quaternion.identity;

            if (context.Prefabs.WindowMoulding != null)
            {
                var moulding = PrefabUtility.InstantiatePrefab(context.Prefabs.WindowMoulding, windowGo.transform) as GameObject;
                if (moulding != null)
                {
                    moulding.name = context.Prefabs.WindowMoulding.name;
                    moulding.transform.localPosition = new Vector3(0f, 0f, -0.034f);
                    moulding.transform.localRotation = Quaternion.identity;
                }
            }
        }

        private static void PlacePlannedRoomContent(GenerationContext context, Transform root, Transform wallsParent)
        {
            if (context.Plan.FloorRoomPlans.Count == 0)
                return;

            var roomsLabelParent = new GameObject("Rooms");
            roomsLabelParent.transform.SetParent(root, false);

            var roomsVisualParent = new GameObject("RoomVisuals");
            roomsVisualParent.transform.SetParent(root, false);

            foreach (var floorPlan in context.Plan.FloorRoomPlans)
            {
                if (floorPlan.Rooms.Count == 0)
                    continue;

                var levelOffset = context.Plan.SkyscraperMode
                    ? context.Plan.FloorOffsets[floorPlan.Level]
                    : Vector3.zero;

                var levelRoomsParent = new GameObject($"L{floorPlan.Level}");
                levelRoomsParent.transform.SetParent(roomsLabelParent.transform, false);

                PlaceInteriorWalls(
                    floorPlan.InteriorWalls,
                    (context.Prefabs.Wall, context.Prefabs.Doorway, context.Prefabs.InteriorDoor),
                    context,
                    wallsParent,
                    floorPlan.FloorY,
                    levelOffset);
                var labelCtx = new RoomLabelCtx(levelRoomsParent.transform, floorPlan.FloorY, floorPlan.Level, levelOffset);
                CreateRoomLabels(floorPlan.Rooms, floorPlan.RoomAt,
                    floorPlan.FloorPlanWidth, floorPlan.FloorPlanDepth, in labelCtx);

                var visCtx = new RoomVisualCtx(roomsVisualParent.transform, floorPlan.FloorY + 0.11f, floorPlan.Level, levelOffset);
                CreateRoomFloorVisuals(floorPlan.Rooms, floorPlan.RoomAt,
                    new Vector2Int(floorPlan.FloorPlanWidth, floorPlan.FloorPlanDepth), in visCtx);
            }
        }

        /// <summary>
        ///     Places floor-slab ceiling tiles above each room to close the interior volume.
        ///     For non-top floors the upper-level floor grid already serves as a ceiling,
        ///     so only the topmost floor's rooms get ceiling tiles.
        ///     Stairwell landing cells are skipped so stairs can pass through.
        /// </summary>
        private static void PlaceRoomCeilings(GenerationContext context, Transform[] ceilingLevelParents)
        {
            if (context.Plan.FloorRoomPlans.Count == 0)
                return;

            var topLevel = context.Plan.FloorCount - 1;
            var slabPrefab = context.Prefabs.UpperFloor != null ? context.Prefabs.UpperFloor : context.Prefabs.Floor;
            if (slabPrefab == null)
                return;

            // Flat roofs sit directly on the ceiling slab so it should be thinner.
            // Shed roofs need a tiny Y offset so ceilings don't z-fight with panels.
            var (ceilingYScale, ceilingYOffset) = GetCeilingAdjustments(context);

            foreach (var floorPlan in context.Plan.FloorRoomPlans)
            {
                // Skip non-top floors — upper-level floor slabs already serve as ceilings.
                if (floorPlan.Level != topLevel)
                    continue;

                if (floorPlan.Rooms.Count == 0)
                    continue;

                var levelOffset = context.Plan.SkyscraperMode
                    ? context.Plan.FloorOffsets[floorPlan.Level]
                    : Vector3.zero;

                // Ceiling sits at the top of perimeter walls for this level.
                var ceilingY = floorPlan.FloorY + WallHeight + ceilingYOffset;

                var levelParent = ceilingLevelParents[floorPlan.Level];
                var ctx = new CeilingTileCtx(levelParent, slabPrefab, ceilingY, floorPlan.Level, levelOffset, ceilingYScale);
                for (var ri = 0; ri < floorPlan.Rooms.Count; ri++)
                    PlaceRoomCeilingTiles(in ctx, floorPlan.Rooms[ri].Name, ri,
                        floorPlan.RoomAt, floorPlan.Rooms[ri], context.Plan.SkipUpperLandingFloors);
            }
        }

        private static (float scale, float offset) GetCeilingAdjustments(GenerationContext context)
        {
            var isFlat = context.ActiveRoofType != null && context.ActiveRoofType.Style == RoofStyle.Flat;
            var isShed = context.ActiveRoofType != null && context.ActiveRoofType.Style == RoofStyle.Shed;
            return (isFlat ? 0.4f : 1f, isShed ? -0.01f : 0f);
        }

        private readonly struct CeilingTileCtx
        {
            public readonly Transform FloorsParent;
            public readonly GameObject SlabPrefab;
            public readonly float CeilingY;
            public readonly int Level;
            public readonly Vector3 LevelOffset;
            public readonly float YScale;

            public CeilingTileCtx(Transform floorsParent, GameObject slabPrefab,
                float ceilingY, int level, Vector3 levelOffset, float yScale)
            {
                FloorsParent = floorsParent;
                SlabPrefab = slabPrefab;
                CeilingY = ceilingY;
                Level = level;
                LevelOffset = levelOffset;
                YScale = yScale;
            }
        }

        private static void PlaceRoomCeilingTiles(
            in CeilingTileCtx ctx, string roomName, int roomIndex,
            int[,] roomAt, RoomRegion fallbackRoom,
            HashSet<FloorCell> skipCells)
        {
            if (roomAt != null)
            {
                var gridW = roomAt.GetLength(0);
                var gridD = roomAt.GetLength(1);
                for (var ix = 0; ix < gridW; ix++)
                {
                    for (var iz = 0; iz < gridD; iz++)
                    {
                        if (roomAt[ix, iz] != roomIndex) continue;
                        if (skipCells != null && skipCells.Contains(new FloorCell(ctx.Level, ix, iz))) continue;
                        PlaceSingleCeilingTile(in ctx, roomName, ix, iz);
                    }
                }
                return;
            }

            for (var ix = fallbackRoom.Ix0; ix < fallbackRoom.Ix1; ix++)
            {
                for (var iz = fallbackRoom.Iz0; iz < fallbackRoom.Iz1; iz++)
                {
                    if (skipCells != null && skipCells.Contains(new FloorCell(ctx.Level, ix, iz))) continue;
                    PlaceSingleCeilingTile(in ctx, roomName, ix, iz);
                }
            }
        }

        private static void PlaceSingleCeilingTile(
            in CeilingTileCtx ctx, string roomName, int ix, int iz)
        {
            var go = PrefabUtility.InstantiatePrefab(ctx.SlabPrefab, ctx.FloorsParent) as GameObject;
            if (go == null) return;

            go.name = $"Ceiling_{roomName}_L{ctx.Level}_{ix}_{iz}";
            go.transform.localPosition = new Vector3(
                ix * CellSize + ctx.LevelOffset.x, ctx.CeilingY, iz * CellSize + ctx.LevelOffset.z);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(1f, ctx.YScale, 1f);
        }

        private readonly struct RoomVisualCtx
        {
            public readonly Transform Parent;
            public readonly float VisualY;
            public readonly int LevelIndex;
            public readonly Vector3 OriginOffset;

            public RoomVisualCtx(Transform parent, float visualY, int levelIndex, Vector3 originOffset)
            {
                Parent = parent;
                VisualY = visualY;
                LevelIndex = levelIndex;
                OriginOffset = originOffset;
            }
        }

        /// <summary>
        ///     Creates transparent coloured quad meshes on the floor surface for each room,
        ///     similar to city district lot fills. Colour is keyed to <see cref="RoomType"/>.
        /// </summary>
        private static void CreateRoomFloorVisuals(
            System.Collections.Generic.List<RoomRegion> rooms,
            int[,] roomAt, Vector2Int gridSize, in RoomVisualCtx ctx)
        {
            if (rooms.Count == 0) return;

            if (roomAt != null && roomAt.GetLength(0) == gridSize.x && roomAt.GetLength(1) == gridSize.y)
                CreatePerCellRoomVisuals(rooms, roomAt, gridSize, in ctx);
            else
                CreateBoundingBoxRoomVisuals(rooms, in ctx);
        }

        private static void CreatePerCellRoomVisuals(
            System.Collections.Generic.List<RoomRegion> rooms,
            int[,] roomAt, Vector2Int gridSize, in RoomVisualCtx ctx)
        {
            var cellSize = CellSize;
            var halfCell = cellSize * 0.5f;
            var gd = gridSize.y;

            var roomCells = new System.Collections.Generic.List<int>[rooms.Count];
            for (var ri = 0; ri < rooms.Count; ri++)
                roomCells[ri] = new System.Collections.Generic.List<int>();

            for (var iz = 0; iz < gridSize.y; iz++)
                for (var ix = 0; ix < gridSize.x; ix++)
                {
                    var ri = roomAt[ix, iz];
                    if (ri >= 0 && ri < rooms.Count)
                        roomCells[ri].Add(ix * gd + iz);
                }

            for (var ri = 0; ri < rooms.Count; ri++)
            {
                if (roomCells[ri].Count == 0) continue;
                var room = rooms[ri];
                var quadCount = roomCells[ri].Count;
                var meshVerts = new Vector3[quadCount * 4];
                var meshTris = new int[quadCount * 6];

                for (var q = 0; q < quadCount; q++)
                {
                    var idx = roomCells[ri][q];
                    var ix = idx / gd;
                    var iz = idx % gd;
                    var cx = ix * cellSize + ctx.OriginOffset.x;
                    var cz = iz * cellSize + ctx.OriginOffset.z;
                    var inset = 0.02f;
                    var hi = halfCell - inset;
                    meshVerts[q * 4 + 0] = new Vector3(cx - hi, 0f, cz - hi);
                    meshVerts[q * 4 + 1] = new Vector3(cx + hi, 0f, cz - hi);
                    meshVerts[q * 4 + 2] = new Vector3(cx + hi, 0f, cz + hi);
                    meshVerts[q * 4 + 3] = new Vector3(cx - hi, 0f, cz + hi);
                    var b = q * 4;
                    meshTris[q * 6 + 0] = b; meshTris[q * 6 + 1] = b + 2; meshTris[q * 6 + 2] = b + 1;
                    meshTris[q * 6 + 3] = b; meshTris[q * 6 + 4] = b + 3; meshTris[q * 6 + 5] = b + 2;
                }

                var mesh = new Mesh { name = $"RoomCells_{room.Name}_L{ctx.LevelIndex}" };
                mesh.vertices = meshVerts;
                mesh.triangles = meshTris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var go = new GameObject($"RoomVis_{room.Name}_L{ctx.LevelIndex}");
                go.transform.SetParent(ctx.Parent, false);
                go.transform.localPosition = new Vector3(0f, ctx.VisualY, 0f);
                go.transform.localRotation = Quaternion.identity;
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = CreateRoomVisualMaterial(room.Name, ctx.LevelIndex, ResolveRandomizedRoomColor(room.RoomType));
                _bakedMeshes.Add(mesh);
                _bakedMaterials.Add(mr.sharedMaterial);
            }
        }

        private static void CreateBoundingBoxRoomVisuals(
            System.Collections.Generic.List<RoomRegion> rooms, in RoomVisualCtx ctx)
        {
            foreach (var room in rooms)
            {
                var w = (room.Ix1 - room.Ix0) * CellSize;
                var d = (room.Iz1 - room.Iz0) * CellSize;
                if (w <= 0f || d <= 0f) continue;
                var cx = (room.Ix0 + room.Ix1) * 0.5f * CellSize - CellSize * 0.5f + ctx.OriginOffset.x;
                var cz = (room.Iz0 + room.Iz1) * 0.5f * CellSize - CellSize * 0.5f + ctx.OriginOffset.z;

                var hw = w * 0.5f;
                var hd = d * 0.5f;
                var verts = new Vector3[]
                {
                    new Vector3(-hw, 0f, -hd), new Vector3( hw, 0f, -hd),
                    new Vector3( hw, 0f,  hd), new Vector3(-hw, 0f,  hd)
                };
                var tris = new int[] { 0, 2, 1, 0, 3, 2 };

                var mesh = new Mesh { name = $"RoomQuad_{room.Name}_L{ctx.LevelIndex}" };
                mesh.vertices = verts;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var go = new GameObject($"RoomVis_{room.Name}_L{ctx.LevelIndex}");
                go.transform.SetParent(ctx.Parent, false);
                go.transform.localPosition = new Vector3(cx, ctx.VisualY, cz);
                go.transform.localRotation = Quaternion.identity;
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = CreateRoomVisualMaterial(room.Name, ctx.LevelIndex, ResolveRandomizedRoomColor(room.RoomType));
                _bakedMeshes.Add(mesh);
                _bakedMaterials.Add(mr.sharedMaterial);
            }
        }

        private static Material CreateRoomVisualMaterial(string roomName, int levelIndex, Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard"));
            mat.name = $"RoomMat_{roomName}_L{levelIndex}";
            mat.color = color;
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3001;
            }
            return mat;
        }

        private static void PlaceInteriorWalls(
            System.Collections.Generic.List<PlannedInteriorWallSegment> plannedWalls,
            (GameObject wall, GameObject doorway, GameObject interiorDoor) prefabs,
            GenerationContext context,
            Transform parent,
            float wallY,
            Vector3 originOffset)
        {
            foreach (var plannedWall in plannedWalls)
                PlaceInteriorWallSegment(plannedWall, prefabs, context, parent, wallY, originOffset);
        }

        private static void PlaceInteriorWallSegment(
            PlannedInteriorWallSegment plannedWall,
            (GameObject wall, GameObject doorway, GameObject interiorDoor) prefabs,
            GenerationContext context,
            Transform parent,
            float wallY,
            Vector3 originOffset)
        {
            var (prefab, isOverrideDoor) = ResolveInteriorWallPrefab(plannedWall, prefabs);
            if (prefab == null)
                return;

            // Place kit doorway frame behind override doors
            if (isOverrideDoor && prefabs.doorway != null)
                PlaceDoorFrame(plannedWall, prefabs.doorway, parent, wallY, originOffset, context);

            var go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (go == null)
                return;

            go.name = plannedWall.IsDoorway
                ? $"IntWall_Door_{plannedWall.RoomA}_{plannedWall.RoomB}"
                : $"IntWall_{plannedWall.RoomA}_{plannedWall.RoomB}_{plannedWall.SegmentIndex:D2}";
            go.transform.localPosition = new Vector3(
                plannedWall.Segment.Position.x + originOffset.x,
                wallY,
                plannedWall.Segment.Position.z + originOffset.z);
            go.transform.localRotation = plannedWall.Segment.Rotation;

            if (isOverrideDoor)
                ApplyDoorAdapter(go, context, isExterior: false);
        }

        private static (GameObject prefab, bool isOverrideDoor) ResolveInteriorWallPrefab(
            PlannedInteriorWallSegment plannedWall,
            (GameObject wall, GameObject doorway, GameObject interiorDoor) prefabs)
        {
            if (!plannedWall.IsDoorway)
                return (prefabs.wall, false);

            var prefab = prefabs.interiorDoor ?? prefabs.doorway;
            var isOverrideDoor = prefabs.interiorDoor != null && prefab != prefabs.doorway;
            return (prefab, isOverrideDoor);
        }

        private static void PlaceDoorFrame(
            PlannedInteriorWallSegment plannedWall,
            GameObject doorwayPrefab,
            Transform parent,
            float wallY,
            Vector3 originOffset,
            GenerationContext context)
        {
            var doorFrame = PrefabUtility.InstantiatePrefab(doorwayPrefab, parent) as GameObject;
            if (doorFrame == null)
                return;

            doorFrame.name = $"IntWall_DoorFrame_{plannedWall.RoomA}_{plannedWall.RoomB}";
            doorFrame.transform.localPosition = new Vector3(
                plannedWall.Segment.Position.x + originOffset.x, wallY,
                plannedWall.Segment.Position.z + originOffset.z);
            doorFrame.transform.localRotation = plannedWall.Segment.Rotation;
            SetFrameMaterials(doorFrame, context);
        }

        private readonly struct RoomLabelCtx
        {
            public readonly Transform Parent;
            public readonly float FloorY;
            public readonly int FloorIndex;
            public readonly Vector3 OriginOffset;

            public RoomLabelCtx(Transform parent, float floorY, int floorIndex, Vector3 originOffset)
            {
                Parent = parent;
                FloorY = floorY;
                FloorIndex = floorIndex;
                OriginOffset = originOffset;
            }
        }

        private static void CreateRoomLabels(System.Collections.Generic.List<RoomRegion> rooms,
            int[,] roomAt, int gridW, int gridD, in RoomLabelCtx ctx)
        {
            for (var ri = 0; ri < rooms.Count; ri++)
            {
                var room = rooms[ri];
                var (ix0, iz0, ix1, iz1) = ComputeRoomBounds(room, roomAt, gridW, gridD, ri);
                PlaceRoomLabel(room, ix0, iz0, ix1, iz1, in ctx);
            }
        }

        private static void PlaceRoomLabel(RoomRegion room,
            int ix0, int iz0, int ix1, int iz1, in RoomLabelCtx ctx)
        {
            var label = new GameObject($"{room.Name}_L{ctx.FloorIndex}");
            label.transform.SetParent(ctx.Parent, false);
            label.transform.localPosition = new Vector3(
                (ix0 + ix1) * 0.5f * CellSize - CellSize * 0.5f + ctx.OriginOffset.x,
                ctx.FloorY + 1f,
                (iz0 + iz1) * 0.5f * CellSize - CellSize * 0.5f + ctx.OriginOffset.z);

            var volume = label.AddComponent<RoomVolume>();
            volume.roomName = room.Name;
            volume.floorIndex = ctx.FloorIndex;
            volume.boundsMin =
                new Vector3(ix0 * CellSize - CellSize * 0.5f + ctx.OriginOffset.x,
                    ctx.FloorY,
                    iz0 * CellSize - CellSize * 0.5f + ctx.OriginOffset.z);
            volume.boundsMax =
                new Vector3(ix1 * CellSize - CellSize * 0.5f + ctx.OriginOffset.x,
                    ctx.FloorY,
                    iz1 * CellSize - CellSize * 0.5f + ctx.OriginOffset.z);
        }
        private static (int ix0, int iz0, int ix1, int iz1) ComputeRoomBounds(
            RoomRegion room, int[,] roomAt, int gridW, int gridD, int ri)
        {
            if (roomAt == null || roomAt.GetLength(0) != gridW || roomAt.GetLength(1) != gridD)
                return (room.Ix0, room.Iz0, room.Ix1, room.Iz1);

            var bounds = TryScanRoomBounds(roomAt, gridW, gridD, ri);
            return bounds.found
                ? (bounds.ix0, bounds.iz0, bounds.ix1, bounds.iz1)
                : (room.Ix0, room.Iz0, room.Ix1, room.Iz1);
        }

        private static (bool found, int ix0, int iz0, int ix1, int iz1) TryScanRoomBounds(
            int[,] roomAt, int gridW, int gridD, int ri)
        {
            var ix0 = gridW; var iz0 = gridD; var ix1 = 0; var iz1 = 0;
            var any = false;
            for (var ix = 0; ix < gridW; ix++)
                any |= ScanRoomColumn(roomAt, gridD, ri, ix, ref ix0, ref iz0, ref ix1, ref iz1);

            return (any, ix0, iz0, ix1, iz1);
        }

        private static bool ScanRoomColumn(
            int[,] roomAt, int gridD, int ri, int ix,
            ref int ix0, ref int iz0, ref int ix1, ref int iz1)
        {
            var found = false;
            for (var iz = 0; iz < gridD; iz++)
            {
                if (roomAt[ix, iz] != ri) continue;
                found = true;
                ix0 = Mathf.Min(ix0, ix);
                iz0 = Mathf.Min(iz0, iz);
                ix1 = Mathf.Max(ix1, ix + 1);
                iz1 = Mathf.Max(iz1, iz + 1);
            }
            return found;
        }


    }
}
#endif
