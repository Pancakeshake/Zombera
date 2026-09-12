#region

using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Core;

#endregion

namespace Zombera.UI.Menus
{
    public sealed partial class MainMenuController
    {
        private readonly struct StartFlowConfig
        {
            public readonly bool RequireCharacterCreationBeforeStart;
            public readonly bool AllowCharacterCreatorBypassWhenUiBlocked;
            public readonly float RepeatedStartBypassWindowSeconds;

            public StartFlowConfig(
                bool requireCharacterCreationBeforeStart,
                bool allowCharacterCreatorBypassWhenUiBlocked,
                float repeatedStartBypassWindowSeconds)
            {
                RequireCharacterCreationBeforeStart = requireCharacterCreationBeforeStart;
                AllowCharacterCreatorBypassWhenUiBlocked = allowCharacterCreatorBypassWhenUiBlocked;
                RepeatedStartBypassWindowSeconds = repeatedStartBypassWindowSeconds;
            }
        }

        private sealed class StartFlowRuntimeState
        {
            public float LastStartAttemptAt = -10f;
            public int LastStartHandledFrame = -1;
            public int RepeatedStartWhileCreatorVisible;
        }

        private enum LoadGameGateState
        {
            Allowed,
            MissingSlotConfiguration,
            MissingSaveFile
        }

        private bool _hasCompletedCharacterCreation;
        private readonly StartFlowRuntimeState _startFlowState = new();

        private void HandleStartGameRequested()
        {
            if (IsDuplicateStartInCurrentFrame()) return;
            AutoResolveReferences();

            if (loadCharacterCreatorOnStartRequest) EnsureCharacterCreatorReadyForStartFlow();

            var config = GetStartFlowConfig();
            if (config.RequireCharacterCreationBeforeStart && !_hasCompletedCharacterCreation)
            {
                if (TryHandleMissingCreatorBypass(config)) return;
                if (TryHandleCreatorVisibilityGate(config)) return;
            }

            Debug.Log("[MainMenuController] Start requirements satisfied; starting session.", this);
            StartSession();
        }

        private StartFlowConfig GetStartFlowConfig()
        {
            return new StartFlowConfig(
                requireCharacterCreationBeforeStart,
                allowCharacterCreatorBypassWhenUiBlocked,
                repeatedStartBypassWindowSeconds);
        }

        private bool IsDuplicateStartInCurrentFrame()
        {
            // UI listeners and pointer fallback can both fire on the same frame.
            // Ignore duplicate same-frame start requests so a single click cannot bypass creator flow.
            if (_startFlowState.LastStartHandledFrame == Time.frameCount) return true;

            _startFlowState.LastStartHandledFrame = Time.frameCount;
            return false;
        }

        private bool TryHandleMissingCreatorBypass(in StartFlowConfig config)
        {
            if (characterCreatorPanel != null) return false;

            if (config.AllowCharacterCreatorBypassWhenUiBlocked)
            {
                Debug.LogWarning(
                    "[MainMenuController] Character creator is missing; bypassing requirement and starting session.",
                    this);
                _hasCompletedCharacterCreation = true;
                StartSession();
                return true;
            }

            Debug.LogError(
                "[MainMenuController] Character creator is required before start, but Character Creator Panel is not assigned.",
                this);
            return true;
        }

        private bool TryHandleCreatorVisibilityGate(in StartFlowConfig config)
        {
            if (characterCreatorPanel == null) return false;

            var now = Time.unscaledTime;

            if (!characterCreatorPanel.IsVisible)
            {
                _startFlowState.RepeatedStartWhileCreatorVisible = 0;
                _startFlowState.LastStartAttemptAt = now;
                Debug.Log("[MainMenuController] Start requested: showing character creator first.", this);
                characterCreatorPanel.Show();
                return true;
            }

            if (now - _startFlowState.LastStartAttemptAt <= config.RepeatedStartBypassWindowSeconds)
                _startFlowState.RepeatedStartWhileCreatorVisible++;
            else
                _startFlowState.RepeatedStartWhileCreatorVisible = 1;

            _startFlowState.LastStartAttemptAt = now;

            if (ShouldBypassCreatorAfterRepeatedStart(config))
            {
                Debug.LogWarning(
                    "[MainMenuController] Repeated Start clicks while creator is visible; bypassing creator requirement.",
                    this);
                _hasCompletedCharacterCreation = true;
                StartSession();
                return true;
            }

            characterCreatorPanel.Show();
            return true;
        }

