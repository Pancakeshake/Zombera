using System.Collections.Generic;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Session cache of highway height profiles built during terrain flatten for mesh placement.
    /// </summary>
    public static class HighwayRoadHeightProfiles
    {
        private static readonly Dictionary<int, PolylineHeightProfile> Profiles = new(8);

        public static int Count => Profiles.Count;

        public static void Clear() => Profiles.Clear();

        public static void Register(int roadId, PolylineHeightProfile profile)
        {
            if (profile == null || !profile.IsValid)
                return;

            Profiles[roadId] = profile;
        }

        public static bool TryGet(int roadId, out PolylineHeightProfile profile) =>
            Profiles.TryGetValue(roadId, out profile);

        public static bool HasProfile(int roadId) => Profiles.ContainsKey(roadId);
    }

}
