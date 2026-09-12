using UnityEngine;
using System.Collections.Generic;
using Zombera.World;
using Zombera.World.Roads;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.City
{
    public sealed class TownSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private List<TownPrefabEntry> townPrefabs;
        [SerializeField] private float spawnChance = 1f;
        [SerializeField] private string citiesRootName = "Cities";
        [SerializeField] private GameObject marketPrefab;
        
        [Header("References")]
        [SerializeField] private WorldTileStreamSource tileStreamBridge;
        
        private RoadNetworkRuntime _globalNetwork;
        private readonly HashSet<int> _spawnedTownIds = new();

        [System.Serializable]
        public struct TownPrefabEntry
        {
            public TownType type;
            public GameObject prefab;
        }

        private void OnEnable()
        {
            if (tileStreamBridge == null)
                tileStreamBridge = WorldTileStreamSourceUtility.FindBridge();

            WorldTileStreamSourceUtility.SubscribeTileApplied(tileStreamBridge, HandleTileApplied);
        }

        private void OnDisable()
        {
            WorldTileStreamSourceUtility.UnsubscribeTileApplied(tileStreamBridge, HandleTileApplied);
        }

        public void Initialize(RoadNetworkRuntime network)
        {
            _globalNetwork = network;
            _spawnedTownIds.Clear();
            Debug.Log($"[TownSpawner] Initialized with {network?.TownNodes?.Count ?? 0} potential town nodes.");
        }

        private void HandleTileApplied(WorldTileInfo tile)
        {
            if (_globalNetwork == null) return;

            var tileRect = tile.WorldRectXZ;
            
            foreach (var town in _globalNetwork.TownNodes)
            {
                if (_spawnedTownIds.Contains(town.id)) continue;
                
                if (tileRect.Contains(town.positionXZ))
                {
                    SpawnTown(town, tile);
                }
            }
        }

        private void SpawnTown(TownNode town, WorldTileInfo tile)
        {
            if (Random.value > spawnChance) return;

            var prefab = GetPrefabForType(town.type);
            if (prefab == null)
            {
                Debug.LogWarning($"[TownSpawner] No prefab found for TownType {town.type}");
                return;
            }

            var cityRoot = new GameObject(ResolveCityName(town)).transform;
            cityRoot.SetParent(ResolveCitiesRoot(), false);

            var townY = SampleHeight(town.positionXZ, tile);
            var townInstance = Instantiate(
                prefab, new Vector3(town.positionXZ.x, townY, town.positionXZ.y),
                Quaternion.identity, cityRoot);
            townInstance.name = "Town";

            if (town.hasMarket && marketPrefab != null)
            {
                var marketY = SampleHeight(town.marketPositionXZ, tile);
                var market = Instantiate(
                    marketPrefab,
                    new Vector3(town.marketPositionXZ.x, marketY, town.marketPositionXZ.y),
                    Quaternion.identity, cityRoot);
                market.name = "Market";
            }

            _spawnedTownIds.Add(town.id);
            Debug.Log($"[TownSpawner] Spawned city '{cityRoot.name}' at {town.positionXZ}");
        }

        private Transform ResolveCitiesRoot()
        {
            var existing = GameObject.Find(citiesRootName);
            if (existing != null)
                return existing.transform;

            var root = new GameObject(citiesRootName);
            return root.transform;
        }

        private static string ResolveCityName(TownNode town)
        {
            if (!string.IsNullOrWhiteSpace(town.displayName))
                return town.displayName;
            return $"Town_{town.type}_{town.id}";
        }

        private static float SampleHeight(Vector2 positionXZ, WorldTileInfo tile)
        {
            var terrain = tile.Terrain;
            if (terrain == null || terrain.terrainData == null)
                return 0f;

            var pos = terrain.transform.position;
            var size = terrain.terrainData.size;
            var x = Mathf.Clamp(positionXZ.x, pos.x, pos.x + size.x);
            var z = Mathf.Clamp(positionXZ.y, pos.z, pos.z + size.z);
            return terrain.SampleHeight(new Vector3(x, 0f, z));
        }

        private GameObject GetPrefabForType(TownType type)
        {
            foreach (var entry in townPrefabs)
            {
                if (entry.type == type) return entry.prefab;
            }
            return null;
        }
    }
}