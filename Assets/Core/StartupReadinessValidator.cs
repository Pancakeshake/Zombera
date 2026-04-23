using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.AI;
using Zombera.BaseBuilding;
using Zombera.Characters;
using Zombera.Debugging;
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
        private static readonly string[] WorldSceneFallbackNames =
        {
            "World_MapMagicStream",
            "World_Map_MagicStream"
        };

        public enum ValidationMode
        {
            Auto,
            BootOrMenu,
            WorldSession
        }

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

        [Header("Save Slot Validation")]
        [SerializeField] private bool checkSaveSlotAvailability;
#pragma warning disable CS0414
        [SerializeField] private int requiredSaveFormatVersion = 1;
#pragma warning restore CS0414
        [SerializeField] private bool strictMode;

        [Header("World Optional Warnings")]
        [SerializeField] private bool warnWhenNoSurvivorAi;
        [SerializeField] private bool warnWhenNoBaseStorage;
        [SerializeField] private bool warnWhenNoDebugKeybinds;

        [Header("Streamed MapMagic / Procedural World")]
        [Tooltip("When enabled, WorldManager procedural streaming mode must have MapMagic terrain authority wired.")]
        [SerializeField] private bool includeStreamedMapMagicChecks = true;

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
            return RunValidation(ValidationMode.Auto);
        }

        public bool RunValidation(ValidationMode mode)
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

            bool runMenuChecks = includeMainMenuRuntimeChecks;
            bool runWorldChecks = includeWorldRuntimeChecks;

            if (mode == ValidationMode.BootOrMenu)
            {
                runWorldChecks = false;
            }
            else if (mode == ValidationMode.WorldSession)
            {
                runWorldChecks = true;
            }

            if (runMenuChecks && IsSceneLoaded(mainMenuSceneName))
            {
                ValidateMainMenuRuntime();
            }

            if (runWorldChecks && IsAnySceneLoaded(WorldSceneCandidates()))
            {
                ValidateWorldRuntime();
            }

            if (checkSaveSlotAvailability)
            {
                ValidateSaveSlots();
            }

            lastValidationPassed = errors.Count <= 0;
            EmitValidationSummary();

            if (strictMode && !lastValidationPassed)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPaused = true;
