using System.Collections.Generic;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Size class for a region city site. Controls default footprint extents,
    ///     highway exit count and district mix via <see cref="CitySiteTypePresets"/>.
    ///     The street grid scales automatically from the footprint extents.
    /// </summary>
    public enum CitySiteType
    {
        Village = 0,
        SmallTown = 1,
        Town = 2,
        City = 3,
        Metropolis = 4
    }

    /// <summary>
    ///     Default values per <see cref="CitySiteType"/>. Applied when a site's type
    ///     changes (editor) so footprint size, guaranteed exits and district mix stay
    ///     consistent with the chosen size class.
    /// </summary>
    public static class CitySiteTypePresets
    {
        public static float HalfWidth(CitySiteType type) => type switch
        {
            CitySiteType.Village => 140f,
            CitySiteType.SmallTown => 200f,
            CitySiteType.Town => 280f,
            CitySiteType.City => 760f,
            CitySiteType.Metropolis => 960f,
            _ => 280f
        };

        public static float HalfDepth(CitySiteType type) => type switch
        {
            CitySiteType.Village => 120f,
            CitySiteType.SmallTown => 180f,
            CitySiteType.Town => 240f,
            CitySiteType.City => 660f,
            CitySiteType.Metropolis => 860f,
            _ => 240f
        };

        public static int GuaranteedExitCount(CitySiteType type) => type switch
        {
            CitySiteType.Village => 1,
            CitySiteType.SmallTown => 1,
            CitySiteType.Town => 2,
            CitySiteType.City => 3,
            CitySiteType.Metropolis => 4,
            _ => 2
        };

        /// <summary>
        ///     Default weighted district mix per size class. Includes every district
        ///     (even 0-weight ones) so the editor shows the full set — zero means
        ///     disabled, e.g. no CityCore or Hospital in villages and small towns.
        /// </summary>
        public static List<CityDistrictWeight> BuildDefaultMix(CitySiteType type)
        {
            var w = Weights(type);
            return new List<CityDistrictWeight>
            {
                new() { district = CityDistrictType.Residential, weight = w.residential },
                new() { district = CityDistrictType.Commercial, weight = w.commercial },
                new() { district = CityDistrictType.Industrial, weight = w.industrial },
                new() { district = CityDistrictType.Park, weight = w.park },
                new() { district = CityDistrictType.CityCore, weight = w.cityCore },
                new() { district = CityDistrictType.Hospital, weight = w.hospital },
                new() { district = CityDistrictType.Military, weight = w.military }
            };
        }

        private static (
            float residential, float commercial, float industrial, float park,
            float cityCore, float hospital, float military) Weights(CitySiteType type) => type switch
        {
            CitySiteType.Village => (65f, 5f, 5f, 25f, 0f, 0f, 0f),
            CitySiteType.SmallTown => (62f, 10f, 8f, 13f, 7f, 0f, 0f),
            CitySiteType.Town => (52f, 13f, 12f, 10f, 8f, 5f, 0f),
            CitySiteType.City => (45f, 14f, 15f, 8f, 12f, 5f, 2f),
            CitySiteType.Metropolis => (40f, 15f, 18f, 8f, 14f, 4f, 3f),
            _ => (52f, 13f, 12f, 10f, 8f, 5f, 0f)
        };
    }
}
