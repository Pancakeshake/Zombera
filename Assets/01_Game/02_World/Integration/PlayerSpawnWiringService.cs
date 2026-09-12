#region

using System;
using System.Linq;
using UnityEngine;
using Zombera.Combat;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.UI;
using Object = UnityEngine.Object;

#endregion

namespace Zombera.Characters
{
    public static class PlayerSpawnWiringService
    {
        public static Camera EnsureWorldCamera(Camera preferredCamera, Vector3 focusPoint)
        {
            LogDiag($"EnsureWorldCamera: preferred={(preferredCamera != null ? preferredCamera.name : "(null)")}, focus={focusPoint}.", preferredCamera);

            var worldCamera = IsUsableGameplayCamera(preferredCamera)
                ? preferredCamera
                : ResolveUsableGameplayCamera();

            if (worldCamera == null)
            {
                worldCamera = CreateRuntimeWorldCamera(focusPoint);
                LogDiag($"EnsureWorldCamera: created runtime camera '{worldCamera.name}'.", worldCamera);
            }
            else
            {
                LogDiag($"EnsureWorldCamera: selected existing camera '{worldCamera.name}'.", worldCamera);
            }

            NormalizeGameplayCameraForWorld(worldCamera);
            LogDiag($"EnsureWorldCamera: normalized '{worldCamera.name}' (enabled={worldCamera.enabled}, clearFlags={worldCamera.clearFlags}, targetTexture={(worldCamera.targetTexture != null ? worldCamera.targetTexture.name : "none")}).", worldCamera);

            return worldCamera;
        }

        public static void BindCameraToUnit(GameObject unitObject, Camera worldCamera)
        {
            if (unitObject == null || worldCamera == null) return;

            LogDiag($"BindCameraToUnit: unit='{unitObject.name}', camera='{worldCamera.name}'.", unitObject);

            var input = unitObject.GetComponent<PlayerInputController>();
            if (input != null) input.SetWorldCamera(worldCamera);
            LogDiag($"BindCameraToUnit: inputController={(input != null ? "found" : "missing")}.", unitObject);

            var follow = worldCamera.GetComponent<PlayerFollowCamera>();
            if (follow == null)
            {
                follow = worldCamera.gameObject.AddComponent<PlayerFollowCamera>();
                LogDiag($"BindCameraToUnit: added PlayerFollowCamera to '{worldCamera.name}'.", worldCamera);
            }

            if (follow != null)
            {
                follow.SetTarget(unitObject.transform);
                LogDiag($"BindCameraToUnit: assigned follow target '{unitObject.name}'.", worldCamera);
            }
            else
            {
                LogDiag($"BindCameraToUnit: failed to resolve PlayerFollowCamera on '{worldCamera.name}'.", worldCamera);
            }
        }

        public static void WireInputSystems(GameObject unitObject)
        {
            if (unitObject == null) return;

            var input = unitObject.GetComponent<PlayerInputController>();
            if (input == null) return;

            var combatManager = Object.FindFirstObjectByType<CombatManager>();
            var combatSystem = Object.FindFirstObjectByType<CombatSystem>();
            var squadManager = SquadManager.Instance != null
                ? SquadManager.Instance
                : Object.FindFirstObjectByType<SquadManager>();

            input.InjectSystems(combatManager, combatSystem, squadManager);

            if (unitObject.GetComponent<ContainerInteractor>() == null) unitObject.AddComponent<ContainerInteractor>();
        }

        public static void BindHudToUnit(Unit unit)
        {
            if (unit == null) return;

            var hudManagers =
                Object.FindObjectsByType<HUDManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // ReSharper disable once LoopCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var hudManager in hudManagers) hudManager?.BindPlayerUnit(unit);
        }

        private static Camera ResolveUsableGameplayCamera()
        {
            if (IsUsableGameplayCamera(Camera.main)) return Camera.main;

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return cameras.FirstOrDefault(IsUsableGameplayCamera);
        }

        private static bool IsUsableGameplayCamera(Camera candidate)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) return false;

            if (candidate.targetTexture != null) return false;

            var candidateName = candidate.gameObject.name;
            return string.IsNullOrWhiteSpace(candidateName)
                   || candidateName.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static Camera CreateRuntimeWorldCamera(Vector3 focusPoint)
        {
            var cameraObject = new GameObject("RuntimeWorldCamera") { tag = "MainCamera" };

            var runtimeCamera = cameraObject.AddComponent<Camera>();
            runtimeCamera.clearFlags = CameraClearFlags.Skybox;
            runtimeCamera.cullingMask = ~0;
            runtimeCamera.nearClipPlane = 0.05f;
            runtimeCamera.farClipPlane = 3000f;

            cameraObject.transform.position = focusPoint + new Vector3(0f, 12f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            if (Object.FindFirstObjectByType<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();

            return runtimeCamera;
        }

        private static void NormalizeGameplayCameraForWorld(Camera camera)
        {
            if (camera == null) return;

            camera.enabled = true;
            camera.cameraType = CameraType.Game;
            camera.targetDisplay = 0;

            // Preview/portrait pipelines can leave an RT target behind; world gameplay needs direct display output.
            if (camera.targetTexture != null) camera.targetTexture = null;

            if (camera.clearFlags != CameraClearFlags.Skybox)
                camera.clearFlags = CameraClearFlags.Skybox;

            if (camera.cullingMask == 0)
                camera.cullingMask = ~0;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDiag(string message, Object context = null)
        {
            Debug.Log($"[PlayerSpawnWiringService] {message}", context);
        }
    }
}