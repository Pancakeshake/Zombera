using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Data;

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Central registry for all BuildingData assets and BuildPiece prefabs.
    ///     Provides fast O(1) lookup during save/load.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Building/Building Save Registry", fileName = "BuildingSaveRegistry")]
    public sealed class BuildingSaveRegistry : ScriptableObject
    {
        [Header("Blueprints")]
        public List<BuildingData> buildingData = new();

        [Header("Modular Pieces")]
        public List<GameObject> buildPiecePrefabs = new();

        private readonly Dictionary<string, BuildingData> _buildingLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> _pieceLookup = new(StringComparer.OrdinalIgnoreCase);
        private bool _isInitialized;

        public void Initialize()
        {
            _buildingLookup.Clear();
            foreach (var data in buildingData)
            {
                if (data == null || string.IsNullOrWhiteSpace(data.buildingId)) continue;
                _buildingLookup[data.buildingId] = data;
            }

            _pieceLookup.Clear();
            foreach (var prefab in buildPiecePrefabs)
            {
                if (prefab == null) continue;
                _pieceLookup[prefab.name] = prefab;
            }

            _isInitialized = true;
        }

        public BuildingData GetBuilding(string buildingId)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrWhiteSpace(buildingId)) return null;
            return _buildingLookup.TryGetValue(buildingId, out var data) ? data : null;
        }

        public GameObject GetPiecePrefab(string prefabName)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrWhiteSpace(prefabName)) return null;
            return _pieceLookup.TryGetValue(prefabName, out var prefab) ? prefab : null;
        }

        public void Clear()
        {
            buildingData.Clear();
            buildPiecePrefabs.Clear();
            _buildingLookup.Clear();
            _pieceLookup.Clear();
            _isInitialized = false;
        }
    }
}