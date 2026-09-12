#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#endregion

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Serializable profile payload for explicit appearance customization.
    /// </summary>
    [Serializable]
    public sealed class CharacterAppearanceProfile
    {
        public string raceName = "HumanFemale";
        public List<CharacterDnaEntry> bodyValues = new();
        public CharacterWardrobeSelection wardrobeSelection = new();
        public Color skinColor = new(0.96f, 0.82f, 0.82f, 1f);
        public Color hairColor = new(0.35f, 0.22f, 0.14f, 1f);
        public Color eyeColor = new(0.28f, 0.40f, 1f, 1f);

        public static CharacterAppearanceProfile CreateDefault()
        {
            var profile = new CharacterAppearanceProfile();
            profile.Sanitize();
            return profile;
        }

        public static string Serialize(CharacterAppearanceProfile profile, bool prettyPrint = false)
        {
            var safeProfile = profile ?? CreateDefault();
            safeProfile.Sanitize();
            return JsonUtility.ToJson(safeProfile, prettyPrint);
        }

        public static CharacterAppearanceProfile Deserialize(string profileJson)
        {
            if (string.IsNullOrWhiteSpace(profileJson)) return CreateDefault();

            CharacterAppearanceProfile profile;

            try
            {
                profile = JsonUtility.FromJson<CharacterAppearanceProfile>(profileJson);
            }
            catch
            {
                return CreateDefault();
            }

            if (profile == null) return CreateDefault();

            profile.Sanitize();
            return profile;
        }

        public void Sanitize()
        {
            raceName = string.IsNullOrWhiteSpace(raceName)
                ? "HumanFemale"
                : raceName.Trim();

            bodyValues ??= new List<CharacterDnaEntry>();

            var sanitizedEntries = new List<CharacterDnaEntry>(bodyValues.Count);
            var seenDnaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in bodyValues.Where(entry => entry != null))
            {
                entry.Sanitize();
                if (string.IsNullOrEmpty(entry.dnaName)) continue;

                if (!seenDnaNames.Add(entry.dnaName)) continue;

                sanitizedEntries.Add(entry);
            }

            bodyValues = sanitizedEntries;

            wardrobeSelection ??= new CharacterWardrobeSelection();

            wardrobeSelection.Sanitize();
            skinColor.a = 1f;
            hairColor.a = 1f;
            eyeColor.a = 1f;
        }
    }
}