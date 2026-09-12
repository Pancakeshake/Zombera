#region

using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Core;
using Zombera.UI;
using Zombera.World.Simulation;

#endregion

// ReSharper disable InvertIf

namespace Zombera.World
{
    public partial class WorldManager
    {
        private void Awake()
        {
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
            ResolveRuntimeReferencesIfNeeded(force: true);
            TrySuppressEasyBuildAutomaticPersistence();
            EnsureProceduralStreamingBridge();
            EnsureStreamedCityBuilderProvisioned();
            EnforceWorldSpawnedBuildingMode();
            SetSimulationActive(IsWorldSessionStateActive());
        }

        private void Start()
        {
            if (initializeOnStart && IsWorldSessionStateActive()) InitializeWorld();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoadedForBuildSuppression;
            SceneManager.sceneLoaded += HandleSceneLoadedForBuildSuppression;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoadedForBuildSuppression;
        }

        private void Update()
        {
            if (IsSimulationActive && !IsWorldSessionStateActive())
                SetSimulationActive(false);

            if (!IsSimulationActive) return;

            if (runValidationZombieSpawnFromWorldManager)
                TrySpawnValidationZombieNearPlayer();

            _chunkStreamingTickTimer += Time.deltaTime;
            _worldSimulationTimer += Time.deltaTime;

            if (_chunkStreamingTickTimer >= chunkStreamingTickInterval)
            {
                _chunkStreamingTickTimer = 0f;
                RunChunkStreamingTick();
            }

            if (_worldSimulationTimer >= worldSimulationInterval)
            {
                _worldSimulationTimer = 0f;
                RunWorldSimulationTick();
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoadedForBuildSuppression;
            IsSimulationActive = false;
            worldBuildingMaterializer?.DestroyAllViews();
            ProceduralWorldSession.Clear();
            StreamedWorldChunkState.Clear();
        }

        private void HandleSceneLoadedForBuildSuppression(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            _forceEasyBuildRuntimeRescan = true;
            _nextEasyBuildRuntimeRescanAt = 0f;
        }
    }
}
