using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.AI;
using Zombera.BaseBuilding;
using Zombera.Characters;
using Zombera.Debugging.DebugTools;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.UI.Menus;
using Zombera.World;

namespace Zombera.Core
{
    /// <summary>
    /// Validates critical runtime setup for the current prototype loop.
    /// Emits clear errors/warnings to speed up scene wiring and playtest readiness checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartupReadinessValidator : MonoBehaviour
    {
        [Header("Execution")]
        [SerializeField] private bool runValidationOnAwake;
        [SerializeField] private bool runOnlyOnce;
        [SerializeField] private bool includeBuildSettingsChecks = true;
        [SerializeField] private bool includeCoreSystemChecks = true;
        [SerializeField] private bool includeWorldRuntimeChecks = true;
        [SerializeField] private bool includeMainMenuRuntimeChecks = true;
        [SerializeField] private bool logSuccessSummary = true;

        [Header("Scene Names")]
        [SerializeField] private string bootSceneName = "Boot";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string worldSceneName = "World";

        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        private bool hasRun;
        private bool lastValidationPassed = true;

        public bool LastValidationPassed => lastValidationPassed;

        private void Awake()
        {
            if (runValidationOnAwake)
            {
                RunValidation();
            }
        }

        public bool RunValidation()
        {
            if (runOnlyOnce && hasRun)
            {
                return lastValidationPassed;
            }

            hasRun = true;
            errors.Clear();
            warnings.Clear();

            if (includeBuildSettingsChecks)
            {
                ValidateBuildSettings();
            }

            if (includeCoreSystemChecks)
            {
                ValidateCoreSystems();
            }

            if (includeMainMenuRuntimeChecks && IsSceneLoaded(mainMenuSceneName))
            {
                ValidateMainMenuRuntime();
            }

            if (includeWorldRuntimeChecks && IsSceneLoaded(worldSceneName))
            {
                ValidateWorldRuntime();
            }

            lastValidationPassed = errors.Count <= 0;
            EmitValidationSummary();
            return lastValidationPassed;

            // TODO: Extend checks for save slot availability and version compatibility.
            // TODO: Add optional strict mode for CI/editor validation workflows.
        }

        private void ValidateBuildSettings()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;

            if (sceneCount <= 0)
            {
                AddError("Build Settings has no scenes. Add Boot, MainMenu, and World scenes.");
                return;
            }

            int bootIndex = GetBuildIndex(bootSceneName);
            int menuIndex = GetBuildIndex(mainMenuSceneName);
            int worldIndex = GetBuildIndex(worldSceneName);

            if (bootIndex < 0)
            {
                AddError($"Build Settings missing scene '{bootSceneName}'.");
            }
            else if (bootIndex != 0)
            {
                AddWarning($"Scene '{bootSceneName}' should be build index 0.");
            }

            if (menuIndex < 0)
            {
                AddError($"Build Settings missing scene '{mainMenuSceneName}'.");
            }

            if (worldIndex < 0)
            {
                AddError($"Build Settings missing scene '{worldSceneName}'.");
            }

            if (menuIndex >= 0 && worldIndex >= 0 && menuIndex > worldIndex)
            {
                AddWarning($"Scene order should place '{mainMenuSceneName}' before '{worldSceneName}'.");
            }
        }

        private void ValidateCoreSystems()
        {
            RequireComponent<GameManager>("GameManager");
            RequireComponent<EventSystem>("EventSystem");
            RequireComponent<TimeSystem>("TimeSystem");
            RequireComponent<SaveSystem>("SaveSystem");

            RequireComponent<UnitManager>("UnitManager");
            RequireComponent<CombatManager>("CombatManager");
            RequireComponent<AIManager>("AIManager");
            RequireComponent<SquadManager>("SquadManager");
            RequireComponent<ZombieManager>("ZombieManager");
            RequireComponent<LootManager>("LootManager");
            RequireComponent<BaseManager>("BaseManager");
            RequireComponent<SaveManager>("SaveManager");

            RecommendComponent<DebugManager>("DebugManager");
        }

        private void ValidateMainMenuRuntime()
        {
            RequireComponent<MainMenuController>("MainMenuController");
        }

        private void ValidateWorldRuntime()
        {
            RequireComponent<WorldManager>("WorldManager");
            RequireComponent<RegionSystem>("RegionSystem");
            RequireComponent<ChunkLoader>("ChunkLoader");
            RequireComponent<ChunkGenerator>("ChunkGenerator");
            RequireComponent<LootSpawner>("LootSpawner");
            RequireComponent<WorldEventSystem>("WorldEventSystem");

            Unit[] units = FindObjectsOfType<Unit>();
            int playerCount = 0;

            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];

                if (unit != null && unit.Role == UnitRole.Player)
                {
                    playerCount++;
                }
            }

            if (playerCount <= 0)
            {
                AddError("World scene has no active Player unit.");
            }

            if (FindObjectOfType<SurvivorAI>() == null)
            {
                AddWarning("World scene has no active SurvivorAI (recruitment loop may be blocked).");
            }

            if (FindObjectOfType<ZombieAI>() == null)
            {
                AddWarning("World scene has no active ZombieAI (combat loop may be underpopulated).");
            }

            bool hasLootContainer = FindObjectOfType<LootContainer>() != null;
            bool hasLootSpawner = FindObjectOfType<LootSpawner>() != null;

            if (!hasLootContainer && !hasLootSpawner)
            {
                AddWarning("World scene has no loot containers or LootSpawner.");
            }

            if (FindObjectOfType<BaseStorage>() == null)
            {
                AddWarning("World scene has no BaseStorage (base loop verification may be blocked).");
            }

            if (FindObjectOfType<DebugKeybinds>() == null)
            {
                AddWarning("DebugKeybinds not found in loaded scenes (debug hotkeys unavailable).");
            }
        }

        private bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.IsValid() && scene.isLoaded && string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetBuildIndex(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return -1;
            }

            int sceneCount = SceneManager.sceneCountInBuildSettings;

            for (int index = 0; index < sceneCount; index++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(index);
                string buildSceneName = Path.GetFileNameWithoutExtension(path);

                if (string.Equals(buildSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private void RequireComponent<T>(string displayName) where T : Component
        {
            if (FindObjectOfType<T>() == null)
            {
                AddError($"Missing required component: {displayName}");
            }
        }

        private void RecommendComponent<T>(string displayName) where T : Component
        {
            if (FindObjectOfType<T>() == null)
            {
                AddWarning($"Recommended component not found: {displayName}");
            }
        }

        private void AddError(string message)
        {
            errors.Add(message);
            Debug.LogError($"[StartupReadinessValidator] {message}", this);
        }

        private void AddWarning(string message)
        {
            warnings.Add(message);
            Debug.LogWarning($"[StartupReadinessValidator] {message}", this);
        }

        private void EmitValidationSummary()
        {
            if (errors.Count <= 0 && warnings.Count <= 0)
            {
                if (logSuccessSummary)
                {
                    Debug.Log("[StartupReadinessValidator] Validation passed with no issues.", this);
                }

                return;
            }

            string summary = $"[StartupReadinessValidator] Validation complete. Errors: {errors.Count}, Warnings: {warnings.Count}.";

            if (errors.Count > 0)
            {
                Debug.LogError(summary, this);
            }
            else
            {
                Debug.LogWarning(summary, this);
            }
        }
    }
}