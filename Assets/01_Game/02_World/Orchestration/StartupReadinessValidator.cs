#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
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
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

#endregion

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ConvertIfStatementToReturnStatement

namespace Zombera.Core
{
    using CoreEventBusType = Zombera.Core.CoreEventBus;
    using UiInputModule = UnityEngine.EventSystems.BaseInputModule;
    using UnityUiEventSystem = UnityEngine.EventSystems.EventSystem;

    /// <summary>
    ///     Validates critical runtime setup for the current prototype loop.
    ///     Emits clear errors/warnings to speed up scene wiring and playtest readiness checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartupReadinessValidator : MonoBehaviour
    {
        public enum ValidationMode
        {
            Auto,
            BootOrMenu,
            WorldSession
        }

        private static readonly string[] WorldSceneFallbackNames =
        {
            "World_MapMagicStream",
            "World_Map_MagicStream"
        };

        [Header("Execution")] [SerializeField] private bool runValidationOnAwake;

        [SerializeField] private bool runOnlyOnce;
        [SerializeField] private bool includeBuildSettingsChecks = true;
        [SerializeField] private bool includeCoreSystemChecks = true;
        [SerializeField] private bool includeWorldRuntimeChecks = true;
        [SerializeField] private bool includeMainMenuRuntimeChecks = true;
        [SerializeField] private bool logSuccessSummary = true;

        [Header("Scene Names")] [SerializeField]
        private string bootSceneName = "Boot";

        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string worldSceneName = "World";

        [Header("Save Slot Validation")] [SerializeField]
        private bool checkSaveSlotAvailability;
#pragma warning disable CS0414
        [SerializeField] private int requiredSaveFormatVersion = 1;
#pragma warning restore CS0414
        [SerializeField] private bool strictMode;

        [Header("World Optional Warnings")] [SerializeField]
        private bool warnWhenNoSurvivorAi;

        [SerializeField] private bool warnWhenNoBaseStorage;
        [SerializeField] private bool warnWhenNoDebugKeybinds;

        [Tooltip(
            "When enabled, warns if no ZombieController exists in loaded scenes. Off by default: zombies are spawned at runtime by ZombieManager, so this is almost always a false positive at session start.")]
        [SerializeField]
        private bool warnWhenNoZombieControllersInScene;

        [Header("Streamed MapMagic / Procedural World")]
        [Tooltip("When enabled, WorldManager procedural streaming mode must have MapMagic terrain authority wired.")]
        [SerializeField]
        private bool includeStreamedMapMagicChecks = true;

        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        private bool _hasRun;

        public bool LastValidationPassed { get; private set; } = true;

        private void Awake()
        {
            if (runValidationOnAwake) RunValidation();
        }

        public bool RunValidation()
        {
            return RunValidation(ValidationMode.Auto);
        }

        public bool RunValidation(ValidationMode mode)
        {
            if (runOnlyOnce && _hasRun) return LastValidationPassed;

            _hasRun = true;
            _errors.Clear();
            _warnings.Clear();

            if (includeBuildSettingsChecks) ValidateBuildSettings();

            if (includeCoreSystemChecks) ValidateCoreSystems();

            var runMenuChecks = includeMainMenuRuntimeChecks;
            var runWorldChecks = mode switch
            {
                ValidationMode.BootOrMenu => false,
                ValidationMode.WorldSession => true,
                _ => includeWorldRuntimeChecks
            };

            if (runMenuChecks && IsSceneLoaded(mainMenuSceneName)) ValidateMainMenuRuntime();

            if (runWorldChecks && IsAnySceneLoaded(WorldSceneCandidates())) ValidateWorldRuntime();

            if (checkSaveSlotAvailability) ValidateSaveSlots();

            LastValidationPassed = _errors.Count <= 0;
            EmitValidationSummary();

            if (!strictMode || LastValidationPassed) return LastValidationPassed;

#if UNITY_EDITOR
            EditorApplication.isPaused = true;
#endif
            Debug.LogError("[StartupReadinessValidator] Strict mode: validation failed. Play paused.", this);

            return LastValidationPassed;
        }

        private void ValidateSaveSlots()
        {
            var saveFolderPath = Path.Combine(Application.persistentDataPath, "Saves");

            if (!Directory.Exists(saveFolderPath))
            {
                AddWarning($"Save folder not found at '{saveFolderPath}'. No saves have been written yet.");
                return;
            }

            var indexPath = Path.Combine(saveFolderPath, "index.json");

            if (!File.Exists(indexPath))
            {
                AddWarning("Save index not found. No slots have been recorded yet.");
                return;
            }

            // Check the save files referenced by the index are present.
            try
            {
                var json = File.ReadAllText(indexPath);
                var index = JsonUtility.FromJson<SaveMetadataIndex>(json);

                if (index is not { slotIds: { Count: > 0 } })
                {
                    AddWarning("Save index exists but contains no slots.");
                    return;
                }

                foreach (var slotId in index.slotIds)
                {
                    if (string.IsNullOrWhiteSpace(slotId)) continue;

                    if (!File.Exists(Path.Combine(saveFolderPath, slotId + ".sav")))
                        AddWarning($"Save slot '{slotId}' is in the index but the .sav file is missing.");
                }
            }
            catch (Exception e)
            {
                AddWarning($"Could not read save index: {e.Message}");
            }
        }

        private void ValidateBuildSettings()
        {
            var sceneCount = SceneManager.sceneCountInBuildSettings;

            if (sceneCount <= 0)
            {
                AddError("Build Settings has no scenes. Add Boot, MainMenu, and World scenes.");
                return;
            }

            var bootIndex = GetBuildIndex(bootSceneName);
            var menuIndex = GetBuildIndex(mainMenuSceneName);
            var worldIndex = GetFirstBuildIndex(WorldSceneCandidates(), out var resolvedWorldSceneName);

            if (bootIndex < 0)
                AddError($"Build Settings missing scene '{bootSceneName}'.");
            else if (bootIndex != 0) AddWarning($"Scene '{bootSceneName}' should be build index 0.");

            if (menuIndex < 0) AddError($"Build Settings missing scene '{mainMenuSceneName}'.");

            if (worldIndex < 0)
                AddError($"Build Settings missing world scene. Checked {GetWorldSceneCandidateSummary()}.");
            else if (!string.Equals(resolvedWorldSceneName, worldSceneName, StringComparison.OrdinalIgnoreCase))
                AddWarning(
                    $"Configured world scene '{worldSceneName}' is missing from Build Settings. Using '{resolvedWorldSceneName}' for validation.");

            if (menuIndex >= 0 && worldIndex >= 0 && menuIndex > worldIndex)
                AddWarning($"Scene order should place '{mainMenuSceneName}' before '{resolvedWorldSceneName}'.");
        }

        private void ValidateCoreSystems()
        {
            RequireComponent<GameManager>("GameManager");
            RequireComponent<CoreEventBusType>("Core Event Bus (Zombera.Core.CoreEventBus)");
            ValidateUnityUiEventSystemAvailability();
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

        private void ValidateUnityUiEventSystemAvailability()
        {
            var eventSystems =
                FindObjectsByType<UnityUiEventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (eventSystems is not { Length: > 0 })
            {
                AddWarning(
                    "No Unity UI EventSystem found. UI navigation may fail until RuntimeUiEventSystemUtility creates one.");
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            UnityUiEventSystem preferred = null;
            UnityUiEventSystem persistentFallback = null;

            foreach (var candidate in eventSystems)
            {
                if (candidate == null) continue;

                var candidateScene = candidate.gameObject.scene;
                if (candidateScene == activeScene)
                {
                    preferred = candidate;
                    break;
                }

                if (persistentFallback == null && candidateScene.IsValid() && candidateScene.buildIndex < 0)
                    persistentFallback = candidate;
            }

            preferred ??= persistentFallback;
            preferred ??= eventSystems.FirstOrDefault(static system => system != null);

            if (preferred == null)
            {
                AddWarning("No valid Unity UI EventSystem instance found.");
                return;
            }

            if (!preferred.isActiveAndEnabled || !preferred.gameObject.activeInHierarchy)
            {
                AddWarning(
                    $"Unity UI EventSystem '{preferred.name}' exists but is inactive. " +
                    "UI navigation may fail until RuntimeUiEventSystemUtility re-enables a preferred system.");
                return;
            }

            var hasEnabledInputModule = preferred.GetComponents<UiInputModule>()
                .Any(static module => module != null && module.enabled);

            if (!hasEnabledInputModule)
                AddWarning(
                    $"Unity UI EventSystem '{preferred.name}' has no enabled input module. " +
                    "Enable InputSystemUIInputModule or StandaloneInputModule.");
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

            var units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            var playerCount = units.Count(static unit => unit != null && unit.Role == UnitRole.Player);

            if (playerCount <= 0) AddError("World scene has no active Player unit.");

            if (warnWhenNoSurvivorAi && FindFirstObjectByType<SurvivorController>() == null)
                AddWarning("World scene has no active SurvivorController (recruitment loop may be blocked).");

            if (warnWhenNoZombieControllersInScene && FindFirstObjectByType<ZombieController>() == null)
                AddWarning("World scene has no active ZombieController (expected only when debugging prefab-placed zombies).");

            if (FindFirstObjectByType<LootContainer>() == null && FindFirstObjectByType<LootSpawner>() == null)
                AddWarning("World scene has no loot containers or LootSpawner.");

            if (warnWhenNoBaseStorage && FindFirstObjectByType<BaseStorage>() == null)
                AddWarning("World scene has no BaseStorage (base loop verification may be blocked).");

            if (warnWhenNoDebugKeybinds && FindFirstObjectByType<DebugKeybinds>() == null)
                AddWarning("DebugKeybinds not found in loaded scenes (debug hotkeys unavailable).");

            if (includeStreamedMapMagicChecks) ValidateStreamedWorldPipeline();
        }

        private void ValidateStreamedWorldPipeline()
        {
            var worldManager = FindFirstObjectByType<WorldManager>();
            if (worldManager == null || !worldManager.UseProceduralStreamingWorld) return;

            if (worldManager.TileStreamBridge == null)
            {
                AddError(
                    "WorldManager has procedural streaming enabled but WorldTileStreamSource is not assigned.");
            }

            if (worldManager.WorldGenerationBackend == null
                && FindFirstObjectByType<WorldTileStreamSource>() == null)
            {
                AddError(
                    "WorldManager has procedural streaming enabled but no IWorldGenerationBackend / WorldTileStreamSource is present.");
            }

            if (FindFirstObjectByType<StreamingNavMeshTileService>() == null)
            {
                AddWarning(
                    "Procedural streaming is enabled but no StreamingNavMeshTileService is present. " +
                    "Add the component to the scene and assign it on PlayerSpawner for per-tile NavMesh builds.");
            }

            if (FindFirstObjectByType<WorldBuilderService>() == null
                && worldManager.WorldGenerationBackend == null)
            {
                AddWarning(
                    "No WorldBuilderService / IWorldGenerationBackend found for procedural streaming profile validation.");
            }
        }

        private IEnumerable<string> WorldSceneCandidates()
        {
            if (!string.IsNullOrWhiteSpace(worldSceneName)) yield return worldSceneName;

            foreach (var fallbackName in WorldSceneFallbackNames)
            {
                if (string.IsNullOrWhiteSpace(fallbackName)
                    || (!string.IsNullOrWhiteSpace(worldSceneName)
                        && string.Equals(worldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                yield return fallbackName;
            }
        }

        private static bool IsAnySceneLoaded(IEnumerable<string> sceneNames)
        {
            return sceneNames.Any(IsSceneLoaded);
        }

        private static bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static int GetBuildIndex(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return -1;

            var sceneCount = SceneManager.sceneCountInBuildSettings;

            for (var index = 0; index < sceneCount; index++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(index);
                var buildSceneName = Path.GetFileNameWithoutExtension(path);

                if (string.Equals(buildSceneName, sceneName, StringComparison.OrdinalIgnoreCase)) return index;
            }

            return -1;
        }

        private int GetFirstBuildIndex(IEnumerable<string> sceneNames, out string resolvedSceneName)
        {
            foreach (var sceneName in sceneNames)
            {
                var buildIndex = GetBuildIndex(sceneName);
                if (buildIndex < 0) continue;

                resolvedSceneName = sceneName;
                return buildIndex;
            }

            resolvedSceneName = worldSceneName;
            return -1;
        }

        private string GetWorldSceneCandidateSummary()
        {
            var summary = string.IsNullOrWhiteSpace(worldSceneName)
                ? "(no configured world scene)"
                : $"'{worldSceneName}'";

            var fallbackSummary = WorldSceneFallbackNames
                .Where(fallbackName => !string.IsNullOrWhiteSpace(fallbackName))
                .Where(fallbackName => string.IsNullOrWhiteSpace(worldSceneName)
                                       || !string.Equals(worldSceneName, fallbackName,
                                           StringComparison.OrdinalIgnoreCase))
                .Select(fallbackName => $"'{fallbackName}'")
                .ToArray();

            if (fallbackSummary.Length <= 0) return summary;

            return $"{summary}, {string.Join(", ", fallbackSummary)}";
        }

        private void RequireComponent<T>(string displayName) where T : Component
        {
            if (FindAnyObjectByType<T>(FindObjectsInactive.Include) == null) AddError($"Missing required component: {displayName}");
        }

        private void RecommendComponent<T>(string displayName) where T : Component
        {
            if (FindFirstObjectByType<T>() == null) AddWarning($"Recommended component not found: {displayName}");
        }

        private void AddError(string message)
        {
            _errors.Add(message);
            Debug.LogError($"[StartupReadinessValidator] {message}", this);
        }

        private void AddWarning(string message)
        {
            _warnings.Add(message);
            Debug.LogWarning($"[StartupReadinessValidator] {message}", this);
        }

        private void EmitValidationSummary()
        {
            if (_errors.Count <= 0 && _warnings.Count <= 0)
            {
                if (logSuccessSummary) Debug.Log("[StartupReadinessValidator] Validation passed with no issues.", this);
                return;
            }

            var summary =
                $"[StartupReadinessValidator] Validation complete. Errors: {_errors.Count}, Warnings: {_warnings.Count}.";

            if (_errors.Count > 0)
                Debug.LogError(summary, this);
            else
                Debug.LogWarning(summary, this);
        }
    }
}