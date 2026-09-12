#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.World;
using Zombera.Environment;
using Zombera.Inventory;
using Zombera.UI.SquadManagement;

namespace Zombera.Systems
{
    /// <summary>
    ///     Central save orchestration manager that coordinates persistence across gameplay systems.
    ///     Uses a provider-based architecture for modularity and maintainability.
    /// </summary>
    public sealed partial class SaveManager : MonoBehaviour, IGameSystem, ISaveManagerGateway
    {
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private ItemSaveRegistry itemSaveRegistry;
        [SerializeField] [Min(30f)] private float autosaveIntervalSeconds = 300f;

        private readonly List<ISaveProvider> _providers = new();
        private float _autosaveTimer;
        private float _sessionPlayTime;
        private GameSaveData _deferredRestoreData;
        private bool _hasDeferredRestore;

        public string ActiveSlotId { get; private set; }

        /// <summary>Prefer GameManager's SaveManager; otherwise the instance with the most providers.</summary>
        public static SaveManager ResolveActive()
        {
            if (GameManager.HasInstance)
            {
                var sessionManager = GameManager.Instance.ActiveSaveManager;
                if (sessionManager != null) return sessionManager;
            }

            var managers = UnityEngine.Object.FindObjectsByType<SaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers == null || managers.Length == 0) return null;
            if (managers.Length == 1) return managers[0];

            SaveManager best = managers[0];
            var bestCount = -1;
            for (var i = 0; i < managers.Length; i++)
            {
                var candidate = managers[i];
                if (candidate == null) continue;

                var count = candidate.GetRegisteredProviderCount();
                if (count <= bestCount) continue;
                bestCount = count;
                best = candidate;
            }

            return best;
        }

        public int GetRegisteredProviderCount()
        {
            RefreshProviders();
            return _providers.Count;
        }

        private bool IsAuthoritativeInstance()
        {
            var active = ResolveActive();
            return active == null || ReferenceEquals(active, this);
        }

        public void SetActiveSlot(string slotId)
        {
            ActiveSlotId = slotId;
            _autosaveTimer = autosaveIntervalSeconds;
        }

        private void Update()
        {
            if (!IsInitialized || string.IsNullOrWhiteSpace(ActiveSlotId)) return;
            if (!IsAuthoritativeInstance()) return;

            if (GameManager.HasInstance)
            {
                var state = GameManager.Instance.CurrentState;
                if (state is not GameState.LoadingWorld and not GameState.Playing and not GameState.Paused)
                    return;
            }

            _sessionPlayTime += Time.deltaTime;
            _autosaveTimer -= Time.deltaTime;

            if (_autosaveTimer > 0f) return;

            _autosaveTimer = autosaveIntervalSeconds;
            SaveGame(ActiveSlotId);
        }

        public bool IsInitialized { get; private set; }

        private void OnEnable()
        {
            SaveManagerGateway.Resolver = ResolveActive;
        }

        private void OnDisable()
        {
            if (SaveManagerGateway.Resolver == ResolveActive)
                SaveManagerGateway.Resolver = null;
        }

        public void Initialize()
        {
            if (saveSystem != null && !saveSystem.IsInitialized) saveSystem.Initialize();
            if (itemSaveRegistry != null) itemSaveRegistry.Initialize();

            EnsureDefaultProvidersPresent();
            RefreshProviders();

            if (IsInitialized) return;

            IsInitialized = true;
            _autosaveTimer = autosaveIntervalSeconds;
        }

        /// <summary>
        ///     Boot scene SaveManager historically shipped with only CraftingSaveProvider; ensure the full provider set exists.
        /// </summary>
        private void EnsureDefaultProvidersPresent()
        {
            EnsureProviderComponent<WorldSaveProvider>();
            EnsureProviderComponent<PlayerSaveProvider>();
            EnsureProviderComponent<MapSaveProvider>();
            EnsureProviderComponent<ZombieSaveProvider>();
            EnsureProviderComponent<BuildingSaveProvider>();
            EnsureProviderComponent<LootSaveProvider>();
            EnsureProviderComponent<PickupSaveProvider>();
            EnsureProviderComponent<CraftingSaveProvider>();
            EnsureProviderComponent<JobSaveProvider>();
            EnsureProviderComponent<FactionSaveProvider>();
        }

