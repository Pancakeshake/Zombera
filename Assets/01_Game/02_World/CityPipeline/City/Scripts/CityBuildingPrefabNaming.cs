using System;

namespace Zombera.World.City
{
    /// <summary>
    ///     Parses district labels from modular assembled building / proxy prefab names.
    /// </summary>
    public static class CityBuildingPrefabNaming
    {
        public static bool TryParseDistrictFromPrefabName(string prefabName, out CityDistrictType district)
        {
            district = CityDistrictType.Mixed;
            if (string.IsNullOrWhiteSpace(prefabName))
                return false;

            var values = (CityDistrictType[])Enum.GetValues(typeof(CityDistrictType));
            for (var i = 0; i < values.Length; i++)
            {
                var candidate = values[i];
                if (candidate is CityDistrictType.Mixed or CityDistrictType.Park)
                    continue;

                if (!prefabName.StartsWith(candidate + "_", StringComparison.OrdinalIgnoreCase))
                    continue;

                district = candidate;
                return true;
            }

            if (prefabName.StartsWith("Building_", StringComparison.OrdinalIgnoreCase))
            {
                district = CityDistrictType.Mixed;
                return true;
            }

            return false;
        }

        public static bool TryParseSourceNameFromProxyPrefabName(string proxyPrefabName, out string sourceName)
        {
            sourceName = string.Empty;
            if (string.IsNullOrWhiteSpace(proxyPrefabName))
                return false;

            if (!proxyPrefabName.EndsWith("_Proxy", StringComparison.OrdinalIgnoreCase))
                return false;

            var withoutSuffix = proxyPrefabName[..^"_Proxy".Length];
            var separator = withoutSuffix.LastIndexOf('_');
            if (separator <= 0)
            {
                sourceName = withoutSuffix;
                return true;
            }

            var tail = withoutSuffix[(separator + 1)..];
            if (tail.Length == 8 && IsHexToken(tail))
            {
                sourceName = withoutSuffix[..separator];
                return !string.IsNullOrWhiteSpace(sourceName);
            }

            sourceName = withoutSuffix;
            return true;
        }

        private static bool IsHexToken(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var isHex = c is >= '0' and <= '9'
                          or >= 'a' and <= 'f'
                          or >= 'A' and <= 'F';
                if (!isHex)
                    return false;
            }

            return true;
        }
    }
}
