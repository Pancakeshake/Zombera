#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    internal static class CityPrefabRoadHubSetup
    {
        private const string MenuPath = "Tools/World/Roads/Authoring/Setup City Prefab Road Hub";

        [MenuItem(MenuPath, priority = -500)]
        private static void SetupActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("City Prefab Road Hub", "Open the City Prefab Creation Hub scene first.", "OK");
                return;
            }

            var hub = Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>();
            if (hub == null)
            {
                var plane = GameObject.Find("Plane");
                var position = plane != null ? plane.transform.position : Vector3.zero;

                var go = new GameObject("CityRoadNetwork");
                Undo.RegisterCreatedObjectUndo(go, "Create City Road Network Hub");
                go.transform.position = position;
                hub = Undo.AddComponent<CityPrefabRoadNetworkBuilder>(go);
            }

            CityPrefabRoadStackProvisioner.SetupHubScene(hub);
            Selection.activeGameObject = hub.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);

            var hasRoadNetwork = GameObject.Find("Road Network") != null;
            EditorUtility.DisplayDialog(
                "City Prefab Road Hub",
                "City prefab road stack ready (no WorldManager required).\n\n" +
                "ProceduralRoadSystem: added under CityRoadStack child.\n" +
                "EasyRoads Road Network: " + (hasRoadNetwork ? "found" : "MISSING — create via GameObject > 3D Object > EasyRoads3D > New Road Network") + ".\n\n" +
                "Select CityRoadNetwork and click Generate City Roads.",
                "OK");
        }
    }
}
#endif
