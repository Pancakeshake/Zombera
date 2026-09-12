#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.World.Roads;
using Random = System.Random;

#endregion

namespace Zombera.World.City
{
    [Serializable]
    public sealed class StreamedCityBuildingEntry
    {
        public string id = "Building";
        public GameObject prefab;

        [Min(0f)] public float weight = 1f;

        [Header("Footprint (meters)")]
        [Min(1f)]
        public float footprintWidthMeters = 12f;

        [Min(1f)] public float footprintDepthMeters = 12f;

        [Range(-180f, 180f)] public float yawOffsetDegrees;

        [Header("Road Zone Filter")]
        [Tooltip("Optional road zones this entry can spawn in. Leave empty to allow all zones.")]
        public List<RoadCityZone> allowedRoadCityZones = new();

        [Header("Proxy Swap")]
        [Tooltip("When enabled and proxyPrefab is assigned, city spawning instantiates the proxy first and swaps to prefab at runtime.")]
        public bool useProxySwap = true;

        [Tooltip("Lightweight visual/collider proxy prefab used for streamed spawn.")]
        public GameObject proxyPrefab;

        [Min(1f)]
        [Tooltip("Distance from a character at which the proxy swaps into the full destructible prefab.")]
        public float proxySwapDistanceMeters = 10f;

        [Tooltip("If true, first damage received by the proxy forces an immediate swap to the full building prefab.")]
        public bool proxySwapOnFirstDamage = true;

        [Header("Structural Components")]
        [Tooltip("Adds a StructureHealth component at runtime if the prefab does not already have one.")]
        public bool ensureStructureHealth = true;

        [Min(1f)] public float structureMaxHealth = 250f;

        [Tooltip("Adds a BuildPiece component at runtime if the prefab does not already have one.")]
        public bool ensureBuildPiece = true;

        public BuildPieceCategory buildPieceCategory = BuildPieceCategory.Other;
    }

    [CreateAssetMenu(menuName = "Zombera/World/Streamed City Catalog", fileName = "StreamedCityCatalog")]
    public sealed class StreamedCityCatalog : ScriptableObject
    {
        [SerializeField] private List<StreamedCityBuildingEntry> entries = new();

        public List<StreamedCityBuildingEntry> Entries => entries;

        public bool HasValidEntries
        {
            get
            {
                if (entries == null || entries.Count == 0) return false;

                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry?.prefab == null || entry.weight <= 0f) continue;
                    return true;
                }

                return false;
            }
        }

        public bool TryPickEntry(Random rng, out StreamedCityBuildingEntry result) =>
            TryPickEntryWithPredicate(rng, static _ => true, out result);

        public bool TryPickEntryForRoadZone(
            Random rng,
            RoadCityZone roadZone,
            out StreamedCityBuildingEntry result)
        {
            result = null;
            if (rng == null || entries == null || entries.Count == 0) return false;

            if (roadZone == RoadCityZone.Unknown)
                return TryPickEntry(rng, out result);

            var normalizedZone = NormalizeRoadCityZone(roadZone);

            if (TryPickEntryWithPredicate(rng,
                    entry => EntryExplicitlyMatchesZone(entry, normalizedZone),
                    out result))
                return true;

            if (TryPickEntryWithPredicate(rng,
                    entry => EntryAllowsGeneralZoneFallback(entry),
                    out result))
                return true;

            return TryPickEntry(rng, out result);
        }

        private bool TryPickEntryWithPredicate(
            Random rng,
            Func<StreamedCityBuildingEntry, bool> predicate,
            out StreamedCityBuildingEntry result)
        {
            result = null;
            if (!CanPickCatalogEntry(rng, predicate))
                return false;

            var totalWeight = SumPredicateWeights(predicate);
            if (totalWeight <= 0f)
                return false;

            return TryRollPredicateEntry(rng, predicate, totalWeight, out result);
        }

        private bool CanPickCatalogEntry(Random rng, Func<StreamedCityBuildingEntry, bool> predicate) =>
            rng != null && predicate != null && entries is { Count: > 0 };

        private float SumPredicateWeights(Func<StreamedCityBuildingEntry, bool> predicate)
        {
            var totalWeight = 0f;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!IsEligibleCatalogEntry(entries[i], predicate))
                    continue;

                totalWeight += entries[i].weight;
            }

            return totalWeight;
        }

        private bool TryRollPredicateEntry(
            Random rng,
            Func<StreamedCityBuildingEntry, bool> predicate,
            float totalWeight,
            out StreamedCityBuildingEntry result)
        {
            result = null;
            var roll = rng.NextDouble() * totalWeight;
            var cumulative = 0.0;
            StreamedCityBuildingEntry fallback = null;
            return TryRollFromEntries(roll, ref cumulative, ref fallback, predicate, out result);
        }

        private bool TryRollFromEntries(
            double roll,
            ref double cumulative,
            ref StreamedCityBuildingEntry fallback,
            Func<StreamedCityBuildingEntry, bool> predicate,
            out StreamedCityBuildingEntry result)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (TrySelectPredicateRollWinner(
                        entries[i], predicate, roll,
                        ref cumulative, ref fallback, out result))
                    return true;
            }
            result = fallback;
            return result != null;
        }

        private static bool TrySelectPredicateRollWinner(
            StreamedCityBuildingEntry entry,
            Func<StreamedCityBuildingEntry, bool> predicate,
            double roll,
            ref double cumulative,
            ref StreamedCityBuildingEntry fallback,
            out StreamedCityBuildingEntry winner)
        {
            winner = null;
            if (!IsEligibleCatalogEntry(entry, predicate))
                return false;

            fallback = entry;
            cumulative += entry.weight;
            if (roll > cumulative)
                return false;

            winner = entry;
            return true;
        }

        private static bool IsEligibleCatalogEntry(
            StreamedCityBuildingEntry entry,
            Func<StreamedCityBuildingEntry, bool> predicate)
        {
            return entry?.prefab != null && entry.weight > 0f && predicate(entry);
        }

        private static bool EntryExplicitlyMatchesZone(StreamedCityBuildingEntry entry, RoadCityZone zone)
        {
            if (entry == null || entry.allowedRoadCityZones == null || entry.allowedRoadCityZones.Count == 0)
                return false;

            var normalizedZone = NormalizeRoadCityZone(zone);
            for (var i = 0; i < entry.allowedRoadCityZones.Count; i++)
            {
                var candidateZone = NormalizeRoadCityZone(entry.allowedRoadCityZones[i]);
                if (candidateZone == normalizedZone)
                    return true;
            }

            return false;
        }

        private static bool EntryAllowsGeneralZoneFallback(StreamedCityBuildingEntry entry)
        {
            if (entry == null) return false;

            if (entry.allowedRoadCityZones == null || entry.allowedRoadCityZones.Count == 0)
                return true;

            for (var i = 0; i < entry.allowedRoadCityZones.Count; i++)
            {
                var zone = NormalizeRoadCityZone(entry.allowedRoadCityZones[i]);
                if (zone == RoadCityZone.Mixed)
                    return true;
            }

            return false;
        }

        private static RoadCityZone NormalizeRoadCityZone(RoadCityZone zone)
        {
            return zone == RoadCityZone.Unknown ? RoadCityZone.Mixed : zone;
        }
    }
}
