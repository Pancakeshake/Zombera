using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Views
{
    [AddComponentMenu("Zombera/World/World State View Registry")]
    [DisallowMultipleComponent]
    public sealed class WorldStateViewRegistry : MonoBehaviour
    {
        private readonly Dictionary<WorldEntityId, WorldBuildingView> _buildingViews = new();
        private readonly Dictionary<WorldTileKey, List<WorldEntityId>> _buildingIdsByLoadedTile = new();
        private readonly Dictionary<WorldEntityId, List<WorldTileKey>> _loadedTilesByBuildingId = new();

        public int BuildingViewCount => _buildingViews.Count;
        public int LoadedTileCount => _buildingIdsByLoadedTile.Count;
        public bool IsTileLoaded(WorldTileKey tile) => _buildingIdsByLoadedTile.ContainsKey(tile);

        public bool TryGetBuildingView(WorldEntityId id, out WorldBuildingView view)
        {
            if (_buildingViews.TryGetValue(id, out view) && view != null)
                return true;

            _buildingViews.Remove(id);
            return false;
        }

        public bool HasBuildingView(WorldEntityId id) => TryGetBuildingView(id, out _);

        public void RegisterBuildingView(WorldTileKey loadedTile, WorldBuildingView view)
        {
            if (view == null || !view.HasWorldEntityId)
                return;

            var id = view.WorldEntityId;
            if (IsEmpty(id))
                return;

            _buildingViews[id] = view;
            AddUnique(GetOrCreateTileList(loadedTile), id);
            AddUnique(GetOrCreateBuildingTileList(id), loadedTile);
        }

        public bool UnregisterLoadedTile(WorldTileKey loadedTile, bool destroyUnreferencedViews)
        {
            if (!_buildingIdsByLoadedTile.TryGetValue(loadedTile, out var ids))
                return false;

            _buildingIdsByLoadedTile.Remove(loadedTile);
            for (var i = 0; i < ids.Count; i++)
                RemoveLoadedTileReference(ids[i], loadedTile, destroyUnreferencedViews);
            return true;
        }

        public void DestroyAllViews()
        {
            foreach (var pair in _buildingViews)
                DestroyViewObject(pair.Value);

            _buildingViews.Clear();
            _buildingIdsByLoadedTile.Clear();
            _loadedTilesByBuildingId.Clear();
        }

        public void CopyLoadedTiles(List<WorldTileKey> results)
        {
            if (results == null)
                return;

            results.Clear();
            foreach (var pair in _buildingIdsByLoadedTile)
                results.Add(pair.Key);
        }

        public void CopyBuildingViews(List<WorldBuildingView> results)
        {
            if (results == null)
                return;

            results.Clear();
            foreach (var pair in _buildingViews)
            {
                if (pair.Value != null)
                    results.Add(pair.Value);
            }
        }

        public int GetLoadedTileReferenceCount(WorldEntityId id)
        {
            return _loadedTilesByBuildingId.TryGetValue(id, out var tiles) ? tiles.Count : 0;
        }

        private void RemoveLoadedTileReference(
            WorldEntityId id,
            WorldTileKey loadedTile,
            bool destroyUnreferencedViews)
        {
            if (!_loadedTilesByBuildingId.TryGetValue(id, out var tiles))
                return;

            tiles.Remove(loadedTile);
            if (tiles.Count > 0)
                return;

            _loadedTilesByBuildingId.Remove(id);
            if (!_buildingViews.TryGetValue(id, out var view))
                return;

            _buildingViews.Remove(id);
            if (destroyUnreferencedViews)
                DestroyViewObject(view);
        }

        private List<WorldEntityId> GetOrCreateTileList(WorldTileKey tile)
        {
            if (_buildingIdsByLoadedTile.TryGetValue(tile, out var ids))
                return ids;

            ids = new List<WorldEntityId>(8);
            _buildingIdsByLoadedTile.Add(tile, ids);
            return ids;
        }

        private List<WorldTileKey> GetOrCreateBuildingTileList(WorldEntityId id)
        {
            if (_loadedTilesByBuildingId.TryGetValue(id, out var tiles))
                return tiles;

            tiles = new List<WorldTileKey>(2);
            _loadedTilesByBuildingId.Add(id, tiles);
            return tiles;
        }

        private static void AddUnique<T>(List<T> list, T value)
        {
            if (list.Contains(value))
                return;

            list.Add(value);
        }

        private static bool IsEmpty(WorldEntityId id) =>
            id.kind == WorldEntityKind.None && string.IsNullOrEmpty(id.value);

        private static void DestroyViewObject(WorldBuildingView view)
        {
            if (view == null)
                return;

            if (Application.isPlaying)
                Destroy(view.gameObject);
            else
                DestroyImmediate(view.gameObject);
        }
    }
}