        private bool ShouldBypassCreatorAfterRepeatedStart(in StartFlowConfig config)
        {
            return config.AllowCharacterCreatorBypassWhenUiBlocked
                   && _startFlowState.RepeatedStartWhileCreatorVisible >= 2;
        }

        private void HandleContinueRequested()
        {
            var saveSystem = FindFirstObjectByType<SaveSystem>();
            if (saveSystem == null) return;

            var latestSlot = saveSystem.GetMostRecentSlotId();
            if (string.IsNullOrWhiteSpace(latestSlot)) return;

            if (GameManagerGateway.Instance == null)
            {
                Debug.LogError("[MainMenuController] Continue requested, but GameManager is unavailable.");
                return;
            }

            Debug.Log($"[MainMenuController] Continue requested for slot '{latestSlot}'.", this);
            GameManagerGateway.Instance.LoadGame(latestSlot);
            Hide();
        }

        private void HandleLoadGameRequested()
        {
            Debug.Log($"[MainMenuController] HandleLoadGameRequested. Panel assigned: {loadSavePanel != null}");
            if (loadSavePanel != null)
            {
                loadSavePanel.ShowLoadMode();
            }
            else
            {
                Debug.LogWarning("[MainMenuController] Load Game requested, but loadSavePanel is not assigned.");
            }
        }

        private void EnsureCharacterCreatorReadyForStartFlow()
        {
            if (characterCreatorPanel == null) return;

            var panelObject = characterCreatorPanel.gameObject;
            if (panelObject != null && !panelObject.activeSelf) panelObject.SetActive(true);

            characterCreatorPanel.Initialize();
        }

        private void ForceHideCharacterCreatorPanelsAtMenuBoot()
        {
            var creators =
                FindObjectsByType<CharacterCreatorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var activeScene = gameObject.scene;

            foreach (var creator in creators)
            {
                if (creator == null || creator.gameObject.scene != activeScene) continue;

                if (loadCharacterCreatorOnStartRequest && !creator.IsInitialized)
                {
                    creator.gameObject.SetActive(false);
                    continue;
                }

                creator.Hide();
            }
        }

        private void HandleCharacterCreationConfirmed(string characterName)
        {
            _ = characterName;
            _hasCompletedCharacterCreation = true;
            StartSession();
        }

        private void StartSession()
        {
            // Hide the character creator panel BEFORE the world session starts so
            // WorldHUDCanvas (sortingOrder=Hud/10) cannot render over it when
            // GameState.Playing fires and enables the canvas.
            characterCreatorPanel?.Hide();
            PrepareCharacterCreatorsForSceneTransition();
            Debug.Log("[MainMenuController] StartSession invoked.", this);

            if (GameManagerGateway.Instance != null)
            {
                if (!GameManagerGateway.Instance.IsInitialized) GameManagerGateway.Instance.InitializeSystems();

                var request = BuildWorldSessionRequest();
                Debug.Log(
                    $"[MainMenuController] Delegating world start to GameManager (tier={request.Tier}, seed={request.Seed}).",
                    this);
                GameManagerGateway.Instance.StartNewGame(request);
                Hide();
                return;
            }

            if (string.IsNullOrWhiteSpace(worldSceneName)) return;

            if (!TryResolveLoadableWorldScene(worldSceneName, out var resolvedWorldSceneName))
            {
                Debug.LogError(
                    $"[MainMenuController] No loadable world scene found. Checked {GetWorldSceneCandidateSummary(worldSceneName)}.",
                    this);
                return;
            }

            if (useLoadingSceneWhenNoGameManager &&
                !string.IsNullOrWhiteSpace(loadingSceneName) &&
                Application.CanStreamedLevelBeLoaded(loadingSceneName))
            {
                LoadingSceneController.ConfigureFallback(
                    loadingSceneName,
                    resolvedWorldSceneName,
                    SceneManager.GetActiveScene().name);
                SceneManager.LoadScene(loadingSceneName);
                Hide();
                return;
            }

            SceneManager.LoadScene(resolvedWorldSceneName);
            Hide();
        }

