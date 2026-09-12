using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{

    /// <summary>
    ///     One city site in a region build. Layout style parameters (spacing, ring
    ///     radius, corner arcs, etc.) come from the hub's CityMathRoadLayout template;
    ///     per-site fields here only override position, seed, extents, feature
    ///     toggles, highway participation and the district flavour.
    /// </summary>
    [Serializable]
    public sealed class CityHubSite
    {
        public string displayName = "New City";

        [Tooltip("Size class — picking a type applies its footprint, exit-count and district-mix preset.")]
        public CitySiteType siteType = CitySiteType.Town;

        public Vector2 centerXZ;

        [Tooltip("Weighted district composition of this city's blocks. Weight 0 disables a " +
                 "district (e.g. no CityCore or Hospital in a small town). 'Mixed' fills the remainder.")]
        public List<CityDistrictWeight> districtMix = new();

        [Tooltip("0 = derived deterministically from the region seed + site index.")]
        [Min(0)] public int layoutSeed;

        [Min(50f)] public float halfWidthMeters = 280f;
        [Min(50f)] public float halfDepthMeters = 240f;

        public bool generateStreetGrid = true;
        public bool generateArterialRing = true;
        public bool generateHighwayExits = true;

        [Min(0)]
        [Tooltip("Highway exits guaranteed on this site (0 = legacy probabilistic). Appended sites' highways connect to the nearest existing highway.")]
        public int guaranteedHighwayExitCount = 2;

        [Tooltip("When false this site is excluded from the inter-city highway network.")]
        public bool connectToRegionHighways = true;
    }
}