#endif
                Debug.LogError("[StartupReadinessValidator] Strict mode: validation failed. Play paused.", this);
            }

            return lastValidationPassed;
        }

        private void ValidateSaveSlots()
        {
            string saveFolderPath = Path.Combine(Application.persistentDataPath, "Saves");

            if (!Directory.Exists(saveFolderPath))
            {
                AddWarning($"Save folder not found at '{saveFolderPath}'. No saves have been written yet.");
                return;
            }

            string indexPath = Path.Combine(saveFolderPath, "index.json");

            if (!File.Exists(indexPath))
            {
                AddWarning("Save index not found. No slots have been recorded yet.");
                return;
            }

            // Check the save files referenced by the index are present.
            try
            {
                string json = File.ReadAllText(indexPath);
                SaveMetadataIndex index = JsonUtility.FromJson<SaveMetadataIndex>(json);

                if (index == null || index.slotIds == null || index.slotIds.Count == 0)
                {
                    AddWarning("Save index exists but contains no slots.");
                    return;
                }

                foreach (string slotId in index.slotIds)
                {
                    string savePath = Path.Combine(saveFolderPath, slotId + ".sav");

                    if (!File.Exists(savePath))
                    {
                        AddWarning($"Save slot '{slotId}' is in the index but the .sav file is missing.");
                    }
                }
            }
            catch (Exception e)
            {
                AddWarning($"Could not read save index: {e.Message}");
            }
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
            int worldIndex = GetFirstBuildIndex(WorldSceneCandidates(), out string resolvedWorldSceneName);

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
                AddError($"Build Settings missing world scene. Checked {GetWorldSceneCandidateSummary()}.");
            }
            else if (!string.Equals(resolvedWorldSceneName, worldSceneName, StringComparison.OrdinalIgnoreCase))
            {
                AddWarning($"Configured world scene '{worldSceneName}' is missing from Build Settings. Using '{resolvedWorldSceneName}' for validation.");
            }

            if (menuIndex >= 0 && worldIndex >= 0 && menuIndex > worldIndex)
            {
                AddWarning($"Scene order should place '{mainMenuSceneName}' before '{resolvedWorldSceneName}'.");
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

            Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
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

            if (warnWhenNoSurvivorAi && FindFirstObjectByType<SurvivorAI>() == null)
            {
                AddWarning("World scene has no active SurvivorAI (recruitment loop may be blocked).");
            }

            if (FindFirstObjectByType<ZombieAI>() == null)
            {
                AddWarning("World scene has no active ZombieAI (combat loop may be underpopulated).");
            }

            bool hasLootContainer = FindFirstObjectByType<LootContainer>() != null;
            bool hasLootSpawner = FindFirstObjectByType<LootSpawner>() != null;

            if (!hasLootContainer && !hasLootSpawner)
            {
                AddWarning("World scene has no loot containers or LootSpawner.");
            }

            if (warnWhenNoBaseStorage && FindFirstObjectByType<BaseStorage>() == null)
            {
                AddWarning("World scene has no BaseStorage (base loop verification may be blocked).");
            }

            if (warnWhenNoDebugKeybinds && FindFirstObjectByType<DebugKeybinds>() == null)
            {
                AddWarning("DebugKeybinds not found in loaded scenes (debug hotkeys unavailable).");
            }

            if (includeStreamedMapMagicChecks)
            {
                ValidateStreamedMapMagicWorld();
            }
        }

        private void ValidateStreamedMapMagicWorld()
        {
            WorldManager worldManager = FindFirstObjectByType<WorldManager>();
            if (worldManager == null || !worldManager.UseProceduralStreamingWorld)
            {
                return;
            }

            MapMagic.Core.MapMagicObject[] mapMagics = FindObjectsByType<MapMagic.Core.MapMagicObject>(FindObjectsSortMode.None);
            if (mapMagics == null || mapMagics.Length == 0)
            {
                AddError("WorldManager has procedural streaming enabled but no MapMagicObject exists in loaded scenes (no terrain authority).");
                return;
            }

            if (mapMagics.Length > 1)
            {
                AddWarning($"Multiple MapMagicObject instances ({mapMagics.Length}) are active. PlayerSpawner stabilization expects a single primary terrain authority.");
            }

            if (worldManager.TileStreamBridge == null)
            {
                AddWarning("WorldManager procedural streaming is on but MapMagicTileStreamBridge is missing (WorldManager should add one at runtime).");
            }

            for (int i = 0; i < mapMagics.Length; i++)
            {
                if (mapMagics[i] != null && mapMagics[i].graph == null)
                {
                    AddWarning($"MapMagicObject '{mapMagics[i].name}' has no graph assigned.");
                }
            }

            if (FindFirstObjectByType<StreamingNavMeshTileService>() == null)
            {
                AddWarning(
                    "Procedural streaming is enabled but no StreamingNavMeshTileService is present. " +
                    "Add the component to the scene and assign it on PlayerSpawner for per-tile NavMesh builds (otherwise runtime uses a single large bake).");
            }
        }

        private IEnumerable<string> WorldSceneCandidates()
        {
            if (!string.IsNullOrWhiteSpace(worldSceneName))
            {
                yield return worldSceneName;
            }

            for (int i = 0; i < WorldSceneFallbackNames.Length; i++)
            {
                string fallbackName = WorldSceneFallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(worldSceneName)
                    && string.Equals(worldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return fallbackName;
            }
        }

        private bool IsAnySceneLoaded(IEnumerable<string> sceneNames)
        {
            foreach (string sceneName in sceneNames)
            {
                if (IsSceneLoaded(sceneName))
                {
                    return true;
                }
            }

            return false;
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

        private int GetFirstBuildIndex(IEnumerable<string> sceneNames, out string resolvedSceneName)
        {
            foreach (string sceneName in sceneNames)
            {
                int buildIndex = GetBuildIndex(sceneName);
                if (buildIndex >= 0)
                {
                    resolvedSceneName = sceneName;
                    return buildIndex;
                }
            }

            resolvedSceneName = worldSceneName;
            return -1;
        }

        private string GetWorldSceneCandidateSummary()
        {
            string summary = string.IsNullOrWhiteSpace(worldSceneName)
                ? "(no configured world scene)"
                : $"'{worldSceneName}'";

            for (int i = 0; i < WorldSceneFallbackNames.Length; i++)
            {
                string fallbackName = WorldSceneFallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(worldSceneName)
                    && string.Equals(worldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                summary += $", '{fallbackName}'";
            }

            return summary;
        }

        private void RequireComponent<T>(string displayName) where T : Component
        {
            if (FindFirstObjectByType<T>() == null)
            {
                AddError($"Missing required component: {displayName}");
            }
        }

        private void RecommendComponent<T>(string displayName) where T : Component
        {
            if (FindFirstObjectByType<T>() == null)
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