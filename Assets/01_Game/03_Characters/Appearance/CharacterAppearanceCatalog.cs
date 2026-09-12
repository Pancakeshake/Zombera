#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     ScriptableObject catalog for character appearance options including races, wardrobe, and DNA definitions.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/UI/Character Appearance Catalog", fileName = "CharacterAppearanceCatalog")]
    public sealed class CharacterAppearanceCatalog : ScriptableObject
    {
        public List<CharacterRaceOption> races = new();
        public List<CharacterWardrobeOption> wardrobeOptions = new();
        public List<CharacterDnaControlDefinition> dnaControls = new();

        public CharacterRaceOption FindRace(string raceName)
        {
            return races.Find(r => string.Equals(r.name, raceName, StringComparison.OrdinalIgnoreCase));
        }

        public List<CharacterWardrobeOption> GetWardrobeOptions(string raceName, string slotName)
        {
            return wardrobeOptions.FindAll(o => 
                string.Equals(o.slotName, slotName, StringComparison.OrdinalIgnoreCase) && 
                (string.IsNullOrEmpty(o.raceTag) || string.Equals(o.raceTag, raceName, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Serializable]
    public sealed class CharacterRaceOption
    {
        public string name;
        public string displayName;
        public bool isMale;
    }

    [Serializable]
    public sealed class CharacterWardrobeOption
    {
        public string recipeName;
        public string displayName;
        public string slotName; // e.g., "Hair", "Beard"
        public string raceTag; // Optional: restrict to specific race
    }

    [Serializable]
    public sealed class CharacterDnaControlDefinition
    {
        public string dnaName;
        public string displayName;
        public float defaultValue = 0.5f;
        public Vector2 outputRange = new(0.40f, 0.60f);
        public bool isLocked;
        public Vector2 lockedOutputRange = new(0.45f, 0.55f);
    }
}
