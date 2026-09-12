using UnityEngine;
using Zombera.Core;
using Zombera.World;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.Roads
{
    public sealed partial class ProceduralRoadSystem
    {
        private void Awake()
        {
            ResolveReferences();
            if (!IsDedicatedCityRoadStack())
                EnsureGlobalRoadNetwork();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshTileStreamSubscription(tileStreamBridge);
            TryGenerateRoadsForDeployedTilesIfNeeded();
        }

        private void OnDisable()
        {
            RefreshTileStreamSubscription(null);
            ClearAllTileRoads();
        }

        private int _cachedGlobalNetworkSeed = int.MinValue;

        public void InvalidateGlobalRoadNetwork()
        {
            _globalNetwork = null;
            _cachedGlobalNetworkSeed = int.MinValue;
        }

        private void GenerateGlobalNetwork(bool forceWorldMapLayout = false)
        {
            var seed = RoadNetworkSeedResolver.Resolve(settings);

            var generationSettings = settings;
            RoadNetworkSettings temporarySettings = null;

            if (forceWorldMapLayout || generationSettings == null)
            {
                temporarySettings = ScriptableObject.CreateInstance<RoadNetworkSettings>();

                if (generationSettings != null)
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(generationSettings), temporarySettings);

                temporarySettings.useMapMagicSplineOutput = false;
                temporarySettings.roadLayoutSource = RoadLayoutSource.MathematicalWorldMap;
                generationSettings = temporarySettings;
            }

            _globalNetwork = ProceduralRoadGenerator.Generate(seed, generationSettings);
            _cachedGlobalNetworkSeed = seed;

            if (temporarySettings != null)
            {
                if (Application.isPlaying)
                    Destroy(temporarySettings);
                else
                    DestroyImmediate(temporarySettings);
            }

            Debug.Log(
                "[ProceduralRoadSystem] Generated global network seed=" + seed +
                " layout=" + (generationSettings.UsesMathematicalWorldMapLayout ? "WorldMap" : "MapMagicSplines") +
                " roads=" + _globalNetwork.Roads.Count + ".",
                this);
        }

        private void EnsureGlobalRoadNetwork(bool forceRegenerate = false)
        {
            var seed = RoadNetworkSeedResolver.Resolve(settings);
            if (!forceRegenerate &&
                _globalNetwork != null &&
                _globalNetwork.Roads is { Count: > 0 } &&
                _cachedGlobalNetworkSeed == seed)
                return;

            GenerateGlobalNetwork(forceWorldMapLayout: settings != null && settings.UsesMathematicalWorldMapLayout);
        }

        private void RefreshTileStreamSubscription(WorldTileStreamSource bridge)
        {
            if (_activeBridgeEvents != null)
                _activeBridgeEvents.TileAppliedForGameplay -= HandleTileApplied;

            _activeBridge = bridge;
            _activeBridgeEvents = WorldTileStreamSourceUtility.AsGameplayEvents(bridge);
            if (_activeBridgeEvents == null || !isActiveAndEnabled) return;

            _activeBridgeEvents.TileAppliedForGameplay += HandleTileApplied;
        }

        private void HandleTileApplied(WorldTileInfo tile)
        {
            if (!autoGenerateOnTileApply || ShouldSkipRuntimeTileApplyGeneration()) return;
            if (!IsWorldSessionActiveForRoadGeneration()) return;

            GenerateRoadsForTile(tile);
        }

        /// <summary>
        /// Roads must only stream during an active world session (LoadingWorld/Playing/Paused).
        /// Scenes without a GameManager (dev/prototype) are treated as always active.
        /// </summary>
        private static bool IsWorldSessionActiveForRoadGeneration() => WorldSessionGate.IsAllowed;

        private void GenerateRoadsForFinishedTerrainInternal()
        {
            ResolveReferences();

            var usingWorldMapLayout = settings == null || settings.UsesMathematicalWorldMapLayout;

            if (usingWorldMapLayout)
            {
                EnsureGlobalRoadNetwork(forceRegenerate: true);

                Debug.Log("[ProceduralRoadSystem] Refining world-map road plan based on finished terrain...");
                ProceduralRoadGenerator.RefinePlanForTerrain(_globalNetwork, settings);
            }

            var bridge = _activeBridge != null ? _activeBridge : WorldTileStreamSourceUtility.FindBridge();
            if (bridge == null) return;

            var tiles = new System.Collections.Generic.List<WorldTileInfo>(64);
            bridge.CopyTilesAtOrAbove(WorldTileState.TerrainReady, tiles);
            Debug.Log("[ProceduralRoadSystem] Building roads for " + tiles.Count + " tiles...");
            for (var i = 0; i < tiles.Count; i++)
                GenerateRoadsForTile(tiles[i]);
        }
    }
}
