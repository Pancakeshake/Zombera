using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public enum WorldStateDiffKind
    {
        Added = 0,
        Removed = 1,
        Modified = 2
    }

    public sealed class WorldStateDiffEntry
    {
        public WorldStateDiffKind Kind { get; }
        public string Path { get; }
        public string Before { get; }
        public string After { get; }

        public WorldStateDiffEntry(WorldStateDiffKind kind, string path, string before, string after)
        {
            Kind = kind;
            Path = path ?? string.Empty;
            Before = before ?? string.Empty;
            After = after ?? string.Empty;
        }
    }

    public sealed class WorldStateDiff
    {
        public List<WorldStateDiffEntry> Entries { get; } = new();
        public bool HasChanges => Entries.Count > 0;
    }

    public static class WorldStateDiffer
    {
        public static WorldStateDiff Diff(WorldState before, WorldState after)
        {
            return Diff(WorldStateSnapshot.Capture(before), WorldStateSnapshot.Capture(after));
        }

        public static WorldStateDiff Diff(WorldStateSnapshot before, WorldStateSnapshot after)
        {
            var diff = new WorldStateDiff();
            AddRemovedAndModified(before.Values, after.Values, diff);
            AddAdded(before.Values, after.Values, diff);
            return diff;
        }

        private static void AddRemovedAndModified(
            IReadOnlyDictionary<string, string> before,
            IReadOnlyDictionary<string, string> after,
            WorldStateDiff diff)
        {
            foreach (var pair in before)
            {
                if (!after.TryGetValue(pair.Key, out var afterValue))
                {
                    diff.Entries.Add(new WorldStateDiffEntry(WorldStateDiffKind.Removed, pair.Key, pair.Value, string.Empty));
                    continue;
                }

                if (pair.Value != afterValue)
                    diff.Entries.Add(new WorldStateDiffEntry(WorldStateDiffKind.Modified, pair.Key, pair.Value, afterValue));
            }
        }

        private static void AddAdded(
            IReadOnlyDictionary<string, string> before,
            IReadOnlyDictionary<string, string> after,
            WorldStateDiff diff)
        {
            foreach (var pair in after)
            {
                if (!before.ContainsKey(pair.Key))
                    diff.Entries.Add(new WorldStateDiffEntry(WorldStateDiffKind.Added, pair.Key, string.Empty, pair.Value));
            }
        }
    }
}
