using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.UI;

namespace Zombera.Editor
{
    /// <summary>
    /// Applies recommended runtime headshot portrait defaults to all SquadPortraitStrip instances in open scenes.
    /// </summary>
    public static class SquadPortraitStripTool
    {
        [MenuItem("Tools/Zombera/UI/Portraits/Auto Apply Headshot + Cache Defaults (Open Scenes)")]
        private static void AutoApplyHeadshotAndCacheDefaultsInOpenScenes()
        {
            SquadPortraitStrip[] strips = Object.FindObjectsByType<SquadPortraitStrip>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (strips == null || strips.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Auto Apply Portrait Defaults",
                    "No SquadPortraitStrip components were found in open scenes.",
                    "OK");
                return;
            }

            int updatedCount = 0;
            var touchedSceneHandles = new HashSet<int>();

            for (int i = 0; i < strips.Length; i++)
            {
                SquadPortraitStrip strip = strips[i];
                if (strip == null)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(strip);
                bool changed = false;

                changed |= SetBool(so, "useRuntimeHeadshotCapture", true);
                changed |= SetInt(so, "runtimeHeadshotResolution", 256);
                changed |= SetFloat(so, "runtimeHeadshotDistance", 0.62f);
                changed |= SetFloat(so, "runtimeHeadshotVerticalOffset", 0.03f);
                changed |= SetFloat(so, "runtimeHeadshotLookOffset", 0.05f);
                changed |= SetFloat(so, "runtimeHeadshotFieldOfView", 24f);
                changed |= SetBool(so, "runtimeHeadshotUseFillLight", true);
                changed |= SetFloat(so, "runtimeHeadshotFillLightIntensity", 1.2f);
                changed |= SetColor(so, "runtimeHeadshotFillLightColor", new Color(1f, 0.98f, 0.94f, 1f));

                if (!changed)
                {
                    continue;
                }

                Undo.RecordObject(strip, "Auto Apply Squad Portrait Defaults");
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(strip);
                updatedCount++;

                Scene scene = strip.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded)
                {
                    touchedSceneHandles.Add(scene.handle);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            Debug.Log(
                $"[SquadPortraitStripTool] Auto-apply complete. Found={strips.Length}, Updated={updatedCount}, ScenesTouched={touchedSceneHandles.Count}.");

            EditorUtility.DisplayDialog(
                "Auto Apply Portrait Defaults",
                $"Processed SquadPortraitStrip components: {strips.Length}\n" +
                $"Updated: {updatedCount}\n" +
                $"Scenes touched: {touchedSceneHandles.Count}\n\n" +
                "Headshot capture defaults and portrait cache settings are now aligned.",
                "OK");
        }

        private static bool SetBool(SerializedObject so, string propertyPath, bool value)
        {
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean || property.boolValue == value)
            {
                return false;
            }

            property.boolValue = value;
            return true;
        }

        private static bool SetInt(SerializedObject so, string propertyPath, int value)
        {
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Integer || property.intValue == value)
            {
                return false;
            }

            property.intValue = value;
            return true;
        }

        private static bool SetFloat(SerializedObject so, string propertyPath, float value)
        {
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Float || Mathf.Approximately(property.floatValue, value))
            {
                return false;
            }

            property.floatValue = value;
            return true;
        }

        private static bool SetColor(SerializedObject so, string propertyPath, Color value)
        {
            SerializedProperty property = so.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Color || property.colorValue == value)
            {
                return false;
            }

            property.colorValue = value;
            return true;
        }
    }
}
