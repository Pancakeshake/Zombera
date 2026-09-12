using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Deterministic wilderness tree/rock scatter from <see cref="WorldNatureProfile"/>.</summary>
    public sealed partial class WorldNaturePlacer
    {
        private const int MaxAttemptsPerInstance = 12;
        private const float DefaultMinElevation = -50f;
        private const float DefaultMaxElevation = 900f;

        private sealed class PlacementPassState
        {
            public IWorldTerrainQuery TerrainQuery;
            public Rect Bounds;
            public float SeaLevel;
            public int GrassLayerCount;
            public WorldSitePlan CitySites;
            public DeterministicRng Rng;
            public int SpawnedGameObjects;
            public int TerrainTrees;
            public int RejectedAttempts;
            public int AttemptBudget;
        }

        public IEnumerator Place(WorldBuildContext context)
        {
            if (context?.Profile?.Nature == null)
                yield break;

            var terrainQuery = context.WorldBuilder?.TerrainQuery;
            if (terrainQuery == null)
                yield break;

            EnsureRoot();
            RebuildTerrainCache();
            var entries = context.Profile.Nature.Entries;
            if (entries == null || entries.Count == 0)
                yield break;

            BeginTerrainTreePass(entries);

            var bounds = context.Scope.BoundsXZ;
            var state = new PlacementPassState
            {
                TerrainQuery = terrainQuery,
                Bounds = bounds,
                SeaLevel = context.Profile.Hydrology != null
                    ? context.Profile.Hydrology.SeaLevelWorldY
                    : 0f,
                GrassLayerCount = ResolveGrassLayerIndices(
                    context.Profile.Surfaces,
                    _grassLayerScratch),
                CitySites = context.Artifacts?.Sites,
                Rng = new DeterministicRng(WorldSubsystemSeeds.Derive(
                    context.Session.Seed,
                    context.Session.ProfileVersion,
                    WorldSubsystemSeeds.Nature))
            };

            var areaKm2 = Mathf.Max(0.01f, bounds.width * bounds.height * 1e-6f);
            var placeEntries = PlaceEntries(entries, areaKm2, state);
            while (placeEntries.MoveNext())
                yield return placeEntries.Current;

            FlushTerrainTrees(bounds);

            Debug.Log(
                $"[WorldNaturePlacer] scope={bounds.width:F0}x{bounds.height:F0}m " +
                $"areaKm2={areaKm2:F2} terrainTrees={state.TerrainTrees} " +
                $"gameObjects={state.SpawnedGameObjects} rejected={state.RejectedAttempts} " +
                $"terrainsCached={_terrainCache.Count}");
        }

        private IEnumerator PlaceEntries(
            IReadOnlyList<WorldNatureEntry> entries,
            float areaKm2,
            PlacementPassState state)
        {
            for (var e = 0; e < entries.Count; e++)
            {
                var entry = entries[e];
                if (entry?.Prefab == null)
                    continue;

                var targetCount = ComputeTargetCount(entry, areaKm2);
                var placeEntry = PlaceEntryInstances(entry, targetCount, state);
                while (placeEntry.MoveNext())
                    yield return placeEntry.Current;
            }
        }

        private IEnumerator PlaceEntryInstances(
            WorldNatureEntry entry,
            int targetCount,
            PlacementPassState state)
        {
            for (var n = 0; n < targetCount; n++)
            {
                if (!TryPickPosition(entry, state, out var sample))
                {
                    state.RejectedAttempts++;
                    state.AttemptBudget++;
                    if ((state.AttemptBudget & 255) == 255)
                        yield return null;
                    continue;
                }

                if (!TryCommitPlacement(entry, sample, state))
                    continue;

                if (((state.SpawnedGameObjects + state.TerrainTrees) & 31) == 31)
                    yield return null;
            }
        }

        private bool TryCommitPlacement(
            WorldNatureEntry entry,
            WorldTerrainSample sample,
            PlacementPassState state)
        {
            if (entry.PlacementMode == WorldNaturePlacementMode.TerrainTree)
            {
                if (!TryBufferTerrainTree(entry, sample, state.Rng))
                    return false;
                state.TerrainTrees++;
                return true;
            }

            if (!TryInstantiate(entry, sample, state.Rng, out var instance))
                return false;

            _spawned.Add(instance);
            state.SpawnedGameObjects++;
            return true;
        }

        private static int ComputeTargetCount(WorldNatureEntry entry, float areaKm2)
        {
            var density = Mathf.Max(0f, entry.DensityPerKm2);
            if (density <= 0f)
                return 0;

            var fromDensity = Mathf.RoundToInt(density * areaKm2);
            if (entry.MaxInstancesPerTile <= 0)
                return fromDensity;

            var cap = entry.MaxInstancesPerTile * 64;
            return Mathf.Min(fromDensity, cap);
        }

        private bool TryPickPosition(
            WorldNatureEntry entry,
            PlacementPassState state,
            out WorldTerrainSample sample)
        {
            sample = default;
            for (var attempt = 0; attempt < MaxAttemptsPerInstance; attempt++)
            {
                var x = Mathf.Lerp(state.Bounds.xMin, state.Bounds.xMax, state.Rng.NextFloat01());
                var z = Mathf.Lerp(state.Bounds.yMin, state.Bounds.yMax, state.Rng.NextFloat01());
                var pos = new Vector2(x, z);
                if (!state.TerrainQuery.TrySample(pos, out sample))
                    continue;
                if (!MatchesEntry(entry, sample, state.SeaLevel))
                    continue;
                if (!PassesGrassGate(entry, pos, _grassLayerScratch, state.GrassLayerCount))
                    continue;
                if (CityExclusionBounds.ContainsAny(state.CitySites, pos))
                    continue;

                return true;
            }

            return false;
        }

        private static bool MatchesEntry(
            WorldNatureEntry entry,
            WorldTerrainSample sample,
            float seaLevel)
        {
            if (sample.Water.DepthMeters > 0.15f)
                return false;
            if (entry.MinDistanceToWaterMeters > 0f &&
                sample.Water.DistanceMeters < entry.MinDistanceToWaterMeters)
                return false;
            if (sample.SlopeDegrees > entry.MaxSlopeDegrees)
                return false;

            if (!BiomeMatches(entry, sample.Biome.DominantStableId))
                return false;

            var minElev = entry.ElevationRangeMeters.x;
            var maxElev = entry.ElevationRangeMeters.y;
            if (maxElev <= minElev)
            {
                minElev = DefaultMinElevation;
                maxElev = DefaultMaxElevation;
            }

            var elevAboveSea = sample.HeightWorldY - seaLevel;
            if (elevAboveSea < minElev || elevAboveSea > maxElev)
                return false;

            return true;
        }

        private static bool BiomeMatches(WorldNatureEntry entry, string dominantStableId)
        {
            var allowed = entry.AllowedBiomeIds;
            if (allowed == null || allowed.Length == 0)
                return true;
            if (string.IsNullOrEmpty(dominantStableId))
                return false;

            for (var i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == dominantStableId)
                    return true;
            }

            return false;
        }

        private bool TryInstantiate(
            WorldNatureEntry entry,
            WorldTerrainSample sample,
            DeterministicRng rng,
            out GameObject instance)
        {
            instance = null;
            var scaleMin = entry.ScaleRange.x > 0f ? entry.ScaleRange.x : 0.85f;
            var scaleMax = entry.ScaleRange.y > scaleMin ? entry.ScaleRange.y : scaleMin + 0.3f;
            var scale = Mathf.Lerp(scaleMin, scaleMax, rng.NextFloat01());
            var yaw = rng.NextFloat01() * 360f;
            var pos = new Vector3(sample.WorldXZ.x, sample.HeightWorldY, sample.WorldXZ.y);

            instance = Instantiate(entry.Prefab, pos, Quaternion.Euler(0f, yaw, 0f), _root);
            instance.transform.localScale = Vector3.one * scale;
            return instance != null;
        }
    }
}
