using System.Collections.Generic;
using Den.Tools;
using Den.Tools.Tasks;
using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.Core;
using Zombera.Characters;

namespace Zombera.World
{
    /// <summary>
    /// Per–MapMagic-tile NavMesh builds: each tile gets its own <see cref="NavMeshData"/> instances (per agent type),
    /// updated when MapMagic applies terrain and removed before tile reset. Reduces full-world rebakes while streaming.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreamingNavMeshTileService : MonoBehaviour
    {
        [Header("Authority")]
        [Tooltip("When enabled, PlayerSpawner defers runtime NavMesh building to this service.")]
        [SerializeField] private bool driveRuntimeNavMesh = true;
        [SerializeField] private PlayerSpawner playerSpawner;

        [Header("Bake tuning (overridden from PlayerSpawner when assigned)")]
        [SerializeField, Min(0.1f)] private float navMeshVoxelSize = 0.25f;
        [SerializeField, Range(64, 1024)] private int navMeshTileSize = 256;
        [SerializeField, Range(0f, 75f)] private float navMeshMaxSlopeDegrees = 72f;
        [SerializeField, Min(0f)] private float navMeshStepHeightMeters = 2.4f;
        [SerializeField, Min(0f)] private float navMeshMinRegionArea = 0.1f;
        [SerializeField, Min(20f)] private float navMeshVerticalHalfExtent = 180f;
        [SerializeField, Min(0f)] private float tileBoundsHorizontalPadding = 12f;

        private readonly Dictionary<Coord, TileNavInstances> _tiles = new Dictionary<Coord, TileNavInstances>();
        private Scene _ownerScene;

        public bool IsDrivingRuntimeNavMesh => driveRuntimeNavMesh && enabled;
        public bool LastBootstrapHadTriangles { get; private set; }

        private sealed class TileNavInstances
        {
            public readonly List<NavMeshData> Datas = new List<NavMeshData>(4);
            public readonly List<NavMeshDataInstance> Instances = new List<NavMeshDataInstance>(4);
        }

        private void Awake()
        {
            if (playerSpawner == null)
            {
                playerSpawner = FindFirstObjectByType<PlayerSpawner>();
            }

            if (playerSpawner != null)
            {
                _ownerScene = playerSpawner.gameObject.scene;
            }
        }

        private void OnEnable()
        {
            TerrainTile.OnTileApplied += HandleTileApplied;
            TerrainTile.OnBeforeResetTerrain += HandleBeforeResetTerrain;
        }

        private void OnDisable()
        {
            TerrainTile.OnTileApplied -= HandleTileApplied;
            TerrainTile.OnBeforeResetTerrain -= HandleBeforeResetTerrain;
            RemoveAllTiles();
        }

        public void ConfigureFromTuning(
            float voxelSize,
            int tileSize,
            float maxSlopeDegrees,
            float stepHeightMeters,
            float minRegionArea,
            float verticalHalfExtent)
        {
            navMeshVoxelSize = voxelSize;
            navMeshTileSize = tileSize;
            navMeshMaxSlopeDegrees = maxSlopeDegrees;
            navMeshStepHeightMeters = stepHeightMeters;
            navMeshMinRegionArea = minRegionArea;
            navMeshVerticalHalfExtent = verticalHalfExtent;
        }

        /// <summary>
        /// Builds or refreshes every currently deployed MapMagic tile in the player scene (used for bootstrap / all-complete).
        /// </summary>
        public bool RebuildAllDeployedTilesInPlayerScene()
        {
            if (!IsDrivingRuntimeNavMesh)
            {
                return false;
            }

            if (!IsWorldSessionStateForNavMeshWork())
            {
                return false;
            }

            if (playerSpawner == null)
            {
                playerSpawner = FindFirstObjectByType<PlayerSpawner>();
            }

            if (playerSpawner != null)
            {
                _ownerScene = playerSpawner.gameObject.scene;
            }

            if (!_ownerScene.IsValid())
            {
                MapMagicObject mmScene = FindFirstObjectByType<MapMagicObject>();
                if (mmScene != null)
                {
                    _ownerScene = mmScene.gameObject.scene;
                }
            }

            MapMagicObject[] mapMagics = FindObjectsByType<MapMagicObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool any = false;
            for (int i = 0; i < mapMagics.Length; i++)
            {
                MapMagicObject mm = mapMagics[i];
                if (mm == null || mm.tiles == null)
                {
                    continue;
                }

                if (_ownerScene.IsValid() && mm.gameObject.scene != _ownerScene)
                {
                    continue;
                }

                foreach (KeyValuePair<Coord, TerrainTile> kvp in mm.tiles.grid)
                {
                    if (kvp.Value == null)
                    {
                        continue;
                    }

                    if (BakeTile(mm, kvp.Value))
                    {
                        any = true;
                    }
                }
            }

            LastBootstrapHadTriangles = any && HasAnyNavMeshTriangles();
            return LastBootstrapHadTriangles;
        }

        /// <summary>
        /// Removes NavMesh tiles whose coords are no longer present on any MapMagic instance in the player scene.
        /// </summary>
        public void PruneStaleTilesInPlayerScene()
        {
            if (!_ownerScene.IsValid())
            {
                return;
            }

            HashSet<Coord> live = new HashSet<Coord>();
            MapMagicObject[] mapMagics = FindObjectsByType<MapMagicObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < mapMagics.Length; i++)
            {
                MapMagicObject mm = mapMagics[i];
                if (mm == null || mm.tiles == null)
                {
                    continue;
                }

                if (_ownerScene.IsValid() && mm.gameObject.scene != _ownerScene)
                {
                    continue;
                }

                foreach (KeyValuePair<Coord, TerrainTile> kvp in mm.tiles.grid)
                {
                    live.Add(kvp.Key);
                }
            }

            List<Coord> stale = new List<Coord>();
            foreach (Coord key in _tiles.Keys)
            {
                if (!live.Contains(key))
                {
                    stale.Add(key);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                RemoveTile(stale[i]);
            }
        }

        private void HandleTileApplied(TerrainTile tile, TileData data, StopToken stop)
        {
            if (!IsDrivingRuntimeNavMesh || tile == null || tile.mapMagic == null)
            {
                return;
            }

            if (!IsWorldSessionStateForNavMeshWork())
            {
                return;
            }

            if (!_ownerScene.IsValid())
            {
                _ownerScene = tile.mapMagic.gameObject.scene;
            }

            if (_ownerScene.IsValid() && tile.mapMagic.gameObject.scene != _ownerScene)
            {
                return;
            }

            BakeTile(tile.mapMagic, tile);
            PruneStaleTilesInPlayerScene();
        }

        private void HandleBeforeResetTerrain(TerrainTile tile)
        {
            if (!IsDrivingRuntimeNavMesh || tile == null)
            {
                return;
            }

            if (!IsWorldSessionStateForNavMeshWork())
            {
                return;
            }

            RemoveTile(tile.coord);
        }

        private static bool IsWorldSessionStateForNavMeshWork()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return true;
            }

