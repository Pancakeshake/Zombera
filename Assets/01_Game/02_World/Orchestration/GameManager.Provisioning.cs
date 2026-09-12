using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World;
using Zombera.World.Roads;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        private void ClearEasyBuildPersistenceForFreshSession()
        {
            try
            {
                var sceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(worldSceneName)) sceneNames.Add(worldSceneName.Trim());

                foreach (var fallback in WorldSceneFallbackNames)
                    if (!string.IsNullOrWhiteSpace(fallback))
                        sceneNames.Add(fallback.Trim());

                var deletedArtifacts = EasyBuildSessionPersistence.ClearForScenes(sceneNames);

                if (deletedArtifacts > 0)
                    Debug.Log($"[GameManager] Cleared {deletedArtifacts} Easy Build save artifact(s) for New Game.",
                        this);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GameManager] Failed to clear Easy Build persistence for New Game: {exception.Message}",
                    this);
            }
        }

        private void EnsureWorldRuntimeComponentsPresent()
        {
            var activeScene = SceneManager.GetActiveScene();

            if (!IsWorldScene(activeScene)) return;

            WorldRuntimeProvisioner.EnsureWorldRuntimeComponentsPresent(activeScene);
        }

        private static bool IsSpawnerAttachedToUnit(PlayerSpawner spawner)
        {
            return WorldRuntimeProvisioner.IsSpawnerAttachedToUnit(spawner);
        }

        private void TryFinalizePendingWorldSession(Scene loadedScene)
        {
            if (!HasPendingSessionRequest() || !IsWorldScene(loadedScene)) return;

            if (_pendingLoadGame)
            {
                var slotId = _pendingLoadSlotId;
                _pendingLoadGame = false;
                _pendingLoadSlotId = null;
                _pendingStartNewGame = false;
                
                PrepareLoadSession(slotId);
                BeginWorldSession(false);
                return;
            }

            if (_pendingStartNewGame)
            {
                _pendingStartNewGame = false;
                _pendingLoadSlotId = null;
                BeginWorldSession(true);
            }
        }

        private void RunReadinessValidation(StartupReadinessValidator.ValidationMode mode)
        {
            if (!runReadinessValidationOnInitialize) return;

            if (startupReadinessValidator == null)
                startupReadinessValidator = FindFirstObjectByType<StartupReadinessValidator>();

            startupReadinessValidator?.RunValidation(mode);
        }

        private void ResolveRuntimeReferences()
        {
            eventSystem = ResolveReference(eventSystem);
            timeSystem = ResolveReference(timeSystem);
            saveSystem = ResolveReference(saveSystem);
            unitManager = ResolveReference(unitManager);
            combatManager = ResolveReference(combatManager);
            aiManager = ResolveReference(aiManager);
            squadManager = ResolveReference(squadManager);
            zombieManager = ResolveReference(zombieManager);
            lootManager = ResolveReference(lootManager);
            baseManager = ResolveReference(baseManager);
            saveManager = ResolveReference(saveManager);
            worldManager = ResolveReference(worldManager);
            proceduralRoadSystem = ResolveReference(proceduralRoadSystem);
        }

        private void InitializeDiscoveredGameSystems()
        {
            RefreshDiscoveredGameSystems();

            for (var i = 0; i < _orderedGameSystems.Count; i++)
            {
                var gameSystem = _orderedGameSystems[i];
                if (gameSystem == null || gameSystem.IsInitialized) continue;

                try
                {
                    gameSystem.Initialize();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[GameManager] Failed to initialize game system '{gameSystem.GetType().Name}': {exception.Message}",
                        this);
                }
            }
        }

        private void ShutdownDiscoveredGameSystems()
        {
            if (_orderedGameSystems.Count == 0)
                RefreshDiscoveredGameSystems();

            for (var i = _orderedGameSystems.Count - 1; i >= 0; i--)
            {
                var gameSystem = _orderedGameSystems[i];
                if (gameSystem == null || !gameSystem.IsInitialized) continue;

                try
                {
                    gameSystem.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[GameManager] Failed to shutdown game system '{gameSystem.GetType().Name}': {exception.Message}",
                        this);
                }
            }

            _orderedGameSystems.Clear();
            _orderedGameSystemLookup.Clear();
        }

        private void RefreshDiscoveredGameSystems()
        {
            _orderedGameSystems.Clear();
            _orderedGameSystemLookup.Clear();

            TryAddDiscoveredGameSystem(eventSystem);
            TryAddDiscoveredGameSystem(timeSystem);
            TryAddDiscoveredGameSystem(saveSystem);
            TryAddDiscoveredGameSystem(combatManager);
            TryAddDiscoveredGameSystem(aiManager);
            TryAddDiscoveredGameSystem(saveManager);
            TryAddDiscoveredGameSystem(zombieManager);
            TryAddDiscoveredGameSystem(lootManager);
            TryAddDiscoveredGameSystem(baseManager);

            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IGameSystem gameSystem)
                    TryAddDiscoveredGameSystem(gameSystem);
            }

            _orderedGameSystems.Sort(CompareGameSystemsForInitializationOrder);
        }

        private void TryAddDiscoveredGameSystem(IGameSystem gameSystem)
        {
            if (gameSystem == null || _orderedGameSystemLookup.Contains(gameSystem)) return;

            _orderedGameSystemLookup.Add(gameSystem);
            _orderedGameSystems.Add(gameSystem);
        }

        private static int CompareGameSystemsForInitializationOrder(IGameSystem left, IGameSystem right)
        {
            var leftPriority = GetGameSystemInitializationPriority(left);
            var rightPriority = GetGameSystemInitializationPriority(right);

            if (leftPriority != rightPriority)
                return leftPriority.CompareTo(rightPriority);

            var leftName = left?.GetType().FullName ?? string.Empty;
            var rightName = right?.GetType().FullName ?? string.Empty;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }

        private static int GetGameSystemInitializationPriority(IGameSystem gameSystem)
        {
            return gameSystem switch
            {
                CoreEventBus => 0,
                TimeSystem => 10,
                SaveSystem => 20,
                CombatManager => 30,
                AIManager => 40,
                SaveManager => 50,
                ZombieManager => 60,
                LootManager => 70,
                BaseManager => 80,
                _ => 500
            };
        }

        public void RegisterSystem<T>(T system) where T : Component
        {
            if (system == null) return;

            if (system is CoreEventBus bus) eventSystem = bus;
            else if (system is TimeSystem ts) timeSystem = ts;
            else if (system is SaveSystem ss) saveSystem = ss;
            else if (system is UnitManager um) unitManager = um;
            else if (system is CombatManager cm) combatManager = cm;
            else if (system is AIManager am) aiManager = am;
            else if (system is SquadManager sm) squadManager = sm;
            else if (system is ZombieManager zm) zombieManager = zm;
            else if (system is LootManager lm) lootManager = lm;
            else if (system is BaseManager bm) baseManager = bm;
            else if (system is SaveManager svm) saveManager = svm;
            else if (system is WorldManager wm) worldManager = wm;
            else if (system is ProceduralRoadSystem prs) proceduralRoadSystem = prs;

            if (system is IGameSystem gameSystem)
                TryAddDiscoveredGameSystem(gameSystem);
        }

        private T ResolveReference<T>(T currentReference) where T : Component
        {
            if (currentReference != null) return currentReference;

            // Try resolving from children first (common for persistent systems).
            var child = GetComponentInChildren<T>(true);
            if (child != null) return child;

            return FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }
    }
}