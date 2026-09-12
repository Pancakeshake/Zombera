namespace Zombera.Core
{
    /// <summary>
    ///     Menu/UI New Game request. Lives in Core so UI can submit without referencing World.City.
    /// </summary>
    public readonly struct WorldSessionRequest
    {
        public WorldSessionRequest(WorldMapSizeTier tier, int seed)
        {
            Tier = tier;
            Seed = seed;
        }

        /// <summary>Map size tier.</summary>
        public WorldMapSizeTier Tier { get; }

        /// <summary>World seed. Zero means GameManager should generate a non-zero seed once.</summary>
        public int Seed { get; }
    }
}