            GameState state = gameManager.CurrentState;
            return state == GameState.LoadingWorld || state == GameState.Playing || state == GameState.Paused;
        }

        private bool BakeTile(MapMagicObject mapMagic, TerrainTile tile)
        {
            Terrain terrain = tile.ActiveTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            RemoveTile(tile.coord);

            Rect wr = tile.WorldRect;
            float pad = Mathf.Max(0f, tileBoundsHorizontalPadding);
            Bounds planarBounds = new Bounds(
                new Vector3(wr.x + wr.width * 0.5f, terrain.transform.position.y + terrain.terrainData.size.y * 0.5f, wr.y + wr.height * 0.5f),
                new Vector3(wr.width + pad * 2f, Mathf.Max(terrain.terrainData.size.y + 8f, navMeshVerticalHalfExtent * 2f), wr.height + pad * 2f));

            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            HashSet<Terrain> included = new HashSet<Terrain>();
            int skipInvalid = 0;
            int skipOutside = 0;

            foreach (Terrain t in FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.terrainData == null)
                {
                    continue;
                }

                if (_ownerScene.IsValid() && t.gameObject.scene != _ownerScene)
                {
                    continue;
                }

                if (!TryAddTerrainIntersectingBounds(t, planarBounds, sources, included, ref skipInvalid, ref skipOutside))
                {
                    continue;
                }
            }

            foreach (MapMagicObject mm in FindObjectsByType<MapMagicObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mm == null || mm.tiles == null)
                {
                    continue;
                }

                if (_ownerScene.IsValid() && mm.gameObject.scene != _ownerScene)
                {
                    continue;
                }

                foreach (Terrain t in mm.tiles.AllActiveTerrains())
                {
                    TryAddTerrainIntersectingBounds(t, planarBounds, sources, included, ref skipInvalid, ref skipOutside);
                }
            }

            if (sources.Count == 0)
            {
                return false;
            }

            HashSet<int> agentTypeIds = new HashSet<int>();
            foreach (NavMeshAgent agent in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (agent == null)
                {
                    continue;
                }

                if (_ownerScene.IsValid() && agent.gameObject.scene != _ownerScene)
                {
                    continue;
                }

                agentTypeIds.Add(agent.agentTypeID);
            }

            if (agentTypeIds.Count == 0)
            {
                agentTypeIds.Add(0);
            }

            TileNavInstances entry = new TileNavInstances();
            bool anyInstance = false;

            foreach (int agentTypeId in agentTypeIds)
            {
                NavMeshBuildSettings settings = NavMesh.GetSettingsByID(agentTypeId);
                settings.overrideVoxelSize = true;
                settings.voxelSize = Mathf.Max(0.1f, navMeshVoxelSize);
                settings.overrideTileSize = true;
                settings.tileSize = Mathf.Clamp(navMeshTileSize, 64, 1024);
                settings.agentSlope = Mathf.Clamp(navMeshMaxSlopeDegrees, 0f, 75f);
                settings.agentClimb = Mathf.Max(0f, navMeshStepHeightMeters);
                settings.minRegionArea = Mathf.Max(0f, navMeshMinRegionArea);

                NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                    settings,
                    sources,
                    planarBounds,
                    Vector3.zero,
                    Quaternion.identity);

                if (data == null)
                {
                    continue;
                }

                NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
                if (instance.valid)
                {
                    entry.Datas.Add(data);
                    entry.Instances.Add(instance);
                    anyInstance = true;
                }
            }

            if (anyInstance)
            {
                _tiles[tile.coord] = entry;
            }

            return anyInstance;
        }

        private void RemoveTile(Coord coord)
        {
            if (!_tiles.TryGetValue(coord, out TileNavInstances entry))
            {
                return;
            }

            _tiles.Remove(coord);

            for (int i = 0; i < entry.Instances.Count; i++)
            {
                NavMeshDataInstance inst = entry.Instances[i];
                if (inst.valid)
                {
                    inst.Remove();
                }
            }

            for (int i = 0; i < entry.Datas.Count; i++)
            {
                if (entry.Datas[i] != null)
                {
                    Destroy(entry.Datas[i]);
                }
            }
        }

        private void RemoveAllTiles()
        {
            List<Coord> keys = new List<Coord>(_tiles.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                RemoveTile(keys[i]);
            }
        }

        private static bool TryAddTerrainIntersectingBounds(
            Terrain terrain,
            Bounds worldBounds,
            List<NavMeshBuildSource> sources,
            HashSet<Terrain> includedTerrains,
            ref int skippedInvalidTransforms,
            ref int skippedOutsideBounds)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            if (includedTerrains != null && includedTerrains.Contains(terrain))
            {
                return false;
            }

            Transform terrainTransform = terrain.transform;
            if (!IsFiniteTransform(terrainTransform))
            {
                skippedInvalidTransforms++;
                return false;
            }

            Bounds terrainBounds = TerrainBoundsWorld(terrain);
            if (!worldBounds.Intersects(terrainBounds))
            {
                skippedOutsideBounds++;
                return false;
            }

            Matrix4x4 terrainTransformMatrix = Matrix4x4.TRS(
                terrainTransform.position,
                terrainTransform.rotation,
                Vector3.one);

            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Terrain,
                sourceObject = terrain.terrainData,
                transform = terrainTransformMatrix,
                area = 0
            });

            includedTerrains?.Add(terrain);
            return true;
        }

        private static Bounds TerrainBoundsWorld(Terrain terrain)
        {
            Vector3 size = terrain.terrainData.size;
            Vector3 center = terrain.transform.position + new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
            return new Bounds(center, size);
        }

        private static bool IsFiniteTransform(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            Vector3 lossy = transform.lossyScale;
            return !(float.IsNaN(lossy.x) || float.IsInfinity(lossy.x) || Mathf.Approximately(lossy.x, 0f));
        }

        private static bool HasAnyNavMeshTriangles()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            return tri.indices != null && tri.indices.Length > 0;
        }
    }
}
