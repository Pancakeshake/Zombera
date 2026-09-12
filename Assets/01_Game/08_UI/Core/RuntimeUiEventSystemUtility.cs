#region

#if ENABLE_INPUT_SYSTEM
using InputSystemUIInputModule = UnityEngine.InputSystem.UI.InputSystemUIInputModule;
using UnityEngine.InputSystem;
#endif
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StandaloneInputModule = UnityEngine.EventSystems.StandaloneInputModule;
using UnityUiEventSystem = UnityEngine.EventSystems.EventSystem;

#endregion

// ReSharper disable LoopCanBeConvertedToQuery

namespace Zombera.UI
{
    /// <summary>
    ///     Ensures runtime UGUI always has one active EventSystem with a usable input module.
    /// </summary>
    public static class RuntimeUiEventSystemUtility
    {
        private const float HeartbeatHealthyRecheckSeconds = 1.25f;
        private const float HeartbeatRecoveryRetrySeconds = 0.25f;
        private static bool _sceneHookInstalled;
        private static UiEventSystemHeartbeat _heartbeat;
        private static bool _ensureInProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneHook()
        {
            if (!Application.isPlaying) return;

            if (_sceneHookInstalled)
            {
                EnsureHeartbeat();
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneHookInstalled = true;
            EnsureHeartbeat();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterSceneLoad()
        {
            if (!Application.isPlaying) return;

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
            if (_ensureInProgress) return;

            _ensureInProgress = true;

            try
            {
                if (Application.isPlaying) InstallSceneHook();

                var systems =
                    Object.FindObjectsByType<UnityUiEventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                var activeScene = SceneManager.GetActiveScene();
                UnityUiEventSystem preferred = null;
                UnityUiEventSystem persistentFallback = null;

                foreach (var candidate in systems)
                {
                    if (candidate == null) continue;

                    if (candidate.gameObject.scene == activeScene)
                    {
                        preferred = candidate;
                        break;
                    }

                    if (persistentFallback == null && IsDontDestroyScene(candidate.gameObject.scene))
                        persistentFallback = candidate;
                }

                preferred ??= persistentFallback;

                if (preferred == null && !ShouldCreateEventSystemForScene(activeScene))
                {
                    foreach (var system in systems)
                        if (system != null)
                            DisableEventSystemAndModules(system);

                    return;
                }

                if (preferred == null)
                {
                    var go = new GameObject("UIEventSystem");
                    preferred = go.AddComponent<UnityUiEventSystem>();
                }

                foreach (var candidate in systems)
                {
                    if (candidate == null || candidate == preferred) continue;

                    DisableEventSystemAndModules(candidate);
                }

                if (!preferred.gameObject.activeSelf) preferred.gameObject.SetActive(true);

                ConfigureUiInputModule(preferred.gameObject);
                preferred.enabled = true;

                var preferredModules =
                    preferred.GetComponents<BaseInputModule>();

                foreach (var preferredModule in preferredModules)
                    if (preferredModule != null)
                        preferredModule.enabled = true;

                if (HasHealthyEventSystem(preferred) && UnityUiEventSystem.current != preferred)
                    UnityUiEventSystem.current = preferred;
            }
            finally
            {
                _ensureInProgress = false;
            }
        }

        private static void EnsureHeartbeat()
        {
            if (!Application.isPlaying) return;

            if (_heartbeat != null) return;

            var existing = Object.FindFirstObjectByType<UiEventSystemHeartbeat>();
            if (existing != null)
            {
                _heartbeat = existing;
                _heartbeat.LateUpdate();
                return;
            }

            var heartbeatObject = new GameObject("UiEventSystemHeartbeat");
            Object.DontDestroyOnLoad(heartbeatObject);
            _heartbeat = heartbeatObject.AddComponent<UiEventSystemHeartbeat>();
            _heartbeat.LateUpdate();
        }

        private static bool IsDontDestroyScene(Scene scene)
        {
            return scene.IsValid() && scene.buildIndex < 0;
        }

        private static bool HasHealthyEventSystem(UnityUiEventSystem eventSystem)
        {
            if (eventSystem == null) return false;
            if (!eventSystem.isActiveAndEnabled || !eventSystem.gameObject.activeInHierarchy) return false;

            var modules = eventSystem.GetComponents<BaseInputModule>();
            foreach (var module in modules)
            {
                if (module == null || !module.enabled) continue;
                return true;
            }

            return false;
        }

        private static bool HasHealthyCurrentEventSystem()
        {
            var current = UnityUiEventSystem.current;
            if (!HasHealthyEventSystem(current)) return false;

            var activeScene = SceneManager.GetActiveScene();
            var currentScene = current.gameObject.scene;
            if (activeScene.IsValid() && currentScene != activeScene && !IsDontDestroyScene(currentScene))
                return false;

            return true;
        }

        private static void DisableEventSystemAndModules(UnityUiEventSystem eventSystem)
        {
            if (eventSystem == null) return;

            eventSystem.enabled = false;

            var modules =
                eventSystem.GetComponents<BaseInputModule>();

            foreach (var module in modules)
                if (module != null)
                    module.enabled = false;

            // Unity still treats multiple enabled EventSystem *components* as an error during OnEnable.
            // Fully deactivate duplicate roots so only the preferred system can become active.
            if (eventSystem.gameObject.activeSelf) eventSystem.gameObject.SetActive(false);
        }

        private static bool ShouldCreateEventSystemForScene(Scene scene)
        {
            if (!scene.IsValid()) return false;

            var raycasters =
                Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            return raycasters.Any(raycaster => raycaster != null && raycaster.gameObject.scene == scene);
        }

        private static void ConfigureUiInputModule(GameObject eventSystemObject)
        {
#if ENABLE_INPUT_SYSTEM
            var inputSystemModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
                inputSystemModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();

            EnsureInputSystemUiActions(inputSystemModule);
            StripCursorCalibrationFromUiPointer(inputSystemModule);

            var legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
            if (legacyModule == null) return;

            if (Application.isPlaying)
                Object.Destroy(legacyModule);
            else
                Object.DestroyImmediate(legacyModule);
#else
            if (eventSystemObject.GetComponent<StandaloneInputModule>() != null) return;

            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void StripCursorCalibrationFromUiPointer(InputSystemUIInputModule module)
        {
            if (module == null) return;

            var pointAction = module.point != null ? module.point.action : null;
            if (pointAction == null) return;

            for (var i = 0; i < pointAction.bindings.Count; i++)
            {
                var binding = pointAction.bindings[i];
                var path = binding.path;
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.Contains("Mouse", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!path.Contains("position", System.StringComparison.OrdinalIgnoreCase)) continue;

                var processors = string.IsNullOrEmpty(binding.overrideProcessors)
                    ? binding.processors
                    : binding.overrideProcessors;
                if (string.IsNullOrEmpty(processors)) continue;

                var cleaned = RemoveProcessorToken(processors, "CursorCalibration");
                if (string.Equals(cleaned, processors, System.StringComparison.Ordinal)) continue;

                pointAction.ApplyBindingOverride(
                    i,
                    string.IsNullOrEmpty(cleaned)
                        ? default
                        : new InputBinding { overrideProcessors = cleaned });
            }
        }

        private static string RemoveProcessorToken(string processors, string token)
        {
            if (string.IsNullOrEmpty(processors) || string.IsNullOrEmpty(token)) return processors;

            var parts = processors.Split(';');
            var kept = new List<string>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (part.Length == 0) continue;
                if (part.Equals(token, System.StringComparison.OrdinalIgnoreCase)) continue;
                kept.Add(part);
            }

            return kept.Count == 0 ? string.Empty : string.Join(";", kept);
        }
#endif

#if ENABLE_INPUT_SYSTEM
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

        private sealed class UiEventSystemHeartbeat : MonoBehaviour
        {
            private float _nextEnsureAt;

            public void LateUpdate()
            {
                if (!Application.isPlaying) return;

                if (Time.unscaledTime < _nextEnsureAt) return;

                if (HasHealthyCurrentEventSystem())
                {
                    _nextEnsureAt = Time.unscaledTime + HeartbeatHealthyRecheckSeconds;
                    return;
                }

                _nextEnsureAt = Time.unscaledTime + HeartbeatRecoveryRetrySeconds;
                EnsureInteractiveEventSystem();
            }
        }
    }
}