using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Pure utility methods for character creation UI and data processing.
    /// </summary>
    public static class CharacterCreatorUtility
    {
        public static Vector2 NormalizeRange(Vector2 range, float fallbackMin, float fallbackMax)
        {
            var min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            var max = Mathf.Clamp01(Mathf.Max(range.x, range.y));

            if (max <= min)
            {
                min = Mathf.Clamp01(Mathf.Min(fallbackMin, fallbackMax));
                max = Mathf.Clamp01(Mathf.Max(fallbackMin, fallbackMax));
            }

            return new Vector2(min, max);
        }

        public static int FindRaceIndexByGender(List<string> options, bool preferMale)
        {
            if (options == null || options.Count == 0) return -1;

            for (var index = 0; index < options.Count; index++)
            {
                var candidate = options[index];
                if (preferMale == IsMaleRaceName(candidate)) return index;
            }

            return -1;
        }

        public static bool IsMaleRaceName(string raceName)
        {
            var normalized = NormalizeRaceNameToken(raceName);
            if (string.IsNullOrEmpty(normalized)) return false;

            if (normalized.Contains("female") || normalized.Contains("girl") || normalized.Contains("woman"))
                return false;

            return normalized.Contains("male") || normalized.Contains("boy") || normalized.Contains("man");
        }

        public static string NormalizeRaceNameToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var buffer = new char[value.Length];
            var count = 0;

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!char.IsLetterOrDigit(character)) continue;

                buffer[count] = char.ToLowerInvariant(character);
                count++;
            }

            return new string(buffer, 0, count);
        }

        public static int FindOptionIndex(List<string> options, string value)
        {
            if (options == null || options.Count == 0) return -1;

            for (var index = 0; index < options.Count; index++)
                if (string.Equals(options[index], value, StringComparison.OrdinalIgnoreCase))
                    return index;

            return -1;
        }

        public static int WrapIndex(int index, int count)
        {
            if (count <= 0) return 0;

            var wrapped = index % count;
            if (wrapped < 0) wrapped += count;

            return wrapped;
        }

        public static string GetOptionValue(List<string> options, int index)
        {
            if (options == null || options.Count == 0) return string.Empty;

            var safeIndex = Mathf.Clamp(index, 0, options.Count - 1);
            var value = options[safeIndex];
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static string GetOptionDisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "None"
                : value;
        }

        public static Color PickRandomPresetSkinTone()
        {
            var presets = CharacterCreatorStyle.PresetSkinTones;
            if (presets == null || presets.Length == 0)
                return CharacterCreatorStyle.SkinToneCaucasian;

            var picked = presets[Random.Range(0, presets.Length)];
            picked.a = 1f;
            return picked;
        }

        public static Color EvaluateTone(float value, Color dark, Color light)
        {
            var color = Color.Lerp(dark, light, Mathf.Clamp01(value));
            color.a = 1f;
            return color;
        }

        public static float EstimateTone(Color color, Color dark, Color light)
        {
            Vector3 colorVector = new(color.r, color.g, color.b);
            Vector3 darkVector = new(dark.r, dark.g, dark.b);
            Vector3 lightVector = new(light.r, light.g, light.b);

            var axis = lightVector - darkVector;
            var denominator = Vector3.Dot(axis, axis);
            if (denominator <= 0.0001f) return 0.5f;

            var projection = Vector3.Dot(colorVector - darkVector, axis) / denominator;
            return Mathf.Clamp01(projection);
        }
    }
}
