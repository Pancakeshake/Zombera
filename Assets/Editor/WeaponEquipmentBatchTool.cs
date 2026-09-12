#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Zombera.Combat;
using Zombera.Data;
using Zombera.Inventory;

namespace Zombera.Editor
{
    /// <summary>
    ///     Batch builds/updates item equipment wiring from weapon visuals + weapon stats.
    ///     - Scans Assets/Prefabs/Weapons for visual assets (prefabs and model GameObjects)
    ///     - Resolves matching WeaponData under Assets/ScriptableObjects/Weapons
    ///     - Creates/updates ItemDefinition under Assets/ScriptableObjects/Items
    /// </summary>
    public static class WeaponEquipmentBatchTool
    {
        private const string MenuPath =
            "Tools/Items/Weapons/Batch Wire Weapon Equipment (Prefabs + Stats)";

        private const string VisualRoot = "Assets/Systems/Weapons";
        private const string WrapperRoot = "Assets/Systems/Weapons/_Wrappers";
        private const string LegacyWrapperRoot = "Assets/Prefabs/Weapons/1.EquipWrappers";
        private const string WeaponDataRoot = "Assets/Systems/Weapons";
        private const string ItemRoot = "Assets/Systems/Weapons";
        private const string GeneratedIconsRoot = "Assets/Art/InventoryIcons/Generated";

        [MenuItem(MenuPath, priority = -500)]
        private static void BatchWireWeaponEquipment()
        {
            RunBatchWireWeaponEquipment(true);
        }

