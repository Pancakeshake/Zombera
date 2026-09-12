#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Stores direct wardrobe recipe picks that are part of appearance customization.
    /// </summary>
    [Serializable]
    public sealed class CharacterWardrobeSelection
    {
        [Serializable]
        public struct WardrobeEntry
        {
            public string slotName;
            public string recipeName;

            public WardrobeEntry(string slot, string recipe)
            {
                slotName = slot;
                recipeName = recipe;
            }
        }

        public List<WardrobeEntry> entries = new();

        public void Sanitize()
        {
            entries ??= new List<WardrobeEntry>();

            var sanitizedEntries = new List<WardrobeEntry>(entries.Count);
            var seenSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var slot = NormalizeString(entry.slotName);
                var recipe = NormalizeString(entry.recipeName);

                if (string.IsNullOrEmpty(slot)) continue;
                if (!seenSlots.Add(slot)) continue;

                sanitizedEntries.Add(new WardrobeEntry(slot, recipe));
            }

            entries = sanitizedEntries;
        }

        private static string NormalizeString(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        public string GetRecipe(string slotName)
        {
            return entries.FirstOrDefault(e => string.Equals(e.slotName, slotName, StringComparison.OrdinalIgnoreCase)).recipeName ?? string.Empty;
        }

        public void SetRecipe(string slotName, string recipeName)
        {
            var index = entries.FindIndex(e => string.Equals(e.slotName, slotName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                entries[index] = new WardrobeEntry(slotName, recipeName);
            }
            else
            {
                entries.Add(new WardrobeEntry(slotName, recipeName));
            }
        }
    }
}