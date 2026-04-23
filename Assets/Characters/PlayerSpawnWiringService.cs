using System;
using UnityEngine;
using Zombera.AI;
using Zombera.Combat;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.UI;

namespace Zombera.Characters
{
    public sealed class PlayerSpawnWiringService
    {
        private readonly MonoBehaviour _host;

        public PlayerSpawnWiringService(MonoBehaviour host)
        {
            _host = host;
        }

        public Camera EnsureWorldCamera(Camera preferredCamera, Vector3 focusPoint)
        {
            Camera worldCamera = IsUsableGameplayCamera(preferredCamera)
                ? preferredCamera
                : ResolveUsableGameplayCamera();

            if (worldCamera == null)
            {
                worldCamera = CreateRuntimeWorldCamera(focusPoint);
            }

            return worldCamera;
        }

        public void BindCameraToUnit(GameObject unitObject, Camera worldCamera)
        {
            if (unitObject == null)
            {
                return;
            }

            PlayerInputController input = unitObject.GetComponent<PlayerInputController>();
            if (input != null)
            {
                input.SetWorldCamera(worldCamera);
            }

            PlayerFollowCamera follow = worldCamera != null ? worldCamera.GetComponent<PlayerFollowCamera>() : null;
            if (follow == null)
            {
                follow = UnityEngine.Object.FindFirstObjectByType<PlayerFollowCamera>();
            }

            if (follow == null && worldCamera != null)
            {
                follow = worldCamera.gameObject.AddComponent<PlayerFollowCamera>();
            }

            if (follow != null)
            {
                follow.SetTarget(unitObject.transform);
            }
        }

        public void WireInputSystems(GameObject unitObject)
        {
            if (unitObject == null)
            {
                return;
            }

            PlayerInputController input = unitObject.GetComponent<PlayerInputController>();
            if (input == null)
            {
                return;
            }

            CombatManager combatManager = UnityEngine.Object.FindFirstObjectByType<CombatManager>();
            CombatSystem combatSystem = UnityEngine.Object.FindFirstObjectByType<CombatSystem>();
            SquadManager squadManager = SquadManager.Instance != null
                ? SquadManager.Instance
                : UnityEngine.Object.FindFirstObjectByType<SquadManager>();

            input.InjectSystems(combatManager, combatSystem, squadManager);

            if (unitObject.GetComponent<ContainerInteractor>() == null)
            {
                unitObject.AddComponent<ContainerInteractor>();
            }
        }

        public void BindHudToUnit(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            HUDManager[] hudManagers = UnityEngine.Object.FindObjectsByType<HUDManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < hudManagers.Length; i++)
            {
                hudManagers[i]?.BindPlayerUnit(unit);
            }

            WorldHUD[] worldHuds = UnityEngine.Object.FindObjectsByType<WorldHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < worldHuds.Length; i++)
            {
                worldHuds[i]?.Bind(unit);
            }
        }

        private static Camera ResolveUsableGameplayCamera()
        {
            if (IsUsableGameplayCamera(Camera.main))
            {
                return Camera.main;
            }

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (IsUsableGameplayCamera(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsUsableGameplayCamera(Camera candidate)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (candidate.targetTexture != null)
            {
                return false;
            }

            string candidateName = candidate.gameObject.name;
            return string.IsNullOrWhiteSpace(candidateName)
                || candidateName.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static Camera CreateRuntimeWorldCamera(Vector3 focusPoint)
        {
            GameObject cameraObject = new GameObject("RuntimeWorldCamera");
            cameraObject.tag = "MainCamera";

            Camera runtimeCamera = cameraObject.AddComponent<Camera>();
            runtimeCamera.clearFlags = CameraClearFlags.Skybox;
            runtimeCamera.cullingMask = ~0;
            runtimeCamera.nearClipPlane = 0.05f;
            runtimeCamera.farClipPlane = 3000f;

            cameraObject.transform.position = focusPoint + new Vector3(0f, 12f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            return runtimeCamera;
        }
    }
}