        public static string RunBatchWireWeaponEquipment(bool showDialog)
        {
            if (!AssetDatabase.IsValidFolder(VisualRoot))
            {
                var missingRootSummary = "Visual root folder was not found:\n" + VisualRoot;
                if (showDialog)
                    EditorUtility.DisplayDialog(
                        "Weapon Equipment Batch",
                        missingRootSummary,
                        "OK");

                Debug.LogWarning("[WeaponEquipmentBatchTool] " + missingRootSummary);
                return missingRootSummary;
            }

            EnsureFolderHierarchy(ItemRoot);
            EnsureFolderHierarchy(WeaponDataRoot);
            var iconLookup = BuildIconLookup();

            var visuals = CollectVisualAssets();
            if (visuals.Count == 0)
            {
                var noVisualSummary = "No weapon visual assets found under:\n" + VisualRoot;
                if (showDialog)
                    EditorUtility.DisplayDialog(
                        "Weapon Equipment Batch",
                        noVisualSummary,
                        "OK");

                Debug.LogWarning("[WeaponEquipmentBatchTool] " + noVisualSummary);
                return noVisualSummary;
            }

            var weaponDataByRelativeKey = new Dictionary<string, WeaponData>(StringComparer.OrdinalIgnoreCase);
            var weaponDataByNameKey = new Dictionary<string, WeaponData>(StringComparer.OrdinalIgnoreCase);
            BuildWeaponDataLookup(weaponDataByRelativeKey, weaponDataByNameKey);

            var createdItems = 0;
            var updatedItems = 0;
            var createdWeaponData = 0;
            var reusedWeaponData = 0;
            var missingWeaponData = 0;
            var skippedVisuals = 0;
            var iconAssignments = 0;
            var processedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                for (var i = 0; i < visuals.Count; i++)
                {
                    var visual = visuals[i];
                    EditorUtility.DisplayProgressBar(
                        "Batch Wiring Weapon Equipment",
                        "Processing " + visual.path + " (" + (i + 1) + "/" + visuals.Count + ")",
                        (i + 1f) / visuals.Count);

                    if (!TryGetRelativePathWithoutExtension(visual.path, VisualRoot, out var relativePathWithoutExtension))
                    {
                        skippedVisuals++;
                        continue;
                    }

                    var outputRelativePath = BuildOutputRelativePath(relativePathWithoutExtension);
                    if (string.IsNullOrWhiteSpace(outputRelativePath))
                    {
                        skippedVisuals++;
                        continue;
                    }

                    if (!processedRelativePaths.Add(outputRelativePath))
                    {
                        skippedVisuals++;
                        continue;
                    }

                    var profile = ResolveProfile(visual.path);
                    var canonicalName = Path.GetFileNameWithoutExtension(outputRelativePath);
                    var displayName = BuildDisplayName(canonicalName);

                    var itemFolder = Path.GetDirectoryName(outputRelativePath)?.Replace('\\', '/');
                    var dataSubfolder = itemFolder + "/Data";

                    var itemAssetPath = BuildAssetPath(ItemRoot, dataSubfolder + "/" + canonicalName);
                    var legacyItemAssetPath = BuildAssetPath(ItemRoot, outputRelativePath);
                    
                    if (!TryMoveAssetIfNeeded(legacyItemAssetPath, itemAssetPath, out _))
                    {
                        skippedVisuals++;
                        continue;
                    }

                    var item = LoadOrCreateAsset(itemAssetPath, out var createdItem,
                        ScriptableObject.CreateInstance<ItemDefinition>);
                    if (item == null)
                    {
                        skippedVisuals++;
                        continue;
                    }

                    if (createdItem) createdItems++;
                    else updatedItems++;

                    WeaponData weaponData = null;
                    if (profile.requiresWeaponData)
                    {
                        var weaponDataPath = BuildAssetPath(WeaponDataRoot, dataSubfolder + "/" + canonicalName + "_WeaponData");
                        var legacyWeaponDataPath = BuildAssetPath(WeaponDataRoot, outputRelativePath);
                        
                        if (!TryMoveAssetIfNeeded(legacyWeaponDataPath, weaponDataPath, out _))
                        {
                            skippedVisuals++;
                            continue;
                        }

                        weaponData = ResolveOrCreateWeaponData(
                            dataSubfolder + "/" + canonicalName + "_WeaponData",
                            canonicalName,
                            displayName,
                            profile,
                            weaponDataByRelativeKey,
                            weaponDataByNameKey,
                            out var createdWeapon);

                        if (weaponData == null)
                        {
                            missingWeaponData++;
                        }
                        else if (createdWeapon)
                        {
                            createdWeaponData++;
                        }
                        else
                        {
                            reusedWeaponData++;
                        }
                    }

                    if (ApplyItemDefaults(item, visual.asset, outputRelativePath, displayName, profile, weaponData,
                            iconLookup))
                        iconAssignments++;

                    EditorUtility.SetDirty(item);
                    }
}
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary =
                "Weapon equipment batch complete.\n\n" +
                "Visual assets scanned: " + visuals.Count + "\n" +
                "ItemDefinition created: " + createdItems + "\n" +
                "ItemDefinition updated: " + updatedItems + "\n" +
                "Inventory icons assigned/overwritten: " + iconAssignments + "\n" +
                "WeaponData created: " + createdWeaponData + "\n" +
                "WeaponData reused: " + reusedWeaponData + "\n" +
                "WeaponData missing: " + missingWeaponData + "\n" +
                "Skipped visuals: " + skippedVisuals + "\n\n" +
                "Visual source: " + VisualRoot + "\n" +
                "WeaponData source/output: " + WeaponDataRoot + "\n" +
                "ItemDefinition output: " + ItemRoot;

            Debug.Log("[WeaponEquipmentBatchTool] " + summary);
            if (showDialog)
                EditorUtility.DisplayDialog("Weapon Equipment Batch", summary, "OK");

            return summary;
        }

