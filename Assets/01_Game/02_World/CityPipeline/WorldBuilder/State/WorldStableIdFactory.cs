using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static partial class WorldStableIdFactory
    {
        public const int IdAlgorithmVersion = 1;

        private const string DomainLiteral = "zombera-world-entity-id";
        private const ulong FnVOffsetSeed = 14695981039346656037UL;

        public static WorldEntityId CreateRegionId(
            int worldSeed,
            string sourceId,
            int ordinal,
            Rect boundsXZ,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.Region,
                worldSeed,
                WorldEntityKind.None,
                EmptyId(),
                sourceId,
                ordinal,
                Box2(boundsXZ.center, boundsXZ.size * 0.5f),
                collisionRegistry);
        }

        public static WorldEntityId CreateSettlementId(
            int worldSeed,
            WorldEntityId regionId,
            string sourceId,
            int ordinal,
            Vector2 centerXZ,
            Vector2 extentsMeters,
            float yawDegrees,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.Settlement,
                worldSeed,
                WorldEntityKind.Region,
                regionId,
                sourceId,
                ordinal,
                OrientedBox2(centerXZ, extentsMeters, yawDegrees),
                collisionRegistry);
        }

        public static WorldEntityId CreateRoadId(
            int worldSeed,
            WorldEntityKind parentKind,
            WorldEntityId parentId,
            string sourceId,
            int ordinal,
            IReadOnlyList<Vector2> polylineXZ,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.Road,
                worldSeed,
                parentKind,
                parentId,
                sourceId,
                ordinal,
                Road(polylineXZ),
                collisionRegistry);
        }

        public static WorldEntityId CreateDistrictId(
            int worldSeed,
            WorldEntityId settlementId,
            string sourceId,
            int ordinal,
            IReadOnlyList<Vector2> polygonXZ,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.District,
                worldSeed,
                WorldEntityKind.Settlement,
                settlementId,
                sourceId,
                ordinal,
                Polygon(polygonXZ),
                collisionRegistry);
        }

        public static WorldEntityId CreateLotId(
            int worldSeed,
            WorldEntityId districtId,
            string sourceId,
            int ordinal,
            IReadOnlyList<Vector2> polygonXZ,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.Lot,
                worldSeed,
                WorldEntityKind.District,
                districtId,
                sourceId,
                ordinal,
                Polygon(polygonXZ),
                collisionRegistry);
        }

        public static WorldEntityId CreateBuildingId(
            int worldSeed,
            WorldEntityKind parentKind,
            WorldEntityId parentId,
            string sourceId,
            int ordinal,
            Vector2 positionXZ,
            Vector2 extentsMeters,
            float yawDegrees,
            Vector3 scale,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.Building,
                worldSeed,
                parentKind,
                parentId,
                sourceId,
                ordinal,
                BuildingFootprint(positionXZ, extentsMeters, yawDegrees, scale),
                collisionRegistry);
        }

        public static WorldEntityId CreatePoiId(
            int worldSeed,
            WorldEntityKind parentKind,
            WorldEntityId parentId,
            string sourceId,
            int ordinal,
            Vector2 positionXZ,
            float yawDegrees,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.Poi,
                worldSeed,
                parentKind,
                parentId,
                sourceId,
                ordinal,
                Point2(positionXZ, yawDegrees),
                collisionRegistry);
        }

        public static WorldEntityId CreateTerrainModificationId(
            int worldSeed,
            WorldEntityKind parentKind,
            WorldEntityId parentId,
            string sourceId,
            int ordinal,
            IReadOnlyList<Vector2> polygonXZ,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.TerrainModification,
                worldSeed,
                parentKind,
                parentId,
                sourceId,
                ordinal,
                Polygon(polygonXZ),
                collisionRegistry);
        }

        public static WorldEntityId CreateEventId(
            int worldSeed,
            WorldEntityKind parentKind,
            WorldEntityId parentId,
            string sourceId,
            int ordinal,
            Vector2 positionXZ,
            IDictionary<WorldEntityId, string> collisionRegistry = null)
        {
            return CreateId(
                WorldEntityKind.Event,
                worldSeed,
                parentKind,
                parentId,
                sourceId,
                ordinal,
                Point2(positionXZ, 0f),
                collisionRegistry);
        }

        public static WorldEntityId CreateFromHexString(WorldEntityKind kind, string hex) =>
            new(kind, NormalizeHex(hex));

        public static WorldEntityId CreateFromHexString(string hex) =>
            CreateFromHexString(WorldEntityKind.None, hex);

        public static bool TryCreateFromHexString(WorldEntityKind kind, string hex, out WorldEntityId id)
        {
            if (!TryNormalizeHex(hex, out var normalized))
            {
                id = EmptyId();
                return false;
            }

            id = new WorldEntityId(kind, normalized);
            return true;
        }

        private static WorldEntityId CreateId(
            WorldEntityKind kind,
            int worldSeed,
            WorldEntityKind parentKind,
            WorldEntityId parentId,
            string sourceId,
            int ordinal,
            GeometryPayload geometry,
            IDictionary<WorldEntityId, string> collisionRegistry)
        {
            var request = new StableIdRequest(kind, worldSeed, parentKind, parentId, sourceId, ordinal, geometry);
            var lane0 = HashLane(request, 0);
            var lane1 = HashLane(request, 1);
            var id = new WorldEntityId(kind, lane0.ToString("x16") + lane1.ToString("x16"));
            RejectCollision(id, request, collisionRegistry);
            return id;
        }

        private static ulong HashLane(StableIdRequest request, int laneMarker)
        {
            var hasher = new StableHash64(FnVOffsetSeed);
            AppendRequest(ref hasher, request);
            hasher.Append(laneMarker);
            return hasher.Finalize();
        }

        private static void AppendRequest(ref StableHash64 hasher, StableIdRequest request)
        {
            AppendLengthPrefixed(ref hasher, DomainLiteral);
            hasher.Append(IdAlgorithmVersion);
            hasher.Append(request.WorldSeed);
            hasher.Append((int)request.Kind);
            hasher.Append((int)request.ParentKind);
            AppendLengthPrefixed(ref hasher, request.ParentId.value);
            AppendLengthPrefixed(ref hasher, request.SourceId);
            hasher.Append(request.Ordinal);
            AppendGeometry(ref hasher, request.Geometry);
        }

        private static void AppendGeometry(ref StableHash64 hasher, GeometryPayload geometry)
        {
            hasher.Append((int)geometry.Kind);
            AppendPoints(ref hasher, geometry.Points);
            AppendPoint(ref hasher, geometry.Center);
            AppendPoint(ref hasher, geometry.Extents);
            hasher.Append(geometry.YawCentidegrees);
            AppendScale(ref hasher, geometry.Scale);
        }

        private static void AppendPoints(ref StableHash64 hasher, IReadOnlyList<QuantizedPoint2> points)
        {
            var count = points != null ? points.Count : 0;
            hasher.Append(count);
            for (var i = 0; i < count; i++)
                AppendPoint(ref hasher, points[i]);
        }

        private static void AppendPoint(ref StableHash64 hasher, QuantizedPoint2 point)
        {
            hasher.Append(unchecked((ulong)point.X));
            hasher.Append(unchecked((ulong)point.Z));
        }

        private static void AppendScale(ref StableHash64 hasher, QuantizedScale3 scale)
        {
            hasher.Append(unchecked((ulong)scale.X));
            hasher.Append(unchecked((ulong)scale.Y));
            hasher.Append(unchecked((ulong)scale.Z));
        }

        private static void AppendLengthPrefixed(ref StableHash64 hasher, string value)
        {
            value = value ?? string.Empty;
            hasher.Append(Encoding.UTF8.GetByteCount(value));
            hasher.Append(value);
        }

        private static void RejectCollision(
            WorldEntityId id,
            StableIdRequest request,
            IDictionary<WorldEntityId, string> collisionRegistry)
        {
            if (collisionRegistry == null)
                return;

            var signature = BuildCollisionSignature(request);
            if (collisionRegistry.TryGetValue(id, out var existing) && existing != signature)
                throw new InvalidOperationException($"World entity id collision detected for {id}.");

            collisionRegistry[id] = signature;
        }

        private static WorldEntityId EmptyId() =>
            new(WorldEntityKind.None, string.Empty);

        private static string NormalizeHex(string hex)
        {
            if (!TryNormalizeHex(hex, out var normalized))
                throw new ArgumentException("World entity ids must be 32 lowercase or uppercase hex characters.", nameof(hex));

            return normalized;
        }

        private static bool TryNormalizeHex(string hex, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(hex) || hex.Length != 32)
                return false;

            for (var i = 0; i < hex.Length; i++)
            {
                var c = hex[i];
                if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                    continue;

                return false;
            }

            normalized = hex.ToLowerInvariant();
            return true;
        }

        private static string BuildCollisionSignature(StableIdRequest request)
        {
            var builder = new StringBuilder(192);
            builder.Append(DomainLiteral).Append('|');
            builder.Append(IdAlgorithmVersion).Append('|');
            builder.Append(request.WorldSeed).Append('|');
            builder.Append((int)request.Kind).Append('|');
            builder.Append((int)request.ParentKind).Append('|');
            builder.Append(request.ParentId).Append('|');
            builder.Append(request.SourceId ?? string.Empty).Append('|');
            builder.Append(request.Ordinal).Append('|');
            AppendGeometrySignature(builder, request.Geometry);
            return builder.ToString();
        }

        private readonly struct StableIdRequest
        {
            public readonly WorldEntityKind Kind;
            public readonly int WorldSeed;
            public readonly WorldEntityKind ParentKind;
            public readonly WorldEntityId ParentId;
            public readonly string SourceId;
            public readonly int Ordinal;
            public readonly GeometryPayload Geometry;

            public StableIdRequest(
                WorldEntityKind kind,
                int worldSeed,
                WorldEntityKind parentKind,
                WorldEntityId parentId,
                string sourceId,
                int ordinal,
                GeometryPayload geometry)
            {
                Kind = kind;
                WorldSeed = worldSeed;
                ParentKind = parentKind;
                ParentId = parentId;
                SourceId = sourceId ?? string.Empty;
                Ordinal = ordinal;
                Geometry = geometry;
            }
        }
    }
}
