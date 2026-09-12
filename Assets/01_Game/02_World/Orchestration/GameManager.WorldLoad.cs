using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.UI;
using Zombera.UI.Menus;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        private IEnumerator LoadWorldSceneAsync(string targetWorldSceneName)
        {
            _worldLoadInProgress = true;
            var loadCompleted = false;
            LoadingScreenOverlay.Show("Loading world scene...");
            var displayedProgress = LoadingProgressPrepareWorldLoad;
            LoadingScreenOverlay.SetProgress(displayedProgress, "Loading world scene...");

            try
            {
                var loadOperation = SceneManager.LoadSceneAsync(targetWorldSceneName, LoadSceneMode.Single);

                if (loadOperation == null)
                {
                    yield return RecoverFromWorldLoadFailure(
                        $"LoadSceneAsync returned null for world scene '{targetWorldSceneName}'.");
                    yield break;
                }

                // Keep the loading scene visible until world data is ready to activate.
                loadOperation.allowSceneActivation = false;

                var loadStartedAt = Time.unscaledTime;
                var loadTimeoutSeconds = Mathf.Max(5f, worldSceneLoadTimeoutSeconds);

                while (loadOperation.progress < 0.9f)
                {
                    if (Time.unscaledTime - loadStartedAt >= loadTimeoutSeconds)
                    {
                        yield return RecoverFromWorldLoadFailure(
                            $"Timed out loading world scene '{targetWorldSceneName}' before activation.");
                        yield break;
                    }

                    var normalized = Mathf.Clamp01(loadOperation.progress / 0.9f);
                    var targetProgress = Mathf.Lerp(LoadingProgressSceneLoadStart, LoadingProgressSceneLoadEnd, normalized);
                    displayedProgress =
                        Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 0.70f);
                    LoadingScreenOverlay.SetProgress(displayedProgress, "Loading world scene...");
                    yield return null;
                }

                var minimumDuration = Mathf.Max(0f, minimumLoadingScreenSeconds);
                var elapsed = 0f;
                var finalizeStartProgress = displayedProgress;

                while (elapsed < minimumDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = minimumDuration > 0f ? Mathf.Clamp01(elapsed / minimumDuration) : 1f;
                    displayedProgress = Mathf.Lerp(finalizeStartProgress, LoadingProgressSceneDataPrepEnd, t);
                    LoadingScreenOverlay.SetProgress(displayedProgress, "Preparing world data...");
                    yield return null;
                }

                yield return null;
                displayedProgress = Mathf.Max(displayedProgress, LoadingProgressSceneActivationStart);
                LoadingScreenOverlay.SetProgress(displayedProgress, "Activating world scene...");

                // Avoid one-frame duplicate UGUI EventSystem overlap during async scene activation.
                DisableUnityUiEventSystemsForSceneTransition();
                loadOperation.allowSceneActivation = true;

                var activationElapsed = 0f;
                var activationTimeoutSeconds = Mathf.Max(2f, worldSceneActivationTimeoutSeconds);

                while (!loadOperation.isDone)
                {
                    activationElapsed += Time.unscaledDeltaTime;

                    if (activationElapsed >= activationTimeoutSeconds)
                    {
                        yield return RecoverFromWorldLoadFailure(
                            $"Timed out activating world scene '{targetWorldSceneName}'.");
                        yield break;
                    }

                    var activationTarget = Mathf.Lerp(
                        LoadingProgressSceneActivationStart,
                        LoadingProgressSceneActivationEnd,
                        Mathf.Clamp01(activationElapsed / 0.75f));
                    displayedProgress = Mathf.Max(displayedProgress, activationTarget);
                    LoadingScreenOverlay.SetProgress(displayedProgress, "Activating world scene...");
                    yield return null;
                }

                LoadingScreenOverlay.SetProgress(Mathf.Max(displayedProgress, LoadingProgressSceneLoaded),
                    "World scene loaded.");
                loadCompleted = true;
            }
            finally
            {
                _worldLoadInProgress = false;

                if (!loadCompleted)
                    LoadingScreenOverlay.Hide();
            }
        }

        private IEnumerator RecoverFromWorldLoadFailure(string reason)
        {
            var message = string.IsNullOrWhiteSpace(reason)
                ? "World load failed."
                : reason.Trim();
            Debug.LogError("[GameManager] " + message, this);

            ClearPendingSessionRequest();
            LoadingScreenOverlay.Show("World load failed.");
            LoadingScreenOverlay.SetProgress(1f, "World load failed. Returning to main menu...");
            yield return LoadingScreenOverlay.WaitForVisualProgress(1f, 1.5f);
            LoadingScreenOverlay.Hide();

            SetGameState(GameState.MainMenu);
            ReturnToMainMenuSceneAfterFailure("world load failure");
        }

        private void HandleWorldSessionStartupFailure(string reason)
        {
            var message = string.IsNullOrWhiteSpace(reason)
                ? "World session startup failed."
                : reason.Trim();
            Debug.LogError("[GameManager] " + message, this);

            ClearPendingSessionRequest();
            _worldLoadInProgress = false;
            LoadingScreenOverlay.Hide();

            SetGameState(GameState.MainMenu);
            ReturnToMainMenuSceneAfterFailure("world session startup");
        }

        private void ReturnToMainMenuSceneAfterFailure(string failureContext)
        {
            if (string.IsNullOrWhiteSpace(mainMenuSceneName)
                || !Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Debug.LogError(
                    $"[GameManager] Cannot recover from {failureContext}: main menu scene '{mainMenuSceneName}' is not loadable.",
                    this);
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() &&
                string.Equals(activeScene.name, mainMenuSceneName, StringComparison.OrdinalIgnoreCase))
                return;

            DisableUnityUiEventSystemsForSceneTransition();
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }

        private void TryLoadMainMenuSceneOnInitialize()
        {
            if (!loadMainMenuSceneOnInitialize || autoStartSessionForTesting ||
                string.IsNullOrWhiteSpace(mainMenuSceneName)) return;

            var activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() &&
                string.Equals(activeScene.name, mainMenuSceneName, StringComparison.OrdinalIgnoreCase)) return;

            if (IsWorldScene(activeScene)
                && (!loadMainMenuWhenInitialSceneIsWorld || autoStartSessionForTesting))
                return;

            if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Debug.LogWarning(
                    $"[GameManager] Main menu scene '{mainMenuSceneName}' is not in Build Settings; cannot switch away from '{activeScene.name}'.",
                    this);
                return;
            }

            DisableUnityUiEventSystemsForSceneTransition();

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private bool TryLoadLoadingSceneForSessionStart()
        {
            if (!useIntermediateLoadingScene || string.IsNullOrWhiteSpace(loadingSceneName)) return false;

            if (!Application.CanStreamedLevelBeLoaded(loadingSceneName))
            {
                Debug.LogWarning(
                    $"[GameManager] Loading scene '{loadingSceneName}' is not in Build Settings. Falling back to direct world load.",
                    this);
                return false;
            }

            var activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() &&
                string.Equals(activeScene.name, loadingSceneName, StringComparison.OrdinalIgnoreCase)) return false;

            DisableUnityUiEventSystemsForSceneTransition();
            SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);
            return true;
        }

        private void BeginPendingWorldLoadOrSession()
        {
            if (!HasPendingSessionRequest()) return;

            LoadingScreenOverlay.SetProgress(LoadingProgressPrepareWorldLoad, "Preparing world load...");

            if (TryLoadWorldSceneForSessionStart()) return;

            var activeScene = SceneManager.GetActiveScene();

            if (!IsWorldScene(activeScene))
            {
                StartCoroutine(RecoverFromWorldLoadFailure(
                    "No loadable world scene is available. Returning to main menu."));
                return;
            }

            TryFinalizePendingWorldSession(activeScene);
        }

        private bool TryLoadWorldSceneForSessionStart()
        {
            if (!loadWorldSceneOnSessionStart) return false;

            if (!TryResolveLoadableWorldScene(out var targetWorldSceneName, out var usedFallback))
            {
                Debug.LogError(
                    $"[GameManager] No loadable world scene found. Checked {GetWorldSceneCandidatesSummary()}.", this);
                return false;
            }

            if (usedFallback)
                Debug.LogWarning(
                    $"[GameManager] Configured world scene '{worldSceneName}' was not loadable. Using '{targetWorldSceneName}' instead.",
                    this);

            if (_worldLoadInProgress) return true;

            var activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() &&
                string.Equals(activeScene.name, targetWorldSceneName, StringComparison.OrdinalIgnoreCase)) return false;

            worldSceneName = targetWorldSceneName;
            StartCoroutine(LoadWorldSceneAsync(targetWorldSceneName));
            return true;
        }
    }
}