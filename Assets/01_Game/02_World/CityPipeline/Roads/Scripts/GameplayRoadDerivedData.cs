using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    [Serializable]
    public struct RoadSamplePoint
    {
        public Vector3 position;
        public Vector3 tangent;
        public string segmentId;
        public GameplayRoadType roadType;
        public RoadCityZone cityZone;
        public float widthMeters;
        public int laneCount;
        public float laneWidthMeters;
        public float speedModifier;
        public float noiseLevel;
        public RoadNavAreaType navArea;
        public bool contributesPathArea;
        public string fromBiomeId;
        public string toBiomeId;
        public float biomeTransition01;
        public bool patrolRoute;
        public bool vehicleRoute;
        public bool lootZone;
        public float lootZoneWeight;
        public bool supportsSidewalks;
        public float sidewalkWidthMeters;
        public bool supportsDecals;
        public float decalDensity;
        public bool roadsideLotEligible;
        public float roadsideLotWeight;
        public float roadsideLotSpacingMeters;
        public float roadsideLotDepthMeters;
    }

    [Serializable]
    public struct ResolvedRoadSpawnPoint
    {
        public string spawnId;
        public string segmentId;
        public Vector3 position;
        public Vector3 tangent;
        public RoadSpawnRole role;
        public float radiusMeters;
        public float weight;
        public bool requiresNavMesh;
        public string tag;
    }

    [CreateAssetMenu(menuName = "Zombera/World/Gameplay Road Derived Data", fileName = "GameplayRoadDerivedData")]
    public sealed class GameplayRoadDerivedData : ScriptableObject
    {
        [SerializeField] [Min(0.5f)] private float sampleSpacingMeters = 4f;
        [SerializeField] private List<RoadSamplePoint> samples = new();
        [SerializeField] private List<ResolvedRoadSpawnPoint> resolvedSpawnPoints = new();

        [Header("Source Stats")]
        [SerializeField] private string sourceGraphName = string.Empty;
        [SerializeField] private int sourceNodeCount;
        [SerializeField] private int sourceSegmentCount;
        [SerializeField] private int sourceSpawnPointCount;

        /// <summary>Inspector / tooling: graph asset name from the last bake.</summary>
        public string SourceGraphName => sourceGraphName;

        /// <summary>Inspector / tooling: node count from the last bake.</summary>
        public int SourceNodeCount => sourceNodeCount;

        /// <summary>Inspector / tooling: segment count from the last bake.</summary>
        public int SourceSegmentCount => sourceSegmentCount;

        /// <summary>Inspector / tooling: spawn point count from the last bake.</summary>
        public int SourceSpawnPointCount => sourceSpawnPointCount;

        public float SampleSpacingMeters
        {
            get => sampleSpacingMeters;
            set => sampleSpacingMeters = Mathf.Max(0.5f, value);
        }

        public IReadOnlyList<RoadSamplePoint> Samples => samples;
        public IReadOnlyList<ResolvedRoadSpawnPoint> ResolvedSpawnPoints => resolvedSpawnPoints;

        public void ReplaceSamples(List<RoadSamplePoint> newSamples)
        {
            samples.Clear();
            if (newSamples == null || newSamples.Count == 0) return;

            samples.AddRange(newSamples);
        }

        public void ReplaceResolvedSpawnPoints(List<ResolvedRoadSpawnPoint> newSpawnPoints)
        {
            resolvedSpawnPoints.Clear();
            if (newSpawnPoints == null || newSpawnPoints.Count == 0) return;

            resolvedSpawnPoints.AddRange(newSpawnPoints);
        }

        public void SetSourceStats(GameplayRoadGraph graph)
        {
            if (graph == null)
            {
                sourceGraphName = string.Empty;
                sourceNodeCount = 0;
                sourceSegmentCount = 0;
                sourceSpawnPointCount = 0;
                return;
            }

            sourceGraphName = graph.name;
            sourceNodeCount = graph.Nodes?.Count ?? 0;
            sourceSegmentCount = graph.Segments?.Count ?? 0;
            sourceSpawnPointCount = graph.SpawnPoints?.Count ?? 0;
        }
    }
}
