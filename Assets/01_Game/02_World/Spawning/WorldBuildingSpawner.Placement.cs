#region

using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

#endregion

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Zombera.World.Spawning
{
    public sealed partial class WorldBuildingSpawner
    {
        private void SpawnAlongLine(
            SplineSampleLine line,
            Terrain terrain,
            Random rng,
            Transform parent,
            List<(Vector3 pos, float radius)> placed,
            CollapseBatchTracker collapseBatch)
        {
            var points = line.Points;
            if (points.Length < 2) return;

            var totalLength = line.TotalLength;
            if (totalLength < lotSpacingMeters) return;

            var spacing = Mathf.Max(2f, lotSpacingMeters);
            var t = spacing * 0.5f;

            while (t < totalLength)
            {
                var pos = line.SampleAtDistance(t);
                var tangent = line.TangentAtDistance(t);
                t += spacing;

                var right = Vector3.Cross(Vector3.up, tangent).normalized;

                var sides = spawnBothSides ? 2 : 1;
                for (var side = 0; side < sides; side++)
                {
                    var sideDir = side == 0 ? right : -right;
                    TrySpawnBuilding(pos, sideDir, terrain, rng, parent, placed, collapseBatch);
                }
            }
        }

        private void TrySpawnBuilding(
            Vector3 roadPos,
            Vector3 sideDir,
            Terrain terrain,
            Random rng,
            Transform parent,
            List<(Vector3 pos, float radius)> placed,
            CollapseBatchTracker collapseBatch)
        {
            if (!TryPickEntry(rng, out var entry)) return;

            var position = ComputeSpawnPosition(roadPos, sideDir, terrain, rng);
            if (IsOverlapping(position, entry.footprintRadiusMeters, placed)) return;

            var rotation = ComputeBuildingRotation(sideDir, entry.yawOffsetDegrees, rng);

            collapseBatch?.IncrementPending();
            _spawnQueue.Enqueue(new PendingSpawn(entry.prefab, position, rotation, parent, entry, collapseBatch));
            placed.Add((position, entry.footprintRadiusMeters));
        }

        private Vector3 ComputeSpawnPosition(Vector3 roadPos, Vector3 sideDir, Terrain terrain, Random rng)
        {
            var jitterX = (float)(rng.NextDouble() * 2.0 - 1.0) * positionJitterMeters;
            var jitterZ = (float)(rng.NextDouble() * 2.0 - 1.0) * positionJitterMeters;
            var candidateXZ = roadPos + sideDir * roadSetbackMeters +
                              new Vector3(jitterX, 0f, jitterZ);
            var terrainY = terrain.SampleHeight(candidateXZ);
            return new Vector3(candidateXZ.x, terrainY, candidateXZ.z);
        }

        private Quaternion ComputeBuildingRotation(Vector3 sideDir, float yawOffsetDegrees, Random rng)
        {
            var baseYaw = Mathf.Atan2(sideDir.x, sideDir.z) * Mathf.Rad2Deg;
            var yawJitter = (float)(rng.NextDouble() * 2.0 - 1.0) * yawJitterDegrees;
            return Quaternion.Euler(0f, baseYaw + yawOffsetDegrees + yawJitter, 0f);
        }

        private static bool IsOverlapping(Vector3 position, float radius, List<(Vector3 pos, float radius)> placed)
        {
            for (var i = 0; i < placed.Count; i++)
            {
                var minDist = placed[i].radius + radius;
                if (Vector3.SqrMagnitude(position - placed[i].pos) < minDist * minDist) return true;
            }

            return false;
        }

        private bool TryPickEntry(Random rng, out BuildingSpawnEntry result)
        {
            result = null;
            if (entries == null || entries.Count == 0) return false;

            var totalWeight = 0f;
            foreach (var e in entries)
            {
                if (e?.prefab == null || e.weight <= 0f) continue;
                totalWeight += e.weight;
            }

            if (totalWeight <= 0f) return false;

            var roll = rng.NextDouble() * totalWeight;
            var cumulative = 0.0;
            BuildingSpawnEntry fallback = null;

            foreach (var e in entries)
            {
                if (e?.prefab == null || e.weight <= 0f) continue;
                fallback = e;
                cumulative += e.weight;
                if (roll <= cumulative)
                {
                    result = e;
                    return true;
                }
            }

            result = fallback;
            return result != null;
        }
    }
}
