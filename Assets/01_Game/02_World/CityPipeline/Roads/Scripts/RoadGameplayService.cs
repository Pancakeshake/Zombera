using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    [AddComponentMenu("Zombera/World/Road Gameplay Service")]
    [DisallowMultipleComponent]
    public sealed class RoadGameplayService : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameplayRoadGraph roadGraph;
        [SerializeField] private GameplayRoadDerivedData bakedData;

        [Header("Runtime Bake")]
        [SerializeField] private bool rebuildFromGraphWhenMissing = true;
        [SerializeField] [Min(0.5f)] private float runtimeSampleSpacingMeters = 4f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] [Min(0.1f)] private float sampleGizmoSphereRadius = 0.2f;
        [SerializeField] [Min(0.1f)] private float spawnGizmoSphereRadius = 0.9f;

        private readonly List<RoadSamplePoint> _runtimeSamples = new(1024);
        private readonly List<ResolvedRoadSpawnPoint> _runtimeSpawnPoints = new(128);

        public GameplayRoadGraph RoadGraph => roadGraph;
        public GameplayRoadDerivedData BakedData => bakedData;
        public IReadOnlyList<RoadSamplePoint> RuntimeSamples => _runtimeSamples;
        public IReadOnlyList<ResolvedRoadSpawnPoint> RuntimeSpawnPoints => _runtimeSpawnPoints;

        private readonly Dictionary<(int x, int z), List<RoadSamplePoint>> _tileRuntimeSamples = new();

        private void Awake()
        {
            RebuildRuntimeCache();
        }

        public void AddTileRuntimeSamples(Vector2Int coord, List<RoadSamplePoint> samples)
        {
            var key = (coord.x, coord.y);
            if (_tileRuntimeSamples.ContainsKey(key))
            {
                // Remove old samples from global list
                foreach (var s in _tileRuntimeSamples[key]) _runtimeSamples.Remove(s);
            }

            _tileRuntimeSamples[key] = samples;
            _runtimeSamples.AddRange(samples);
        }

        public void ClearTileRuntimeSamples(Vector2Int coord)
        {
            var key = (coord.x, coord.y);
            if (_tileRuntimeSamples.TryGetValue(key, out var samples))
            {
                foreach (var s in samples) _runtimeSamples.Remove(s);
                _tileRuntimeSamples.Remove(key);
            }
        }

        public void Configure(GameplayRoadGraph graph, GameplayRoadDerivedData derivedData)
        {
            roadGraph = graph;
            bakedData = derivedData;
            RebuildRuntimeCache();
        }

        [ContextMenu("Rebuild Runtime Cache")]
        public void RebuildRuntimeCache()
        {
            _runtimeSamples.Clear();
            _runtimeSpawnPoints.Clear();

            if (bakedData != null && bakedData.Samples is { Count: > 0 })
            {
                _runtimeSamples.AddRange(bakedData.Samples);
                if (bakedData.ResolvedSpawnPoints is { Count: > 0 })
                    _runtimeSpawnPoints.AddRange(bakedData.ResolvedSpawnPoints);

                return;
            }

            if (!rebuildFromGraphWhenMissing || roadGraph == null) return;

            GameplayRoadBaker.BuildRuntimeCache(
                roadGraph,
                runtimeSampleSpacingMeters,
                _runtimeSamples,
                _runtimeSpawnPoints);
        }

        // ReSharper disable once UnusedMember.Global
        public bool TryGetNearestRoadSample(Vector3 worldPosition, float maxDistanceMeters, out RoadSamplePoint sample)
        {
            sample = default;
            if (_runtimeSamples.Count == 0) return false;

            var maxDistanceSq = Mathf.Max(0f, maxDistanceMeters);
            maxDistanceSq *= maxDistanceSq;

            var found = false;
            var bestDistanceSq = float.MaxValue;

            foreach (var candidate in _runtimeSamples)
            {
                var distanceSq = (candidate.position - worldPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq) continue;
                if (distanceSq >= bestDistanceSq) continue;

                bestDistanceSq = distanceSq;
                sample = candidate;
                found = true;
            }

            return found;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            foreach (var sample in _runtimeSamples)
            {
                Gizmos.color = ResolveRoadColor(sample.roadType);
                Gizmos.DrawSphere(sample.position, sampleGizmoSphereRadius);
            }

            foreach (var spawn in RuntimeSpawnPoints)
            {
                Gizmos.color = ResolveSpawnColor(spawn.role);
                Gizmos.DrawSphere(spawn.position, spawnGizmoSphereRadius);
            }
        }

        private static Color ResolveRoadColor(GameplayRoadType roadType)
        {
            return roadType switch
            {
                GameplayRoadType.Highway => new Color(0.15f, 0.60f, 1f, 0.95f),
                GameplayRoadType.Arterial => new Color(0.95f, 0.70f, 0.20f, 0.95f),
                GameplayRoadType.Local => new Color(0.45f, 0.95f, 0.45f, 0.95f),
                GameplayRoadType.DirtTrack => new Color(0.65f, 0.42f, 0.20f, 0.95f),
                GameplayRoadType.ServiceRoad => new Color(0.75f, 0.75f, 0.75f, 0.95f),
                GameplayRoadType.Footpath => new Color(1f, 0.92f, 0.50f, 0.95f),
                _ => Color.white
            };
        }

        private static Color ResolveSpawnColor(RoadSpawnRole role)
        {
            return role switch
            {
                RoadSpawnRole.AmbientZombie => new Color(0.85f, 0.20f, 0.20f, 0.95f),
                RoadSpawnRole.Patrol => new Color(0.20f, 0.70f, 1f, 0.95f),
                RoadSpawnRole.Vehicle => new Color(0.95f, 0.95f, 0.20f, 0.95f),
                RoadSpawnRole.Loot => new Color(0.95f, 0.60f, 0.15f, 0.95f),
                RoadSpawnRole.Survivor => new Color(0.20f, 0.95f, 0.35f, 0.95f),
                RoadSpawnRole.Encounter => new Color(0.95f, 0.20f, 0.85f, 0.95f),
                _ => Color.white
            };
        }
    }
}
