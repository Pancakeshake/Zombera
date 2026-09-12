using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Zombera.Core;
using Zombera.UI;

namespace Zombera.Testing
{
    /// <summary>
    /// Bootstrap script for test scenes to satisfy gameplay logic dependencies.
    /// Ensures GameManager exists, systems are initialized, and HUD is spawned.
    /// </summary>
    public sealed class TestGameplayBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private GameObject hudPrefab;

        [Header("Settings")]
        [SerializeField] private bool setPlayingStateOnAwake = true;
        [SerializeField] private bool verboseLogs = true;

        private void Awake()
        {
            // 1. Ensure GameManager exists
            if (GameManager.Instance == null)
            {
                var existingGm = Object.FindAnyObjectByType<GameManager>();
                if (existingGm == null)
                {
                    if (gameManagerPrefab != null)
                    {
                        LogInfo("Instantiating GameManager prefab.");
                        var gm = Instantiate(gameManagerPrefab);
                        gm.name = "[GameManager]";
                    }
                    else
                    {
                        Debug.LogError("[TestGameplayBootstrap] GameManager.Instance is null and no prefab is assigned!", this);
                    }
                }
            }

            // 2. Initialize systems and set state
            var gmInstance = GameManager.Instance;
            if (gmInstance != null)
            {
                if (!gmInstance.IsInitialized)
                {
                    LogInfo("Initializing GameManager systems.");
                    gmInstance.InitializeSystems();
                }

                if (setPlayingStateOnAwake)
                {
                    LogInfo("Forcing GameState to Playing.");
                    gmInstance.SetGameState(GameState.Playing);
                }
            }

            // 3. Ensure a world HUD exists (the component that manages the HUD canvas)
            // Look for any component that manages the primary HUD canvas
            var existingController = Object.FindFirstObjectByType<Zombera.UI.WorldHUDController>(FindObjectsInactive.Include);
            var existingManager = Object.FindFirstObjectByType<Zombera.UI.HUDManager>(FindObjectsInactive.Include);

            if (existingController == null && existingManager == null)
            {
                if (hudPrefab != null)
                {
                    LogInfo("Instantiating HUD prefab.");
                    var hud = Instantiate(hudPrefab);
                    hud.name = "HUD";
                }
                else
                {
                    LogInfo("No HUD prefab assigned; skipping HUD spawn.");
                }
            }

            EnsureWorldHudCanvasStartsActive();
        }

        private void EnsureWorldHudCanvasStartsActive()
        {
            var activatedCount = 0;

            var hudControllers = Object.FindObjectsByType<WorldHUDController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < hudControllers.Length; i++)
            {
                var controller = hudControllers[i];
                if (controller == null) continue;
                activatedCount += ActivateGameObjectIfNeeded(controller.gameObject);
            }

            var transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || !string.Equals(t.name, "WorldHUDCanvas", StringComparison.Ordinal)) continue;
                activatedCount += ActivateGameObjectIfNeeded(t.gameObject);
            }

            if (activatedCount > 0)
                LogInfo($"Activated {activatedCount} WorldHUD object(s) to ensure HUD starts enabled in test scene.");
        }

        private static int ActivateGameObjectIfNeeded(GameObject go)
        {
            if (go == null || go.activeSelf) return 0;
            go.SetActive(true);
            return 1;
        }

        private void LogInfo(string message)
        {
            if (verboseLogs) Debug.Log($"[TestGameplayBootstrap] {message}", this);
        }
    }
}
