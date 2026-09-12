#region

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UMA;
using UMA.CharacterSystem;
using Zombera.Core;

#endregion

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Service for applying and capturing character appearance profiles via UMA.
    /// </summary>
    public static class AppearanceProfileService
    {
        private const string DefaultCatalogPath = "CharacterAppearanceCatalog";

        public static CharacterAppearanceProfile GenerateRandomProfile(string raceName = "HumanMale")
        {
            var catalog = Resources.Load<CharacterAppearanceCatalog>(DefaultCatalogPath);
            var profile = CharacterAppearanceProfile.CreateDefault();
            profile.raceName = raceName;

            // Randomize height (0.4 to 0.6 for "standard" variety)
            profile.bodyValues.Add(new CharacterDnaEntry("height", UnityEngine.Random.Range(0.4f, 0.6f)));

            profile.skinColor = CharacterCreatorUtility.PickRandomPresetSkinTone();
            profile.hairColor = CharacterCreatorUtility.EvaluateTone(
                UnityEngine.Random.Range(0.15f, 0.55f),
                CharacterCreatorStyle.HairToneDark,
                CharacterCreatorStyle.HairToneLight);
            profile.eyeColor = CharacterCreatorUtility.EvaluateTone(
                UnityEngine.Random.Range(0.30f, 0.70f),
                CharacterCreatorStyle.EyeToneDark,
                CharacterCreatorStyle.EyeToneLight);

            // Wardrobe randomization from catalog
            if (catalog != null)
            {
                // Hair
                var hairOptions = catalog.GetWardrobeOptions(raceName, "Hair");
                if (hairOptions.Count > 0)
                {
                    var randomHair = hairOptions[UnityEngine.Random.Range(0, hairOptions.Count)];
                    if (!string.IsNullOrEmpty(randomHair.recipeName))
                        profile.wardrobeSelection.SetRecipe("Hair", randomHair.recipeName);
                }

                // Beard (if male)
                if (raceName.Contains("Male"))
                {
                    var beardOptions = catalog.GetWardrobeOptions(raceName, "Beard");
                    if (beardOptions.Count > 0)
                    {
                        var randomBeard = beardOptions[UnityEngine.Random.Range(0, beardOptions.Count)];
                        if (!string.IsNullOrEmpty(randomBeard.recipeName))
                            profile.wardrobeSelection.SetRecipe("Beard", randomBeard.recipeName);
                    }
                }
            }

            profile.Sanitize();
            return profile;
        }

        public static AppearanceProfileOperationReport TryApplyProfile(
            GameObject avatarRoot,
            CharacterAppearanceProfile profile,
            bool rebuildCharacter = true,
            bool forceRebuildWhenUnchanged = false)
        {
            var report = new AppearanceProfileOperationReport();
            if (avatarRoot == null)
            {
                Debug.LogError("[AppearanceProfileService] Cannot apply profile: avatarRoot is null.");
                report.AddWarning("Preview avatar root is null; profile apply skipped.");
                return report;
            }

            var avatar = avatarRoot.GetComponentInChildren<DynamicCharacterAvatar>();
            if (avatar == null)
            {
                Debug.LogError($"[AppearanceProfileService] No DynamicCharacterAvatar found on {avatarRoot.name}. Cannot apply UMA profile.");
                report.AddError($"No DynamicCharacterAvatar found on {avatarRoot.name}. Cannot apply UMA profile.");
                return report;
            }

            var safeProfile = profile ?? CharacterAppearanceProfile.CreateDefault();
            safeProfile.Sanitize();

            bool changed = false;

            // 1. Race
            if (avatar.activeRace == null || !string.Equals(avatar.activeRace.name, safeProfile.raceName, StringComparison.OrdinalIgnoreCase))
            {
                var indexer = UMAAssetIndexer.Instance;
                if (indexer != null && indexer.HasRace(safeProfile.raceName) == null)
                {
                    report.AddError($"Race '{safeProfile.raceName}' not found in UMA library index. Application aborted to prevent NRE.");
                    return report;
                }

                avatar.ChangeRace(safeProfile.raceName);
                changed = true;
            }

            // 2. DNA
            var dna = avatar.GetDNA();
            if (dna != null)
            {
                foreach (var entry in safeProfile.bodyValues)
                {
                    if (dna.TryGetValue(entry.dnaName, out var setter))
                    {
                        if (!Mathf.Approximately(setter.Value, entry.dnaValue))
                        {
                            setter.Set(entry.dnaValue);
                            changed = true;
                        }
                    }
                    else
                    {
                        report.AddWarning($"DNA '{entry.dnaName}' not found on race '{safeProfile.raceName}'.");
                    }
                }
            }

            // 3. Colors
            changed |= ApplyUmaColor(avatar, "Skin", safeProfile.skinColor);
            changed |= ApplyUmaColor(avatar, "Hair", safeProfile.hairColor);
            changed |= ApplyUmaColor(avatar, "Eyes", safeProfile.eyeColor);

            // 4. Wardrobe
            // Instead of clearing all slots, we rely on SetSlot to replace existing ones.
            // If the profile specifies an empty recipe for a slot, we should ClearSlot.
            foreach (var entry in safeProfile.wardrobeSelection.entries)
            {
                if (!string.IsNullOrEmpty(entry.recipeName))
                {
                    avatar.SetSlot(entry.slotName, entry.recipeName);
                    changed = true;
                }
                else
                {
                    avatar.ClearSlot(entry.slotName);
                    changed = true;
                }
            }

            if ((changed || forceRebuildWhenUnchanged) && rebuildCharacter)
            {
                UmaGlobalLibraryService.TryBindAvatarLibrary(avatar, allowGlobalFallback: true);
                UmaAnimationControllerUtility.EnsureAnimationController(avatar);
                avatar.BuildCharacter();
            }

            return report;
        }

        private static bool ApplyUmaColor(DynamicCharacterAvatar avatar, string name, Color color)
        {
            var existingColor = avatar.GetColor(name);
            if (existingColor != null && existingColor.color == color) return false;

            avatar.SetColor(name, color);
            return true;
        }

        public static AppearanceProfileOperationReport TryCaptureProfile(
            GameObject avatarRoot,
            out CharacterAppearanceProfile profile)
        {
            var report = new AppearanceProfileOperationReport();
            profile = CharacterAppearanceProfile.CreateDefault();

            if (avatarRoot == null)
            {
                report.AddWarning("Preview avatar root is null; using default appearance profile.");
                profile.Sanitize();
                return report;
            }

            var avatar = avatarRoot.GetComponentInChildren<DynamicCharacterAvatar>();
            if (avatar == null)
            {
                report.AddError($"No DynamicCharacterAvatar found on {avatarRoot.name}. Cannot capture UMA profile.");
                profile.Sanitize();
                return report;
            }

            // 1. Race
            profile.raceName = avatar.activeRace != null ? avatar.activeRace.name : string.Empty;

            // 2. DNA
            var dna = avatar.GetDNA();
            if (dna != null)
            {
                profile.bodyValues.Clear();
                foreach (var kvp in dna)
                    profile.bodyValues.Add(new CharacterDnaEntry(kvp.Key, kvp.Value.Value));
            }

            // 3. Colors
            profile.skinColor = GetUmaColor(avatar, "Skin", profile.skinColor);
            profile.hairColor = GetUmaColor(avatar, "Hair", profile.hairColor);
            profile.eyeColor = GetUmaColor(avatar, "Eyes", profile.eyeColor);

            // 4. Wardrobe
            profile.wardrobeSelection.entries.Clear();
            var wardrobe = avatar.WardrobeRecipes;
            foreach (var kvp in wardrobe)
                profile.wardrobeSelection.SetRecipe(kvp.Key, kvp.Value.name);

            profile.Sanitize();
            return report;
        }

        private static Color GetUmaColor(DynamicCharacterAvatar avatar, string name, Color fallback)
        {
            var umaColor = avatar.GetColor(name);
            return umaColor != null ? umaColor.color : fallback;
        }

        public static AppearanceProfileOperationReport ValidateRoundTrip(
            GameObject avatarRoot,
            CharacterAppearanceProfile sourceProfile,
            out CharacterAppearanceProfile capturedProfile)
        {
            var report = new AppearanceProfileOperationReport();
            capturedProfile = CharacterAppearanceProfile.CreateDefault();

            var applyReport = TryApplyProfile(avatarRoot, sourceProfile);
            report.Merge(applyReport);

            var captureReport = TryCaptureProfile(avatarRoot, out capturedProfile);
            report.Merge(captureReport);

            return report;
        }
    }

    /// <summary>
    ///     Collects warnings/errors for appearance apply/capture operations.
    /// </summary>
    [Serializable]
    public sealed class AppearanceProfileOperationReport
    {
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<string> Warnings => _warnings;

        public bool Success => _errors.Count == 0;
        public bool HasWarnings => _warnings.Count > 0;

        public void AddError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            _errors.Add(message.Trim());
        }

        public void AddWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            _warnings.Add(message.Trim());
        }

        public void Merge(AppearanceProfileOperationReport other)
        {
            if (other == null) return;

            foreach (var error in other._errors)
                _errors.Add(error);

            foreach (var warning in other._warnings)
                _warnings.Add(warning);
        }

        public string ToMultilineString()
        {
            var builder = new StringBuilder();

            if (_errors.Count > 0)
            {
                builder.AppendLine("Errors:");
                foreach (var error in _errors)
                    builder.AppendLine("- " + error);
            }

            if (_warnings.Count > 0)
            {
                if (builder.Length > 0) builder.AppendLine();

                builder.AppendLine("Warnings:");
                foreach (var warning in _warnings)
                    builder.AppendLine("- " + warning);
            }

            return builder.Length == 0 ? "No issues." : builder.ToString().TrimEnd();
        }
    }
}
