using UnityEngine;
using Zombera.Core;
using Zombera.World;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.Environment;

namespace Zombera.Systems
{
    public sealed class WorldSaveProvider : MonoBehaviour, ICriticalSaveProvider
    {
        [SerializeField] private WorldManager worldManager;
        [SerializeField] private ChunkLoader chunkLoader;

        private WorldBuilderService _worldBuilderService;

        public int Priority => 1000;

        public void OnSave(GameSaveData saveData)
        {
            EnsureReferences();
            PopulateChunkData(saveData);
            PopulateProceduralWorldData(saveData);
            PopulateEnvironmentData(saveData);
        }

        public void OnLoad(GameSaveData saveData)
        {
            EnsureReferences();
            RestoreProceduralWorldData(saveData);
            RestoreEnvironmentData(saveData);
        }

        private void EnsureReferences()
        {
            if (worldManager == null) worldManager = Object.FindFirstObjectByType<WorldManager>();
            if (chunkLoader == null) chunkLoader = Object.FindFirstObjectByType<ChunkLoader>();
            if (_worldBuilderService == null)
                _worldBuilderService = ResolveWorldBuilderService();
        }

        private void PopulateChunkData(GameSaveData saveData)
        {
            if (chunkLoader == null) return;

            foreach (var entry in chunkLoader.LoadedChunks)
            {
                var chunk = entry.Value;
                if (chunk == null) continue;

                saveData.worldChunks.Add(new WorldChunkSaveData
                {
                    coordinates = chunk.Coordinates,
                    seed = chunk.Seed,
                    regionId = chunk.RegionId
                });
            }
        }

        private void PopulateProceduralWorldData(GameSaveData saveData)
        {
            saveData.proceduralWorld ??= new ProceduralWorldSaveData();

            if (worldManager == null) worldManager = Object.FindFirstObjectByType<WorldManager>();

            if (worldManager is not { UseProceduralStreamingWorld: true } || !ProceduralWorldSession.IsActive)
            {
                saveData.proceduralWorld.hasData = false;
                return;
            }

            if (chunkLoader != null)
            {
                foreach (var entry in chunkLoader.LoadedChunks)
                {
                    var chunk = entry.Value;
                    if (chunk is { IsDirty: true })
                        StreamedWorldChunkState.CaptureChunkState(chunk, "autosave_loaded");
                }
            }

            var data = saveData.proceduralWorld;
            data.hasData = true;
            data.worldSeed = ProceduralWorldSession.WorldSeed;
            data.graphVersion = ProceduralWorldSession.GraphVersion ?? string.Empty;
            data.formatVersion = 3;
            data.mapSizeTier = ProceduralWorldSession.MapSizeTier;
            data.tilesPerSide = ProceduralWorldSession.TilesPerSide;
            data.originTileX = ProceduralWorldSession.OriginTileX;
            data.originTileZ = ProceduralWorldSession.OriginTileZ;
            data.profileVersion = ProceduralWorldSession.ProfileVersion;
            data.planFingerprint = ProceduralWorldSession.PlanFingerprint;
            StreamedWorldChunkState.MergeIntoSave(saveData);
            PopulateWorldStatePayload(data);
        }

        private void PopulateWorldStatePayload(ProceduralWorldSaveData data)
        {
            data.worldState = new WorldStatePayloadSaveData();
            if (!ProceduralWorldSession.IsFirstPartySession())
                return;

            var stateManager = _worldBuilderService != null ? _worldBuilderService.StateManager : null;
            if (stateManager != null && stateManager.Availability == WorldStateAvailability.LegacyNoWorldState)
                return;
            if (stateManager == null || !stateManager.HasState)
                throw new System.InvalidOperationException("First-party procedural session has no authoritative WorldState to save.");

            var state = stateManager.CaptureCanonicalCopy();
            var payload = WorldStatePayloadCodec.Encode(state);
            var header = state.header ?? new WorldStateHeader();

            data.worldState.hasData = true;
            data.worldState.schemaVersion = payload.schemaVersion;
            data.worldState.canonicalFormatVersion = payload.canonicalFormatVersion;
            data.worldState.idAlgorithmVersion = WorldStateSchema.IdAlgorithmVersion;
            data.worldState.canonicalHash = payload.hashSha256;
            data.worldState.uncompressedByteCount = payload.uncompressedByteCount;
            data.worldState.compressedByteCount = payload.compressedByteCount;
            data.worldState.worldSeed = header.worldSeed;
            data.worldState.profileVersion = header.profileVersion;
            data.worldState.graphVersion = ProceduralWorldSession.FirstPartyGraphVersion;
            data.worldState.profileFingerprint = header.profileFingerprint ?? string.Empty;
            data.worldState.planFingerprint = header.planFingerprint ?? string.Empty;
            data.worldState.payloadBase64 = payload.payloadBase64;
        }

        private void PopulateEnvironmentData(GameSaveData saveData)
        {
            saveData.environment ??= new EnvironmentSaveData();
            var dayNight = Object.FindFirstObjectByType<DayNightController>();
            var env = saveData.environment;
            if (dayNight != null)
            {
                env.hour = dayNight.CurrentHour;
                env.dayNumber = dayNight.DayNumber;
                saveData.metadata.dayNumber = dayNight.DayNumber;
            }
            else
            {
                saveData.metadata.dayNumber = 1;
            }

            var weather = Object.FindFirstObjectByType<WorldWeatherDirector>();
            if (weather == null) return;

            var snapshot = weather.CaptureSnapshot();
            env.weatherId = snapshot.WeatherId ?? env.weatherId;
            env.transitionProgress = snapshot.TransitionProgress;
            env.weatherRngState = snapshot.RngState;
            env.weatherRngStream = snapshot.RngStream;
            env.hoursUntilNextChange = snapshot.HoursUntilNextChange;
            env.environmentProfileVersion = snapshot.ProfileVersion;
        }

        private void RestoreProceduralWorldData(GameSaveData saveData)
        {
            if (saveData.proceduralWorld is not { hasData: true }) return;
            if (worldManager == null) worldManager = Object.FindFirstObjectByType<WorldManager>();
            if (worldManager == null && saveData.proceduralWorld.worldState is { hasData: true })
                throw new System.InvalidOperationException("WorldManager is required to restore procedural world data.");
            if (worldManager == null)
                return;
            worldManager.ApplyLoadedProceduralWorld(saveData.proceduralWorld);
        }

        private WorldBuilderService ResolveWorldBuilderService()
        {
            if (worldManager != null && worldManager.WorldGenerationBackend is WorldBuilderService service)
                return service;
            return Object.FindFirstObjectByType<WorldBuilderService>(FindObjectsInactive.Include);
        }

        private void RestoreEnvironmentData(GameSaveData saveData)
        {
            if (saveData.environment == null) return;
            var weather = Object.FindFirstObjectByType<WorldWeatherDirector>();
            weather?.RestoreFromSave(saveData.environment);

            var dayNight = Object.FindFirstObjectByType<DayNightController>();
            if (dayNight == null) return;
            dayNight.SetDayNumber(saveData.environment.dayNumber);
            dayNight.SetHour(saveData.environment.hour);
        }
    }
}
