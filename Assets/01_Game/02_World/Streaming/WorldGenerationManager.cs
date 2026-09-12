using System;
using System.Collections;
using UnityEngine;
using Zombera.Core;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World
{
    /// <summary>
    /// Coordinates the staged world generation pipeline after the generation backend
    /// reports terrain readiness via <see cref="WorldTileStreamSource"/>.
    /// </summary>
    public sealed class WorldGenerationManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldTileStreamSource tileStream;
        [SerializeField] private ProceduralRoadSystem roadSystem;
        [SerializeField] private TownSpawner townSpawner;
        [SerializeField] private WorldStreamedCityBuilder cityBuilder;

        [Header("Settings")]
        [SerializeField] private bool generateOnStart;
        [SerializeField] private float initialDelay = 0.5f;

        public bool IsGenerating { get; private set; }
        public bool IsTerrainReady { get; private set; }
        public bool IsRoadsReady { get; private set; }
        public bool IsBuildingsReady { get; private set; }

        private void Start()
        {
            if (generateOnStart)
                StartCoroutine(GenerateWorldSequence());
        }

        public void StartGeneration()
        {
            if (IsGenerating) return;
            StartCoroutine(GenerateWorldSequence());
        }

        private IEnumerator GenerateWorldSequence()
        {
            IsGenerating = true;
            IsTerrainReady = false;
            IsRoadsReady = false;
            IsBuildingsReady = false;

            yield return new WaitForSeconds(initialDelay);

            if (tileStream == null)
                tileStream = FindFirstObjectByType<WorldTileStreamSource>();

            if (tileStream != null)
            {
                Debug.Log("[WorldGenerationManager] Stage 1: Waiting for world terrain tiles...");

                var stageStart = Time.unscaledTime;
                const float terrainStageTimeout = 20f;
                var minTilesToContinue = ProceduralWorldSession.IsSingleTileStressSession() ? 1 : 4;
                var scratch = new System.Collections.Generic.List<WorldTileInfo>(16);

                while (Time.unscaledTime - stageStart < terrainStageTimeout)
                {
                    scratch.Clear();
                    tileStream.CopyTilesAtOrAbove(WorldTileState.TerrainReady, scratch);
                    if (scratch.Count >= minTilesToContinue
                        || StreamedWorldMetrics.MapMagicAllCompleteEvents > 0)
                        break;
                    yield return null;
                }

                Debug.Log(
                    $"[WorldGenerationManager] Terrain threshold reached (tiles={scratch.Count}, applied={StreamedWorldMetrics.MapMagicTileAppliedEvents}).");
            }
            else
            {
                Debug.LogWarning("[WorldGenerationManager] WorldTileStreamSource not found. Skipping terrain stage.");
            }

            IsTerrainReady = true;

            if (roadSystem == null) roadSystem = FindFirstObjectByType<ProceduralRoadSystem>();
            if (roadSystem != null && roadSystem.isActiveAndEnabled)
            {
                Debug.Log("[WorldGenerationManager] Stage 2: Generating roads with EasyRoads...");
                roadSystem.GenerateRoadsForFinishedTerrain();

                if (townSpawner != null)
                {
                    townSpawner.Initialize(roadSystem.GlobalNetwork);
                    Debug.Log("[WorldGenerationManager] TownSpawner initialized.");
                }

                var roadWaitStart = Time.unscaledTime;
                const float roadStageTimeout = 30f;
                while (roadSystem.ProcessedTileCount <= 0
                       && Time.unscaledTime - roadWaitStart < roadStageTimeout)
                    yield return null;

                if (roadSystem.ProcessedTileCount <= 0)
                {
                    Debug.LogWarning(
                        "[WorldGenerationManager] Road stage timed out with processedTileCount=0; continuing pipeline.",
                        roadSystem);
                }
                else
                {
                    Debug.Log("[WorldGenerationManager] Road generation complete.");
                }
            }

            IsRoadsReady = true;

            if (cityBuilder == null) cityBuilder = FindFirstObjectByType<WorldStreamedCityBuilder>();
            if (cityBuilder != null && cityBuilder.isActiveAndEnabled)
            {
                Debug.Log("[WorldGenerationManager] Stage 3: Spawning buildings...");
                cityBuilder.GenerateBuildingsForFinishedRoads();
                Debug.Log("[WorldGenerationManager] Building spawn complete.");
            }

            IsBuildingsReady = true;
            IsGenerating = false;
            Debug.Log("[WorldGenerationManager] World generation pipeline finished.");
        }
    }
}
