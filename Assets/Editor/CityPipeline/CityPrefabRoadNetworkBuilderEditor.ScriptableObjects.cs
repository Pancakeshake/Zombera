#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    internal sealed partial class CityPrefabRoadNetworkBuilderEditor : UnityEditor.Editor
    {
        private const string SoWorldRoot = "Assets/02_Shared/ScriptableObjects/World";

        private static readonly Color DescriptionColor = new(0.50f, 0.60f, 0.75f);
        private static readonly Color HeadingColor = new(0.90f, 0.49f, 0.13f); // amber

        private static readonly Dictionary<string, string> SoPaths = new()
        {
            ["layoutAsset"] = SoWorldRoot + "/CityMathRoadLayoutAsset.asset",
            ["roadNetworkSettings"] = SoWorldRoot + "/RoadNetworkSettings.asset",
            ["streetscapeConfig"] = SoWorldRoot + "/City/CItygen/CityStreetscapeConfig.asset",
            ["regionAsset"] = SoWorldRoot + "/City/CItygen/CityRegion.asset",
            ["buildConfig"] = SoWorldRoot + "/CityBuildConfig.asset",
            ["districtLotTerrainConfig"] = SoWorldRoot + "/DistrictLotTerrainConfig.asset",
            ["districtLotTerrainLayout"] = SoWorldRoot + "/DistrictLotTerrainLayout.asset",
        };

        private static readonly Dictionary<string, string> SoDescriptions = new()
        {
            ["layoutAsset"] =
                "Defines the procedurally generated street grid — city size, block dimensions, "
                + "street spacing, arterial ring placement, and highway exit count.",
            ["roadNetworkSettings"] =
                "Road infrastructure — lane widths, surface materials, junction connectors, terrain "
                + "flattening, footpaths, street lamps, pathfinding, and junction markings.",
            ["streetscapeConfig"] =
                "Above-road content — traffic lights, street signs, lamps, utility poles, power lines, "
                + "parked cars, benches, mailboxes, hydrants, trash cans, trees, and building prefabs. "
                + "Prefabs and placement behaviour together.",
            ["regionAsset"] =
                "Multi-city region — the list of city sites. Enable with the 'Multi-City (Region)' toggle: "
                + "step 1 generates every site in one pass with inter-city highways linking them.",
            ["buildConfig"] =
                "Build-time behaviour flags — test layout mode, transform alignment, "
                + "auto road-stack creation, district generation, building placement, "
                + "decoration scatter, and lot sizes.",
            ["districtLotTerrainConfig"] =
                "Per-district terrain surface definitions — concrete pads, asphalt carparks, "
                + "backyard grass, side paths. Maps each district type to a list of surface materials "
                + "and coverage zones (full lot, front yard, backyard, side strip, carpark).",
            ["districtLotTerrainLayout"] =
                "Per-district lot terrain subdivision geometry — front yard depth, driveway width "
                + "and position, side yard margins, building pad size, footpath width, and the "
                + "surface type assigned to each sub-zone (grass, asphalt, concrete, paver, gravel).",
        };

        private static void DrawScriptableObjectSection(
            SerializedObject serializedObject,
            CityPrefabRoadNetworkBuilder builder)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            var headingStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = HeadingColor } };
            EditorGUILayout.LabelField("Scriptable Objects", headingStyle);
            if (GUILayout.Button("Auto-Wire", GUILayout.Width(80f)))
                AutoWireScriptableObjects(serializedObject);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            DrawSoField(serializedObject, "layoutAsset", "City Math Road Layout");

            EditorGUILayout.Space(6f);
            DrawSoField(serializedObject, "roadNetworkSettings", "Road Network Settings");

            EditorGUILayout.Space(6f);
            DrawSoField(serializedObject, "streetscapeConfig", "Streetscape Config");

            EditorGUILayout.Space(6f);
            DrawSoField(serializedObject, "regionAsset", "City Region (Multi-City)");
            if (builder != null && builder.HasSessionRegionOverride)
            {
                EditorGUILayout.HelpBox(
                    "Build session has a temporary region override (world scatter / site plan). " +
                    "The field above stays as your authored CityRegion.asset.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6f);
            DrawSoField(serializedObject, "buildConfig", "Build Config");

            EditorGUILayout.Space(6f);
            DrawSoField(serializedObject, "districtLotTerrainConfig", "District Lot Terrain Config");

            EditorGUILayout.Space(6f);
            DrawSoField(serializedObject, "districtLotTerrainLayout", "District Lot Terrain Layout");
        }

        private static void DrawSoField(SerializedObject serializedObject, string propertyName, string label)
        {
            var prop = serializedObject.FindProperty(propertyName);
            if (prop == null) return;

            EditorGUILayout.PropertyField(prop, new GUIContent(label));

            if (SoDescriptions.TryGetValue(propertyName, out var description))
            {
                var style = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                {
                    normal = { textColor = DescriptionColor }
                };
                EditorGUILayout.LabelField(description, style);
            }
        }

        private static void AutoWireScriptableObjects(SerializedObject serializedObject)
        {
            var wired = 0;
            foreach (var kvp in SoPaths)
            {
                var prop = serializedObject.FindProperty(kvp.Key);
                if (prop == null || prop.objectReferenceValue != null)
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<Object>(kvp.Value);
                if (asset == null)
                    continue;

                prop.objectReferenceValue = asset;
                wired++;
            }

            if (wired > 0)
            {
                serializedObject.ApplyModifiedProperties();
                Debug.Log($"[CityPrefabRoadNetworkBuilderEditor] Auto-wired {wired} ScriptableObject(s).");
            }
        }
    }
}
#endif
