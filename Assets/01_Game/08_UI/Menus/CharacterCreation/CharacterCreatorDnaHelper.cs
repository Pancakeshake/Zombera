using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Helper for handling DNA value conversions and entry lookups in the character creator.
    /// </summary>
    public static class CharacterCreatorDnaHelper
    {
        public const string HeightDnaName = "height";

        public static float ConvertBodySliderToDnaValue(
            string dnaName, 
            float sliderValue, 
            CharacterDnaControlDefinition definition,
            Vector2 bodyDnaOutputRange,
            Vector2 heightDnaOutputRangeNormalized,
            float bodySliderOutputMax)
        {
            var t = Mathf.Clamp01(sliderValue);

            if (!string.Equals(dnaName, HeightDnaName, StringComparison.OrdinalIgnoreCase))
            {
                var range = definition?.outputRange ?? bodyDnaOutputRange;
                if (definition is { isLocked: true }) range = definition.lockedOutputRange;

                return Mathf.Clamp01(Mathf.Lerp(range.x, range.y, t));
            }

            var maxOutput = Mathf.Clamp(bodySliderOutputMax, 0.1f, 1f);
            var heightN = CharacterCreatorUtility.NormalizeRange(heightDnaOutputRangeNormalized, 0.46f, 0.54f);
            var min = Mathf.Clamp01(heightN.x) * maxOutput;
            var max = Mathf.Clamp01(heightN.y) * maxOutput;
            if (max <= min)
            {
                min = 0.46f * maxOutput;
                max = 0.54f * maxOutput;
            }

            return Mathf.Clamp01(Mathf.Lerp(min, max, t));
        }

        public static float ConvertBodyDnaToSliderValue(
            string dnaName, 
            float dnaValue, 
            CharacterDnaControlDefinition definition,
            Vector2 bodyDnaOutputRange,
            Vector2 heightDnaOutputRangeNormalized,
            float bodySliderOutputMax)
        {
            var v = Mathf.Clamp01(dnaValue);

            if (!string.Equals(dnaName, HeightDnaName, StringComparison.OrdinalIgnoreCase))
            {
                var range = definition?.outputRange ?? bodyDnaOutputRange;
                if (definition is { isLocked: true }) range = definition.lockedOutputRange;

                var denom = Mathf.Max(0.0001f, range.y - range.x);
                return Mathf.Clamp01((v - range.x) / denom);
            }

            var maxOutput = Mathf.Clamp(bodySliderOutputMax, 0.1f, 1f);
            var heightN = CharacterCreatorUtility.NormalizeRange(heightDnaOutputRangeNormalized, 0.46f, 0.54f);
            var min = Mathf.Clamp01(heightN.x) * maxOutput;
            var max = Mathf.Clamp01(heightN.y) * maxOutput;
            var heightDenom = Mathf.Max(0.0001f, max - min);
            return Mathf.Clamp01((v - min) / heightDenom);
        }

        public static CharacterDnaControlDefinition FindDnaControlDefinition(
            string dnaName, 
            CharacterAppearanceCatalog optionCatalog)
        {
            return optionCatalog?.dnaControls.Find(d =>
                string.Equals(d.dnaName, dnaName, StringComparison.OrdinalIgnoreCase));
        }

        public static float GetDnaValue(string dnaName, float fallback, CharacterAppearanceProfile currentProfile)
        {
            var entry = FindDnaEntry(dnaName, currentProfile);
            if (entry == null) return fallback;

            return Mathf.Clamp01(entry.dnaValue);
        }

        public static void SetDnaValue(string dnaName, float value, CharacterAppearanceProfile currentProfile)
        {
            var entry = FindDnaEntry(dnaName, currentProfile);
            if (entry == null)
            {
                entry = new CharacterDnaEntry(dnaName, value);
                currentProfile.bodyValues.Add(entry);
                return;
            }

            entry.dnaValue = Mathf.Clamp01(value);
        }

        public static CharacterDnaEntry FindDnaEntry(string dnaName, CharacterAppearanceProfile currentProfile)
        {
            if (currentProfile == null) return null;
            currentProfile.bodyValues ??= new List<CharacterDnaEntry>();

            return currentProfile.bodyValues.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.dnaName, dnaName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
