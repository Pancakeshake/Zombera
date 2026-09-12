#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        /// <summary>
        ///     Stamps the main ground door's outward yaw offset onto the prefab root
        ///     (<see cref="BuildingDoorAnchor"/>) so placement faces the correct wall
        ///     toward the road without guessing which StairSocket is the front door.
        ///     Mapping matches <see cref="CityBuildingRoadFacingUtility.GetDoorYawOffset"/>:
        ///     +Z=0, -Z=180, +X=270, -X=90.
        ///     NOTE: perimeter wall segments face INWARD (their forward points into the
        ///     house, matching exterior-stair placement), so the door's outward
        ///     direction is <c>Rotation * back</c>, not forward.
        /// </summary>
        private static void StampMainDoorAnchor(GameObject root, GenerationPlan plan)
        {
            if (root == null || plan == null)
                return;

            var doorForward = Vector3.zero;
            var found = false;

            for (var l = 0; l < plan.PerimeterWallPlans.Count && !found; l++)
            {
                var levelPlan = plan.PerimeterWallPlans[l];
                if (levelPlan.Level != 0)
                    continue;

                for (var s = 0; s < levelPlan.Segments.Count; s++)
                {
                    var seg = levelPlan.Segments[s];
                    if (!seg.IsDoor)
                        continue;

                    // Wall segments face inward; the door's outward direction is backward.
                    doorForward = seg.Segment.Rotation * Vector3.back;
                    found = true;
                    break;
                }
            }

            var anchor = root.GetComponent<BuildingDoorAnchor>();
            if (anchor == null)
                anchor = root.AddComponent<BuildingDoorAnchor>();

            if (!found)
            {
                anchor.hasDoor = false;
                return;
            }

            doorForward.y = 0f;
            if (doorForward.sqrMagnitude < 0.0001f)
            {
                anchor.hasDoor = false;
                return;
            }

            doorForward.Normalize();
            var yaw = Mathf.Atan2(doorForward.x, doorForward.z) * Mathf.Rad2Deg;
            var offset = ((-Mathf.Round(yaw / 90f) * 90f) % 360f + 360f) % 360f;

            anchor.hasDoor = true;
            anchor.doorYawOffset = offset;
        }

        private static void ApplyDoorAdapter(GameObject doorGo, GenerationContext context, bool isExterior)
        {
            // 1. Scale first so bounds reflect final mesh size
            var kit = context?.Settings?.KitConfig;
            var doorScale = kit != null ? kit.doorScale : (context?.Settings?.DoorScale ?? Vector3.one);
            if (doorScale != Vector3.one)
                doorGo.transform.localScale = doorScale;

            // 2. Measure scaled bounds and adjust Y so the mesh bottom aligns with the doorframe
            var renderers = doorGo.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var combinedBounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    combinedBounds.Encapsulate(renderers[i].bounds);

                // Distance from pivot to bottom of scaled mesh (negative if bottom is below pivot)
                var bottomOffset = combinedBounds.min.y - doorGo.transform.position.y;
                if (Mathf.Abs(bottomOffset) > 0.001f)
                    doorGo.transform.localPosition -= new Vector3(0f, bottomOffset, 0f);

            // All doors need an additional 0.1 Y lift to sit correctly in the frame
            doorGo.transform.localPosition += new Vector3(0f, 0.1f, 0f);
            }

            // 3. Flip rotation 180°: third-party doors often face the opposite direction
            doorGo.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);

            // 4. Assign FreeWoodDoorPack materials so doors don't take wall materials.
            // Exterior doors each get one random material; all interior doors share one pick.
            ApplyRandomDoorMaterials(doorGo, context, isExterior);

            // 5. Fix the sibling door frame renderer so it gets wall materials
            ApplyDoorFrameMaterials(doorGo, context);
        }

        /// <summary>
        ///     Load the wall materials directly from the kit Wall prefab and apply them
        ///     to the SM_Doorway renderer on a door frame GameObject.
        /// </summary>
        private static void SetFrameMaterials(GameObject frameGo, GenerationContext context)
        {
            if (frameGo == null || context == null) return;

            var r = frameGo.GetComponentInChildren<Renderer>(true);
            if (r == null) return;

            // Use the kit's actual wall prefab, or fall back to hardcoded path.
            var wallPrefab = context.Prefabs?.Wall;
            if (wallPrefab == null)
            {
                wallPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Wall.prefab");
            }

            if (wallPrefab == null) return;

            var wallR = wallPrefab.GetComponentInChildren<Renderer>(true);
            if (wallR == null || wallR.sharedMaterials == null || wallR.sharedMaterials.Length == 0) return;

            r.sharedMaterials = wallR.sharedMaterials;
            UnityEditor.EditorUtility.SetDirty(r);
        }

        private static void ApplyDoorFrameMaterials(GameObject doorGo, GenerationContext context)
        {
            var parent = doorGo.transform.parent;
            if (parent == null) return;

            // Find sibling door frame — named with "DoorFrame" instead of "_Door_"
            Transform frameT = null;
            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name.IndexOf("DoorFrame", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    frameT = c;
                    break;
                }
            }
            if (frameT == null || frameT.gameObject == null) return;
            SetFrameMaterials(frameT.gameObject, context);
        }

        private const string FreeWoodDoorMaterialsRoot = "Assets/03_ThirdParty/Free Wood Door Pack/Materials";

        private static void ApplyRandomDoorMaterials(GameObject doorGo, GenerationContext context, bool isExterior)
        {
            var materialPaths = CollectDoorMaterialPaths();
            if (materialPaths.Count == 0)
                return;

            var mat = ResolveDoorMaterial(materialPaths, context, isExterior);
            if (mat == null)
                return;

            ApplyMaterialToAllRenderers(doorGo, mat);
        }

        private static List<string> CollectDoorMaterialPaths()
        {
            var materialPaths = new List<string>();
            if (!System.IO.Directory.Exists(FreeWoodDoorMaterialsRoot))
                return materialPaths;

            foreach (var dir in System.IO.Directory.GetDirectories(FreeWoodDoorMaterialsRoot))
            {
                foreach (var file in System.IO.Directory.GetFiles(dir, "*.mat"))
                {
                    var dirName = System.IO.Path.GetFileName(dir);
                    if (dirName.IndexOf("Knob", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    materialPaths.Add(file);
                }
            }

            return materialPaths;
        }

        private static Material ResolveDoorMaterial(List<string> materialPaths, GenerationContext context, bool isExterior)
        {
            if (isExterior)
            {
                var pick = materialPaths[UnityEngine.Random.Range(0, materialPaths.Count)];
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(pick);
            }

            if (context.CachedInteriorDoorMaterial == null)
            {
                var pick = materialPaths[UnityEngine.Random.Range(0, materialPaths.Count)];
                context.CachedInteriorDoorMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(pick);
            }
            return context.CachedInteriorDoorMaterial;
        }

        private static void ApplyMaterialToAllRenderers(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                    {
                        mats[i] = mat;
                        changed = true;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    UnityEditor.EditorUtility.SetDirty(r);
                }
            }
        }

        private static GameObject ResolvePerimeterWallPrefab(
            GenerationContext context,
            PlannedPerimeterSegment plannedSegment)
        {
            if (plannedSegment.IsDoor)
                return context.Prefabs.ExteriorDoor ?? context.Prefabs.Doorway;

            if (plannedSegment.IsWindow)
                return context.Prefabs.Window;

            return context.Prefabs.Wall;
        }

        private static string ResolvePerimeterWallTag(PlannedPerimeterSegment plannedSegment, GameObject prefab)
        {
            if (plannedSegment.IsDoor)
                return "Door";

            return prefab.name.Replace(".prefab", "").Replace("Building_", string.Empty);
        }

        private static void TryPlaceExteriorStairsForDoor(
            GenerationContext context,
            Transform stairsParent,
            PlannedPerimeterSegment plannedSegment)
        {
            if (context.Prefabs.ExteriorStairs == null)
                return;

            var inward = plannedSegment.Segment.Rotation * Vector3.forward;
            var stairsPos = plannedSegment.Segment.Position - inward * ExteriorStairsConstants.OutwardOffset;
            stairsPos += (plannedSegment.Segment.Rotation * Vector3.right) * ExteriorStairsConstants.LateralOffset;
            stairsPos.y = ExteriorStairsConstants.YOffset;

            var exteriorStairs = PrefabUtility.InstantiatePrefab(context.Prefabs.ExteriorStairs, stairsParent) as GameObject;
            if (exteriorStairs == null)
                return;

            exteriorStairs.name = $"ExteriorStairs_Door_{plannedSegment.SegmentIndex:D2}";
            exteriorStairs.transform.localPosition = stairsPos;
            exteriorStairs.transform.localRotation =
                plannedSegment.Segment.Rotation * Quaternion.Euler(0f, 180f, 0f);

            var stairSocket = new GameObject($"StairSocket_{plannedSegment.SegmentIndex:D2}");
            stairSocket.transform.SetParent(stairsParent, false);
            stairSocket.transform.localPosition = stairsPos;
            stairSocket.transform.localRotation = Quaternion.identity;
        }

    }
}
#endif
