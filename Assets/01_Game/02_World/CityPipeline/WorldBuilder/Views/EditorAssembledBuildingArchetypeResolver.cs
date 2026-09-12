#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.Views
{
    [AddComponentMenu("Zombera/World/Editor Assembled Building Archetype Resolver")]
    [DisallowMultipleComponent]
    public sealed class EditorAssembledBuildingArchetypeResolver : MonoBehaviour, IBuildingArchetypeResolver
    {
        [SerializeField] private CityPrefabRoadNetworkBuilder _builder;
        [SerializeField] private bool _preferProxyPrefabs = true;
        [SerializeField] private float _proxySwapDistanceMeters = 10f;
        [SerializeField] private bool _proxySwapOnFirstDamage = true;
        [SerializeField] private bool _ensureStructureHealth = true;
        [SerializeField] private float _structureMaxHealth = 250f;
        [SerializeField] private bool _ensureBuildPiece = true;
        [SerializeField] private BuildPieceCategory _buildPieceCategory = BuildPieceCategory.Other;

        private readonly List<CityAssembledBuildingCatalogEntry> _entries = new();
        private bool _loaded;

        public void Configure(CityPrefabRoadNetworkBuilder builder)
        {
            if (builder != null)
                _builder = builder;

            _loaded = false;
            _entries.Clear();
        }

        public bool TryResolve(BuildingState building, out BuildingArchetypeResolution resolution)
        {
            resolution = default;
            if (building == null || !EnsureCatalogLoaded())
                return false;

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry?.prefab == null || !Matches(entry, building))
                    continue;

                resolution = CreateResolution(building, entry);
                return resolution.IsValid;
            }

            return false;
        }

        private bool EnsureCatalogLoaded()
        {
            if (_loaded)
                return _entries.Count > 0;

            _loaded = true;
            _entries.Clear();
            if (_builder == null)
                return false;

            var loaded = _builder.LoadWorldStateBuildingCatalog();
            if (loaded == null)
                return false;

            for (var i = 0; i < loaded.Count; i++)
            {
                if (loaded[i]?.prefab != null)
                    _entries.Add(loaded[i]);
            }

            return _entries.Count > 0;
        }

        private BuildingArchetypeResolution CreateResolution(
            BuildingState building,
            CityAssembledBuildingCatalogEntry entry)
        {
            var usesProxy = _preferProxyPrefabs && entry.proxyPrefab != null;
            var prefab = usesProxy ? entry.proxyPrefab : entry.prefab;
            return new BuildingArchetypeResolution(
                building.archetypeId,
                prefab,
                entry.prefab,
                usesProxy,
                _proxySwapDistanceMeters,
                _proxySwapOnFirstDamage,
                _ensureStructureHealth,
                _structureMaxHealth,
                _ensureBuildPiece,
                _buildPieceCategory);
        }

        private static bool Matches(CityAssembledBuildingCatalogEntry entry, BuildingState building)
        {
            return MatchesId(entry.assetPath, building.archetypeId) ||
                   MatchesId(entry.id, building.archetypeId) ||
                   MatchesId(entry.id, building.typeId) ||
                   MatchesPrefabName(entry.prefab, building.archetypeId) ||
                   MatchesPrefabName(entry.prefab, building.typeId) ||
                   MatchesPrefabName(entry.proxyPrefab, building.archetypeId) ||
                   MatchesPrefabName(entry.proxyPrefab, building.typeId);
        }

        private static bool MatchesPrefabName(GameObject prefab, string candidate) =>
            prefab != null && MatchesId(prefab.name, candidate);

        private static bool MatchesId(string expected, string candidate)
        {
            return !string.IsNullOrWhiteSpace(expected) &&
                   !string.IsNullOrWhiteSpace(candidate) &&
                   string.Equals(expected, candidate, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
