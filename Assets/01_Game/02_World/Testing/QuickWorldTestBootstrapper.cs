using UnityEngine;
using Zombera.Core;
using Zombera.World;
using Zombera.Characters;
using System.Collections;

namespace Zombera.Testing
{
    /// <summary>
    /// Lightweight bootstrapper to run World Generation and Character Flow 
    /// in test scenes without going through Boot -> MainMenu.
    /// </summary>
    public sealed class QuickWorldTestBootstrapper : MonoBehaviour
    {
        [Header("Procedural World")]
        [SerializeField] private int testSeed = 12345;
        [SerializeField] private string graphVersion = "QuickTest";

        [Header("Prefabs")]
        [SerializeField] private GameObject gameManagerPrefab;

        [Header("Settings")]
        [SerializeField] private bool autoStartGeneration = true;
        [SerializeField] private float startDelay = 0.2f;

        private void Awake()
        {
            // 1. Ensure GameManager exists (required for state management and events)
            if (GameManager.Instance == null)
            {
                if (gameManagerPrefab != null)
                {
                    var gm = Instantiate(gameManagerPrefab);
                    gm.name = "[GameManager]";
                    Debug.Log("[QuickWorldTestBootstrapper] Instantiated GameManager prefab.");
                }
                else
                {
                    Debug.LogWarning("[QuickWorldTestBootstrapper] GameManager prefab not assigned. Systems may fail.");
                }
            }

            // 2. Begin Procedural Session
            if (!ProceduralWorldSession.IsActive)
            {
                ProceduralWorldSession.Begin(testSeed, graphVersion);
                Debug.Log($"[QuickWorldTestBootstrapper] ProceduralWorldSession started with seed: {testSeed}");
            }
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(startDelay);

            // 3. Trigger World Generation Manager
            if (autoStartGeneration)
            {
                var wgm = Object.FindFirstObjectByType<WorldGenerationManager>();
                if (wgm != null)
                {
                    Debug.Log("[QuickWorldTestBootstrapper] Triggering WorldGenerationManager...");
                    wgm.StartGeneration();
                }
                else
                {
                    Debug.LogWarning("[QuickWorldTestBootstrapper] WorldGenerationManager not found in scene!");
                }
            }

            // 4. Force GameManager state to Playing
            // This allows PlayerSpawner and other systems to proceed.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
                Debug.Log("[QuickWorldTestBootstrapper] Forced GameManager state to Playing.");
            }
        }
    }
}
