#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Tests.Editor.WorldStateTests
{
    public sealed class WorldStateDifferTests
    {
        [Test]
        public void Diff_SingleFieldChange_ReportsModifiedPath()
        {
            var header = CreateHeader(tilesPerSide: 2, tileSizeMeters: 100f);
            var before = CreateFreshState(header);
            var after = WorldStateCloner.Clone(before);
            after.clock.currentHour = 12L;

            var diff = WorldStateDiffer.Diff(before, after);

            Assert.IsTrue(diff.HasChanges);
            var entry = FindEntry(diff, "$.clock.currentHour");
            Assert.NotNull(entry);
            Assert.AreEqual(WorldStateDiffKind.Modified, entry.Kind);
            Assert.AreEqual("0", entry.Before);
            Assert.AreEqual("12", entry.After);
        }

        private static WorldStateDiffEntry FindEntry(WorldStateDiff diff, string path)
        {
            for (var i = 0; i < diff.Entries.Count; i++)
            {
                if (diff.Entries[i].Path == path)
                    return diff.Entries[i];
            }

            return null;
        }

        private static WorldStateHeader CreateHeader(int tilesPerSide, float tileSizeMeters)
        {
            var sideMeters = tilesPerSide * tileSizeMeters;
            return new WorldStateHeader
            {
                worldSeed = 97531,
                tilesPerSide = tilesPerSide,
                tileSizeMeters = tileSizeMeters,
                worldOriginXZ = Vector2.zero,
                worldBoundsXZ = new Rect(0f, 0f, sideMeters, sideMeters)
            };
        }

        private static WorldState CreateFreshState(WorldStateHeader header)
        {
            var managerObject = new GameObject("WorldStateDifferTests");
            var manager = managerObject.AddComponent<WorldStateManager>();
            try
            {
                Assert.IsTrue(manager.TryCreateFresh(header, out _));
                return manager.CaptureCanonicalCopy();
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }
    }
}
#endif
