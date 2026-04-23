using System;
using UnityEngine;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    /// Represents one controllable DNA value in the character creator profile.
    /// </summary>
    [Serializable]
    public sealed class CharacterDnaEntry
    {
        public string dnaName = string.Empty;

        [Range(0f, 1f)]
        public float dnaValue = 0.5f;

        public CharacterDnaEntry()
        {
        }

        public CharacterDnaEntry(string name, float value)
        {
            dnaName = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            dnaValue = Mathf.Clamp01(value);
        }

        public void Sanitize()
        {
            dnaName = string.IsNullOrWhiteSpace(dnaName) ? string.Empty : dnaName.Trim();
            dnaValue = Mathf.Clamp01(dnaValue);
        }
    }
}
