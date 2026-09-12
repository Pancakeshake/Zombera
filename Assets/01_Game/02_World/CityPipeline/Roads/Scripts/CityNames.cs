using System.Collections.Generic;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Seeded random city names: prefix + suffix combos ("Ashford",
    ///     "Blackwater", ...). Used by the region scatter to give every generated
    ///     city a display name. Deterministic — same seed, same names.
    /// </summary>
    public static class CityNames
    {
        private static readonly string[] Prefixes =
        {
            "Ash", "Black", "Cold", "Copper", "Dead", "Dust", "East", "Elm",
            "Fair", "Fall", "Feral", "Fox", "Ghost", "Gray", "Grim", "Hollow",
            "Iron", "Juniper", "Lone", "Lost", "Marrow", "New", "North", "Oak",
            "Old", "Pale", "Pine", "Quiet", "Red", "Rust", "Salem", "Silver",
            "South", "Still", "Stone", "Thorn", "West", "Willow", "Wolf", "Worn"
        };

        private static readonly string[] Suffixes =
        {
            "bank", "bend", "borough", "brook", "canyon", "creek", "crossing",
            "dale", "fall", "field", "flat", "ford", "gate", "grove", "harbor",
            "haven", "hill", "hollow", "junction", "lake", "landing", "marsh",
            "meadow", "mill", "moor", "outpost", "pass", "peak", "point",
            "prairie", "ridge", "run", "shade", "spring", "station", "stead",
            "valley", "watch", "well", "wood"
        };

        /// <summary>
        ///     Picks an unused city name. When <paramref name="used"/> is supplied,
        ///     returns a name not already in the set and registers it.
        /// </summary>
        public static string Pick(System.Random rng, HashSet<string> used = null)
        {
            const int maxAttempts = 24;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var name = Prefixes[rng.Next(Prefixes.Length)] + Suffixes[rng.Next(Suffixes.Length)];
                if (used == null || used.Add(name))
                    return name;
            }

            return Prefixes[rng.Next(Prefixes.Length)] + " " + rng.Next(10, 99);
        }
    }
}