        private static List<VisualAssetRecord> CollectVisualAssets()
        {
            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { VisualRoot });
            var results = new List<VisualAssetRecord>(guids.Length);

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsSupportedVisualAssetPath(path)) continue;

                var normalizedPath = path.Replace('\\', '/');
                if (normalizedPath.StartsWith(WrapperRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.Equals(WrapperRoot, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.StartsWith(LegacyWrapperRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.Equals(LegacyWrapperRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                results.Add(new VisualAssetRecord(path, asset));
            }

            results.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private static bool IsSupportedVisualAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            return extension is ".prefab" or ".fbx" or ".obj";
        }

        private static void BuildWeaponDataLookup(
            IDictionary<string, WeaponData> weaponDataByRelativeKey,
            IDictionary<string, WeaponData> weaponDataByNameKey)
        {
            var weaponGuids = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDataRoot });
            for (var i = 0; i < weaponGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(weaponGuids[i]);
                var weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (weaponData == null) continue;

                if (TryGetRelativePathWithoutExtension(path, WeaponDataRoot, out var relative))
                {
                    var relativeKey = NormalizePathKey(relative);
                    weaponDataByRelativeKey[relativeKey] = weaponData;
                }

                var nameKey = NormalizeNameKey(Path.GetFileNameWithoutExtension(path));
                if (!string.IsNullOrWhiteSpace(nameKey) && !weaponDataByNameKey.ContainsKey(nameKey))
                    weaponDataByNameKey[nameKey] = weaponData;
            }
        }

        private static WeaponData ResolveOrCreateWeaponData(
            string relativePathWithoutExtension,
            string canonicalName,
            string displayName,
            GenerationProfile profile,
            IDictionary<string, WeaponData> weaponDataByRelativeKey,
            IDictionary<string, WeaponData> weaponDataByNameKey,
            out bool created)
        {
            created = false;

            var relativeKey = NormalizePathKey(relativePathWithoutExtension);
            if (weaponDataByRelativeKey.TryGetValue(relativeKey, out var exactMatch) && exactMatch != null)
                return exactMatch;

            var baseName = Path.GetFileNameWithoutExtension(relativePathWithoutExtension);
            var nameKey = NormalizeNameKey(baseName);
            if (!string.IsNullOrWhiteSpace(nameKey)
                && weaponDataByNameKey.TryGetValue(nameKey, out var nameMatch)
                && nameMatch != null)
            {
                weaponDataByRelativeKey[relativeKey] = nameMatch;
                return nameMatch;
            }

            var newWeaponDataPath = BuildAssetPath(WeaponDataRoot, relativePathWithoutExtension);
            var weaponData = LoadOrCreateAsset(newWeaponDataPath, out created, ScriptableObject.CreateInstance<WeaponData>);
            if (weaponData == null) return null;

            ApplyWeaponDataDefaults(weaponData, relativePathWithoutExtension, canonicalName, displayName, profile, created);
            EditorUtility.SetDirty(weaponData);

            weaponDataByRelativeKey[relativeKey] = weaponData;
            if (!string.IsNullOrWhiteSpace(nameKey) && !weaponDataByNameKey.ContainsKey(nameKey))
                weaponDataByNameKey[nameKey] = weaponData;

            return weaponData;
        }

        private static bool ApplyItemDefaults(
            ItemDefinition item,
            GameObject visualAsset,
            string relativePathWithoutExtension,
            string displayName,
            GenerationProfile profile,
            WeaponData weaponData,
            IReadOnlyDictionary<string, Sprite> iconLookup)
        {
            if (item == null || visualAsset == null) return false;

            item.itemId = BuildItemId(relativePathWithoutExtension, profile.itemType);
            item.displayName = displayName;
            item.itemType = profile.itemType;
            item.stackable = profile.stackable;
            item.maxStack = profile.maxStack;

            if (item.weight <= 0f) item.weight = profile.defaultWeight;

            item.worldPickupPrefab = visualAsset;
            item.equippedWeaponData = weaponData;

            if (profile.forceRightHandEquip)
            {
                item.enforceSpecificEquipSlot = true;
                item.forcedEquipSlot = EquipmentSlot.PrimaryWeapon;
                item.equippedVisualPrefab = visualAsset;

                if (IsNearZeroScale(item.equippedVisualLocalScale))
                    item.equippedVisualLocalScale = Vector3.one;
            }
            else
            {
                item.enforceSpecificEquipSlot = false;
                if (profile.itemType is ItemType.Ammo or ItemType.Material)
                    item.equippedVisualPrefab = null;
            }

            return TryAssignInventoryIcon(item, visualAsset, relativePathWithoutExtension, iconLookup);
        }

        private static IReadOnlyDictionary<string, Sprite> BuildIconLookup()
        {
            var map = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(GeneratedIconsRoot)) return map;

            var iconGuids = AssetDatabase.FindAssets("t:Sprite", new[] { GeneratedIconsRoot });
            for (var i = 0; i < iconGuids.Length; i++)
            {
                var iconPath = AssetDatabase.GUIDToAssetPath(iconGuids[i]);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite == null) continue;

                var filenameKey = NormalizeNameKey(Path.GetFileNameWithoutExtension(iconPath));
                if (!string.IsNullOrWhiteSpace(filenameKey)) map[filenameKey] = sprite;

                var spriteNameKey = NormalizeNameKey(sprite.name);
                if (!string.IsNullOrWhiteSpace(spriteNameKey)) map[spriteNameKey] = sprite;
            }

            return map;
        }

        private static bool TryAssignInventoryIcon(
            ItemDefinition item,
            GameObject visualAsset,
            string relativePathWithoutExtension,
            IReadOnlyDictionary<string, Sprite> iconLookup)
        {
            if (item == null || iconLookup == null || iconLookup.Count == 0) return false;

            var visualNameKey = NormalizeNameKey(visualAsset != null ? visualAsset.name : string.Empty);
            if (!string.IsNullOrWhiteSpace(visualNameKey)
                && iconLookup.TryGetValue(visualNameKey, out var icon)
                && icon != null)
            {
                item.inventoryIcon = icon;
                return true;
            }

            var relativeNameKey = NormalizeNameKey(Path.GetFileNameWithoutExtension(relativePathWithoutExtension));
            if (!string.IsNullOrWhiteSpace(relativeNameKey)
                && iconLookup.TryGetValue(relativeNameKey, out icon)
                && icon != null)
            {
                item.inventoryIcon = icon;
                return true;
            }

            return false;
        }

        private static bool IsNearZeroScale(Vector3 scale)
        {
            return Mathf.Abs(scale.x) < 0.0001f
                   || Mathf.Abs(scale.y) < 0.0001f
                   || Mathf.Abs(scale.z) < 0.0001f;
        }

        private static void ApplyWeaponDataDefaults(
            WeaponData weaponData,
            string relativePathWithoutExtension,
            string canonicalName,
            string displayName,
            GenerationProfile profile,
            bool newlyCreated)
        {
            if (weaponData == null) return;

            var idSource = string.IsNullOrWhiteSpace(relativePathWithoutExtension) ? canonicalName : relativePathWithoutExtension;
            weaponData.weaponId = BuildWeaponId(idSource);
            weaponData.displayName = displayName;
            weaponData.weaponCategory = profile.weaponCategory;

            if (!newlyCreated) return;

            var defaults = ResolveWeaponDefaults(profile.weaponCategory);
            weaponData.baseDamage = defaults.baseDamage;
            weaponData.effectiveRange = defaults.range;
            weaponData.fireRate = defaults.fireRate;
            weaponData.magazineSize = defaults.magazineSize;
            weaponData.recoilForce = defaults.recoil;
            weaponData.spreadAngle = defaults.spread;
            weaponData.reloadTimeSeconds = defaults.reloadTime;
            weaponData.animationProfileId = defaults.animationProfileId;
        }

        private static GenerationProfile ResolveProfile(string visualPath)
        {
            var lowerPath = (visualPath ?? string.Empty).Replace('\\', '/').ToLowerInvariant();

            if (lowerPath.Contains("/ammo/"))
                return new GenerationProfile(ItemType.Ammo, WeaponCategory.Bow, true, 200, 0.05f, false, false);

            if (lowerPath.Contains("/arrow/"))
                return new GenerationProfile(ItemType.Ammo, WeaponCategory.Bow, true, 200, 0.05f, false, false);

            if (lowerPath.Contains("/scopes/"))
                return new GenerationProfile(ItemType.Material, WeaponCategory.Rifle, false, 1, 0.35f, false, false);

            if (lowerPath.Contains("/throwables/"))
                return new GenerationProfile(ItemType.Weapon, WeaponCategory.Throwable, false, 1, 1f, true, true);

            if (lowerPath.Contains("/bows/"))
                return new GenerationProfile(ItemType.Weapon, WeaponCategory.Bow, false, 1, 2f, true, true);

            if (lowerPath.Contains("/bow/"))
                return new GenerationProfile(ItemType.Weapon, WeaponCategory.Bow, false, 1, 2f, true, true);

            if (lowerPath.Contains("/melee weapons/") || lowerPath.Contains("/crafted weapons/") ||
                lowerPath.Contains("/tools/"))
                return new GenerationProfile(ItemType.Weapon, WeaponCategory.Melee, false, 1, 2.5f, true, true);

            if (lowerPath.Contains("/guns/shotguns/"))
                return new GenerationProfile(ItemType.Weapon, WeaponCategory.Shotgun, false, 1, 3f, true, true);

            if (lowerPath.Contains("/guns/pistols/"))
                return new GenerationProfile(ItemType.Weapon, WeaponCategory.Pistol, false, 1, 1.4f, true, true);

            if (lowerPath.Contains("/guns/") || lowerPath.Contains("/legendary weapons"))
                return new GenerationProfile(ItemType.Weapon, WeaponCategory.Rifle, false, 1, 3.5f, true, true);

            if (lowerPath.Contains("/gun/") || lowerPath.Contains("/50_cal/"))
                return new GenerationProfile(ItemType.Weapon, WeaponCategory.Rifle, false, 1, 3.5f, true, true);

            return new GenerationProfile(ItemType.Weapon, WeaponCategory.Melee, false, 1, 2f, true, true);
        }

        private static WeaponDefaults ResolveWeaponDefaults(WeaponCategory category)
        {
            return category switch
            {
                WeaponCategory.Pistol => new WeaponDefaults(16f, 32f, 5.5f, 15, 0.35f, 2f, 1.4f, "pistol"),
                WeaponCategory.Rifle => new WeaponDefaults(24f, 65f, 8.2f, 30, 0.55f, 2.5f, 2.2f, "rifle"),
                WeaponCategory.Shotgun => new WeaponDefaults(38f, 18f, 1.3f, 8, 0.75f, 5f, 2.8f, "shotgun"),
                WeaponCategory.Melee => new WeaponDefaults(18f, 2f, 1.4f, 1, 0f, 0f, 0f, "melee"),
                WeaponCategory.Throwable => new WeaponDefaults(55f, 22f, 0.7f, 1, 0f, 0f, 1f, "throwable"),
                WeaponCategory.Bow => new WeaponDefaults(12f, 25f, 1f, 1, 0.3f, 1.5f, 0.9f, "bow"),
                _ => new WeaponDefaults(12f, 20f, 1f, 1, 0.2f, 1f, 1f, string.Empty)
            };
        }

        private static bool TryGetRelativePathWithoutExtension(
            string assetPath,
            string rootFolder,
            out string relativePathWithoutExtension)
        {
            relativePathWithoutExtension = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(rootFolder)) return false;

            var normalizedAssetPath = assetPath.Replace('\\', '/');
            var normalizedRoot = rootFolder.Replace('\\', '/').TrimEnd('/');

            if (!normalizedAssetPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return false;

            var relativePath = normalizedAssetPath[normalizedRoot.Length..].TrimStart('/');
            if (string.IsNullOrWhiteSpace(relativePath)) return false;

            relativePathWithoutExtension = Path.ChangeExtension(relativePath, null)?.Replace('\\', '/');
            return !string.IsNullOrWhiteSpace(relativePathWithoutExtension);
        }

        private static string BuildAssetPath(string assetRoot, string relativePathWithoutExtension)
        {
            return assetRoot.TrimEnd('/') + "/" + relativePathWithoutExtension.TrimStart('/') + ".asset";
        }

        private static string BuildOutputRelativePath(string relativePathWithoutExtension)
        {
            var sanitized = SanitizeRelativePath(relativePathWithoutExtension);
            if (string.IsNullOrWhiteSpace(sanitized)) return string.Empty;

            // Keep generated asset paths aligned with weapon prefab relative paths.
            return sanitized.Trim('/');
        }

        private static bool TryMoveAssetIfNeeded(string sourcePath, string destinationPath, out string failureReason)
        {
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                failureReason = "Source or destination path was empty.";
                return false;
            }

            if (NormalizePathKey(sourcePath).Equals(NormalizePathKey(destinationPath), StringComparison.OrdinalIgnoreCase))
                return true;

            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
                return true;

            // If destination exists, we overwrite it with the source.
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
            {
                AssetDatabase.DeleteAsset(destinationPath);
            }

            var directory = Path.GetDirectoryName(destinationPath)?.Replace('\\', '/');
            EnsureFolderHierarchy(directory);

            var moveError = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(moveError))
            {
                failureReason = "MoveAsset failed: " + moveError;
                return false;
            }

            return true;
        }

        private static T LoadOrCreateAsset<T>(string assetPath, out bool created, Func<T> create)
            where T : ScriptableObject
        {
            created = false;

            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;

            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            EnsureFolderHierarchy(directory);

            var instance = create();
            if (instance == null) return null;

            AssetDatabase.CreateAsset(instance, assetPath);
            created = true;
            return instance;
        }

        private static void EnsureFolderHierarchy(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            folderPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal)) return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string BuildDisplayName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "Item";

            var sanitized = StripMeshyPrefix(rawName);
            var normalized = sanitized.Replace('_', ' ').Replace('-', ' ').Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
                normalized = normalized.Replace("  ", " ");

            return string.IsNullOrWhiteSpace(normalized) ? sanitized.Trim() : normalized;
        }

        private static string BuildItemId(string relativePathWithoutExtension, ItemType itemType)
        {
            var slug = BuildSlug(relativePathWithoutExtension);
            return itemType switch
            {
                ItemType.Weapon => "weapon_" + slug,
                ItemType.Ammo => "ammo_" + slug,
                ItemType.Material => "material_" + slug,
                _ => "item_" + slug
            };
        }

        private static string BuildWeaponId(string relativePathWithoutExtension)
        {
            return "weapon_" + BuildSlug(relativePathWithoutExtension) + "_default";
        }

        private static string BuildSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "item";

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value.Trim())
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
                else if (ch is ' ' or '-' or '_' or '/') sb.Append('_');
            }

            var slug = sb.ToString().Trim('_');
            while (slug.Contains("__", StringComparison.Ordinal)) slug = slug.Replace("__", "_");
            return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
        }

        private static string NormalizePathKey(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim().ToLowerInvariant();
        }

        private static string SanitizeRelativePath(string relativePathWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(relativePathWithoutExtension)) return string.Empty;

            var normalized = NormalizePath(relativePathWithoutExtension);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

            var segments = normalized.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                var sanitized = StripMeshyPrefix(segments[i]);
                if (!string.IsNullOrWhiteSpace(sanitized)) segments[i] = sanitized;
            }

            return string.Join("/", segments).Trim('/');
        }

        private static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private static string StripMeshyPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var trimmed = value.Trim();

            const string aiPrefix = "Meshy_AI_";
            while (trimmed.StartsWith(aiPrefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[aiPrefix.Length..];

            const string prefix = "Meshy_";
            while (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[prefix.Length..];

            trimmed = Regex.Replace(trimmed, @"__?\d+_texture$", string.Empty, RegexOptions.IgnoreCase);
            trimmed = Regex.Replace(trimmed, @"_texture$", string.Empty, RegexOptions.IgnoreCase);
            trimmed = trimmed.Trim('_', '-', ' ');

            return string.IsNullOrWhiteSpace(trimmed) ? value.Trim() : trimmed;
        }

        private static string NormalizeNameKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));

            return sb.ToString();
        }

        private readonly struct VisualAssetRecord
        {
            public readonly string path;
            public readonly GameObject asset;

            public VisualAssetRecord(string path, GameObject asset)
            {
                this.path = path;
                this.asset = asset;
            }
        }

        private readonly struct GenerationProfile
        {
            public readonly ItemType itemType;
            public readonly WeaponCategory weaponCategory;
            public readonly bool stackable;
            public readonly int maxStack;
            public readonly float defaultWeight;
            public readonly bool forceRightHandEquip;
            public readonly bool requiresWeaponData;

            public GenerationProfile(
                ItemType itemType,
                WeaponCategory weaponCategory,
                bool stackable,
                int maxStack,
                float defaultWeight,
                bool forceRightHandEquip,
                bool requiresWeaponData)
            {
                this.itemType = itemType;
                this.weaponCategory = weaponCategory;
                this.stackable = stackable;
                this.maxStack = Mathf.Max(1, maxStack);
                this.defaultWeight = Mathf.Max(0.01f, defaultWeight);
                this.forceRightHandEquip = forceRightHandEquip;
                this.requiresWeaponData = requiresWeaponData;
            }
        }

        private readonly struct WeaponDefaults
        {
            public readonly float baseDamage;
            public readonly float range;
            public readonly float fireRate;
            public readonly int magazineSize;
            public readonly float recoil;
            public readonly float spread;
            public readonly float reloadTime;
            public readonly string animationProfileId;

            public WeaponDefaults(
                float baseDamage,
                float range,
                float fireRate,
                int magazineSize,
                float recoil,
                float spread,
                float reloadTime,
                string animationProfileId)
            {
                this.baseDamage = baseDamage;
                this.range = range;
                this.fireRate = fireRate;
                this.magazineSize = Mathf.Max(1, magazineSize);
                this.recoil = Mathf.Max(0f, recoil);
                this.spread = Mathf.Max(0f, spread);
                this.reloadTime = Mathf.Max(0f, reloadTime);
                this.animationProfileId = animationProfileId ?? string.Empty;
            }
        }
    }
}
#endif
