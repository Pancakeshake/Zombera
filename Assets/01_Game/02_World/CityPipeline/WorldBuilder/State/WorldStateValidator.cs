using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static partial class WorldStateValidator
    {
        public const string SchemaUnsupported = "WS_SCHEMA_UNSUPPORTED";
        public const string HeaderInvalid = "WS_HEADER_INVALID";
        public const string TileDuplicate = "WS_TILE_DUPLICATE";
        public const string TileOutOfRange = "WS_TILE_OUT_OF_RANGE";
        public const string IdInvalid = "WS_ID_INVALID";
        public const string IdKindMismatch = "WS_ID_KIND_MISMATCH";
        public const string IdDuplicate = "WS_ID_DUPLICATE";
        public const string OwnerTileMismatch = "WS_OWNER_TILE_MISMATCH";
        public const string ReferenceDangling = "WS_REFERENCE_DANGLING";
        public const string ReferenceKind = "WS_REFERENCE_KIND";
        public const string GeometryInvalid = "WS_GEOMETRY_INVALID";
        public const string FloatNonFinite = "WS_FLOAT_NONFINITE";
        public const string ArchetypeMissing = "WS_ARCHETYPE_MISSING";
        public const string HealthInvalid = "WS_HEALTH_INVALID";
        public const string EventInvalid = "WS_EVENT_INVALID";
        public const string ListNotCanonical = "WS_LIST_NOT_CANONICAL";

        public static WorldValidationReport Validate(WorldState state) =>
            Validate(state, WorldValidationMode.Basic, null);

        public static WorldValidationReport Validate(
            WorldState state,
            WorldValidationMode mode,
            WorldValidationContext context = null)
        {
            var report = new WorldValidationReport();
            if (state == null)
            {
                Add(report, HeaderInvalid, default, "$", "WorldState is null.");
                return report;
            }

            ValidateHeader(state.header, report);
            var ids = CollectIds(state, report);
            ValidateTiles(state, mode, report);
            ValidateTileContents(state, ids, mode, context, report);
            ValidateEvents(state.pendingEvents, "$.pendingEvents", ids, mode, report);
            ValidateEvents(state.eventHistory, "$.eventHistory", ids, mode, report);
            report.IsValid = report.Errors.Count == 0;
            return report;
        }

        private static void ValidateHeader(WorldStateHeader header, WorldValidationReport report)
        {
            if (header == null)
            {
                Add(report, HeaderInvalid, default, "$.header", "WorldState header is null.");
                return;
            }

            ValidateHeaderVersions(header, report);
            RequireFinite(header.worldOriginXZ, "$.header.worldOriginXZ", default, report);
            RequireFinite(header.worldBoundsXZ, "$.header.worldBoundsXZ", default, report);

            if (header.tilesPerSide <= 0)
                Add(report, HeaderInvalid, default, "$.header.tilesPerSide", "tilesPerSide must be positive.", "> 0", header.tilesPerSide.ToString());
            RequireFinite(header.tileSizeMeters, "$.header.tileSizeMeters", default, report);
            if (IsFinite(header.tileSizeMeters) && header.tileSizeMeters <= 0f)
                Add(report, HeaderInvalid, default, "$.header.tileSizeMeters", "tileSizeMeters must be finite and positive.", "> 0", header.tileSizeMeters.ToString());
        }

        private static void ValidateHeaderVersions(WorldStateHeader header, WorldValidationReport report)
        {
            if (header.schemaVersion != WorldStateSchema.CurrentVersion)
                Add(report, SchemaUnsupported, default, "$.header.schemaVersion", "Unsupported WorldState schema version.", WorldStateSchema.CurrentVersion.ToString(), header.schemaVersion.ToString());
            if (header.canonicalFormatVersion != WorldStateSchema.CanonicalFormatVersion)
                Add(report, SchemaUnsupported, default, "$.header.canonicalFormatVersion", "Unsupported canonical format version.", WorldStateSchema.CanonicalFormatVersion.ToString(), header.canonicalFormatVersion.ToString());
            if (header.idAlgorithmVersion != WorldStateSchema.IdAlgorithmVersion)
                Add(report, SchemaUnsupported, default, "$.header.idAlgorithmVersion", "Unsupported world id algorithm version.", WorldStateSchema.IdAlgorithmVersion.ToString(), header.idAlgorithmVersion.ToString());
        }

        private static void ValidateTiles(
            WorldState state,
            WorldValidationMode mode,
            WorldValidationReport report)
        {
            var tiles = new Dictionary<WorldTileKey, int>();
            if (state.tiles == null)
            {
                Add(report, HeaderInvalid, default, "$.tiles", "Tile list is null.");
                return;
            }

            var previous = default(WorldTileKey);
            for (var i = 0; i < state.tiles.Count; i++)
            {
                var path = $"$.tiles[{i}]";
                var tile = state.tiles[i];
                if (tile == null)
                {
                    Add(report, HeaderInvalid, default, path, "Tile partition is null.");
                    continue;
                }

                ValidateTileKey(state.header, tile.key, path + ".key", report);
                if (tiles.ContainsKey(tile.key))
                    Add(report, TileDuplicate, default, path, "Duplicate tile partition.", "unique tile key", tile.key.ToString());
                else
                    tiles.Add(tile.key, i);

                if (mode == WorldValidationMode.Canonical && i > 0 && previous.CompareTo(tile.key) > 0)
                    Add(report, ListNotCanonical, default, "$.tiles", "Tiles must be sorted by z, then x.");
                previous = tile.key;
            }

        }

        private static void ValidateTileKey(
            WorldStateHeader header,
            WorldTileKey key,
            string path,
            WorldValidationReport report)
        {
            if (header == null)
                return;

            var inRange = key.x >= 0 &&
                key.z >= 0 &&
                key.x < header.tilesPerSide &&
                key.z < header.tilesPerSide;
            if (!inRange)
                Add(report, TileOutOfRange, default, path, "Tile key is outside header grid.", $"0..{header.tilesPerSide - 1}", key.ToString());
        }

        private static void Add(
            WorldValidationReport report,
            string code,
            WorldEntityId entityId,
            string fieldPath,
            string message,
            string expected = "",
            string actual = "")
        {
            report.AddIssue(
                WorldValidationSeverity.Error,
                code,
                entityId,
                fieldPath,
                message,
                expected,
                actual);
        }

        private static bool IsEmpty(WorldEntityId id) =>
            id.kind == WorldEntityKind.None && string.IsNullOrEmpty(id.value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);

        private static bool IsFinite(Rect value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.width) && IsFinite(value.height);
    }
}
