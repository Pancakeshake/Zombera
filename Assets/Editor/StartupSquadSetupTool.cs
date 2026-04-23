using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;

namespace Zombera.Editor
{
    /// <summary>
    /// Configures and validates startup test-squad wiring for world sessions.
    /// </summary>
    public static class StartupSquadSetupTool
    {
        private static readonly int[] RecommendedSkillTiers =
        {
            1, 10, 15, 20, 25,
            30, 35, 40, 45, 50,
            55, 60, 65, 70, 75,
            80, 85, 90, 95, 100
        };

        [MenuItem("Tools/Zombera/Squad/Apply 20-Character Startup Test Squad Defaults")]
        private static void ApplyTwentyCharacterStartupTestDefaults()
        {
            PlayerSpawner spawner = FindPlayerSpawnerInActiveScene();
            if (spawner == null)
            {
                EditorUtility.DisplayDialog(
                    "Startup Squad Setup",
                    "No PlayerSpawner was found in the active scene.",
                    "OK");
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(spawner);
            bool changed = false;
            bool assignedSquadPrefabFallback = false;

            changed |= SetBool(serializedSpawner, "spawnStartupSquadOnWorldStart", true);
            changed |= SetInt(serializedSpawner, "startupSquadTotalCount", 20);
            changed |= SetInt(serializedSpawner, "startupInitialCharacterCount", 20);
            changed |= SetInt(serializedSpawner, "minimumStartupSquadTotalCount", 20);
            changed |= SetFloat(serializedSpawner, "startupSquadRingRadius", 4.5f);
            changed |= SetBool(serializedSpawner, "applyStartupSquadSkillTiers", true);
            changed |= SetBool(serializedSpawner, "logStartupSquadSpawning", true);
            changed |= SetIntArray(serializedSpawner, "startupSquadSkillTiers", RecommendedSkillTiers);

            SerializedProperty squadPrefabProperty = serializedSpawner.FindProperty("startupSquadMemberPrefab");
            if (squadPrefabProperty != null && squadPrefabProperty.objectReferenceValue == null)
            {
                SerializedProperty playerPrefabProperty = serializedSpawner.FindProperty("playerPrefab");
                if (playerPrefabProperty != null && playerPrefabProperty.objectReferenceValue != null)
                {
                    squadPrefabProperty.objectReferenceValue = playerPrefabProperty.objectReferenceValue;
                    assignedSquadPrefabFallback = true;
                    changed = true;
                }
            }

            if (changed)
            {
                serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(spawner);
                Scene scene = spawner.gameObject.scene;
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            string resultMessage =
                "Applied startup 20-character test squad defaults to PlayerSpawner.\n\n" +
                "- Spawn startup squad on world start: enabled\n" +
                "- Total roster size: 20 (player + 19 NPCs)\n" +
                "- Initial/minimum roster floor: 20\n" +
                "- Skill tiers: 1, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100\n\n" +
                (assignedSquadPrefabFallback
                    ? "startupSquadMemberPrefab was empty and was set to playerPrefab."
                    : "startupSquadMemberPrefab was left unchanged.");

            EditorUtility.DisplayDialog("Startup Squad Setup", resultMessage, "OK");
        }

        [MenuItem("Tools/Zombera/Squad/Validate Startup Test Squad Wiring")]
        private static void ValidateStartupTestSquadWiring()
        {
            PlayerSpawner spawner = FindPlayerSpawnerInActiveScene();
            if (spawner == null)
            {
                EditorUtility.DisplayDialog(
                    "Startup Squad Validation",
                    "No PlayerSpawner was found in the active scene.",
                    "OK");
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(spawner);
            SerializedProperty playerPrefabProperty = serializedSpawner.FindProperty("playerPrefab");
            SerializedProperty squadPrefabProperty = serializedSpawner.FindProperty("startupSquadMemberPrefab");
            SerializedProperty spawnEnabledProperty = serializedSpawner.FindProperty("spawnStartupSquadOnWorldStart");
            SerializedProperty squadCountProperty = serializedSpawner.FindProperty("startupSquadTotalCount");
            SerializedProperty tiersProperty = serializedSpawner.FindProperty("startupSquadSkillTiers");

            int warnings = 0;
            System.Text.StringBuilder summary = new System.Text.StringBuilder();
            summary.AppendLine("Startup squad wiring validation:");
            summary.AppendLine();

            GameObject playerPrefab = playerPrefabProperty != null
                ? playerPrefabProperty.objectReferenceValue as GameObject
                : null;
            if (playerPrefab == null)
            {
                warnings++;
                summary.AppendLine("- Missing playerPrefab on PlayerSpawner.");
            }
            else
            {
                summary.AppendLine($"- playerPrefab: {playerPrefab.name}");
            }

            bool spawnEnabled = spawnEnabledProperty != null && spawnEnabledProperty.boolValue;
            if (!spawnEnabled)
            {
                warnings++;
                summary.AppendLine("- spawnStartupSquadOnWorldStart is disabled.");
            }
            else
            {
                summary.AppendLine("- Startup squad spawning is enabled.");
            }

            int rosterCount = squadCountProperty != null ? Mathf.Max(1, squadCountProperty.intValue) : 0;
            if (rosterCount < 2)
            {
                warnings++;
                summary.AppendLine("- startupSquadTotalCount is below 2 (needs player + at least one NPC).");
            }
            else
            {
                summary.AppendLine($"- startupSquadTotalCount: {rosterCount}");
            }

            GameObject squadPrefab = squadPrefabProperty != null
                ? squadPrefabProperty.objectReferenceValue as GameObject
                : null;
            if (squadPrefab == null)
            {
                warnings++;
                summary.AppendLine("- startupSquadMemberPrefab is not assigned (runtime falls back to playerPrefab).");
            }
            else
            {
                summary.AppendLine($"- startupSquadMemberPrefab: {squadPrefab.name}");

                if (squadPrefab.GetComponent<Unit>() == null)
                {
                    warnings++;
                    summary.AppendLine("  - Warning: squad prefab has no Unit component.");
                }

                if (squadPrefab.GetComponent<Systems.SquadMember>() == null)
                {
                    warnings++;
                    summary.AppendLine("  - Warning: squad prefab has no SquadMember component (runtime will add one).");
                }
            }

            int tierCount = tiersProperty != null ? tiersProperty.arraySize : 0;
            if (tierCount <= 0)
            {
                warnings++;
                summary.AppendLine("- startupSquadSkillTiers is empty.");
            }
            else
            {
                summary.AppendLine($"- startupSquadSkillTiers count: {tierCount}");
                if (tierCount < rosterCount)
                {
                    warnings++;
                    summary.AppendLine("  - Warning: fewer tiers than roster count; final tier value will be reused.");
                }
            }

            summary.AppendLine();
            summary.AppendLine(warnings == 0
                ? "Validation passed with no warnings."
                : $"Validation finished with {warnings} warning(s). Review details above.");

            EditorUtility.DisplayDialog("Startup Squad Validation", summary.ToString(), "OK");
            Debug.Log("[StartupSquadSetupTool] " + summary);
        }

        private static PlayerSpawner FindPlayerSpawnerInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return null;
            }

            PlayerSpawner[] spawners = Object.FindObjectsByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < spawners.Length; i++)
            {
                PlayerSpawner spawner = spawners[i];
                if (spawner != null && spawner.gameObject.scene == activeScene)
                {
                    return spawner;
                }
            }

            return null;
        }

        private static bool SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean || property.boolValue == value)
            {
                return false;
            }

            property.boolValue = value;
            return true;
        }

        private static bool SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer || property.intValue == value)
            {
                return false;
            }

            property.intValue = value;
            return true;
        }

        private static bool SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Float || Mathf.Abs(property.floatValue - value) <= 0.0001f)
            {
                return false;
            }

            property.floatValue = value;
            return true;
        }

        private static bool SetIntArray(SerializedObject serializedObject, string propertyName, int[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return false;
            }

            bool changed = property.arraySize != values.Length;
            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element == null || element.propertyType != SerializedPropertyType.Integer)
                {
                    continue;
                }

                if (element.intValue != values[i])
                {
                    element.intValue = values[i];
                    changed = true;
                }
            }

            return changed;
        }
    }
}