        private void PrepareCharacterCreatorsForSceneTransition()
        {
            var preparedCreators = new HashSet<CharacterCreatorController>();

            if (characterCreatorPanel != null)
            {
                characterCreatorPanel.PrepareForSceneTransition();
                preparedCreators.Add(characterCreatorPanel);
            }

            var creators =
                FindObjectsByType<CharacterCreatorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var activeScene = gameObject.scene;

            foreach (var creator in creators)
            {
                if (creator == null || creator.gameObject.scene != activeScene || preparedCreators.Contains(creator))
                    continue;

                creator.PrepareForSceneTransition();
            }
        }

        private void HandleSettingsRequested()
        {
            settingsPanel?.Show();
        }

        private void HandleCharacterCreatorVisibilityChanged(bool isVisible)
        {
            SetMainTitleVisible(!isVisible);
        }

        private void RefreshTitleVisibility()
        {
            var isCharacterCreatorOpen = characterCreatorPanel != null && characterCreatorPanel.IsVisible;
            SetMainTitleVisible(!isCharacterCreatorOpen);
        }

        private void SetMainTitleVisible(bool isVisible)
        {
            if (mainTitleImage != null) mainTitleImage.enabled = isVisible;
        }

        private void RefreshLoadGameButtonState()
        {
            var saveSystem = FindFirstObjectByType<SaveSystem>();
            if (saveSystem != null && !saveSystem.IsInitialized) saveSystem.Initialize();

            bool hasSaves = false;

            if (saveSystem != null)
            {
                hasSaves = saveSystem.GetAvailableSlotIds().Count > 0;
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(hasSaves);
            }

            if (loadGameButton != null)
            {
                var canOpenLoadPanel = loadSavePanel != null;
                var hasRequiredSave = !disableLoadButtonWhenSaveMissing || hasSaves;
                loadGameButton.interactable = canOpenLoadPanel && hasRequiredSave;
            }
        }

        private LoadGameGateState EvaluateLoadGameGateState()
        {
            var saveSystem = FindFirstObjectByType<SaveSystem>();
            if (saveSystem == null || saveSystem.GetAvailableSlotIds().Count == 0)
                return LoadGameGateState.MissingSaveFile;

            return LoadGameGateState.Allowed;
        }

        private bool HasLoadableSaveSlot(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId)) return false;

            var saveFolderName = string.IsNullOrWhiteSpace(loadGameSaveFolderName)
                ? "Saves"
                : loadGameSaveFolderName.Trim();

            var savePath = Path.Combine(Application.persistentDataPath, saveFolderName, slotId + ".sav");
            return File.Exists(savePath) || File.Exists(savePath + ".bak");
        }

        private static void HandleQuitRequested()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private WorldSessionRequest BuildWorldSessionRequest()
        {
            SyncWorldOptionsFromUi();
            return new WorldSessionRequest(selectedMapSizeTier, worldSeed);
        }

        private void SyncWorldOptionsFromUi()
        {
            if (mapSizeTierDropdown != null)
            {
                var index = Mathf.Clamp(mapSizeTierDropdown.value, 0, 2);
                selectedMapSizeTier = (WorldMapSizeTier)index;
            }

            if (worldSeedInputField == null || string.IsNullOrWhiteSpace(worldSeedInputField.text))
                return;

            if (int.TryParse(worldSeedInputField.text.Trim(), out var parsedSeed))
                worldSeed = parsedSeed;
        }
    }
}
