using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class PoiStateGenerationProjection
    {
        public static List<PoiState> CreatePois(
            WorldMapSession session,
            IReadOnlyList<WorldPoiRecord> records)
        {
            var pois = new List<PoiState>();
            if (records == null)
                return pois;

            var regionId = WorldStateSiteGenerationProjection.CreateWorldRegionId(session);
            var collisionRegistry = new Dictionary<WorldEntityId, string>();
            for (var i = 0; i < records.Count; i++)
            {
                if (TryCreatePoi(session, regionId, records[i], i, collisionRegistry, out var poi))
                    pois.Add(poi);
            }

            return pois;
        }

        private static bool TryCreatePoi(
            WorldMapSession session,
            WorldEntityId regionId,
            WorldPoiRecord record,
            int ordinal,
            IDictionary<WorldEntityId, string> collisionRegistry,
            out PoiState poi)
        {
            poi = null;
            if (record == null)
                return false;

            var sourceId = !string.IsNullOrWhiteSpace(record.EntryId)
                ? record.EntryId + ":" + record.StableId.ToString("x16")
                : "poi:" + record.StableId.ToString("x16");

            poi = new PoiState
            {
                id = WorldStableIdFactory.CreatePoiId(
                    session.Seed,
                    WorldEntityKind.Region,
                    regionId,
                    sourceId,
                    ordinal,
                    record.PositionXZ,
                    record.YawDegrees,
                    collisionRegistry),
                sourceId = sourceId,
                regionId = regionId,
                archetypeId = record.EntryId ?? string.Empty,
                mapMarkerId = record.MapMarkerId ?? string.Empty,
                positionXZ = record.PositionXZ,
                yawDegrees = record.YawDegrees,
                footprintMeters = record.FootprintMeters
            };
            return true;
        }
    }
}