        private void EnsureProviderComponent<TProvider>() where TProvider : MonoBehaviour
        {
            if (GetComponent<TProvider>() != null) return;
            if (!typeof(ISaveProvider).IsAssignableFrom(typeof(TProvider))) return;

            gameObject.AddComponent<TProvider>();
            Debug.Log("[SaveManager] Auto-added missing provider " + typeof(TProvider).Name + " on '" + name + "'.", this);
        }

        private void RefreshProviders()
        {
            _providers.Clear();

            // Unity does not resolve plain C# interfaces via GetComponentsInChildren<ISaveProvider>().
            CollectProvidersFromBehaviours(GetComponents<MonoBehaviour>());
            CollectProvidersFromBehaviours(GetComponentsInChildren<MonoBehaviour>(true));

            if (_providers.Count <= 1)
            {
                var sceneBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                CollectProvidersFromBehaviours(sceneBehaviours);
            }

            if (_providers.Count == 0)
            {
                Debug.LogWarning(
                    "[SaveManager] No save providers found on '" + name +
                    "'. Squad/player/world state will not be captured or restored.",
                    this);
                return;
            }

            Debug.Log(
                "[SaveManager] Registered " + _providers.Count + " provider(s) on '" + name + "': " +
                string.Join(", ", _providers.Select(p => p.GetType().Name)) + ".",
                this);
        }

