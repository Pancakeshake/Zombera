using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.BuildingSystem;
using Zombera.World;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        /// <summary>
        ///     When MainMenu is shown, drop any other loaded world scenes (e.g. editor multi-scene or stray additivity)
        ///     so World systems do not run beside character creation.
        /// </summary>
        private void UnloadLoadedWorldScenesLeavingMainMenu(Scene mainMenuScene)
        {
            if (!mainMenuScene.IsValid() || !mainMenuScene.isLoaded) return;

            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var candidate = SceneManager.GetSceneAt(i);

                if (!candidate.IsValid() || !candidate.isLoaded || candidate.handle == mainMenuScene.handle) continue;

                if (!ShouldUnloadForMainMenu(candidate)) continue;

                var unload = SceneManager.UnloadSceneAsync(candidate);
                if (unload == null)
                    Debug.LogWarning(
                        $"[GameManager] Failed to unload world scene '{candidate.name}' while presenting main menu.",
                        this);
            }
        }

        private bool ShouldUnloadForMainMenu(Scene scene)
        {
            if (IsLoadingScene(scene)) return true;
            if (IsWorldScene(scene)) return true;
            if (SceneContainsWorldRuntimeMarkers(scene)) return true;
            return false;
        }

        private static bool SceneContainsWorldRuntimeMarkers(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root == null) continue;
                if (root.GetComponentInChildren<Terrain>(true) != null) return true;
                if (root.GetComponentInChildren<WorldManager>(true) != null) return true;
                if (root.GetComponentInChildren<WorldTileStreamSource>(true) != null) return true;
                if (root.GetComponentInChildren<WorldStreamedCityBuilder>(true) != null) return true;
            }

            return false;
        }

        private void ApplyWorldRuntimeStateGuards(GameState state, string source)
        {
            if (!enforceMainMenuWorldRuntimeGuards) return;

            var shouldEnableWorldRuntime = IsWorldRuntimeState(state);

            SetBehaviourEnabledForAll<WorldTileStreamSource>(shouldEnableWorldRuntime);
            SetBehaviourEnabledForAll<WorldStreamedCityBuilder>(shouldEnableWorldRuntime);
            SetBehaviourEnabledForAll<RuntimePlacedStructureFixer>(shouldEnableWorldRuntime);
            SetBehaviourEnabledForAll<ProceduralRoadSystem>(shouldEnableWorldRuntime);
            SetBehaviourEnabledForAll<StreamingNavMeshTileService>(shouldEnableWorldRuntime);

            if (!shouldEnableWorldRuntime && logMainMenuRuntimeSnapshot)
                LogMainMenuRuntimeSnapshot(source);
        }

        private static bool IsWorldRuntimeState(GameState state)
        {
            return state == GameState.LoadingWorld || state == GameState.Playing || state == GameState.Paused;
        }

        private static void SetBehaviourEnabledForAll<T>(bool enabled) where T : Behaviour
        {
            var behaviours = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;
                if (behaviour.enabled == enabled) continue;
                behaviour.enabled = enabled;
            }
        }

        private void LogMainMenuRuntimeSnapshot(string source)
        {
            var loadedSceneNames = LoadedScenes().Select(static scene => scene.name).ToArray();
            var loadedSummary = loadedSceneNames.Length > 0
                ? string.Join(", ", loadedSceneNames)
                : "(none)";

            var terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var tileStreams =
                FindObjectsByType<WorldTileStreamSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var cityBuilders =
                FindObjectsByType<WorldStreamedCityBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var structureFixers =
                FindObjectsByType<RuntimePlacedStructureFixer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var activeSceneName = SceneManager.GetActiveScene().name;

            Debug.Log(
                $"[GameManager] MainMenu runtime snapshot ({source}) state={CurrentState}, activeScene='{activeSceneName}', loadedScenes=[{loadedSummary}], " +
                $"terrains={terrains.Length}, tileStreams={tileStreams.Length}, " +
                $"cityBuilders={cityBuilders.Length}, runtimeStructureFixers={structureFixers.Length}.",
                this);
        }
    }
}
