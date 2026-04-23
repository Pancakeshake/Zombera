using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    /// Serializable profile payload for explicit appearance customization.
    /// </summary>
    [Serializable]
    public sealed class CharacterAppearanceProfile
    {
        public string raceName = "HumanFemale";
        public List<CharacterDnaEntry> bodyValues = new List<CharacterDnaEntry>();
        public CharacterWardrobeSelection wardrobeSelection = new CharacterWardrobeSelection();
        public Color skinColor = new Color(0.96f, 0.82f, 0.82f, 1f);
        public Color hairColor = new Color(0.35f, 0.22f, 0.14f, 1f);
        public Color eyeColor = new Color(0.28f, 0.40f, 1f, 1f);

        public static CharacterAppearanceProfile CreateDefault()
        {
            CharacterAppearanceProfile profile = new CharacterAppearanceProfile();
            profile.Sanitize();
            return profile;
        }

        public static string Serialize(CharacterAppearanceProfile profile, bool prettyPrint = false)
        {
            CharacterAppearanceProfile safeProfile = profile ?? CreateDefault();
            safeProfile.Sanitize();
            return JsonUtility.ToJson(safeProfile, prettyPrint);
        }

        public static CharacterAppearanceProfile Deserialize(string profileJson)
        {
            if (string.IsNullOrWhiteSpace(profileJson))
            {
                return CreateDefault();
            }

            CharacterAppearanceProfile profile;

            try
            {
                profile = JsonUtility.FromJson<CharacterAppearanceProfile>(profileJson);
            }
            catch
            {
                return CreateDefault();
            }

            if (profile == null)
            {
                return CreateDefault();
            }

            profile.Sanitize();
            return profile;
        }

        public void Sanitize()
        {
            raceName = string.IsNullOrWhiteSpace(raceName)
                ? "HumanFemale"
                : raceName.Trim();

            if (bodyValues == null)
            {
                bodyValues = new List<CharacterDnaEntry>();
            }

            List<CharacterDnaEntry> sanitizedEntries = new List<CharacterDnaEntry>(bodyValues.Count);
            HashSet<string> seenDnaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < bodyValues.Count; index++)
            {
                CharacterDnaEntry entry = bodyValues[index];
                if (entry == null)
                {
                    continue;
                }

                entry.Sanitize();
                if (string.IsNullOrEmpty(entry.dnaName))
                {
                    continue;
                }

                if (!seenDnaNames.Add(entry.dnaName))
                {
                    continue;
                }

                sanitizedEntries.Add(entry);
            }

            bodyValues = sanitizedEntries;

            if (wardrobeSelection == null)
            {
                wardrobeSelection = new CharacterWardrobeSelection();
            }

            wardrobeSelection.Sanitize();
            skinColor.a = 1f;
            hairColor.a = 1f;
            eyeColor.a = 1f;
        }
    }
}
