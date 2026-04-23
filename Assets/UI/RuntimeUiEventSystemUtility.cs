using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using InputSystemUIInputModule = UnityEngine.InputSystem.UI.InputSystemUIInputModule;
#endif
using StandaloneInputModule = UnityEngine.EventSystems.StandaloneInputModule;
using UnityUiEventSystem = UnityEngine.EventSystems.EventSystem;

namespace Zombera.UI
{
    /// <summary>
    /// Ensures runtime UGUI always has one active EventSystem with a usable input module.
    /// </summary>
    public static class RuntimeUiEventSystemUtility
    {
        private static bool sceneHookInstalled;
        private static UiEventSystemHeartbeat heartbeat;
        private static bool ensureInProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneHook()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (sceneHookInstalled)
            {
                EnsureHeartbeat();
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneHookInstalled = true;
            EnsureHeartbeat();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterSceneLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            InstallSceneHook();
            EnsureInteractiveEventSystem();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            EnsureInteractiveEventSystem();
        }

        public static void EnsureInteractiveEventSystem()
        {
            if (ensureInProgress)
            {
                return;
            }

            ensureInProgress = true;

            try
            {
                if (Application.isPlaying)
                {
                    InstallSceneHook();
                }

                UnityUiEventSystem[] systems =
                    Object.FindObjectsByType<UnityUiEventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                Scene activeScene = SceneManager.GetActiveScene();
                UnityUiEventSystem preferred = null;
                UnityUiEventSystem persistentFallback = null;

                for (int i = 0; i < systems.Length; i++)
                {
                    UnityUiEventSystem candidate = systems[i];

                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.gameObject.scene == activeScene)
                    {
                        preferred = candidate;
                        break;
                    }

                    if (persistentFallback == null && IsDontDestroyScene(candidate.gameObject.scene))
                    {
                        persistentFallback = candidate;
                    }
                }

                if (preferred == null)
                {
                    preferred = persistentFallback;
                }

                if (preferred == null)
                {
                    if (!ShouldCreateEventSystemForScene(activeScene))
                    {
                        for (int i = 0; i < systems.Length; i++)
                        {
                            if (systems[i] != null)
                            {
                                DisableEventSystemAndModules(systems[i]);
                            }
                        }

                        return;
                    }

                    GameObject go = new GameObject("UIEventSystem");
                    preferred = go.AddComponent<UnityUiEventSystem>();
                }

                for (int i = 0; i < systems.Length; i++)
                {
                    UnityUiEventSystem candidate = systems[i];
                    if (candidate == null || candidate == preferred)
                    {
                        continue;
                    }

                    DisableEventSystemAndModules(candidate);
                }

                if (!preferred.gameObject.activeSelf)
                {
                    preferred.gameObject.SetActive(true);
                }

                ConfigureUiInputModule(preferred.gameObject);
                preferred.enabled = true;

                if (UnityUiEventSystem.current != preferred)
                {
                    UnityUiEventSystem.current = preferred;
                }

                UnityEngine.EventSystems.BaseInputModule[] preferredModules =
                    preferred.GetComponents<UnityEngine.EventSystems.BaseInputModule>();

                for (int i = 0; i < preferredModules.Length; i++)
                {
                    if (preferredModules[i] != null)
                    {
                        preferredModules[i].enabled = true;
                    }
                }
            }
            finally
            {
                ensureInProgress = false;
            }
        }

        private static void EnsureHeartbeat()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (heartbeat != null)
            {
                return;
            }

            UiEventSystemHeartbeat existing = Object.FindFirstObjectByType<UiEventSystemHeartbeat>();
            if (existing != null)
            {
                heartbeat = existing;
                return;
            }

            GameObject heartbeatObject = new GameObject("UiEventSystemHeartbeat");
            Object.DontDestroyOnLoad(heartbeatObject);
            heartbeat = heartbeatObject.AddComponent<UiEventSystemHeartbeat>();
        }

        private static bool IsDontDestroyScene(Scene scene)
        {
            return scene.IsValid() && scene.buildIndex < 0;
        }

        private static void DisableEventSystemAndModules(UnityUiEventSystem eventSystem)
        {
            if (eventSystem == null)
            {
                return;
            }

            eventSystem.enabled = false;

            UnityEngine.EventSystems.BaseInputModule[] modules =
                eventSystem.GetComponents<UnityEngine.EventSystems.BaseInputModule>();

            for (int m = 0; m < modules.Length; m++)
            {
                if (modules[m] != null)
                {
                    modules[m].enabled = false;
                }
            }
        }

        private sealed class UiEventSystemHeartbeat : MonoBehaviour
        {
            private float nextEnsureAt;

            private void LateUpdate()
            {
                if (!Application.isPlaying)
                {
                    return;
                }

                if (Time.unscaledTime < nextEnsureAt)
                {
                    return;
                }

                nextEnsureAt = Time.unscaledTime + 0.25f;
                EnsureInteractiveEventSystem();
            }
        }

        private static bool ShouldCreateEventSystemForScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            GraphicRaycaster[] raycasters =
                Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < raycasters.Length; i++)
            {
                GraphicRaycaster raycaster = raycasters[i];

                if (raycaster == null)
                {
                    continue;
                }

                if (raycaster.gameObject.scene == scene)
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
                    Object.Destroy(legacyModule);
                }
                else
                {
                    Object.DestroyImmediate(legacyModule);
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