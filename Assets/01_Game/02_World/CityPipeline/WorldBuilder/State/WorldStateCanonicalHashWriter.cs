using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateCanonicalHashWriter
    {
        private const string Prefix = "Zombera.WorldState.CanonicalHash";
        private readonly Stream _stream;

        public WorldStateCanonicalHashWriter(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public void Write(WorldState state)
        {
            if (state == null)
                throw new InvalidOperationException("Cannot hash a null WorldState.");

            WriteString(Prefix);
            WriteInt32(WorldStateSchema.CurrentVersion);
            WriteInt32(WorldStateSchema.CanonicalFormatVersion);
            WriteHeader(state.header);
            WriteInt64(state.revision);
            WriteClock(state.clock);
            WriteList(state.tiles, WriteTile);
            WriteList(state.pendingEvents, WriteEvent);
            WriteList(state.eventHistory, WriteEvent);
        }

        private void WriteHeader(WorldStateHeader header)
        {
            if (header == null)
                throw new InvalidOperationException("Cannot hash a WorldState with a null header.");

            WriteInt32(header.schemaVersion);
            WriteInt32(header.canonicalFormatVersion);
            WriteInt32(header.idAlgorithmVersion);
            WriteString(header.generatorId);
            WriteInt32(header.generatorVersion);
            WriteInt32(header.worldSeed);
            WriteInt32((int)header.mapSizeTier);
            WriteInt32(header.profileVersion);
            WriteString(header.profileFingerprint);
            WriteString(header.planFingerprint);
            WriteVector2(header.worldOriginXZ);
            WriteInt32(header.tilesPerSide);
            WriteSingle(header.tileSizeMeters);
            WriteRect(header.worldBoundsXZ);
        }

        private void WriteClock(WorldSimulationClockState clock)
        {
            if (clock == null)
                throw new InvalidOperationException("Cannot hash a WorldState with a null clock.");

            WriteInt64(clock.currentHour);
            WriteInt64(clock.nextEventSequence);
        }

        private void WriteTile(WorldTilePartitionState tile)
        {
            if (tile == null)
                throw new InvalidOperationException("Cannot hash a WorldState with a null tile.");

            WriteTileKey(tile.key);
            WriteTerrain(tile.terrain);
            WriteList(tile.regions, WriteRegion);
            WriteList(tile.settlements, WriteSettlement);
            WriteList(tile.roads, WriteRoad);
            WriteList(tile.districts, WriteDistrict);
            WriteList(tile.lots, WriteLot);
            WriteList(tile.buildings, WriteBuilding);
            WriteList(tile.pois, WritePoi);
        }

        private void WriteTerrain(TerrainChunkState terrain)
        {
            if (terrain == null)
                throw new InvalidOperationException("Cannot hash a WorldState with null terrain.");

            WriteString(terrain.baseGeneratorId);
            WriteInt32(terrain.baseGeneratorVersion);
            WriteString(terrain.baseGenerationFingerprint);
            WriteSingle(terrain.seaLevelWorldY);
            WriteSingle(terrain.terrainBaseWorldY);
            WriteSingle(terrain.verticalSizeMeters);
            WriteInt32(terrain.heightmapResolution);
            WriteInt32(terrain.alphamapResolution);
            WriteInt32(terrain.baseMapResolution);
            WriteInt32(terrain.detailResolution);
            WriteInt32(terrain.detailSamplesPerPatch);
            WriteList(terrain.modifications, WriteTerrainModification);
        }

        private void WriteEntityId(WorldEntityId id)
        {
            WriteInt32((int)id.kind);
            WriteString(id.value);
        }

        private void WriteTileKey(WorldTileKey key)
        {
            WriteInt32(key.x);
            WriteInt32(key.z);
        }

        private void WriteVector2(Vector2 value)
        {
            WriteSingle(value.x);
            WriteSingle(value.y);
        }

        private void WriteVector3(Vector3 value)
        {
            WriteSingle(value.x);
            WriteSingle(value.y);
            WriteSingle(value.z);
        }

        private void WriteQuaternion(Quaternion value)
        {
            WriteSingle(value.x);
            WriteSingle(value.y);
            WriteSingle(value.z);
            WriteSingle(value.w);
        }

        private void WriteRect(Rect value)
        {
            WriteSingle(value.x);
            WriteSingle(value.y);
            WriteSingle(value.width);
            WriteSingle(value.height);
        }

        private void WriteBool(bool value)
        {
            _stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        private void WriteInt32(int value)
        {
            unchecked
            {
                _stream.WriteByte((byte)value);
                _stream.WriteByte((byte)(value >> 8));
                _stream.WriteByte((byte)(value >> 16));
                _stream.WriteByte((byte)(value >> 24));
            }
        }

        private void WriteInt64(long value)
        {
            unchecked
            {
                _stream.WriteByte((byte)value);
                _stream.WriteByte((byte)(value >> 8));
                _stream.WriteByte((byte)(value >> 16));
                _stream.WriteByte((byte)(value >> 24));
                _stream.WriteByte((byte)(value >> 32));
                _stream.WriteByte((byte)(value >> 40));
                _stream.WriteByte((byte)(value >> 48));
                _stream.WriteByte((byte)(value >> 56));
            }
        }

        private void WriteSingle(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            var bits = BitConverter.ToInt32(bytes, 0);
            WriteInt32(bits);
        }

        private void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteInt32(bytes.Length);
            _stream.Write(bytes, 0, bytes.Length);
        }

        private void WriteList<T>(List<T> records, Action<T> writer)
        {
            WriteInt32(records?.Count ?? 0);
            if (records == null)
                return;

            for (var i = 0; i < records.Count; i++)
                writer(records[i]);
        }
    }
}
