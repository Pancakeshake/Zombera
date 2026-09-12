using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static partial class WorldStateValidator
    {
        private static void ValidateEvents(List<WorldEventState> records, string path, Dictionary<WorldEntityId, WorldEntityKind> ids, WorldValidationMode mode, WorldValidationReport report)
        {
            if (records == null)
                return;

            WorldEventState previous = null;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var recordPath = IdPath(path, record?.id ?? default, i);
                if (record == null)
                {
                    Add(report, EventInvalid, default, recordPath, "World event is null.");
                    continue;
                }

                RequireOptionalReference(record.id, record.targetId, WorldEntityKind.None, ids, recordPath + ".targetId", report);
                RequireFinite(record.magnitude, recordPath + ".magnitude", record.id, report);
                if (record.sequence <= 0)
                    Add(report, EventInvalid, record.id, recordPath + ".sequence", "Event sequence must be positive.", "> 0", record.sequence.ToString());
                if (record.scheduledHour < 0)
                    Add(report, EventInvalid, record.id, recordPath + ".scheduledHour", "Event scheduled hour must be non-negative.", ">= 0", record.scheduledHour.ToString());
                if (record.status != WorldEventStatus.Pending && record.resolvedHour < 0)
                    Add(report, EventInvalid, record.id, recordPath + ".resolvedHour", "Resolved events must include a resolved hour.");
                if (mode == WorldValidationMode.Canonical && previous != null && CompareEvents(previous, record) > 0)
                    Add(report, ListNotCanonical, record.id, path, "Events must be sorted by scheduledHour, sequence, then id.");
                previous = record;
            }
        }

        private static void RequireReference(WorldEntityId id, WorldEntityId target, WorldEntityKind expectedKind, Dictionary<WorldEntityId, WorldEntityKind> ids, string path, WorldValidationReport report)
        {
            if (IsEmpty(target))
            {
                Add(report, ReferenceDangling, id, path, "Required reference is missing.", expectedKind.ToString(), WorldEntityKind.None.ToString());
                return;
            }

            RequireOptionalReference(id, target, expectedKind, ids, path, report);
        }

        private static void RequireOptionalReference(WorldEntityId id, WorldEntityId target, WorldEntityKind expectedKind, Dictionary<WorldEntityId, WorldEntityKind> ids, string path, WorldValidationReport report)
        {
            if (IsEmpty(target))
                return;

            if (!ids.TryGetValue(target, out var actualKind))
            {
                Add(report, ReferenceDangling, id, path, "Referenced entity does not exist.", expectedKind.ToString(), target.ToString());
                return;
            }

            if (expectedKind != WorldEntityKind.None && actualKind != expectedKind)
                Add(report, ReferenceKind, id, path, "Referenced entity kind does not match.", expectedKind.ToString(), actualKind.ToString());
        }

        private static void RequireOwnerTile(WorldStateHeader header, Vector3 position, WorldTileKey ownerTile, WorldEntityId id, string path, WorldValidationReport report)
        {
            RequireOwnerTile(header, new Vector2(position.x, position.z), ownerTile, id, path, report);
        }

        private static void RequireOwnerTile(WorldStateHeader header, Vector2 position, WorldTileKey ownerTile, WorldEntityId id, string path, WorldValidationReport report)
        {
            if (header == null || !IsFinite(position))
                return;

            if (!WorldTileOwnership.TryResolveOwnerTile(header, position, out var resolved, false))
            {
                Add(report, OwnerTileMismatch, id, path, "Could not resolve owner tile for position.", ownerTile.ToString(), "out of range");
                return;
            }

            if (resolved != ownerTile)
                Add(report, OwnerTileMismatch, id, path, "Entity is stored in the wrong tile partition.", resolved.ToString(), ownerTile.ToString());
        }

        private static void RequireKnownArchetype(WorldEntityId id, string archetypeId, WorldValidationContext context, bool building, string path, WorldValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                Add(report, ArchetypeMissing, id, path, "Archetype id is required.");
                return;
            }

            if (context == null || !context.RequireKnownArchetypes)
                return;

            var known = building
                ? context.KnowsBuildingArchetype(archetypeId)
                : context.KnowsPoiArchetype(archetypeId);
            if (!known)
                Add(report, ArchetypeMissing, id, path, "Archetype id was not found in the validation context.", "known archetype", archetypeId);
        }

        private static void RequireFinite(float value, string path, WorldEntityId id, WorldValidationReport report)
        {
            if (!IsFinite(value))
                Add(report, FloatNonFinite, id, path, "Float value must be finite.", "finite", value.ToString());
        }

        private static void RequireFinite(Vector2 value, string path, WorldEntityId id, WorldValidationReport report)
        {
            RequireFinite(value.x, path + ".x", id, report);
            RequireFinite(value.y, path + ".y", id, report);
        }

        private static void RequireFinite(Vector3 value, string path, WorldEntityId id, WorldValidationReport report)
        {
            RequireFinite(value.x, path + ".x", id, report);
            RequireFinite(value.y, path + ".y", id, report);
            RequireFinite(value.z, path + ".z", id, report);
        }

        private static void RequireFinite(Quaternion value, string path, WorldEntityId id, WorldValidationReport report)
        {
            RequireFinite(value.x, path + ".x", id, report);
            RequireFinite(value.y, path + ".y", id, report);
            RequireFinite(value.z, path + ".z", id, report);
            RequireFinite(value.w, path + ".w", id, report);
        }

        private static void RequireFinite(Rect value, string path, WorldEntityId id, WorldValidationReport report)
        {
            RequireFinite(value.x, path + ".x", id, report);
            RequireFinite(value.y, path + ".y", id, report);
            RequirePositive(value.width, id, path + ".width", report);
            RequirePositive(value.height, id, path + ".height", report);
        }

        private static void RequirePositive(float value, WorldEntityId id, string path, WorldValidationReport report)
        {
            RequireFinite(value, path, id, report);
            if (IsFinite(value) && value <= 0f)
                Add(report, GeometryInvalid, id, path, "Value must be positive.", "> 0", value.ToString());
        }

        private static void RequireRange01(float value, WorldEntityId id, string path, WorldValidationReport report)
        {
            RequireFinite(value, path, id, report);
            if (IsFinite(value) && (value < 0f || value > 1f))
                Add(report, HealthInvalid, id, path, "Value must be in [0, 1].", "0..1", value.ToString());
        }

        private static void ValidateVectorList(List<Vector2> points, WorldEntityId id, string path, int minCount, WorldValidationReport report)
        {
            if (points == null || points.Count < minCount)
            {
                Add(report, GeometryInvalid, id, path, "Point list does not contain enough points.", minCount.ToString(), points?.Count.ToString() ?? "null");
                return;
            }

            for (var i = 0; i < points.Count; i++)
                RequireFinite(points[i], $"{path}[{i}]", id, report);
        }

        private static void RequireSorted<T>(
            List<T> records,
            System.Func<T, WorldEntityId> idSelector,
            string path,
            WorldValidationReport report)
        {
            if (records == null || records.Count < 2)
                return;

            var previous = idSelector(records[0]);
            for (var i = 1; i < records.Count; i++)
            {
                var current = idSelector(records[i]);
                if (previous.CompareTo(current) > 0)
                {
                    Add(report, ListNotCanonical, current, path, "Entity list must be sorted by typed id.");
                    return;
                }

                previous = current;
            }
        }

        private static string TilePath(WorldTilePartitionState tile) =>
            tile == null ? "$.tiles[null]" : $"$.tiles[{tile.key.x},{tile.key.z}]";

        private static string IdPath(string listPath, WorldEntityId id, int index) =>
            IsEmpty(id) ? $"{listPath}[{index}]" : $"{listPath}[{id}]";
    }
}
