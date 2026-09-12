using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public sealed class TerrainChunkState
    {
        public string baseGeneratorId = "WorldBuilder.Terrain";
        public int baseGeneratorVersion = 1;
        public string baseGenerationFingerprint = string.Empty;
        public float seaLevelWorldY;
        public float terrainBaseWorldY;
        public float verticalSizeMeters = 600f;
        public int heightmapResolution;
        public int alphamapResolution;
        public int baseMapResolution;
        public int detailResolution;
        public int detailSamplesPerPatch;
        public List<TerrainModificationState> modifications = new();
    }

    [Serializable]
    public sealed class TerrainModificationState
    {
        public WorldEntityId id;
        public TerrainModificationKind kind;
        public WorldEntityId sourceEntityId;
        public long createdAtHour;
        public Rect boundsXZ;
        public List<Vector2> outlineXZ = new();
        public float intensity01;
        public bool active = true;
    }
}
