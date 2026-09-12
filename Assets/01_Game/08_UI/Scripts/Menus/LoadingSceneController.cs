#region

#if ENABLE_INPUT_SYSTEM
using InputSystemUIInputModule = UnityEngine.InputSystem.UI.InputSystemUIInputModule;
#endif
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Zombera.Core;
using UnityUiEventSystem = UnityEngine.EventSystems.EventSystem;

#endregion

// ReSharper disable LoopCanBeConvertedToQuery

namespace Zombera.UI.Menus
{
    /// <summary>
    ///     Fallback world loader used only when Loading scene is entered without GameManager alive.
    ///     GameManager-driven sessions still own the authoritative load flow.
    /// </summary>
    public sealed class LoadingSceneController : MonoBehaviour
    {
        private const string DefaultLoadingSceneName = "Loading";
        private const string DefaultWorldSceneName = "World";
        private const string DefaultMainMenuSceneName = "MainMenu";

        private static bool _fallbackWorldLoadStarted;
        private static string _fallbackConfiguredLoadingSceneName = DefaultLoadingSceneName;
        private static string _fallbackConfiguredWorldSceneName = DefaultWorldSceneName;
        private static string _fallbackConfiguredMainMenuSceneName = DefaultMainMenuSceneName;

        private static readonly string[] WorldSceneFallbackNames =
        {
            "World_MapMagicStream",
            "World_Map_MagicStream"
        };

        [SerializeField] private string loadingSceneName = DefaultLoadingSceneName;
        [SerializeField] private string worldSceneName = DefaultWorldSceneName;
        [SerializeField] private string mainMenuSceneName = DefaultMainMenuSceneName;
        [SerializeField] [Min(0f)] private float minimumDisplaySeconds = 1f;
        [SerializeField] [Min(5f)] private float worldSceneLoadTimeoutSeconds = 90f;
        [SerializeField] [Min(2f)] private float worldSceneActivationTimeoutSeconds = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneLoadedHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _fallbackWorldLoadStarted = false;
            _fallbackConfiguredLoadingSceneName = DefaultLoadingSceneName;
            _fallbackConfiguredWorldSceneName = DefaultWorldSceneName;
            _fallbackConfiguredMainMenuSceneName = DefaultMainMenuSceneName;
        }

        public static void ConfigureFallback(string loadingSceneName, string worldSceneName, string mainMenuSceneName)
        {
            if (!string.IsNullOrWhiteSpace(loadingSceneName))
                _fallbackConfiguredLoadingSceneName = loadingSceneName.Trim();

            if (!string.IsNullOrWhiteSpace(worldSceneName))
                _fallbackConfiguredWorldSceneName = worldSceneName.Trim();

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
                _fallbackConfiguredMainMenuSceneName = mainMenuSceneName.Trim();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = mode;

            EnsureDisplayCameraPresentForLoadingScene(scene);

            if (!scene.IsValid() ||
                !string.Equals(scene.name, _fallbackConfiguredLoadingSceneName, StringComparison.OrdinalIgnoreCase))
            {
                _fallbackWorldLoadStarted = false;
                return;
            }

            if (GameManagerGateway.HasInstance || _fallbackWorldLoadStarted) return;

            if (!TryResolveLoadableWorldScene(_fallbackConfiguredWorldSceneName, out var resolvedWorldSceneName))
            {
                Debug.LogError(
                    $"[LoadingSceneController] No loadable world scene found. Checked {GetWorldSceneCandidateSummary(_fallbackConfiguredWorldSceneName)}.");
                return;
            }

            var runnerObject = new GameObject("LoadingSceneFallbackRunner");
            DontDestroyOnLoad(runnerObject);

            var runner = runnerObject.AddComponent<LoadingSceneController>();
            runner.loadingSceneName = _fallbackConfiguredLoadingSceneName;
            runner.worldSceneName = resolvedWorldSceneName;
            runner.mainMenuSceneName = _fallbackConfiguredMainMenuSceneName;
            runner.BeginFallbackLoad();
        }

        private void BeginFallbackLoad()
        {
            if (_fallbackWorldLoadStarted)
            {
                Destroy(gameObject);
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() ||
                !string.Equals(activeScene.name, loadingSceneName, StringComparison.OrdinalIgnoreCase))
            {
                Destroy(gameObject);
                return;
            }

            if (!TryResolveLoadableWorldScene(worldSceneName, out var resolvedWorldSceneName))
            {
                StartCoroutine(HandleFallbackFailure(
                    $"No loadable world scene found. Checked {GetWorldSceneCandidateSummary(worldSceneName)}."));
                return;
            }

            worldSceneName = resolvedWorldSceneName;

            _fallbackWorldLoadStarted = true;
            LoadingScreenOverlay.Show("Preparing world load...");
            LoadingScreenOverlay.SetProgress(0f, "Preparing world load...");
            DisableUnityUiEventSystemsForSceneTransition();
            StartCoroutine(LoadWorldSceneAsync());
        }

