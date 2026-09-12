#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Data;
using Zombera.Debugging;
using Zombera.Inventory;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    public static partial class ClothingPrefabAndWrapperBatchTool
    {
        private static readonly Dictionary<string, EquipmentSlot> ExactEquipmentSlotMap =
            new(StringComparer.Ordinal)
            {
                ["primaryweapon"] = EquipmentSlot.LeftHand,
                ["lefthand"] = EquipmentSlot.LeftHand,
                ["secondaryweapon"] = EquipmentSlot.RightHand,
                ["righthand"] = EquipmentSlot.RightHand,
                ["head"] = EquipmentSlot.Head,
                ["body"] = EquipmentSlot.Chest,
                ["chest"] = EquipmentSlot.Chest,
                ["utility"] = EquipmentSlot.Belt,
                ["belt"] = EquipmentSlot.Belt,
                ["face"] = EquipmentSlot.Face,
                ["back"] = EquipmentSlot.Back,
                ["legs"] = EquipmentSlot.Legs,
                ["feet"] = EquipmentSlot.Feet
            };

        private static readonly (EquipmentSlot Slot, string[] Tokens)[] EquipmentSlotContainsAnyRules =
        {
            (EquipmentSlot.Back, new[] { "back" }),
            (EquipmentSlot.Chest, new[] { "chest", "torso", "shirt", "body", "top" }),
            (EquipmentSlot.Head, new[] { "head", "helmet", "hat" }),
            (EquipmentSlot.Face, new[] { "face", "mask" }),
            (EquipmentSlot.Legs, new[] { "leg", "pant", "trouser", "bottom" }),
            (EquipmentSlot.Feet, new[] { "feet", "foot", "boot", "shoe" }),
            (EquipmentSlot.Belt, new[] { "belt", "waist", "utility" }),
            (EquipmentSlot.LeftHand, new[] { "lefthand" }),
            (EquipmentSlot.RightHand, new[] { "righthand" })
        };

        private static readonly (EquipmentSlot Slot, string[] Tokens)[] EquipmentSlotContainsAllRules =
        {
            (EquipmentSlot.LeftHand, new[] { "left", "hand" }),
            (EquipmentSlot.RightHand, new[] { "right", "hand" })
        };

        private readonly struct ItemDefinitionDefaultsContext
        {
            public readonly GameObject SourcePrefab;
            public readonly GameObject WrapperPrefab;
            public readonly UMA.CharacterSystem.UMAWardrobeRecipe UmaRecipe;
            public readonly string RelativePathWithoutExtension;
            public readonly bool HasResolvedSlot;
            public readonly EquipmentSlot ResolvedSlot;
            public readonly ArmorData ArmorData;
            public readonly bool IsNewAsset;
            public readonly IReadOnlyDictionary<string, Sprite> IconLookup;

            public ItemDefinitionDefaultsContext(
                GameObject sourcePrefab,
                GameObject wrapperPrefab,
                UMA.CharacterSystem.UMAWardrobeRecipe umaRecipe,
                string relativePathWithoutExtension,
                bool hasResolvedSlot,
                EquipmentSlot resolvedSlot,
                ArmorData armorData,
                bool isNewAsset,
                IReadOnlyDictionary<string, Sprite> iconLookup)
            {
                SourcePrefab = sourcePrefab;
                WrapperPrefab = wrapperPrefab;
                UmaRecipe = umaRecipe;
                RelativePathWithoutExtension = relativePathWithoutExtension;
                HasResolvedSlot = hasResolvedSlot;
                ResolvedSlot = resolvedSlot;
                ArmorData = armorData;
                IsNewAsset = isNewAsset;
                IconLookup = iconLookup;
            }
        }

        private static string MapEquipmentSlotToUmaSlot(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Head => "Helmet",
                EquipmentSlot.Face => "Face",
                EquipmentSlot.Chest => "Chest",
                EquipmentSlot.Back => "Shoulders",
                EquipmentSlot.Belt => "Belt",
                EquipmentSlot.Legs => "Legs",
                EquipmentSlot.Feet => "Feet",
                EquipmentSlot.LeftHand => "Hands",
                EquipmentSlot.RightHand => "Hands",
                _ => "None"
            };
        }


        private static List<ItemDefinition> LoadClothingItemDefinitions()
        {
            var items = new List<ItemDefinition>();
            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemRoot });

            foreach (var itemGuid in itemGuids)
            {
                var itemPath = AssetDatabase.GUIDToAssetPath(itemGuid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
                if (item == null || items.Contains(item) || !IsClothingItemDefinition(item)) continue;

                items.Add(item);
            }

            return items;
        }


        private static int SyncClothingToPlayerSpawnerInventory(IReadOnlyList<ItemDefinition> clothingItems)
        {
            var spawners = Object.FindObjectsByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var syncedCount = 0;

            for (var i = 0; i < spawners.Length; i++)
            {
                var spawner = spawners[i];
                if (!TryGetSpawnerInventoryProperty(spawner, out var so, out var itemsProp)) continue;

                var mergedItems = CollectMergedSpawnerInventoryItems(itemsProp, clothingItems);
                ApplyMergedItemsToInventoryProperty(itemsProp, mergedItems);

                so.ApplyModifiedPropertiesWithoutUndo();
                MarkSpawnerDirty(spawner);

                syncedCount++;
            }

            return syncedCount;
        }


        private static bool TryGetSpawnerInventoryProperty(
            PlayerSpawner spawner,
            out SerializedObject serializedSpawner,
            out SerializedProperty itemsProp)
        {
            serializedSpawner = null;
            itemsProp = null;
            if (spawner == null) return false;

            serializedSpawner = new SerializedObject(spawner);
            itemsProp = serializedSpawner.FindProperty("devModeSpawnInventoryItems");
            return itemsProp != null;
        }


        private static List<ItemDefinition> CollectMergedSpawnerInventoryItems(
            SerializedProperty itemsProp,
            IReadOnlyList<ItemDefinition> clothingItems)
        {
            var mergedItems = new List<ItemDefinition>();
            AppendExistingSpawnerItems(itemsProp, mergedItems);
            AppendDistinctItems(clothingItems, mergedItems);
            return mergedItems;
        }


        private static void AppendExistingSpawnerItems(SerializedProperty itemsProp, List<ItemDefinition> mergedItems)
        {
            for (var index = 0; index < itemsProp.arraySize; index++)
            {
                var existing = itemsProp.GetArrayElementAtIndex(index).objectReferenceValue as ItemDefinition;
                if (existing == null || mergedItems.Contains(existing)) continue;

                mergedItems.Add(existing);
            }
        }


        private static void AppendDistinctItems(
            IReadOnlyList<ItemDefinition> sourceItems,
            List<ItemDefinition> destination)
        {
            if (sourceItems == null) return;

            for (var index = 0; index < sourceItems.Count; index++)
            {
                var item = sourceItems[index];
                if (item == null || destination.Contains(item)) continue;

                destination.Add(item);
            }
        }


        private static void ApplyMergedItemsToInventoryProperty(
            SerializedProperty itemsProp,
            List<ItemDefinition> mergedItems)
        {
            itemsProp.arraySize = mergedItems.Count;
            for (var index = 0; index < mergedItems.Count; index++)
                itemsProp.GetArrayElementAtIndex(index).objectReferenceValue = mergedItems[index];
        }


        private static void MarkSpawnerDirty(PlayerSpawner spawner)
        {
            EditorUtility.SetDirty(spawner);
            if (spawner.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        }


        private static int EnableDevSpawnInventoryInDebugSettings()
        {
            var synced = 0;
            var debugSettingsGuids = AssetDatabase.FindAssets("t:DebugSettings");

            foreach (var debugSettingsGuid in debugSettingsGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(debugSettingsGuid);
                var settings = AssetDatabase.LoadAssetAtPath<DebugSettings>(path);
                if (settings == null) continue;

                var so = new SerializedObject(settings);
                var prop = so.FindProperty("enableDevSpawnFullInventory");
                if (prop == null) continue;

                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                synced++;
            }

            return synced;
        }


        private static bool ApplyItemDefinitionDefaults(ItemDefinition item, in ItemDefinitionDefaultsContext context)
        {
            if (item == null || context.SourcePrefab == null) return false;

            item.itemId = BuildClothesItemId(context.RelativePathWithoutExtension);
            item.displayName = BuildDisplayName(StripMeshyPrefix(Path.GetFileNameWithoutExtension(context.RelativePathWithoutExtension)));
            item.itemType = ItemType.Generic;
            item.stackable = false;
            item.maxStack = 1;
            item.worldPickupPrefab = context.SourcePrefab;
            item.equippedWeaponData = null;
            item.equippedArmorData = context.ArmorData;
            item.equippedVisualPrefab = context.WrapperPrefab != null ? context.WrapperPrefab : context.SourcePrefab;
            item.appearanceWardrobeRecipe = context.UmaRecipe;

            if (item.weight <= 0f) item.weight = 1f;

            item.enforceSpecificEquipSlot = context.HasResolvedSlot;
            if (context.HasResolvedSlot)
                item.forcedEquipSlot = context.ResolvedSlot;

            if (context.IsNewAsset)
            {
                item.equippedVisualLocalPosition = Vector3.zero;
                item.equippedVisualLocalEulerAngles = Vector3.zero;
            }

            if (IsNearZeroScale(item.equippedVisualLocalScale))
                item.equippedVisualLocalScale = Vector3.one;

            return TryAssignInventoryIcon(
                item,
                context.SourcePrefab,
                context.RelativePathWithoutExtension,
                context.IconLookup);
        }


        private static void ApplyArmorDataDefaults(
            ArmorData armorData,
            string relativePathWithoutExtension,
            EquipmentSlot resolvedSlot,
            bool isNewAsset)
        {
            if (armorData == null) return;

            armorData.armorId = BuildArmorId(relativePathWithoutExtension);
            armorData.displayName = BuildDisplayName(StripMeshyPrefix(Path.GetFileNameWithoutExtension(relativePathWithoutExtension)));
            armorData.equipSlot = resolvedSlot;

            if (!isNewAsset) return;

            armorData.armorRating = ResolveDefaultArmorRating(resolvedSlot);
            armorData.damageResistances = ResolveDefaultArmorResistances(resolvedSlot);
            armorData.statBonuses = ResolveDefaultArmorStatBonuses(resolvedSlot);
        }


        private static bool IsClothingItemDefinition(ItemDefinition item)
        {
            if (item == null) return false;
            if (item.equippedArmorData != null) return true;

            var id = item.itemId ?? string.Empty;
            return id.StartsWith("clothes_", StringComparison.OrdinalIgnoreCase)
                   || id.StartsWith("clothing_", StringComparison.OrdinalIgnoreCase);
        }


        private static string AppendArmorSuffix(string relativePathWithoutExtension)
        {
            var directory = Path.GetDirectoryName(relativePathWithoutExtension)?.Replace('\\', '/');
            var filename = Path.GetFileName(relativePathWithoutExtension);

            if (string.IsNullOrWhiteSpace(directory))
                return filename + "_Armor";

            return directory.TrimEnd('/') + "/" + filename + "_Armor";
        }


        private static bool TryResolveEquipmentSlot(string relativePathWithoutExtension, out EquipmentSlot slot)
        {
            slot = EquipmentSlot.Chest;
            if (string.IsNullOrWhiteSpace(relativePathWithoutExtension)) return false;

            var normalized = NormalizePath(relativePathWithoutExtension);
            var segments = normalized.Split('/');

            for (var i = 0; i < segments.Length; i++)
                if (TryResolveEquipmentSlotSegment(segments[i], out slot))
                    return true;

            return false;
        }


        private static bool TryResolveEquipmentSlotFromRootFolder(string relativePathWithoutExtension,
            out EquipmentSlot slot)
        {
            slot = EquipmentSlot.Chest;
            if (string.IsNullOrWhiteSpace(relativePathWithoutExtension)) return false;

            var normalized = NormalizePath(relativePathWithoutExtension);
            if (string.IsNullOrWhiteSpace(normalized)) return false;

            var firstSeparator = normalized.IndexOf('/');
            var rootSegment = firstSeparator >= 0 ? normalized[..firstSeparator] : normalized;
            return TryResolveEquipmentSlotSegment(rootSegment, out slot);
        }


        private static bool TryResolveEquipmentSlotSegment(string segment, out EquipmentSlot slot)
        {
            slot = EquipmentSlot.Chest;
            var key = NormalizeNameKey(StripMeshyPrefix(segment));
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (ExactEquipmentSlotMap.TryGetValue(key, out slot)) return true;
            if (TryResolveEquipmentSlotByContainsAnyRule(key, out slot)) return true;
            return TryResolveEquipmentSlotByContainsAllRule(key, out slot);
        }


        private static bool TryResolveEquipmentSlotByContainsAnyRule(string key, out EquipmentSlot slot)
        {
            for (var i = 0; i < EquipmentSlotContainsAnyRules.Length; i++)
            {
                var (mappedSlot, tokens) = EquipmentSlotContainsAnyRules[i];
                if (!ContainsAnyToken(key, tokens)) continue;

                slot = mappedSlot;
                return true;
            }

            slot = EquipmentSlot.Chest;
            return false;
        }


        private static bool TryResolveEquipmentSlotByContainsAllRule(string key, out EquipmentSlot slot)
        {
            for (var i = 0; i < EquipmentSlotContainsAllRules.Length; i++)
            {
                var (mappedSlot, tokens) = EquipmentSlotContainsAllRules[i];
                if (!ContainsAllTokens(key, tokens)) continue;

                slot = mappedSlot;
                return true;
            }

            slot = EquipmentSlot.Chest;
            return false;
        }


        private static bool ContainsAnyToken(string key, IReadOnlyList<string> tokens)
        {
            if (string.IsNullOrWhiteSpace(key) || tokens == null) return false;

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (key.Contains(token, StringComparison.Ordinal)) return true;
            }

            return false;
        }


        private static bool ContainsAllTokens(string key, IReadOnlyList<string> tokens)
        {
            if (string.IsNullOrWhiteSpace(key) || tokens == null || tokens.Count == 0) return false;

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (!key.Contains(token, StringComparison.Ordinal)) return false;
            }

            return true;
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


        private static string BuildClothesItemId(string relativePathWithoutExtension)
        {
            return "clothes_" + BuildSlug(relativePathWithoutExtension);
        }


        private static string BuildArmorId(string relativePathWithoutExtension)
        {
            return "armor_" + BuildSlug(relativePathWithoutExtension);
        }


        private static float ResolveDefaultArmorRating(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Head => 6f,
                EquipmentSlot.Chest => 10f,
                EquipmentSlot.Back => 5f,
                EquipmentSlot.Legs => 7f,
                EquipmentSlot.Feet => 4f,
                EquipmentSlot.Belt => 3f,
                EquipmentSlot.Face => 3f,
                EquipmentSlot.LeftHand => 2f,
                EquipmentSlot.RightHand => 2f,
                _ => 5f
            };
        }


        private static ArmorDamageResistance[] ResolveDefaultArmorResistances(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Head => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Generic, resistance = 0.08f },
                    new ArmorDamageResistance { damageType = DamageType.Melee, resistance = 0.1f }
                },
                EquipmentSlot.Chest => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Generic, resistance = 0.12f },
                    new ArmorDamageResistance { damageType = DamageType.Melee, resistance = 0.15f },
                    new ArmorDamageResistance { damageType = DamageType.Ranged, resistance = 0.12f },
                    new ArmorDamageResistance { damageType = DamageType.Fire, resistance = 0.05f }
                },
                EquipmentSlot.Back => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Generic, resistance = 0.06f },
                    new ArmorDamageResistance { damageType = DamageType.Ranged, resistance = 0.08f }
                },
                EquipmentSlot.Legs => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Generic, resistance = 0.1f },
                    new ArmorDamageResistance { damageType = DamageType.Melee, resistance = 0.1f }
                },
                EquipmentSlot.Feet => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Generic, resistance = 0.05f }
                },
                EquipmentSlot.Belt => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Generic, resistance = 0.04f }
                },
                EquipmentSlot.Face => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Generic, resistance = 0.04f },
                    new ArmorDamageResistance { damageType = DamageType.Fire, resistance = 0.06f }
                },
                EquipmentSlot.LeftHand => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Melee, resistance = 0.03f }
                },
                EquipmentSlot.RightHand => new[]
                {
                    new ArmorDamageResistance { damageType = DamageType.Melee, resistance = 0.03f }
                },
                _ => Array.Empty<ArmorDamageResistance>()
            };
        }


        private static ArmorStatBonus[] ResolveDefaultArmorStatBonuses(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Head => new[]
                {
                    new ArmorStatBonus { skill = UnitSkillType.Scavenging, flatBonus = 1 }
                },
                EquipmentSlot.Chest => new[]
                {
                    new ArmorStatBonus { skill = UnitSkillType.Toughness, flatBonus = 1 }
                },
                EquipmentSlot.Back => new[]
                {
                    new ArmorStatBonus { skill = UnitSkillType.Endurance, flatBonus = 1 }
                },
                EquipmentSlot.Legs => new[]
                {
                    new ArmorStatBonus { skill = UnitSkillType.Agility, flatBonus = 1 }
                },
                EquipmentSlot.Feet => new[]
                {
                    new ArmorStatBonus { skill = UnitSkillType.Agility, flatBonus = 1 }
                },
                EquipmentSlot.Face => new[]
                {
                    new ArmorStatBonus { skill = UnitSkillType.Stealth, flatBonus = 1 }
                },
                _ => Array.Empty<ArmorStatBonus>()
            };
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


        private static string NormalizeNameKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));

            return sb.ToString();
        }


        private static IReadOnlyDictionary<string, Sprite> BuildIconLookup()
        {
            return ItemPipelineAssetUtility.BuildIconLookup(GeneratedIconsRoot, NormalizeNameKey);
        }


        private static bool TryAssignInventoryIcon(
            ItemDefinition item,
            GameObject sourcePrefab,
            string relativePathWithoutExtension,
            IReadOnlyDictionary<string, Sprite> iconLookup)
        {
            if (item == null || iconLookup == null || iconLookup.Count == 0) return false;

            var prefabNameKey = NormalizeNameKey(sourcePrefab != null ? sourcePrefab.name : string.Empty);
            if (!string.IsNullOrWhiteSpace(prefabNameKey)
                && iconLookup.TryGetValue(prefabNameKey, out var icon)
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
    }
}
#endif
