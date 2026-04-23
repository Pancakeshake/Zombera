using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    /// Applies and captures character appearance data from DynamicCharacterAvatar.
    /// </summary>
    public static class UmaAppearanceService
    {
        private const string DefaultRaceName = "HumanFemale";
        private const string HairSlotName = "Hair";
        private const string BeardSlotName = "Beard";
        private const string SkinColorName = "Skin";
        private const string HairColorName = "Hair";
        private const string EyeColorName = "Eyes";

        private const float DnaTolerance = 0.001f;
        private const float ColorTolerance = 0.004f;

        public static UmaAppearanceOperationReport TryApplyProfile(
            DynamicCharacterAvatar avatar,
            CharacterAppearanceProfile profile,
            bool rebuildCharacter = true)
        {
            UmaAppearanceOperationReport report = new UmaAppearanceOperationReport();

            if (avatar == null)
            {
                report.AddError("Avatar is null.");
                return report;
            }

            CharacterAppearanceProfile safeProfile = profile ?? CharacterAppearanceProfile.CreateDefault();
            safeProfile.Sanitize();

            bool anyChange = false;

            anyChange |= TryApplyRace(avatar, safeProfile.raceName, report);
            anyChange |= TryApplyDnaEntries(avatar, safeProfile.bodyValues, report);
            anyChange |= TryApplyWardrobeSelection(avatar, safeProfile.wardrobeSelection, report);
            anyChange |= TryApplyColor(avatar, SkinColorName, safeProfile.skinColor, report);
            anyChange |= TryApplyColor(avatar, HairColorName, safeProfile.hairColor, report);
            anyChange |= TryApplyColor(avatar, EyeColorName, safeProfile.eyeColor, report);

            if (rebuildCharacter && anyChange)
            {
                try
                {
                    avatar.BuildCharacter(true, false);
                }
                catch (Exception exception)
                {
                    report.AddError("BuildCharacter failed after applying profile: " + exception.Message);
                }
            }

            return report;
        }

        public static UmaAppearanceOperationReport TryCaptureProfile(
            DynamicCharacterAvatar avatar,
            out CharacterAppearanceProfile profile)
        {
            UmaAppearanceOperationReport report = new UmaAppearanceOperationReport();
            profile = CharacterAppearanceProfile.CreateDefault();

            if (avatar == null)
            {
                report.AddError("Avatar is null.");
                return report;
            }

            profile.raceName = ResolveCurrentRaceName(avatar);

            try
            {
                Dictionary<string, DnaSetter> dna = avatar.GetDNA();
                if (dna == null || dna.Count == 0)
                {
                    report.AddWarning("No DNA values found on avatar.");
                }
                else
                {
                    foreach (KeyValuePair<string, DnaSetter> pair in dna.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Key))
                        {
                            continue;
                        }

                        profile.bodyValues.Add(new CharacterDnaEntry(pair.Key, pair.Value.Get()));
                    }
                }
            }
            catch (Exception exception)
            {
                report.AddError("Failed to capture DNA values: " + exception.Message);
            }

            profile.wardrobeSelection.hairRecipeName = CaptureWardrobeRecipeName(avatar, HairSlotName);
            profile.wardrobeSelection.beardRecipeName = CaptureWardrobeRecipeName(avatar, BeardSlotName);

            if (TryGetSharedColor(avatar, SkinColorName, out Color skinColor))
            {
                profile.skinColor = skinColor;
            }
            else
            {
                report.AddWarning("Shared color 'Skin' is not available on this avatar.");
            }

            if (TryGetSharedColor(avatar, HairColorName, out Color hairColor))
            {
                profile.hairColor = hairColor;
            }
            else
            {
                report.AddWarning("Shared color 'Hair' is not available on this avatar.");
            }

            if (TryGetSharedColor(avatar, EyeColorName, out Color eyeColor))
            {
                profile.eyeColor = eyeColor;
            }
            else
            {
                report.AddWarning("Shared color 'Eyes' is not available on this avatar.");
            }

            profile.Sanitize();
            return report;
        }

        public static UmaAppearanceOperationReport ValidateRoundTrip(
            DynamicCharacterAvatar avatar,
            CharacterAppearanceProfile sourceProfile,
            out CharacterAppearanceProfile capturedProfile)
        {
            UmaAppearanceOperationReport report = new UmaAppearanceOperationReport();
            capturedProfile = CharacterAppearanceProfile.CreateDefault();

            CharacterAppearanceProfile safeSource = sourceProfile ?? CharacterAppearanceProfile.CreateDefault();
            safeSource.Sanitize();

            UmaAppearanceOperationReport applyReport = TryApplyProfile(avatar, safeSource, true);
            report.Merge(applyReport);
            if (!applyReport.Success)
            {
                return report;
            }

            UmaAppearanceOperationReport captureReport = TryCaptureProfile(avatar, out capturedProfile);
            report.Merge(captureReport);
            if (!captureReport.Success)
            {
                return report;
            }

            CompareProfiles(safeSource, capturedProfile, report);
            return report;
        }

        private static bool TryApplyRace(
            DynamicCharacterAvatar avatar,
            string targetRace,
            UmaAppearanceOperationReport report)
        {
            string normalizedTargetRace = string.IsNullOrWhiteSpace(targetRace)
                ? DefaultRaceName
                : targetRace.Trim();

            string currentRace = ResolveCurrentRaceName(avatar);
            if (string.Equals(currentRace, normalizedTargetRace, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            UMAContextBase context = UMAContextBase.Instance;
            if (context == null)
            {
                report.AddError("UMAContextBase.Instance is null; cannot validate race change.");
                return false;
            }

            if (context.GetRace(normalizedTargetRace) == null)
            {
                report.AddError("Race '" + normalizedTargetRace + "' was not found in UMA context.");
                return false;
            }

            bool changed = false;

            try
            {
                changed = avatar.ChangeRace(normalizedTargetRace);
            }
            catch (Exception exception)
            {
                report.AddError("Race change threw an exception: " + exception.Message);
                return false;
            }

            if (!changed)
            {
                report.AddError("Race change to '" + normalizedTargetRace + "' failed.");
                return false;
            }

            return true;
        }

        private static bool TryApplyDnaEntries(
            DynamicCharacterAvatar avatar,
            List<CharacterDnaEntry> entries,
            UmaAppearanceOperationReport report)
        {
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            Dictionary<string, DnaSetter> dna;
            try
            {
                dna = avatar.GetDNA();
            }
            catch (Exception exception)
            {
                report.AddError("Failed to read DNA from avatar: " + exception.Message);
                return false;
            }

            if (dna == null || dna.Count == 0)
            {
                report.AddWarning("Avatar has no DNA values available for this race.");
                return false;
            }

            bool changed = false;

            for (int index = 0; index < entries.Count; index++)
            {
                CharacterDnaEntry entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                entry.Sanitize();
                if (string.IsNullOrEmpty(entry.dnaName))
                {
                    continue;
                }

                if (!dna.TryGetValue(entry.dnaName, out DnaSetter setter) || setter == null)
                {
                    report.AddWarning("DNA key '" + entry.dnaName + "' does not exist on race '" + ResolveCurrentRaceName(avatar) + "'.");
                    continue;
                }

                float targetValue = Mathf.Clamp01(entry.dnaValue);
                if (Mathf.Abs(setter.Get() - targetValue) <= DnaTolerance)
                {
                    continue;
                }

                setter.Set(targetValue);
                changed = true;
            }

            return changed;
        }

        private static bool TryApplyWardrobeSelection(
            DynamicCharacterAvatar avatar,
            CharacterWardrobeSelection selection,
            UmaAppearanceOperationReport report)
        {
            if (selection == null)
            {
                return false;
            }

            selection.Sanitize();

            bool changed = false;
            changed |= TryApplyWardrobeRecipe(avatar, HairSlotName, selection.hairRecipeName, report);
            changed |= TryApplyWardrobeRecipe(avatar, BeardSlotName, selection.beardRecipeName, report);
            return changed;
        }

        private static bool TryApplyWardrobeRecipe(
            DynamicCharacterAvatar avatar,
            string slotName,
            string recipeName,
            UmaAppearanceOperationReport report)
        {
            bool slotExists = HasWardrobeSlot(avatar, slotName);
            string normalizedRecipeName = string.IsNullOrWhiteSpace(recipeName)
                ? string.Empty
                : recipeName.Trim();

            if (!slotExists)
            {
                if (!string.IsNullOrEmpty(normalizedRecipeName))
                {
                    report.AddWarning("Wardrobe slot '" + slotName + "' does not exist on race '" + ResolveCurrentRaceName(avatar) + "'.");
                }

                return false;
            }

            string currentRecipeName = avatar.GetWardrobeItemName(slotName);

            if (string.IsNullOrEmpty(normalizedRecipeName))
            {
                if (string.IsNullOrEmpty(currentRecipeName))
                {
                    return false;
                }

                avatar.ClearSlot(slotName);
                return true;
            }

            if (string.Equals(currentRecipeName, normalizedRecipeName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            UMAContextBase context = UMAContextBase.Instance;
            if (context == null)
            {
                report.AddError("UMAContextBase.Instance is null; cannot set wardrobe recipe '" + normalizedRecipeName + "'.");
                return false;
            }

            UMATextRecipe recipe = context.GetRecipe(normalizedRecipeName, false);
            if (recipe == null)
            {
                report.AddWarning("Wardrobe recipe '" + normalizedRecipeName + "' was not found.");
                return false;
            }

            bool applied = avatar.SetSlot(recipe);
            if (!applied)
            {
                report.AddWarning("Wardrobe recipe '" + normalizedRecipeName + "' is incompatible with race '" + ResolveCurrentRaceName(avatar) + "'.");
                return false;
            }

            return true;
        }

        private static bool TryApplyColor(
            DynamicCharacterAvatar avatar,
            string sharedColorName,
            Color targetColor,
            UmaAppearanceOperationReport report)
        {
            bool hasExistingColor = TryGetSharedColor(avatar, sharedColorName, out Color currentColor);

            if (hasExistingColor && ColorsEqual(currentColor, targetColor))
            {
                return false;
            }

            if (!hasExistingColor)
            {
                report.AddWarning("Shared color '" + sharedColorName + "' was not found and will be injected.");
            }

            avatar.SetColor(sharedColorName, targetColor, new Color(0f, 0f, 0f, 0f), 0f, false);
            return true;
        }

        private static void CompareProfiles(
            CharacterAppearanceProfile source,
            CharacterAppearanceProfile captured,
            UmaAppearanceOperationReport report)
        {
            if (!string.Equals(source.raceName, captured.raceName, StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("Round-trip race mismatch. Source='" + source.raceName + "', Captured='" + captured.raceName + "'.");
            }

            if (!string.Equals(source.wardrobeSelection.hairRecipeName, captured.wardrobeSelection.hairRecipeName, StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("Round-trip hair recipe mismatch. Source='" + source.wardrobeSelection.hairRecipeName + "', Captured='" + captured.wardrobeSelection.hairRecipeName + "'.");
            }

            if (!string.Equals(source.wardrobeSelection.beardRecipeName, captured.wardrobeSelection.beardRecipeName, StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("Round-trip beard recipe mismatch. Source='" + source.wardrobeSelection.beardRecipeName + "', Captured='" + captured.wardrobeSelection.beardRecipeName + "'.");
            }

            if (!ColorsEqual(source.skinColor, captured.skinColor))
            {
                report.AddWarning("Round-trip skin color mismatch.");
            }

            if (!ColorsEqual(source.hairColor, captured.hairColor))
            {
                report.AddWarning("Round-trip hair color mismatch.");
            }

            if (!ColorsEqual(source.eyeColor, captured.eyeColor))
            {
                report.AddWarning("Round-trip eye color mismatch.");
            }

            Dictionary<string, CharacterDnaEntry> capturedByName = new Dictionary<string, CharacterDnaEntry>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < captured.bodyValues.Count; index++)
            {
                CharacterDnaEntry entry = captured.bodyValues[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.dnaName))
                {
                    continue;
                }

                capturedByName[entry.dnaName] = entry;
            }

            for (int index = 0; index < source.bodyValues.Count; index++)
            {
                CharacterDnaEntry sourceEntry = source.bodyValues[index];
                if (sourceEntry == null || string.IsNullOrWhiteSpace(sourceEntry.dnaName))
                {
                    continue;
                }

                if (!capturedByName.TryGetValue(sourceEntry.dnaName, out CharacterDnaEntry capturedEntry))
                {
                    report.AddWarning("Round-trip DNA key missing: '" + sourceEntry.dnaName + "'.");
                    continue;
                }

                if (Mathf.Abs(sourceEntry.dnaValue - capturedEntry.dnaValue) > DnaTolerance)
                {
                    report.AddWarning("Round-trip DNA mismatch for key '" + sourceEntry.dnaName + "'.");
                }
            }
        }

        private static string ResolveCurrentRaceName(DynamicCharacterAvatar avatar)
        {
            if (avatar == null)
            {
                return DefaultRaceName;
            }

            if (avatar.activeRace != null && !string.IsNullOrWhiteSpace(avatar.activeRace.name))
            {
                return avatar.activeRace.name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(avatar.RacePreset))
            {
                return avatar.RacePreset.Trim();
            }

            return DefaultRaceName;
        }

        private static string CaptureWardrobeRecipeName(DynamicCharacterAvatar avatar, string slotName)
        {
            if (!HasWardrobeSlot(avatar, slotName))
            {
                return string.Empty;
            }

            string recipeName = avatar.GetWardrobeItemName(slotName);
            return string.IsNullOrWhiteSpace(recipeName)
                ? string.Empty
                : recipeName.Trim();
        }

        private static bool HasWardrobeSlot(DynamicCharacterAvatar avatar, string slotName)
        {
            if (avatar == null || string.IsNullOrWhiteSpace(slotName))
            {
                return false;
            }

            try
            {
                List<string> slots = avatar.CurrentWardrobeSlots;
                if (slots == null)
                {
                    return false;
                }

                for (int index = 0; index < slots.Count; index++)
                {
                    if (string.Equals(slots[index], slotName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryGetSharedColor(DynamicCharacterAvatar avatar, string colorName, out Color color)
        {
            color = Color.white;

            if (avatar == null || string.IsNullOrWhiteSpace(colorName))
            {
                return false;
            }

            OverlayColorData explicitColor = avatar.GetColor(colorName);
            if (TryExtractAlbedoColor(explicitColor, out color))
            {
                return true;
            }

            OverlayColorData[] sharedColors = avatar.CurrentSharedColors;
            if (sharedColors == null)
            {
                return false;
            }

            for (int index = 0; index < sharedColors.Length; index++)
            {
                OverlayColorData sharedColor = sharedColors[index];
                if (sharedColor == null)
                {
                    continue;
                }

                if (!string.Equals(sharedColor.name, colorName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryExtractAlbedoColor(sharedColor, out color))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractAlbedoColor(OverlayColorData colorData, out Color color)
        {
            color = Color.white;

            if (colorData == null || colorData.channelMask == null || colorData.channelMask.Length == 0)
            {
                return false;
            }

            color = colorData.channelMask[0];
            color.a = 1f;
            return true;
        }

        private static bool ColorsEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= ColorTolerance &&
                Mathf.Abs(a.g - b.g) <= ColorTolerance &&
                Mathf.Abs(a.b - b.b) <= ColorTolerance;
        }
    }

    /// <summary>
    /// Collects errors and warnings from profile apply/capture operations.
    /// </summary>
    public sealed class UmaAppearanceOperationReport
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool Success => errors.Count == 0;
        public bool HasWarnings => warnings.Count > 0;

        public void AddError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            errors.Add(message.Trim());
        }

        public void AddWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            warnings.Add(message.Trim());
        }

        public void Merge(UmaAppearanceOperationReport other)
        {
            if (other == null)
            {
                return;
            }

            for (int index = 0; index < other.errors.Count; index++)
            {
                errors.Add(other.errors[index]);
            }

            for (int index = 0; index < other.warnings.Count; index++)
            {
                warnings.Add(other.warnings[index]);
            }
        }

        public string ToMultilineString()
        {
            StringBuilder builder = new StringBuilder(256);

            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append("Error: ").AppendLine(errors[index]);
            }

            for (int index = 0; index < warnings.Count; index++)
            {
                builder.Append("Warning: ").AppendLine(warnings[index]);
            }

            return builder.ToString().TrimEnd();
        }
    }
}
