using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zombera.Core;
#if ENABLE_INPUT_SYSTEM
using InputSystemUIInputModule = UnityEngine.InputSystem.UI.InputSystemUIInputModule;
#endif
using StandaloneInputModule = UnityEngine.EventSystems.StandaloneInputModule;
using UnityUiEventSystem = UnityEngine.EventSystems.EventSystem;

namespace Zombera.UI.Menus
{
    /// <summary>
    /// Fallback world loader used only when Loading scene is entered without GameManager alive.
    /// GameManager-driven sessions still own the authoritative load flow.
    /// </summary>
    public sealed class LoadingSceneController : MonoBehaviour
    {
        [SerializeField] private string loadingSceneName = "Loading";
        [SerializeField] private string worldSceneName = "World";
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = 1f;

        private static bool fallbackWorldLoadStarted;
        private static readonly string[] WorldSceneFallbackNames =
        {
            "World_MapMagicStream",
            "World_Map_MagicStream"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneLoadedHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            fallbackWorldLoadStarted = false;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = mode;

            if (!scene.IsValid() || !string.Equals(scene.name, "Loading", StringComparison.OrdinalIgnoreCase))
            {
                fallbackWorldLoadStarted = false;
                return;
            }

            if (GameManager.Instance != null || fallbackWorldLoadStarted)
            {
                return;
            }

            if (!TryResolveLoadableWorldScene("World", out string resolvedWorldSceneName))
            {
                Debug.LogError($"[LoadingSceneController] No loadable world scene found. Checked {GetWorldSceneCandidateSummary("World")}.");
                return;
            }

            GameObject runnerObject = new GameObject("LoadingSceneFallbackRunner");
            DontDestroyOnLoad(runnerObject);

            LoadingSceneController runner = runnerObject.AddComponent<LoadingSceneController>();
            runner.loadingSceneName = "Loading";
            runner.worldSceneName = resolvedWorldSceneName;
            runner.BeginFallbackLoad();
        }

        private void BeginFallbackLoad()
        {
            if (fallbackWorldLoadStarted)
            {
                Destroy(gameObject);
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !string.Equals(activeScene.name, loadingSceneName, StringComparison.OrdinalIgnoreCase))
            {
                Destroy(gameObject);
                return;
            }

            if (!TryResolveLoadableWorldScene(worldSceneName, out string resolvedWorldSceneName))
            {
                Debug.LogError($"[LoadingSceneController] No loadable world scene found. Checked {GetWorldSceneCandidateSummary(worldSceneName)}.", this);
                Destroy(gameObject);
                return;
            }

            worldSceneName = resolvedWorldSceneName;

            fallbackWorldLoadStarted = true;
            LoadingScreenOverlay.Show("Preparing world load...");
            LoadingScreenOverlay.SetProgress(0f, "Preparing world load...");
            DisableUnityUiEventSystemsForSceneTransition();
            StartCoroutine(LoadWorldSceneAsync());
        }

        private static bool TryResolveLoadableWorldScene(string configuredWorldSceneName, out string resolvedWorldSceneName)
        {
            if (!string.IsNullOrWhiteSpace(configuredWorldSceneName)
                && Application.CanStreamedLevelBeLoaded(configuredWorldSceneName))
            {
                resolvedWorldSceneName = configuredWorldSceneName;
                return true;
            }

            for (int i = 0; i < WorldSceneFallbackNames.Length; i++)
            {
                string fallbackName = WorldSceneFallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    continue;
                }

                if (!Application.CanStreamedLevelBeLoaded(fallbackName))
                {
                    continue;
                }

                resolvedWorldSceneName = fallbackName;
                return true;
            }

            resolvedWorldSceneName = configuredWorldSceneName;
            return false;
        }

        private static string GetWorldSceneCandidateSummary(string configuredWorldSceneName)
        {
            string summary = string.IsNullOrWhiteSpace(configuredWorldSceneName)
                ? "(no configured world scene)"
                : $"'{configuredWorldSceneName}'";

            for (int i = 0; i < WorldSceneFallbackNames.Length; i++)
            {
                string fallbackName = WorldSceneFallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(configuredWorldSceneName)
                    && string.Equals(configuredWorldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                summary += $", '{fallbackName}'";
            }

            return summary;
        }

        private IEnumerator LoadWorldSceneAsync()
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Single);

