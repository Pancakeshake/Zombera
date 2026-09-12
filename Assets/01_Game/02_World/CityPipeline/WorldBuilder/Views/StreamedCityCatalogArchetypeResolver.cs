using System;
using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Views
{
    [AddComponentMenu("Zombera/World/Streamed City Catalog Archetype Resolver")]
    [DisallowMultipleComponent]
    public sealed class StreamedCityCatalogArchetypeResolver : MonoBehaviour, IBuildingArchetypeResolver
    {
        private const string DefaultCatalogResourcesPath = "World/StreamedCityCatalog";

        [SerializeField] private StreamedCityCatalog _catalog;
        [SerializeField] private bool _preferProxyPrefabs = true;
        [SerializeField] private bool _fallbackToFirstValidEntry;

        public StreamedCityCatalog Catalog => _catalog;

        public void Configure(StreamedCityCatalog catalog)
        {
            if (catalog != null)
                _catalog = catalog;
        }

        public bool TryResolve(BuildingState building, out BuildingArchetypeResolution resolution)
        {
            resolution = default;
            var catalog = ResolveCatalog();
            if (building == null || catalog?.Entries == null)
                return false;

            if (TryFindMatchingEntry(catalog, building, out var entry) ||
                (_fallbackToFirstValidEntry && TryFindFirstValidEntry(catalog, out entry)))
            {
                resolution = CreateResolution(building, entry);
                return resolution.IsValid;
            }

            return false;
        }

        private StreamedCityCatalog ResolveCatalog()
        {
            if (_catalog == null)
                _catalog = Resources.Load<StreamedCityCatalog>(DefaultCatalogResourcesPath);
            return _catalog;
        }

        private static bool TryFindMatchingEntry(
            StreamedCityCatalog catalog,
            BuildingState building,
            out StreamedCityBuildingEntry match)
        {
            match = null;
            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (!IsValidEntry(entry))
                    continue;

                if (Matches(entry, building))
                {
                    match = entry;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindFirstValidEntry(
            StreamedCityCatalog catalog,
            out StreamedCityBuildingEntry match)
        {
            match = null;
            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                if (!IsValidEntry(catalog.Entries[i]))
                    continue;

                match = catalog.Entries[i];
                return true;
            }

            return false;
        }

        private static bool Matches(StreamedCityBuildingEntry entry, BuildingState building)
        {
            return MatchesId(entry.id, building.archetypeId) ||
                   MatchesId(entry.id, building.typeId) ||
                   MatchesId(entry.id, building.sourceId) ||
                   MatchesPrefabName(entry.prefab, building.archetypeId) ||
                   MatchesPrefabName(entry.prefab, building.typeId) ||
                   MatchesPrefabName(entry.proxyPrefab, building.archetypeId) ||
                   MatchesPrefabName(entry.proxyPrefab, building.typeId);
        }

        private BuildingArchetypeResolution CreateResolution(
            BuildingState building,
            StreamedCityBuildingEntry entry)
        {
            var usesProxy = _preferProxyPrefabs && entry.useProxySwap && entry.proxyPrefab != null;
            var prefab = usesProxy ? entry.proxyPrefab : entry.prefab;
            return new BuildingArchetypeResolution(
                building.archetypeId,
                prefab,
                entry.prefab,
                usesProxy,
                entry.proxySwapDistanceMeters,
                entry.proxySwapOnFirstDamage,
                entry.ensureStructureHealth,
                entry.structureMaxHealth,
                entry.ensureBuildPiece,
                entry.buildPieceCategory);
        }

        private static bool IsValidEntry(StreamedCityBuildingEntry entry) =>
            entry?.prefab != null && entry.weight > 0f;

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
