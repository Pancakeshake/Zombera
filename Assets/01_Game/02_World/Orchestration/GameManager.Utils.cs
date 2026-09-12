using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Zombera.UI;
using UnityUiEventSystem = UnityEngine.EventSystems.EventSystem;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        private static void EnsureSingleAudioListener()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (listeners == null || listeners.Length == 0)
            {
                EnsureAudioListenerOnPreferredCamera();
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            AudioListener preferred = null;

            var taggedMainCamera = Camera.main;
            if (taggedMainCamera != null && IsDontDestroyOnLoadObject(taggedMainCamera.gameObject))
            {
                var persistentMainCameraListener = taggedMainCamera.GetComponent<AudioListener>();
                if (persistentMainCameraListener != null && taggedMainCamera.gameObject.activeInHierarchy)
                    preferred = persistentMainCameraListener;
            }

            if (preferred == null)
                foreach (var candidate in listeners)
                    if (candidate != null && candidate.gameObject.activeInHierarchy &&
                        IsDontDestroyOnLoadObject(candidate.gameObject))
                    {
                        preferred = candidate;
                        break;
                    }

            if (preferred == null && taggedMainCamera != null)
                preferred = taggedMainCamera.GetComponent<AudioListener>();

            if (preferred == null)
                foreach (var candidate in listeners)
                    if (candidate != null && candidate.gameObject.scene == activeScene &&
                        candidate.gameObject.activeInHierarchy)
                    {
                        preferred = candidate;
                        break;
                    }

            if (preferred == null)
                foreach (var candidate in listeners)
                    if (candidate != null && candidate.gameObject.activeInHierarchy)
                    {
                        preferred = candidate;
                        break;
                    }

            if (preferred == null) preferred = listeners[0];

            if (preferred == null || !preferred.gameObject.activeInHierarchy)
            {
                EnsureAudioListenerOnPreferredCamera();
                listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (listeners == null || listeners.Length == 0) return;

                preferred = null;

                foreach (var candidate in listeners)
                    if (candidate != null && candidate.gameObject.activeInHierarchy &&
                        IsDontDestroyOnLoadObject(candidate.gameObject))
                    {
                        preferred = candidate;
                        break;
                    }

                if (preferred == null) preferred = listeners[0];
            }

            foreach (var listener in listeners)
            {
                if (listener == null || listener == preferred) continue;

                listener.enabled = false;
            }

            if (preferred != null && preferred.gameObject.activeInHierarchy) preferred.enabled = true;
        }

        private static bool IsDontDestroyOnLoadObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() &&
                   string.Equals(gameObject.scene.name, "DontDestroyOnLoad", System.StringComparison.Ordinal);
        }

        private static bool IsUsableAudioCamera(Camera camera)
        {
            return camera != null && camera.gameObject.activeInHierarchy && camera.enabled &&
                   camera.cameraType == CameraType.Game && camera.targetTexture == null;
        }

        private static void EnsureAudioListenerOnPreferredCamera()
        {
            Camera camera = null;
            var taggedMainCamera = Camera.main;

            if (IsUsableAudioCamera(taggedMainCamera) && IsDontDestroyOnLoadObject(taggedMainCamera.gameObject))
                camera = taggedMainCamera;

            if (camera == null)
            {
                var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var activeScene = SceneManager.GetActiveScene();

                foreach (var candidate in cameras)
                    if (IsUsableAudioCamera(candidate) && IsDontDestroyOnLoadObject(candidate.gameObject))
                    {
                        camera = candidate;
                        break;
                    }

                if (camera == null && IsUsableAudioCamera(taggedMainCamera))
                    camera = taggedMainCamera;

                if (camera == null)
                    foreach (var candidate in cameras)
                        if (IsUsableAudioCamera(candidate) && candidate.gameObject.scene == activeScene)
                        {
                            camera = candidate;
                            break;
                        }

                if (camera == null)
                    foreach (var candidate in cameras)
                        if (IsUsableAudioCamera(candidate))
                        {
                            camera = candidate;
                            break;
                        }
            }

            if (camera == null)
            {
                EnsureFallbackAudioListener();
                return;
            }

            var listener = camera.GetComponent<AudioListener>();
            if (listener == null) listener = camera.gameObject.AddComponent<AudioListener>();

            listener.enabled = true;
        }

        private static void EnsureFallbackAudioListener()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var existing in listeners)
                if (existing != null && existing.gameObject.activeInHierarchy)
                {
                    existing.enabled = true;
                    return;
                }

            var fallback = GameObject.Find(RuntimeAudioListenerObjectName);
            if (fallback == null)
            {
                fallback = new GameObject(RuntimeAudioListenerObjectName);
                DontDestroyOnLoad(fallback);
            }

            if (!fallback.activeSelf) fallback.SetActive(true);

            var listener = fallback.GetComponent<AudioListener>();
            if (listener == null) listener = fallback.AddComponent<AudioListener>();

            listener.enabled = true;
        }

        private static void EnsureUnityUiEventSystemPresent()
        {
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
        }

        private void EnsureDisplayCameraPresentForLoadingScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !IsLoadingScene(scene)) return;

            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Camera reusableGameCamera = null;

            for (var i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (camera == null || camera.gameObject.scene != scene || !camera.gameObject.activeInHierarchy)
                    continue;

                if (camera.cameraType != CameraType.Game) continue;

                if (camera.enabled && camera.targetTexture == null && camera.targetDisplay == 0)
                    return;

                reusableGameCamera ??= camera;
            }

            if (reusableGameCamera != null)
            {
                reusableGameCamera.enabled = true;
                reusableGameCamera.targetTexture = null;
                reusableGameCamera.targetDisplay = 0;
                EnsureAudioListenerOnPreferredCamera();
                return;
            }

            var cameraObject = new GameObject("LoadingDisplayCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            try
            {
                cameraObject.tag = "MainCamera";
            }
            catch (UnityException)
            {
                // Keep loading camera fallback robust if tags are customized.
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
            cameraObject.transform.position = new Vector3(0f, 2f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

            EnsureAudioListenerOnPreferredCamera();

            if (Application.isEditor || Debug.isDebugBuild)
                Debug.Log("[GameManager] Created fallback display camera for Loading scene.", this);
        }

        private static void DisableUnityUiEventSystemsForSceneTransition()
        {
            var systems = FindObjectsByType<UnityUiEventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var candidate in systems)
            {
                if (candidate == null) continue;

                candidate.enabled = false;

                var modules = candidate.GetComponents<BaseInputModule>();
                foreach (var module in modules)
                    if (module != null)
                        module.enabled = false;
            }

            // Clear stale current EventSystem so scene activation doesn't hit duplicate-current warnings.
            if (UnityUiEventSystem.current != null)
                UnityUiEventSystem.current = null;
        }
    }
}