            if (loadOperation == null)
            {
                fallbackWorldLoadStarted = false;
                Destroy(gameObject);
                yield break;
            }

            loadOperation.allowSceneActivation = false;
            float displayedProgress = 0.08f;
            LoadingScreenOverlay.SetProgress(displayedProgress, "Loading world scene...");

            while (loadOperation.progress < 0.9f)
            {
                float normalized = Mathf.Clamp01(loadOperation.progress / 0.9f);
                float targetProgress = Mathf.Lerp(0.10f, 0.84f, normalized);
                displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 0.70f);
                LoadingScreenOverlay.SetProgress(displayedProgress, "Loading world scene...");
                yield return null;
            }

            float minimumDuration = Mathf.Max(0f, minimumDisplaySeconds);
            float elapsed = 0f;
            float finalizeStartProgress = displayedProgress;

            while (elapsed < minimumDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = minimumDuration > 0f ? Mathf.Clamp01(elapsed / minimumDuration) : 1f;
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

            float activationElapsed = 0f;

            while (!loadOperation.isDone)
            {
                activationElapsed += Time.unscaledDeltaTime;
                float activationTarget = Mathf.Lerp(0.93f, 0.97f, Mathf.Clamp01(activationElapsed / 0.75f));
                displayedProgress = Mathf.Max(displayedProgress, activationTarget);
                LoadingScreenOverlay.SetProgress(displayedProgress, "Activating world scene...");
                yield return null;
            }

            yield return null;
            EnsureUnityUiEventSystemPresent();
            LoadingScreenOverlay.SetProgress(Mathf.Max(displayedProgress, 1f), "Ready");
            yield return LoadingScreenOverlay.WaitForVisualProgress(1f, 2.5f);
            LoadingScreenOverlay.Hide();
            fallbackWorldLoadStarted = false;
            Destroy(gameObject);
        }

        private static void DisableUnityUiEventSystemsForSceneTransition()
        {
            UnityUiEventSystem[] systems =
                FindObjectsByType<UnityUiEventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < systems.Length; i++)
            {
                UnityUiEventSystem candidate = systems[i];
                if (candidate == null)
                {
                    continue;
                }

                candidate.enabled = false;

                UnityEngine.EventSystems.BaseInputModule[] modules =
                    candidate.GetComponents<UnityEngine.EventSystems.BaseInputModule>();

                for (int m = 0; m < modules.Length; m++)
                {
                    if (modules[m] != null)
                    {
                        modules[m].enabled = false;
                    }
                }
            }
        }

        private static void EnsureUnityUiEventSystemPresent()
        {
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
        }

        private static bool ShouldAutoCreateUnityUiEventSystem(Scene activeScene)
        {
            GraphicRaycaster[] raycasters =
                FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < raycasters.Length; i++)
            {
                GraphicRaycaster raycaster = raycasters[i];

                if (raycaster == null)
                {
                    continue;
                }

                if (raycaster.gameObject.scene == activeScene)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureUiInputModule(GameObject eventSystemObject)
        {
#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputSystemModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                inputSystemModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            EnsureInputSystemUiActions(inputSystemModule);

            StandaloneInputModule legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(legacyModule);
                }
                else
                {
                    DestroyImmediate(legacyModule);
                }
            }
#else
            if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureInputSystemUiActions(InputSystemUIInputModule module)
        {
            if (module == null)
            {
                return;
            }

            bool hasEssentialActions =
                module.point != null && module.point.action != null &&
                module.leftClick != null && module.leftClick.action != null &&
                module.submit != null && module.submit.action != null;

            if (!hasEssentialActions)
            {
                module.AssignDefaultActions();
            }

            if (module.actionsAsset != null && !module.actionsAsset.enabled)
            {
                module.actionsAsset.Enable();
            }
        }
#endif
    }
}