        private void CollectProvidersFromBehaviours(IReadOnlyList<MonoBehaviour> behaviours)
        {
            if (behaviours == null) return;

            for (var i = 0; i < behaviours.Count; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour == this) continue;
                if (behaviour is not ISaveProvider provider) continue;
                if (_providers.Contains(provider)) continue;

                _providers.Add(provider);
            }
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            IsInitialized = false;
        }

        public bool SaveGame(string slotId)
        {
            if (!IsAuthoritativeInstance())
            {
                Debug.LogWarning(
                    "[SaveManager] SaveGame ignored on non-authoritative instance '" + name +
                    "'. Use SaveManager.ResolveActive().",
                    this);
                return ResolveActive()?.SaveGame(slotId) ?? false;
            }

            SetActiveSlot(slotId);

            if (saveSystem == null)
            {
                Debug.LogError("[SaveManager] SaveGame failed: SaveSystem reference is missing.", this);
                return false;
            }

            try
            {
                if (!saveSystem.IsInitialized) saveSystem.Initialize();
                if (!IsInitialized) Initialize();
                else
                {
                    EnsureDefaultProvidersPresent();
                    RefreshProviders();
                }

                var snapshot = BuildSaveSnapshot();
                saveSystem.SaveGameData(slotId, snapshot);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save game to slot '{slotId}': {ex.Message}", this);
                return false;
            }
        }

        public void PreloadSaveData(string slotId) => PreloadSlotForSession(slotId);

        public void ApplyRuntimeRestore() => ApplyDeferredRestoration();

        public void PreloadLoadGame(string slotId) => PreloadSlotForSession(slotId);

        public void PreloadLoadData(string slotId) => PreloadSlotForSession(slotId);

        public bool PreloadSlotForSession(string slotId)
        {
            if (saveSystem == null)
            {
                Debug.LogWarning("[SaveManager] Preload skipped: SaveSystem reference is missing.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(slotId))
            {
                Debug.LogWarning("[SaveManager] Preload skipped: slot id is null or empty.", this);
                return false;
            }

            if (!IsAuthoritativeInstance())
            {
                Debug.LogWarning(
                    "[SaveManager] Preload redirected from non-authoritative instance '" + name + "'.",
                    this);
                var active = ResolveActive();
                return active != null && active.PreloadSlotForSession(slotId);
            }

            try
            {
                if (!saveSystem.IsInitialized) saveSystem.Initialize();
                if (!IsInitialized) Initialize();

                if (!TryPreloadSlot(slotId, out _deferredRestoreData))
                {
                    Debug.LogError($"[SaveManager] Failed to preload slot '{slotId}': Data not found.");
                    _hasDeferredRestore = false;
                    _deferredRestoreData = null;
                    return false;
                }

                saveSystem.ApplySaveData(_deferredRestoreData);
                SetActiveSlot(slotId);
                _hasDeferredRestore = true;
                LastDeferredRestoreError = null;
                Debug.Log(
                    $"[SaveManager] Preloaded slot '{slotId}' ({_deferredRestoreData.squad?.Count ?? 0} squad members). " +
                    "Waiting for ApplyDeferredRestoration.",
                    this);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Exception during preload of slot '{slotId}': {ex.Message}", this);
                _hasDeferredRestore = false;
                _deferredRestoreData = null;
                return false;
            }
        }

        private bool TryPreloadSlot(string slotId, out GameSaveData saveData)
        {
            saveData = null;
            return saveSystem != null
                   && saveSystem.TryLoadGameData(slotId, out saveData)
                   && saveData != null;
        }

        public void ApplyDeferredRestoration()
        {
            if (!_hasDeferredRestore || _deferredRestoreData == null) return;

            try
            {
                Debug.Log($"[SaveManager] Applying deferred restoration for slot '{ActiveSlotId}'.", this);
                RestoreRuntimeState(_deferredRestoreData);
                RefreshPortraitStudioAfterRestore();
                LastDeferredRestoreError = null;
            }
            catch (CriticalSaveProviderException ex)
            {
                LastDeferredRestoreError = ex;
                Debug.LogError($"[SaveManager] Critical deferred restoration failure: {ex.Message}", this);
                throw;
            }
            catch (Exception ex)
            {
                LastDeferredRestoreError = ex;
                Debug.LogError($"[SaveManager] ApplyDeferredRestoration failed: {ex.Message}", this);
                try { RestoreRuntimeState(new GameSaveData()); } catch { /* ignore */ }
            }
            finally
            {
                _hasDeferredRestore = false;
                _deferredRestoreData = null;
            }
        }

        public void ApplyDeferredRestore() => ApplyDeferredRestoration();

        private void RefreshPortraitStudioAfterRestore()
        {
            if (PortraitStudioManager.Instance == null) return;

            var unitManager = UnityEngine.Object.FindFirstObjectByType<UnitManager>();
            var player = unitManager != null ? unitManager.FindFirstUnitByRole(UnitRole.Player) : null;
            if (player == null) return;

            PortraitStudioManager.Instance.RefreshPortraitFromUnit(player);
        }

        /// <summary>
        ///     Immediate restore (no world spawn deferral). Prefer <see cref="GameManager.LoadGame" /> for gameplay loads.
        /// </summary>
        public void LoadGame(string slotId)
        {
            Debug.LogWarning(
                "[SaveManager] SaveManager.LoadGame restores immediately and skips world spawn deferral. " +
                "Use GameManager.LoadGame for normal loads.",
                this);

            if (PreloadSlotForSession(slotId))
                ApplyDeferredRestoration();
        }

        private GameSaveData BuildSaveSnapshot()
        {
            var saveData = new GameSaveData();

            PopulateMetadata(saveData);

            // Execute all providers to fill the snapshot in deterministic order based on Priority
            var orderedProviders = _providers.OrderByDescending(p => p.Priority).ToList();
            foreach (var provider in orderedProviders)
                InvokeProviderSave(provider, saveData);

            return saveData;
        }

        private void PopulateMetadata(GameSaveData saveData)
        {
            saveData.metadata.slotId = ActiveSlotId;
            saveData.metadata.slotName = ActiveSlotId.ToUpper();
            saveData.metadata.timestamp = DateTime.Now.ToString("MMM dd, yyyy\nHH:mm:ss");
            
            if (saveSystem != null && saveSystem.TryLoadGameData(ActiveSlotId, out var existing))
            {
                saveData.metadata.playTimeSeconds = existing.metadata.playTimeSeconds + _sessionPlayTime;
            }
            else
            {
                saveData.metadata.playTimeSeconds = _sessionPlayTime;
            }
            _sessionPlayTime = 0f;

            var dayNight = UnityEngine.Object.FindFirstObjectByType<DayNightController>();
            saveData.metadata.dayNumber = dayNight != null ? dayNight.DayNumber : 1;

            saveData.metadata.locationName = "Riverside Town";
            saveData.metadata.difficulty = "SURVIVOR";
            saveData.metadata.progressPercent = 0f;
            saveData.metadata.gameVersion = Application.version;
            
            saveData.metadata.screenshotBase64 = SaveScreenshotService.CaptureSaveSlotPreviewBase64();

            saveData.metadata.recentActivity = new List<string>
            {
                $"Last Save: {DateTime.Now:MMM dd, HH:mm:ss}"
            };
        }

        private void RestoreRuntimeState(GameSaveData saveData)
        {
            if (saveData == null) return;

            if (!IsInitialized) Initialize();
            else
            {
                EnsureDefaultProvidersPresent();
                RefreshProviders();
            }

            // Execute providers in deterministic order based on Priority
            var orderedProviders = _providers.OrderByDescending(p => p.Priority).ToList();
            
            foreach (var provider in orderedProviders)
                InvokeProviderLoad(provider, saveData);
        }
        }
        }
        #endregion