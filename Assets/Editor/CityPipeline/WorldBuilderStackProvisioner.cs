#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    /// Scene-visible World Builder stack (service + tile catalog) for authoring scenes.
    /// Runtime provisioning is handled by <see cref="WorldBuilderStackBootstrap"/> via pipeline.
    /// </summary>
    internal static class WorldBuilderStackProvisioner
    {
        private const string StackChildName = "WorldBuilderStack";
        private const string DefaultProfilePath =
            "Assets/02_Shared/ScriptableObjects/World/WorldGenerationProfile.asset";

        [MenuItem("Tools/World/Provision World Builder Stack")]
        public static void ProvisionSelectedOrFindBuilder()
        {
            var builder = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<CityPrefabRoadNetworkBuilder>()
                : null;
            builder ??= Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>();
            if (builder == null)
            {
                Debug.LogWarning("[WorldBuilderStackProvisioner] No CityPrefabRoadNetworkBuilder found.");
                return;
            }

            EnsureForBuilder(builder);
        }

        public static WorldBuilderService TryGetForBuilder(CityPrefabRoadNetworkBuilder builder)
        {
            if (builder == null)
                return null;

            var stackRoot = builder.transform.Find(StackChildName);
            return stackRoot != null ? stackRoot.GetComponent<WorldBuilderService>() : null;
        }

        public static WorldBuilderService EnsureForBuilder(CityPrefabRoadNetworkBuilder builder)
        {
            if (builder == null) return null;

            var scene = builder.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            var stackRoot = builder.transform.Find(StackChildName);
            if (stackRoot == null)
            {
                var stackObject = new GameObject(StackChildName);
                Undo.RegisterCreatedObjectUndo(stackObject, "Create World Builder Stack");
                stackObject.transform.SetParent(builder.transform, false);
                stackRoot = stackObject.transform;
            }

            var service = stackRoot.GetComponent<WorldBuilderService>();
            if (service == null)
                service = Undo.AddComponent<WorldBuilderService>(stackRoot.gameObject);

            var profile = service.Profile ?? LoadDefaultProfile();
            var changed = WorldBuilderStackBootstrap.Ensure(service, builder, profile);

            if (!changed && WorldBuilderStackBootstrap.IsComplete(service))
                return service;

            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                "[WorldBuilderStackProvisioner] World Builder stack ensured (same runtime path as pipeline).",
                service);
            return service;
        }

        private static WorldGenerationProfile LoadDefaultProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorldGenerationProfile>(DefaultProfilePath);
            if (profile != null)
                return profile;

            var guids = AssetDatabase.FindAssets("t:WorldGenerationProfile");
            if (guids.Length == 0)
                return null;

            return AssetDatabase.LoadAssetAtPath<WorldGenerationProfile>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
#endif
