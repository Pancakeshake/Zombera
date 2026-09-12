#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        private static void PlaceGuttering(GenerationContext context, Transform roofParent)
        {
            // Commercial flat/parapet roofs: no eaves guttering or downpipes.
            if (context.BuildingCategory == CityDistrictType.Commercial)
                return;

            var prefs = context.Prefabs;
            if (prefs.Gutter3m == null && prefs.Downpipe == null) return;

            var plan = context.Plan;
            var topLevel = plan.FloorCount - 1;
            var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
            var wallBaseY = LevelFloorSurfaceY(topLevel);
            var buildW = w * CellSize;
            var buildD = d * CellSize;

            Debug.Log($"[PlaceGuttering] off=({off.x:F3},{off.y:F3},{off.z:F3}) w={w} d={d} buildW={buildW:F3} buildD={buildD:F3} wallBaseY={wallBaseY:F3}");

            var gutterXOffset = 0f;
            var gutterYOffset = 0f;
            var gutterZOffset = 0f;
            var roofConfig = context.ActiveRoofType;
            if (roofConfig != null && roofConfig.Style == RoofStyle.Flat)
            {
                // Parapet roofs don't need exposed gutters
                if (roofConfig.ParapetYScale > 0f)
                    return;

                gutterXOffset = roofConfig.FlatRoofGutterXOffset;
                gutterYOffset = roofConfig.FlatRoofGutterYOffset;
                gutterZOffset = roofConfig.FlatRoofGutterZOffset;
            }

            // Shed roofs don't need exposed gutters
            if (roofConfig != null && roofConfig.Style == RoofStyle.Shed)
                return;

            PlaceGutterSegments(prefs, roofParent, d, off, wallBaseY, buildW, gutterXOffset, gutterYOffset, gutterZOffset);
            PlaceGutterBrackets(prefs, roofParent, d);
            PlaceDownpipesOnGutters(prefs, roofParent);
        }

        private static void PlaceGutterSegments(PrefabDependencies prefs, Transform roofParent, int cellCount, Vector3 off, float wallY, float buildW, float xOffset = 0f, float yOffset = 0f, float zOffset = 0f)
        {
            if (prefs.Gutter3m == null) return;

            for (var iz = 0; iz < cellCount; iz++)
            {
                var gz = off.z + iz * CellSize + zOffset;
                var lPos = new Vector3(off.x - 1.7f + xOffset, wallY + yOffset, gz);
                PlaceGutterPiece(prefs.Gutter3m, roofParent, $"Gutter_L_{iz}", lPos, Quaternion.Euler(0f, 270f, 0f));

                var rPos = new Vector3(off.x + buildW - 1.3f + xOffset, wallY + yOffset, gz);
                PlaceGutterPiece(prefs.Gutter3m, roofParent, $"Gutter_R_{iz}", rPos, Quaternion.Euler(0f, 90f, 0f));

                if (iz == 0)
                    Debug.Log($"[PlaceGuttering] iz=0: L={lPos} R={rPos}");
            }
        }

        private static void PlaceGutterBrackets(PrefabDependencies prefs, Transform roofParent, int cellCount)
        {
            if (prefs.GutterBrackets == null) return;

            for (var iz = 0; iz < cellCount; iz++)
            {
                var gutterLeft = roofParent.Find($"Gutter_L_{iz}");
                if (gutterLeft != null)
                    PlaceGutterPiece(prefs.GutterBrackets, gutterLeft, $"Gutter_Bracket_L_{iz}", Vector3.zero, Quaternion.identity);

                var gutterRight = roofParent.Find($"Gutter_R_{iz}");
                if (gutterRight != null)
                    PlaceGutterPiece(prefs.GutterBrackets, gutterRight, $"Gutter_Bracket_R_{iz}", Vector3.zero, Quaternion.identity);
            }
        }

        private static void PlaceDownpipesOnGutters(PrefabDependencies prefs, Transform roofParent)
        {
            if (prefs.Downpipe == null) return;

            var gutterLeft0 = roofParent.Find("Gutter_L_0");
            if (gutterLeft0 != null)
                PlaceGutterPiece(prefs.Downpipe, gutterLeft0, "Downpipe_L", new Vector3(0.3f, 0f, 0f), Quaternion.identity);

            var gutterRight0 = roofParent.Find("Gutter_R_0");
            if (gutterRight0 != null)
                PlaceGutterPiece(prefs.Downpipe, gutterRight0, "Downpipe_R", new Vector3(2.6f, 0f, 0f), Quaternion.identity);
        }

        private static void PlaceGutterPiece(GameObject prefab, Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (go == null) return;
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
        }

        /// <summary>
        ///     Applies scale, Y floor-offset, rotation correction, and random FreeWoodDoorPack materials
        ///     to a third-party door prefab so it fits the modular wall opening.
        /// </summary>
    }
}
#endif
