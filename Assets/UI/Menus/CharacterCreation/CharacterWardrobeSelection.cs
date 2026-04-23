using System;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    /// Stores direct wardrobe recipe picks that are part of appearance customization.
    /// </summary>
    [Serializable]
    public sealed class CharacterWardrobeSelection
    {
        public string hairRecipeName = string.Empty;
        public string beardRecipeName = string.Empty;

        public void Sanitize()
        {
            hairRecipeName = NormalizeRecipeName(hairRecipeName);
            beardRecipeName = NormalizeRecipeName(beardRecipeName);
        }

        private static string NormalizeRecipeName(string recipeName)
        {
            return string.IsNullOrWhiteSpace(recipeName)
                ? string.Empty
                : recipeName.Trim();
        }
    }
}
