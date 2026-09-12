using UnityEngine;
using Zombera.Core;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Finite map size tiers: tile counts, settlement hierarchy, and world bounds.</summary>
    [CreateAssetMenu(
        fileName = "WorldMapSizeSettings",
        menuName = "Zombera/World/World Map Size Settings")]
    public sealed class WorldMapSizeSettings : ScriptableObject
    {
        public const float TileSizeMeters = 1000f;

        [SerializeField] private int smallTilesPerSide = 4;
        [SerializeField] private int mediumTilesPerSide = 8;
        [SerializeField] private int largeTilesPerSide = 10;

        [Header("Ocean Ring (bathymetry shelf)")]
        [Tooltip("Extra tiles beyond the core playable square on each side. Large default 1 → 12×12 full grid.")]
        [SerializeField] private int smallOceanRingTiles;
        [SerializeField] private int mediumOceanRingTiles;
        [SerializeField] private int largeOceanRingTiles = 1;

        [Header("Small Hierarchy")]
        [SerializeField] private int smallMetropolisCount;
        [SerializeField] private int smallCityCount = 1;
        [SerializeField] private int smallSettlementMinCount = 2;
        [SerializeField] private int smallSettlementMaxCount = 2;

        [Header("Medium Hierarchy")]
        [SerializeField] private int mediumMetropolisCount = 1;
        [SerializeField] private int mediumCityCount = 3;
        [SerializeField] private int mediumSettlementMinCount = 6;
        [SerializeField] private int mediumSettlementMaxCount = 6;

        [Header("Large Hierarchy")]
        [Tooltip("Guaranteed metropolises on Large maps.")]
        [SerializeField] private int largeMetropolisCount = 2;
        [Tooltip("Guaranteed cities on Large maps.")]
        [SerializeField] private int largeCityCount = 5;
        [Tooltip("Minimum villages / small towns / towns on Large maps.")]
        [SerializeField] private int largeSettlementMinCount = 13;
        [Tooltip("Maximum villages / small towns / towns on Large maps.")]
        [SerializeField] private int largeSettlementMaxCount = 18;

        [SerializeField] private int initialPlayAreaRadiusTiles = 1;

        [SerializeField] private float largestCityFootprintRadiusMeters = 960f;
        [SerializeField] private float roadExitClearanceMeters = 100f;

        public int SmallTilesPerSide => smallTilesPerSide;
        public int MediumTilesPerSide => mediumTilesPerSide;
        public int LargeTilesPerSide => largeTilesPerSide;

        public int SmallOceanRingTiles => smallOceanRingTiles;
        public int MediumOceanRingTiles => mediumOceanRingTiles;
        public int LargeOceanRingTiles => largeOceanRingTiles;

        public int SmallMetropolisCount => smallMetropolisCount;
        public int SmallCityCount => smallCityCount;
        public int SmallSettlementMinCount => smallSettlementMinCount;
        public int SmallSettlementMaxCount => smallSettlementMaxCount;

        public int MediumMetropolisCount => mediumMetropolisCount;
        public int MediumCityCount => mediumCityCount;
        public int MediumSettlementMinCount => mediumSettlementMinCount;
        public int MediumSettlementMaxCount => mediumSettlementMaxCount;

        public int LargeMetropolisCount => largeMetropolisCount;
        public int LargeCityCount => largeCityCount;
        public int LargeSettlementMinCount => largeSettlementMinCount;
        public int LargeSettlementMaxCount => largeSettlementMaxCount;

        /// <summary>Legacy total used by fingerprints / UI; mirrors max hierarchy total.</summary>
        public int SmallCityTarget => ResolveQuota(WorldMapSizeTier.Small, seed: 0, useMaxSettlements: true).Total;

        /// <summary>Legacy total used by fingerprints / UI; mirrors max hierarchy total.</summary>
        public int MediumCityTarget => ResolveQuota(WorldMapSizeTier.Medium, seed: 0, useMaxSettlements: true).Total;

        /// <summary>Legacy total used by fingerprints / UI; mirrors max hierarchy total.</summary>
        public int LargeCityTarget => ResolveQuota(WorldMapSizeTier.Large, seed: 0, useMaxSettlements: true).Total;

        public int InitialPlayAreaRadiusTiles => initialPlayAreaRadiusTiles;

        /// <summary>Minimum clearance from map edge for city centers.</summary>
        public float EdgeClearanceMeters =>
            Mathf.Max(500f, LargestCityFootprintRadiusMeters + roadExitClearanceMeters);

        /// <summary>Extra meters beyond footprint when spacing sites / clearing highway exits.</summary>
        public float RoadExitClearanceMeters => Mathf.Max(0f, roadExitClearanceMeters);

        /// <summary>Footprint radius used when constraining city centers away from map edges.</summary>
        public float LargestCityFootprintRadiusMeters =>
            Mathf.Max(
                largestCityFootprintRadiusMeters,
                WorldSettlementHierarchy.FootprintRadiusMeters(CitySiteType.Metropolis));

        /// <summary>Minimum distance a city center must keep from the map edge.</summary>
        public float MinimumCenterEdgeInsetMeters =>
            LargestCityFootprintRadiusMeters + RoadExitClearanceMeters;

        /// <summary>Core playable tiles per side (excludes ocean ring).</summary>
        public int GetCoreTilesPerSide(WorldMapSizeTier tier)
        {
            return tier switch
            {
                WorldMapSizeTier.Small => Mathf.Max(1, smallTilesPerSide),
                WorldMapSizeTier.Medium => Mathf.Max(1, mediumTilesPerSide),
                WorldMapSizeTier.Large => Mathf.Max(1, largeTilesPerSide),
                _ => Mathf.Max(1, mediumTilesPerSide)
            };
        }

        public int GetOceanRingTiles(WorldMapSizeTier tier)
        {
            return tier switch
            {
                WorldMapSizeTier.Small => Mathf.Max(0, smallOceanRingTiles),
                WorldMapSizeTier.Medium => Mathf.Max(0, mediumOceanRingTiles),
                WorldMapSizeTier.Large => Mathf.Max(0, largeOceanRingTiles),
                _ => Mathf.Max(0, mediumOceanRingTiles)
            };
        }

        /// <summary>Full terrain grid tiles per side (core + 2× ocean ring).</summary>
        public int GetTilesPerSide(WorldMapSizeTier tier)
        {
            var core = GetCoreTilesPerSide(tier);
            var ring = GetOceanRingTiles(tier);
            return core + ring * 2;
        }

        public Vector2 GetWorldOriginXZ(WorldMapSizeTier tier, Vector2 coreOriginXZ = default)
        {
            var ring = GetOceanRingTiles(tier);
            return new Vector2(
                coreOriginXZ.x - ring * TileSizeMeters,
                coreOriginXZ.y - ring * TileSizeMeters);
        }

        public Rect GetCoreWorldBoundsXZ(WorldMapSizeTier tier, Vector2 coreOriginXZ = default)
        {
            var coreTiles = GetCoreTilesPerSide(tier);
            var size = coreTiles * TileSizeMeters;
            return new Rect(coreOriginXZ.x, coreOriginXZ.y, size, size);
        }

        public WorldMapSession CreateSession(
            WorldMapSizeTier tier,
            int seed,
            int profileVersion,
            Vector2 coreOriginXZ = default)
        {
            return WorldMapSession.CreateWithOceanRing(
                tier,
                seed,
                profileVersion,
                coreOriginXZ,
                GetCoreTilesPerSide(tier),
                GetOceanRingTiles(tier),
                TileSizeMeters);
        }

        /// <summary>
        ///     Total city/settlement sites for the tier. Settlement count is rolled
        ///     inside the configured min/max range from <paramref name="seed"/>.
        /// </summary>
        public int GetCitySiteTarget(WorldMapSizeTier tier, int seed = 0)
        {
            return ResolveQuota(tier, seed).Total;
        }

        public WorldSettlementHierarchyQuota ResolveQuota(
            WorldMapSizeTier tier,
            int seed,
            bool useMaxSettlements = false)
        {
            GetHierarchyCounts(tier, out var metro, out var city, out var minSettlements, out var maxSettlements);
            maxSettlements = Mathf.Max(minSettlements, maxSettlements);
            var settlements = useMaxSettlements
                ? maxSettlements
                : RollSettlementCount(minSettlements, maxSettlements, seed, tier);
            return new WorldSettlementHierarchyQuota(metro, city, settlements);
        }

        public Rect GetWorldBoundsXZ(Vector2 originXZ, WorldMapSizeTier tier)
        {
            var tiles = GetTilesPerSide(tier);
            var size = tiles * TileSizeMeters;
            return new Rect(originXZ.x, originXZ.y, size, size);
        }

        /// <summary>
        ///     Full-grid origin for <paramref name="tier"/>. Prefer <see cref="CreateSession"/> for new code.
        /// </summary>
        public Rect GetWorldBoundsXZ(WorldMapSizeTier tier, Vector2 coreOriginXZ = default)
        {
            return GetWorldBoundsXZ(GetWorldOriginXZ(tier, coreOriginXZ), tier);
        }

        private void GetHierarchyCounts(
            WorldMapSizeTier tier,
            out int metro,
            out int city,
            out int minSettlements,
            out int maxSettlements)
        {
            switch (tier)
            {
                case WorldMapSizeTier.Small:
                    metro = smallMetropolisCount;
                    city = smallCityCount;
                    minSettlements = smallSettlementMinCount;
                    maxSettlements = smallSettlementMaxCount;
                    break;
                case WorldMapSizeTier.Large:
                    metro = largeMetropolisCount;
                    city = largeCityCount;
                    minSettlements = largeSettlementMinCount;
                    maxSettlements = largeSettlementMaxCount;
                    break;
                default:
                    metro = mediumMetropolisCount;
                    city = mediumCityCount;
                    minSettlements = mediumSettlementMinCount;
                    maxSettlements = mediumSettlementMaxCount;
                    break;
            }
        }

        private static int RollSettlementCount(
            int minInclusive,
            int maxInclusive,
            int seed,
            WorldMapSizeTier tier)
        {
            if (maxInclusive <= minInclusive)
                return Mathf.Max(0, minInclusive);

            var rng = new DeterministicRng(seed ^ 0x51FE0001 ^ ((int)tier * 397));
            var span = maxInclusive - minInclusive + 1;
            return minInclusive + Mathf.FloorToInt(rng.NextFloat01() * span);
        }

        private void OnValidate()
        {
            smallTilesPerSide = Mathf.Max(1, smallTilesPerSide);
            mediumTilesPerSide = Mathf.Max(1, mediumTilesPerSide);
            largeTilesPerSide = Mathf.Max(1, largeTilesPerSide);
            smallOceanRingTiles = Mathf.Max(0, smallOceanRingTiles);
            mediumOceanRingTiles = Mathf.Max(0, mediumOceanRingTiles);
            largeOceanRingTiles = Mathf.Max(0, largeOceanRingTiles);

            ClampHierarchy(ref smallMetropolisCount, ref smallCityCount, ref smallSettlementMinCount, ref smallSettlementMaxCount);
            ClampHierarchy(ref mediumMetropolisCount, ref mediumCityCount, ref mediumSettlementMinCount, ref mediumSettlementMaxCount);
            ClampHierarchy(ref largeMetropolisCount, ref largeCityCount, ref largeSettlementMinCount, ref largeSettlementMaxCount);

            largestCityFootprintRadiusMeters = Mathf.Max(
                100f,
                WorldSettlementHierarchy.FootprintRadiusMeters(CitySiteType.Metropolis));
            roadExitClearanceMeters = Mathf.Max(0f, roadExitClearanceMeters);
            initialPlayAreaRadiusTiles = Mathf.Max(0, initialPlayAreaRadiusTiles);
        }

        private static void ClampHierarchy(
            ref int metro,
            ref int city,
            ref int minSettlements,
            ref int maxSettlements)
        {
            metro = Mathf.Max(0, metro);
            city = Mathf.Max(0, city);
            minSettlements = Mathf.Max(0, minSettlements);
            maxSettlements = Mathf.Max(minSettlements, maxSettlements);
        }
    }
}
