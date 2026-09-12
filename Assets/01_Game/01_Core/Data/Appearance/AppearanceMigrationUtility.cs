#region

using System;
using UnityEngine;
using Zombera.Core;

#endregion

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Utility for migrating legacy UMA appearance data to modern character appearance profiles.
    /// </summary>
    public static class AppearanceMigrationUtility
    {
        /// <summary>
        ///     Migrates legacy UMA recipe name to a modern appearance profile JSON.
        /// </summary>
        public static string MigrateLegacyUmaToProfile(string recipeName)
        {
            if (string.IsNullOrWhiteSpace(recipeName)) return string.Empty;

            var profile = CharacterAppearanceProfile.CreateDefault();

            // Simple mapping of recipe names to modern races
            if (recipeName.IndexOf("Female", StringComparison.OrdinalIgnoreCase) >= 0)
                profile.raceName = "HumanFemale";
            else if (recipeName.IndexOf("Male", StringComparison.OrdinalIgnoreCase) >= 0)
                profile.raceName = "HumanMale";

            // Add default height DNA to ensure rig scale is initialized
            if (!profile.bodyValues.Exists(e => string.Equals(e.dnaName, "height", StringComparison.OrdinalIgnoreCase)))
                profile.bodyValues.Add(new CharacterDnaEntry("height", 0.5f));

            return CharacterAppearanceProfile.Serialize(profile);
        }

        /// <summary>
        ///     Checks if a profile needs migration and applies it if necessary.
        /// </summary>
        public static bool TryMigrate(string legacyRecipe, string currentProfileJson, out string migratedProfileJson)
        {
            migratedProfileJson = currentProfileJson;

            // If we have a modern profile, no migration needed
            if (!string.IsNullOrWhiteSpace(currentProfileJson)) return false;

            // If we have a legacy recipe but no profile, migrate
            if (!string.IsNullOrWhiteSpace(legacyRecipe))
            {
                migratedProfileJson = MigrateLegacyUmaToProfile(legacyRecipe);
                return true;
            }

            return false;
        }
    }
}
