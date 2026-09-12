#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    /// Repairs broken script references on the World Generation authoring scene after project moves.
    /// </summary>
    internal static class WorldGenerationSceneRepair
    {
        private const string MenuPath = "Tools/World/Repair World Generation Scene References";

        [MenuItem(MenuPath)]
        private static void RepairActiveScene()
        {
            var worldGen = GameObject.Find("WorldGen");
            if (worldGen == null)
            {
                EditorUtility.DisplayDialog(
                    "World Generation Repair",
                    "Open the World Generation scene first — WorldGen root was not found.",
                    "OK");
                return;
            }

            // Only repair the three stack roots — not the full terrain/generated hierarchy.
            var removed = 0;
            removed += PurgeMissingOnObject(worldGen);
            var cityRoadStack = worldGen.transform.Find("CityRoadStack");
            if (cityRoadStack != null)
                removed += PurgeMissingOnObject(cityRoadStack.gameObject);
            var worldBuilderStack = worldGen.transform.Find("WorldBuilderStack");
            if (worldBuilderStack != null)
                removed += PurgeMissingOnObject(worldBuilderStack.gameObject);

            var builder = worldGen.GetComponent<CityPrefabRoadNetworkBuilder>();
            if (builder == null)
                builder = Undo.AddComponent<CityPrefabRoadNetworkBuilder>(worldGen);

            CityPrefabRoadStackProvisioner.SetupHubScene(builder);
            WorldBuilderStackProvisioner.EnsureForBuilder(builder);
            builder.RefreshReferences();

            EditorUtility.SetDirty(worldGen);
            EditorSceneManager.MarkSceneDirty(worldGen.scene);

            EditorUtility.DisplayDialog(
                "World Generation Repair",
                "Removed " + removed + " broken script slot(s) and re-provisioned road/world-builder stacks.\n\n" +
                "Save the scene when Unity finishes (this scene is large and may take a minute).",
                "OK");
        }

        private static int PurgeMissingOnObject(GameObject go)
        {
            var removed = 0;
            while (true)
            {
                var components = go.GetComponents<Component>();
                var missingIndex = -1;
                for (var i = 0; i < components.Length; i++)
                {
                    if (components[i] != null)
                        continue;

                    missingIndex = i;
                    break;
                }

                if (missingIndex < 0)
                    break;

                var serialized = new SerializedObject(go);
                var prop = serialized.FindProperty("m_Component");
                prop.DeleteArrayElementAtIndex(missingIndex);
                prop.DeleteArrayElementAtIndex(missingIndex);
                serialized.ApplyModifiedProperties();
                removed++;
            }

            return removed;
        }
    }
}
#endif
