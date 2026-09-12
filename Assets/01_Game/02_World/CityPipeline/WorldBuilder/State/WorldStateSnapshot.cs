using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateSnapshot
    {
        private readonly Dictionary<string, string> _values = new();

        public IReadOnlyDictionary<string, string> Values => _values;

        public static WorldStateSnapshot Capture(WorldState state)
        {
            var canonical = WorldStateCanonicalizer.CanonicalizeCopy(state, out var report);
            if (!report.IsValid)
                throw new System.InvalidOperationException("Cannot snapshot invalid WorldState: " + string.Join("; ", report.Errors));

            var snapshot = new WorldStateSnapshot();
            snapshot.AddState(canonical);
            return snapshot;
        }

        private void AddState(WorldState state)
        {
            AddHeader(state.header);
            Add("$.revision", state.revision);
            Add("$.clock.currentHour", state.clock.currentHour);
            Add("$.clock.nextEventSequence", state.clock.nextEventSequence);
            Add("$.tiles.count", state.tiles.Count);
            for (var i = 0; i < state.tiles.Count; i++)
                AddTile(state.tiles[i]);
            AddEvents("$.pendingEvents", state.pendingEvents);
            AddEvents("$.eventHistory", state.eventHistory);
        }

        private void AddHeader(WorldStateHeader header)
        {
            Add("$.header.schemaVersion", header.schemaVersion);
            Add("$.header.canonicalFormatVersion", header.canonicalFormatVersion);
            Add("$.header.idAlgorithmVersion", header.idAlgorithmVersion);
            Add("$.header.generatorId", header.generatorId);
            Add("$.header.generatorVersion", header.generatorVersion);
            Add("$.header.worldSeed", header.worldSeed);
            Add("$.header.mapSizeTier", header.mapSizeTier.ToString());
            Add("$.header.profileVersion", header.profileVersion);
            Add("$.header.profileFingerprint", header.profileFingerprint);
            Add("$.header.planFingerprint", header.planFingerprint);
            Add("$.header.worldOriginXZ", header.worldOriginXZ);
            Add("$.header.tilesPerSide", header.tilesPerSide);
            Add("$.header.tileSizeMeters", header.tileSizeMeters);
            Add("$.header.worldBoundsXZ", header.worldBoundsXZ);
        }

        private void AddTile(WorldTilePartitionState tile)
        {
            var path = TilePath(tile.key);
            Add(path + ".key.x", tile.key.x);
            Add(path + ".key.z", tile.key.z);
            AddTerrain(path + ".terrain", tile.terrain);
            AddRegions(path + ".regions", tile.regions);
            AddSettlements(path + ".settlements", tile.settlements);
            AddRoads(path + ".roads", tile.roads);
            AddDistricts(path + ".districts", tile.districts);
            AddLots(path + ".lots", tile.lots);
            AddBuildings(path + ".buildings", tile.buildings);
            AddPois(path + ".pois", tile.pois);
        }

        private void AddTerrain(string path, TerrainChunkState terrain)
        {
            Add(path + ".baseGeneratorId", terrain.baseGeneratorId);
            Add(path + ".baseGeneratorVersion", terrain.baseGeneratorVersion);
            Add(path + ".baseGenerationFingerprint", terrain.baseGenerationFingerprint);
            Add(path + ".seaLevelWorldY", terrain.seaLevelWorldY);
            Add(path + ".terrainBaseWorldY", terrain.terrainBaseWorldY);
            Add(path + ".verticalSizeMeters", terrain.verticalSizeMeters);
            Add(path + ".heightmapResolution", terrain.heightmapResolution);
            Add(path + ".alphamapResolution", terrain.alphamapResolution);
            Add(path + ".baseMapResolution", terrain.baseMapResolution);
            Add(path + ".detailResolution", terrain.detailResolution);
            Add(path + ".detailSamplesPerPatch", terrain.detailSamplesPerPatch);
            AddTerrainModifications(path + ".modifications", terrain.modifications);
        }

        private void AddTerrainModifications(string path, List<TerrainModificationState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = EntityPath(path, record.id);
                Add(item + ".id", record.id);
                Add(item + ".kind", record.kind.ToString());
                Add(item + ".sourceEntityId", record.sourceEntityId);
                Add(item + ".createdAtHour", record.createdAtHour);
                Add(item + ".boundsXZ", record.boundsXZ);
                AddVectorList(item + ".outlineXZ", record.outlineXZ);
                Add(item + ".intensity01", record.intensity01);
                Add(item + ".active", record.active);
            }
        }

        private void AddEvents(string path, List<WorldEventState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = EntityPath(path, record.id);
                Add(item + ".id", record.id);
                Add(item + ".sequence", record.sequence);
                Add(item + ".type", record.type.ToString());
                Add(item + ".targetId", record.targetId);
                Add(item + ".scheduledHour", record.scheduledHour);
                Add(item + ".resolvedHour", record.resolvedHour);
                Add(item + ".magnitude", record.magnitude);
                Add(item + ".status", record.status.ToString());
                Add(item + ".resultCode", record.resultCode);
            }
        }

        private void AddVectorList(string path, List<Vector2> values)
        {
            Add(path + ".count", values.Count);
            for (var i = 0; i < values.Count; i++)
                Add($"{path}[{i}]", values[i]);
        }

        private void Add(string path, WorldEntityId value) => Add(path, value.ToString());
        private void Add(string path, bool value) => Add(path, value ? "true" : "false");
        private void Add(string path, int value) => Add(path, value.ToString(CultureInfo.InvariantCulture));
        private void Add(string path, long value) => Add(path, value.ToString(CultureInfo.InvariantCulture));
        private void Add(string path, float value) => Add(path, value.ToString("R", CultureInfo.InvariantCulture));
        private void Add(string path, string value) => _values[path] = value ?? string.Empty;

        private void Add(string path, Vector2 value) =>
            Add(path, $"{Format(value.x)},{Format(value.y)}");

        private void Add(string path, Vector3 value) =>
            Add(path, $"{Format(value.x)},{Format(value.y)},{Format(value.z)}");

        private void Add(string path, Quaternion value) =>
            Add(path, $"{Format(value.x)},{Format(value.y)},{Format(value.z)},{Format(value.w)}");

        private void Add(string path, Rect value) =>
            Add(path, $"{Format(value.x)},{Format(value.y)},{Format(value.width)},{Format(value.height)}");

        private static string Format(float value) =>
            value.ToString("R", CultureInfo.InvariantCulture);

        private static string TilePath(WorldTileKey key) =>
            $"$.tiles[{key.x},{key.z}]";

        private static string EntityPath(string listPath, WorldEntityId id) =>
            $"{listPath}[{id}]";
    }
}
