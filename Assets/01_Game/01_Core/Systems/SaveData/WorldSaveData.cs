using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zombera.Core
{
    [Serializable]
    public sealed class WorldStatePayloadSaveData
    {
        public bool hasData;
        public int envelopeVersion = 1;
        public int schemaVersion;
        public int canonicalFormatVersion;
        public int idAlgorithmVersion;
        public string contentEncoding = "gzip+base64";
        public string hashAlgorithm = "sha256";
        public string canonicalHash = string.Empty;
        public int uncompressedByteCount;
        public int compressedByteCount;
        public int worldSeed;
        public int profileVersion;
        public string graphVersion = string.Empty;
        public string profileFingerprint = string.Empty;
        public string planFingerprint = string.Empty;
        public string payloadBase64 = string.Empty;
    }

    [Serializable]
    public sealed class ProceduralWorldSaveData
    {
        public bool hasData;
        public int worldSeed;
        public string graphVersion = string.Empty;
        public int formatVersion = 3;
        public WorldMapSizeTier mapSizeTier = WorldMapSizeTier.Medium;
        public int tilesPerSide;
        public int originTileX;
        public int originTileZ;
        public int profileVersion = 1;
        public ulong planFingerprint;
        public List<ChunkProceduralDeltaSaveData> chunkDeltas = new();
        public WorldStatePayloadSaveData worldState = new();
    }

    [Serializable]
    public sealed class ChunkProceduralDeltaSaveData
    {
        public int chunkX;
        public int chunkZ;
        public bool cleared;
        public string notes = string.Empty;
        public List<string> entityIds = new();
    }

    [Serializable]
    public sealed class WorldChunkSaveData
    {
        [FormerlySerializedAs("Coordinates")] public Vector2Int coordinates;
        [FormerlySerializedAs("Seed")] public int seed;
        [FormerlySerializedAs("RegionId")] public string regionId;
    }
}
