using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Selects wilderness POI placements and publishes stable records.</summary>
    public sealed class WorldPoiPlanner
    {
        public IReadOnlyList<WorldPoiRecord> Plan(
            WorldMapSession session,
            WorldGenerationProfile profile,
            IWorldTerrainQuery terrainQuery,
            DeterministicRng rng,
            WorldSitePlan citySites = null)
        {
            var results = new List<WorldPoiRecord>();
            if (profile?.Pois?.Entries == null || terrainQuery == null) return results;

            var localRng = rng?.CreateStream(unchecked((int)0x504F4901)) ??
                           new DeterministicRng(session.Seed ^ unchecked((int)0x504F4901));
            var entries = profile.Pois.Entries;

            for (var e = 0; e < entries.Count; e++)
            {
                var entry = entries[e];
                if (entry?.Prefab == null) continue;

                var max = Mathf.Max(0, entry.MaxInstancesPerMap);
                for (var n = 0; n < max; n++)
                {
                    var x = Mathf.Lerp(session.WorldBoundsXZ.xMin, session.WorldBoundsXZ.xMax, localRng.NextFloat01());
                    var z = Mathf.Lerp(session.WorldBoundsXZ.yMin, session.WorldBoundsXZ.yMax, localRng.NextFloat01());
                    var pos = new Vector2(x, z);
                    if (!terrainQuery.TrySample(pos, out var sample)) continue;
                    if (sample.Buildability < 0.35f || sample.Water.DepthMeters > 0.05f) continue;
                    if (sample.SlopeDegrees > entry.MaxSlopeDegrees) continue;
                    if (CityExclusionBounds.ContainsAny(citySites, pos)) continue;

                    var hasher = new StableHash64((ulong)session.Seed);
                    hasher.Append(entry.StableId);
                    hasher.Append(n);
                    hasher.Append(pos.x);
                    hasher.Append(pos.y);

                    results.Add(new WorldPoiRecord
                    {
                        StableId = hasher.Finalize(),
                        EntryId = entry.StableId,
                        PositionXZ = pos,
                        YawDegrees = localRng.NextFloat01() * 360f,
                        FootprintMeters = entry.FootprintMeters,
                        MapMarkerId = entry.MapMarkerId
                    });
                }
            }

            return results;
        }
    }
}
