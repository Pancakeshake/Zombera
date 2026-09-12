using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World;
using Zombera.World.Simulation;

namespace Zombera.Core
{
    /// <summary>
    ///     Find-or-create provisioning of the world runtime systems for a world scene.
    ///     Extracted from GameManager so it stays a thin state machine. Idempotent: existing
    ///     scene-wired components are always preferred over runtime-created ones.
    /// </summary>
    internal static class WorldRuntimeProvisioner
    {
        private const string RuntimeRootName = "RuntimeWorldSystems";

        /// <summary>Ensures all world runtime systems exist in the given (already validated) world scene.</summary>
        public static void EnsureWorldRuntimeComponentsPresent(Scene activeScene)
        {
            GameObject runtimeRoot = null;

            _ = FindOrCreateWorldComponent<WorldManager>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<RegionSystem>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<ChunkLoader>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<ChunkGenerator>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<ChunkCache>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<MapSpawner>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<LootSpawner>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<WorldEventSystem>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<WorldSimulationManager>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<ZombieManager>(ref runtimeRoot);

            EnsureSceneLevelPlayerSpawner(activeScene, ref runtimeRoot);
        }

        public static bool IsSpawnerAttachedToUnit(PlayerSpawner spawner)
        {
            return spawner != null
                   && (spawner.GetComponent<Unit>() != null
                       || spawner.GetComponentInParent<Unit>() != null
                       || spawner.GetComponentInChildren<Unit>(true) != null);
        }

        private static T FindOrCreateWorldComponent<T>(ref GameObject runtimeRoot) where T : Component
        {
            var existing = Object.FindFirstObjectByType<T>();

            if (existing != null) return existing;

            EnsureRuntimeRoot(ref runtimeRoot);

            return runtimeRoot.AddComponent<T>();
        }

        private static void EnsureSceneLevelPlayerSpawner(Scene activeScene, ref GameObject runtimeRoot)
        {
            var spawners = Object.FindObjectsByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // If this scene already has any spawner, keep it instead of adding a duplicate runtime spawner.
            if (spawners.Any(spawner =>
                    spawner != null
                    && spawner.gameObject.scene == activeScene))
            {
                return;
            }

            if (spawners.Any(spawner =>
                    spawner != null
                    && spawner.gameObject.scene == activeScene
                    && IsSpawnerAttachedToUnit(spawner)))
                return;

            EnsureRuntimeRoot(ref runtimeRoot);

            runtimeRoot.AddComponent<PlayerSpawner>();
        }

        private static void EnsureRuntimeRoot(ref GameObject runtimeRoot)
        {
            if (runtimeRoot != null) return;

            runtimeRoot = GameObject.Find(RuntimeRootName);

            if (runtimeRoot == null) runtimeRoot = new GameObject(RuntimeRootName);
        }
    }
}
