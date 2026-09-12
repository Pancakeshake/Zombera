#if UNITY_EDITOR
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    /// <summary>
    ///     Picks a modular building district label from footprint size and story count.
    ///     Matches <see cref="CityDistrictType"/> used by city named areas.
    ///     Used only as a fallback when no template is active.
    /// </summary>
    internal static class ModularBuildingCategoryResolver
    {
        public static readonly CityDistrictType[] AllCategoryOptions =
        {
            CityDistrictType.Residential,
            CityDistrictType.Commercial,
            CityDistrictType.Industrial,
            CityDistrictType.Hospital,
            CityDistrictType.Military,
            CityDistrictType.CityCore,
            CityDistrictType.Park,
            CityDistrictType.Mixed
        };

        public static CityDistrictType Resolve(int widthCells, int depthCells, int floorCount, GeneratorSettings settings = null)
        {
            if (settings != null && settings.SkyscraperMode && floorCount >= 12)
                return CityDistrictType.CityCore;

            return ResolveAutomatic(widthCells, depthCells, floorCount);
        }

        public static CityDistrictType ResolveAutomatic(int widthCells, int depthCells, int floorCount)
        {
            var width = Mathf.Max(1, widthCells);
            var depth = Mathf.Max(1, depthCells);
            var footprintCells = width * depth;
            var floors = Mathf.Clamp(floorCount, 1, ModularSingleLevelHouseGeneratorTool.MaxFloors);

            if (floors == 1 && width <= 5 && depth <= 5)
                return CityDistrictType.Residential;

            if (QualifiesForCityCore(footprintCells, floors))
                return CityDistrictType.CityCore;

            if (floors >= 4 && footprintCells >= 12)
                return CityDistrictType.Hospital;

            if (footprintCells >= 20 && floors >= 2 && floors <= 4)
                return CityDistrictType.Military;

            if (footprintCells >= 16 && floors <= 3)
                return CityDistrictType.Industrial;

            if (floors >= 3 || (footprintCells >= 10 && floors >= 2))
                return CityDistrictType.Commercial;

            return CityDistrictType.Residential;
        }

        private static bool QualifiesForCityCore(int footprintCells, int floors)
        {
            if (floors >= 6 && footprintCells >= 18)
                return true;

            if (floors >= 5 && footprintCells >= 25)
                return true;

            return footprintCells >= 32 && floors >= 5;
        }

        public static string FormatPrefabBaseName(CityDistrictType category, int uniqueNumber)
        {
            return category + "_" + uniqueNumber;
        }

        /// <summary>
        ///     Builds a template-driven prefab name like 'Commercial_Kitchen_5x4' or
        ///     'Residential_2_Bedroom_6x4'. A leading category word on the display name
        ///     is stripped so the category isn't repeated.
        /// </summary>
        public static string FormatTemplatePrefabBaseName(
            CityDistrictType category, string templateDisplayName, int widthCells, int depthCells)
        {
            var categoryWord = category.ToString();
            var slug = SlugifyTemplateName(templateDisplayName);

            // Display names like "Commercial Kitchen" already carry the category.
            if (slug.Equals(categoryWord, System.StringComparison.OrdinalIgnoreCase))
                slug = string.Empty;
            else if (slug.StartsWith(categoryWord + "_", System.StringComparison.OrdinalIgnoreCase))
                slug = slug[(categoryWord.Length + 1)..];

            var size = $"{widthCells}x{depthCells}";
            return string.IsNullOrEmpty(slug)
                ? $"{categoryWord}_{size}"
                : $"{categoryWord}_{slug}_{size}";
        }

        /// <summary>
        ///     Converts a display name into a file-safe snake_case slug, e.g.
        ///     "2 Bedroom" → "2_Bedroom", "Commercial Shop" → "Commercial_Shop".
        /// </summary>
        private static string SlugifyTemplateName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return string.Empty;

            var sb = new System.Text.StringBuilder(displayName.Length);
            var previousUnderscore = false;
            foreach (var c in displayName)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    previousUnderscore = false;
                }
                else if (!previousUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    previousUnderscore = true;
                }
            }

            return sb.ToString().Trim('_');
        }

        public static bool TryParseCategoryFromPrefabName(string prefabName, out CityDistrictType category)
        {
            return CityBuildingPrefabNaming.TryParseDistrictFromPrefabName(prefabName, out category);
        }
    }
}
#endif
