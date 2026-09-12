using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Authored district label for city prefab hub blocks (maps to <see cref="Roads.RoadCityZone"/> for building catalogs).
    /// </summary>
    public enum CityDistrictType
    {
        Residential = 0,
        Commercial = 1,
        Industrial = 2,
        Hospital = 3,
        Military = 4,
        CityCore = 5,
        Park = 6,
        Mixed = 7
    }

    /// <summary>
    ///     Weighted district entry for a city's block composition. Weights are
    ///     normalized across the city; 0 disables a district (e.g. no CityCore or
    ///     Hospital in a small town). 'Mixed' is the implicit remainder.
    /// </summary>
    [System.Serializable]
    public struct CityDistrictWeight
    {
        public CityDistrictType district;

        [Range(0f, 100f)]
        [Tooltip("Relative weight across the city's blocks. 0 = disabled.")]
        public float weight;
    }
}