        private static void EnsureDisplayCameraPresentForLoadingScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            if (!string.Equals(scene.name, _fallbackConfiguredLoadingSceneName, StringComparison.OrdinalIgnoreCase))
                return;

            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Camera previewReference = null;

            for (var i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || cam.gameObject.scene != scene || !cam.gameObject.activeInHierarchy || !cam.enabled)
                    continue;

                if (cam.targetTexture == null && cam.targetDisplay == 0 && cam.cameraType == CameraType.Game)
                    return;

                if (previewReference == null && cam.cameraType == CameraType.Game)
                    previewReference = cam;
            }

            var cameraObject = new GameObject("LoadingDisplayCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            try
            {
                cameraObject.tag = "MainCamera";
            }
            catch (UnityException)
            {
                // Keep fallback camera creation robust even if project tags are customized.
            }

            var displayCamera = cameraObject.AddComponent<Camera>();
            displayCamera.enabled = true;
            displayCamera.cameraType = CameraType.Game;
            displayCamera.targetDisplay = 0;
            displayCamera.targetTexture = null;
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = Color.black;
            displayCamera.nearClipPlane = 0.1f;
            displayCamera.farClipPlane = 3000f;

            if (previewReference != null)
            {
                cameraObject.transform.SetPositionAndRotation(previewReference.transform.position,
                    previewReference.transform.rotation);
                displayCamera.fieldOfView = previewReference.fieldOfView;
            }
            else
            {
                cameraObject.transform.position = new Vector3(0f, 2f, -8f);
                cameraObject.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            }

            if (FindFirstObjectByType<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();

            if (Application.isEditor || Debug.isDebugBuild)
                Debug.Log("[LoadingSceneController] Created fallback display camera for Loading scene.");
        }

        private static bool TryResolveLoadableWorldScene(string configuredWorldSceneName,
            out string resolvedWorldSceneName)
        {
            if (!string.IsNullOrWhiteSpace(configuredWorldSceneName)
                && Application.CanStreamedLevelBeLoaded(configuredWorldSceneName))
            {
                resolvedWorldSceneName = configuredWorldSceneName;
                return true;
            }

            foreach (var fallbackName in WorldSceneFallbackNames)
            {
                if (string.IsNullOrWhiteSpace(fallbackName)) continue;

                if (!Application.CanStreamedLevelBeLoaded(fallbackName)) continue;

                resolvedWorldSceneName = fallbackName;
                return true;
            }

            resolvedWorldSceneName = configuredWorldSceneName;
            return false;
        }

        private static string GetWorldSceneCandidateSummary(string configuredWorldSceneName)
        {
            var summary = string.IsNullOrWhiteSpace(configuredWorldSceneName)
                ? "(no configured world scene)"
                : $"'{configuredWorldSceneName}'";

            foreach (var fallbackName in WorldSceneFallbackNames)
            {
                if (string.IsNullOrWhiteSpace(fallbackName)) continue;

                if (!string.IsNullOrWhiteSpace(configuredWorldSceneName)
                    && string.Equals(configuredWorldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase))
                    continue;

                summary += $", '{fallbackName}'";
            }

            return summary;
        }

        private IEnumerator LoadWorldSceneAsync()
        {
            var loadSucceeded = false;

            try
            {
                var loadOperation = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Single);

                if (loadOperation == null)
                {
                    yield return HandleFallbackFailure(
                        $"LoadSceneAsync returned null for world scene '{worldSceneName}'.");
                    yield break;
                }

                loadOperation.allowSceneActivation = false;
                var displayedProgress = 0.08f;
                LoadingScreenOverlay.SetProgress(displayedProgress, "Loading world scene...");

                var loadStartedAt = Time.unscaledTime;
                var loadTimeoutSeconds = Mathf.Max(5f, worldSceneLoadTimeoutSeconds);

                while (loadOperation.progress < 0.9f)
                {
                    if (Time.unscaledTime - loadStartedAt >= loadTimeoutSeconds)
                    {
                        yield return HandleFallbackFailure(
                            $"Timed out loading world scene '{worldSceneName}' before activation.");
                        yield break;
                    }

                    var normalized = Mathf.Clamp01(loadOperation.progress / 0.9f);
                    var targetProgress = Mathf.Lerp(0.10f, 0.84f, normalized);
                    displayedProgress =
                        Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 0.70f);
                    LoadingScreenOverlay.SetProgress(displayedProgress, "Loading world scene...");
                    yield return null;
                }

                var minimumDuration = Mathf.Max(0f, minimumDisplaySeconds);
                var elapsed = 0f;
                var finalizeStartProgress = displayedProgress;

                while (elapsed < minimumDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = minimumDuration > 0f ? Mathf.Clamp01(elapsed / minimumDuration) : 1f;
                    displayedProgress = Mathf.Lerp(finalizeStartProgress, 0.92f, t);
                    LoadingScreenOverlay.SetProgress(displayedProgress, "Preparing world data...");
                    yield return null;
                }

                yield return null;
                displayedProgress = Mathf.Max(displayedProgress, 0.93f);
                LoadingScreenOverlay.SetProgress(displayedProgress, "Activating world scene...");

                // Prevent one-frame duplicate UI EventSystem overlap during activation.
                DisableUnityUiEventSystemsForSceneTransition();
                loadOperation.allowSceneActivation = true;

                var activationElapsed = 0f;
                var activationTimeoutSeconds = Mathf.Max(2f, worldSceneActivationTimeoutSeconds);

                while (!loadOperation.isDone)
                {
                    activationElapsed += Time.unscaledDeltaTime;

                    if (activationElapsed >= activationTimeoutSeconds)
                    {
                        yield return HandleFallbackFailure(
                            $"Timed out activating world scene '{worldSceneName}'.");
                        yield break;
                    }

                    var activationTarget = Mathf.Lerp(0.93f, 0.97f, Mathf.Clamp01(activationElapsed / 0.75f));
                    displayedProgress = Mathf.Max(displayedProgress, activationTarget);
                    LoadingScreenOverlay.SetProgress(displayedProgress, "Activating world scene...");
                    yield return null;
                }

                yield return null;
                EnsureUnityUiEventSystemPresent();
#if ENABLE_INPUT_SYSTEM
                EnsureInputSystemUiActions();
#endif
                LoadingScreenOverlay.SetProgress(Mathf.Max(displayedProgress, 1f), "Ready");
                yield return LoadingScreenOverlay.WaitForVisualProgress(1f, 2.5f);
                LoadingScreenOverlay.Hide();
                loadSucceeded = true;
                _fallbackWorldLoadStarted = false;
                Destroy(gameObject);
            }
            finally
            {
                if (!loadSucceeded)
                {
                    _fallbackWorldLoadStarted = false;
                    LoadingScreenOverlay.Hide();
                    Destroy(gameObject);
                }
            }
        }

        private IEnumerator HandleFallbackFailure(string reason)
        {
            var message = string.IsNullOrWhiteSpace(reason)
                ? "World load failed in fallback loader."
                : reason.Trim();
            Debug.LogError("[LoadingSceneController] " + message, this);

            LoadingScreenOverlay.Show("World load failed.");
            LoadingScreenOverlay.SetProgress(1f, "World load failed. Returning to main menu...");
            yield return LoadingScreenOverlay.WaitForVisualProgress(1f, 1.5f);
            LoadingScreenOverlay.Hide();

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName)
                && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                DisableUnityUiEventSystemsForSceneTransition();
                SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
                yield break;
            }

            Debug.LogError(
                $"[LoadingSceneController] Main menu scene '{mainMenuSceneName}' is not loadable. Cannot recover from fallback load failure.",
                this);
        }

        private static void DisableUnityUiEventSystemsForSceneTransition()
        {
            var systems =
                FindObjectsByType<UnityUiEventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var candidate in systems)
            {
                if (candidate == null) continue;

                candidate.enabled = false;

                var modules =
                    candidate.GetComponents<BaseInputModule>();

                foreach (var module in modules)
                    if (module != null)
                        module.enabled = false;
            }

            if (UnityUiEventSystem.current != null)
                UnityUiEventSystem.current = null;
        }

        private static void EnsureUnityUiEventSystemPresent()
        {
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureInputSystemUiActions()
        {
            var module = FindFirstObjectByType<InputSystemUIInputModule>();
            EnsureInputSystemUiActions(module);
        }

        private static void EnsureInputSystemUiActions(InputSystemUIInputModule module)
        {
            if (module == null) return;

            var hasEssentialActions =
                module.point != null && module.point.action != null &&
                module.leftClick != null && module.leftClick.action != null &&
                module.submit != null && module.submit.action != null;

            if (!hasEssentialActions) module.AssignDefaultActions();

            if (module.actionsAsset != null && !module.actionsAsset.enabled) module.actionsAsset.Enable();
        }
#endif
    }
}