using Zombera.Core;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed class WorldStateLoadPackage
    {
        public WorldState State { get; }
        public string CanonicalHash { get; }
        public int WorldSeed { get; }
        public WorldMapSizeTier Tier { get; }
        public int ProfileVersion { get; }
        public string GraphVersion { get; }
        public string ProfileFingerprint { get; }
        public string PlanFingerprint { get; }

        public WorldStateLoadPackage(
            WorldState state,
            string canonicalHash,
            int worldSeed,
            WorldMapSizeTier tier,
            int profileVersion,
            string graphVersion,
            string profileFingerprint,
            string planFingerprint)
        {
            State = WorldStateCloner.Clone(state);
            CanonicalHash = canonicalHash ?? string.Empty;
            WorldSeed = worldSeed;
            Tier = tier;
            ProfileVersion = profileVersion;
            GraphVersion = graphVersion ?? string.Empty;
            ProfileFingerprint = profileFingerprint ?? string.Empty;
            PlanFingerprint = planFingerprint ?? string.Empty;
        }

        public WorldStateLoadPackage(
            WorldState state,
            string source,
            string profileVersion,
            string buildFingerprint,
            string notes = "")
            : this(
                state,
                buildFingerprint,
                state?.header?.worldSeed ?? 0,
                state?.header?.mapSizeTier ?? default,
                ParseProfileVersion(profileVersion),
                source,
                string.Empty,
                notes)
        {
        }

        public WorldState CreateStateCopy() => WorldStateCloner.Clone(State);

        private static int ParseProfileVersion(string profileVersion) =>
            int.TryParse(profileVersion, out var parsed) ? parsed : 0;
    }
}
