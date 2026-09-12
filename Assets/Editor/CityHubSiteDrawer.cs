#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    ///     Draws a <see cref="CityHubSite"/> in the City Region inspector. Picking a
    ///     City Type applies that type's footprint / exit-count / district preset so
    ///     sites stay consistent with their size class.
    /// </summary>
    [CustomPropertyDrawer(typeof(CityHubSite))]
    public sealed class CityHubSiteDrawer : PropertyDrawer
    {
        private const float RowSpacing = 2f;

        private static readonly string[] FieldNames =
        {
            "siteType", "displayName", "centerXZ", "layoutSeed",
            "halfWidthMeters", "halfDepthMeters", "generateStreetGrid",
            "generateArterialRing", "generateHighwayExits", "guaranteedHighwayExitCount",
            "connectToRegionHighways"
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight;
            var fixedHeight = (FieldNames.Length + 1) * (line + RowSpacing) + 2f;
            var mixProp = property.FindPropertyRelative("districtMix");
            return fixedHeight + EditorGUI.GetPropertyHeight(mixProp, GUIContent.none, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.serializedObject.Update();

            var line = EditorGUIUtility.singleLineHeight;
            var y = position.y + 2f;

            var typeProp = property.FindPropertyRelative("siteType");
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, line), typeProp, new GUIContent("City Type"));
            if (EditorGUI.EndChangeCheck())
            {
                var type = (CitySiteType)typeProp.enumValueIndex;
                property.FindPropertyRelative("halfWidthMeters").floatValue = CitySiteTypePresets.HalfWidth(type);
                property.FindPropertyRelative("halfDepthMeters").floatValue = CitySiteTypePresets.HalfDepth(type);
                property.FindPropertyRelative("guaranteedHighwayExitCount").intValue =
                    CitySiteTypePresets.GuaranteedExitCount(type);
                ApplyMixPreset(property.FindPropertyRelative("districtMix"), type);
                property.serializedObject.ApplyModifiedProperties();
            }

            y += line + RowSpacing;

            for (var i = 1; i < FieldNames.Length; i++)
            {
                var child = property.FindPropertyRelative(FieldNames[i]);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), child, true);
                y += line + RowSpacing;
            }

            var mixProp = property.FindPropertyRelative("districtMix");
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(mixProp, GUIContent.none, true)),
                mixProp, new GUIContent("District Mix (weights)"), true);
        }

        private static void ApplyMixPreset(SerializedProperty mixProp, CitySiteType type)
        {
            var preset = CitySiteTypePresets.BuildDefaultMix(type);
            mixProp.ClearArray();
            mixProp.arraySize = preset.Count;
            for (var i = 0; i < preset.Count; i++)
            {
                var entry = mixProp.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("district").enumValueIndex = (int)preset[i].district;
                entry.FindPropertyRelative("weight").floatValue = preset[i].weight;
            }
        }
    }
}
#endif
