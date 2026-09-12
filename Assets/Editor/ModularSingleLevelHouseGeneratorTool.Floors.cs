#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        private static void PlaceFoundationsAndFloors(GenerationContext context, Transform[] floorLevelParents)
        {
            PlaceGroundFoundationsAndFloors(context, floorLevelParents[0]);
            PlaceUpperLevelFloors(context, floorLevelParents);
        }

        private static void PlaceGroundFoundationsAndFloors(GenerationContext context, Transform groundFloorParent)
        {
            var plan = context.Plan;
            for (var ix = 0; ix < plan.Width; ix++)
            {
                for (var iz = 0; iz < plan.Depth; iz++)
                    PlaceGroundFoundationAndFloor(context, groundFloorParent, ix, iz);
            }
        }

        private static void PlaceGroundFoundationAndFloor(
            GenerationContext context,
            Transform floorsParent,
            int ix,
            int iz)
        {
            var basePos = new Vector3(ix * CellSize, 0f, iz * CellSize);

            var foundationGo = PrefabUtility.InstantiatePrefab(context.Prefabs.Foundation, floorsParent) as GameObject;
            if (foundationGo == null)
                return;

            foundationGo.name = $"Foundation_L0_{ix}_{iz}";
            foundationGo.transform.localPosition = basePos;
            foundationGo.transform.localRotation = Quaternion.identity;

            var floorGo = PrefabUtility.InstantiatePrefab(context.Prefabs.Floor, floorsParent) as GameObject;
            if (floorGo == null)
                return;

            floorGo.name = $"Floor_L0_{ix}_{iz}";
            floorGo.transform.localPosition = basePos + new Vector3(0f, LevelFloorSurfaceY(0), 0f);
            floorGo.transform.localRotation = Quaternion.identity;
        }

        private static void PlaceUpperLevelFloors(GenerationContext context, Transform[] floorLevelParents)
        {
            var plan = context.Plan;
            for (var level = 1; level < plan.FloorCount; level++)
                PlaceUpperLevelFloorGrid(context, floorLevelParents[level], level);
        }

        private static void PlaceUpperLevelFloorGrid(GenerationContext context, Transform levelParent, int level)
        {
            var plan = context.Plan;
            var levelWidth = plan.SkyscraperMode ? plan.FloorWidths[level] : plan.Width;
            var levelDepth = plan.SkyscraperMode ? plan.FloorDepths[level] : plan.Depth;
            var offset = plan.SkyscraperMode ? plan.FloorOffsets[level] : Vector3.zero;
            var floorY = LevelFloorSurfaceY(level);
            var slabPrefab = context.Prefabs.UpperFloor != null ? context.Prefabs.UpperFloor : context.Prefabs.Floor;
            var slabLabel = context.Prefabs.UpperFloor != null ? "UpperFloor" : "Floor";

            for (var ix = 0; ix < levelWidth; ix++)
            {
                for (var iz = 0; iz < levelDepth; iz++)
                {
                    if (plan.SkipUpperLandingFloors.Contains(new FloorCell(level, ix, iz)))
                        continue;

                    PlaceUpperFloorSlab(
                        slabPrefab,
                        levelParent,
                        slabLabel,
                        level,
                        (ix, iz),
                        offset,
                        floorY);
                }
            }
        }

        private static void PlaceUpperFloorSlab(
            GameObject slabPrefab,
            Transform floorsParent,
            string slabLabel,
            int level,
            (int x, int z) cell,
            Vector3 offset,
            float floorY)
        {
            var floorGo = PrefabUtility.InstantiatePrefab(slabPrefab, floorsParent) as GameObject;
            if (floorGo == null)
                return;

            floorGo.name = $"{slabLabel}_L{level}_{cell.x}_{cell.z}";
            floorGo.transform.localPosition = new Vector3(
                cell.x * CellSize + offset.x,
                floorY,
                cell.z * CellSize + offset.z);
            floorGo.transform.localRotation = Quaternion.identity;
        }

        private static void PlaceStairs(GenerationContext context, Transform stairsParent)
        {
            if (!context.Plan.NeedsStairs || context.Prefabs.Stair == null)
                return;

            var plan = context.Plan;

            for (var s = 0; s < plan.StairTransitions.Length; s++)
            {
                var transition = plan.StairTransitions[s];
                // Use the UPPER floor's offset so the stair top aligns with the floor hole.
                // The hole is cut in the upper floor's local grid, which is offset from origin.
                var upperLevel = transition.LowerLevel + 1;
                var upperOffset = plan.SkyscraperMode ? plan.FloorOffsets[upperLevel] : Vector3.zero;
                var cx = transition.Ix * CellSize + upperOffset.x;
                var cz = transition.Iz * CellSize + upperOffset.z;
                var go = PrefabUtility.InstantiatePrefab(context.Prefabs.Stair, stairsParent) as GameObject;
                if (go == null) continue;
                go.name = $"Stair_L{transition.LowerLevel}_to_L{upperLevel}_{s}";
                go.transform.localPosition = new Vector3(cx, 0f, cz);
                go.transform.localRotation = Quaternion.Euler(0f, transition.RotationY, 0f);
                var walkY = LevelFloorSurfaceY(transition.LowerLevel) + FloorSlabTopAboveRoot;
                AlignRenderersBottomToY(go, walkY);
            }
        }

        private static void PlaceFuseBox(GenerationContext context, Transform wallsParent)
        {
            var kit = context.Settings?.KitConfig;
            var folder = kit?.fuseBoxSourceFolder;
            if (string.IsNullOrWhiteSpace(folder))
                return;

            var prefab = ResolveDoorSource(folder, context.Random);
            if (prefab == null)
                return;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (go == null)
                return;

            // Pick any ground-floor wall segment that isn't a door or window.
            var candidates = new System.Collections.Generic.List<Transform>();
            for (var i = 0; i < wallsParent.childCount; i++)
            {
                var child = wallsParent.GetChild(i);
                var n = child.name;
                if (n.StartsWith("Wall_L0_", System.StringComparison.Ordinal)
                    && !n.Contains("_Door") && !n.Contains("_Window"))
                {
                    candidates.Add(child);
                }
            }

            // Commercial: fuseboxes go on the wall opposite the storefront only
            // (shops snap side-to-side, so side walls stay clear).
            if (context.BuildingCategory == CityDistrictType.Commercial)
                candidates = FilterFuseBoxCandidatesToBackWall(context, candidates);

            if (candidates.Count == 0) return;
            var wallTransform = candidates[context.Random.RangeExclusive(0, candidates.Count)];

            var fuseBox = (GameObject)PrefabUtility.InstantiatePrefab(go, wallTransform);
            fuseBox.name = $"FuseBox_{go.name}";
            fuseBox.transform.localPosition = new Vector3(0f, 1f, -0.02f);
            fuseBox.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Placed fusebox '{go.name}' on '{wallTransform.name}'");
        }

        private static System.Collections.Generic.List<Transform> FilterFuseBoxCandidatesToBackWall(
            GenerationContext context, System.Collections.Generic.List<Transform> candidates)
        {
            var plan = context.Plan;
            var doorIndex = plan.PrimaryDoorSegmentIndex;
            var width = plan.Width;
            var depth = plan.Depth;
            if (doorIndex < 0 || width <= 0 || depth <= 0)
                return candidates;

            int frontStart;
            if (doorIndex < width) frontStart = 0;
            else if (doorIndex < 2 * width) frontStart = width;
            else if (doorIndex < 2 * width + depth) frontStart = 2 * width;
            else frontStart = 2 * width + depth;

            int backStart;
            if (frontStart == 0) backStart = width;
            else if (frontStart == width) backStart = 0;
            else if (frontStart == 2 * width) backStart = 2 * width + depth;
            else backStart = 2 * width;

            var backLength = frontStart < 2 * width ? width : depth;

            var filtered = new System.Collections.Generic.List<Transform>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var idx = ParseWallSegmentIndex(candidates[i].name);
                if (idx >= backStart && idx < backStart + backLength)
                    filtered.Add(candidates[i]);
            }

            if (filtered.Count == 0)
                Debug.Log($"[ModularSingleLevelHouseGeneratorTool] No fusebox wall on the back side — skipping fusebox.");

            return filtered;
        }

        private static int ParseWallSegmentIndex(string wallName)
        {
            var marker = "_L0_";
            var start = wallName.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0)
                return -1;

            var tail = wallName.Substring(start + marker.Length);
            var length = 0;
            while (length < tail.Length && char.IsDigit(tail[length]))
                length++;

            return length > 0 ? int.Parse(tail.Substring(0, length)) : -1;
        }

    }
}
#endif
