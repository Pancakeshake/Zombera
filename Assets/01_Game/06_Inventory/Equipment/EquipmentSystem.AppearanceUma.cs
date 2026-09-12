#region

using System;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using Zombera.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace Zombera.Inventory
{
    public sealed partial class EquipmentSystem
    {
        private void SyncEquipmentAppearanceFromEquipment(bool rebuildCharacter = true)
        {
            if (!syncEquipmentAppearanceFromEquipment) return;
            if (!TryResolveAvatar(out var avatar)) return;

            var changed = false;
            changed |= ClearMappedUmaSlots(avatar);
            changed |= ApplyEquippedWardrobeEntries(avatar);

            TryRebuildAvatarIfNeeded(avatar, changed, rebuildCharacter);
        }

        private bool TryResolveAvatar(out DynamicCharacterAvatar avatar)
        {
            avatar = _cachedAvatar != null ? _cachedAvatar : GetComponentInChildren<DynamicCharacterAvatar>();
            return avatar != null;
        }

        private bool ClearMappedUmaSlots(DynamicCharacterAvatar avatar)
        {
            var changed = false;

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var umaSlot = MapEquipmentSlotToUmaSlot(slot);
                if (string.IsNullOrEmpty(umaSlot)) continue;

                // Keep existing behavior: always clear mapped slots before re-apply.
                avatar.ClearSlot(umaSlot);
                changed = true;
            }

            return changed;
        }

        private bool ApplyEquippedWardrobeEntries(DynamicCharacterAvatar avatar)
        {
            var changed = false;

            foreach (var binding in equippedItems)
            {
                if (binding.item == null) continue;

                if (TryApplyWardrobeRecipeAsset(avatar, binding))
                {
                    changed = true;
                    continue;
                }

                if (TryApplyWardrobeRecipeName(avatar, binding))
                    changed = true;
            }

            return changed;
        }

        private static bool TryApplyWardrobeRecipeAsset(DynamicCharacterAvatar avatar, EquipmentSlotBinding binding)
        {
            var recipeAsset = binding.item.appearanceWardrobeRecipe as UMAWardrobeRecipe;
            if (recipeAsset == null) return false;

            if (!string.IsNullOrEmpty(binding.item.appearanceWardrobeSlotOverride))
                avatar.SetSlot(binding.item.appearanceWardrobeSlotOverride, recipeAsset.name);
            else
                avatar.SetSlot(recipeAsset);

            return true;
        }

        private static bool TryApplyWardrobeRecipeName(DynamicCharacterAvatar avatar, EquipmentSlotBinding binding)
        {
            var recipeName = binding.item.appearanceWardrobeRecipeName;
            if (string.IsNullOrEmpty(recipeName)) return false;

            var slotName = string.IsNullOrEmpty(binding.item.appearanceWardrobeSlotOverride)
                ? MapEquipmentSlotToUmaSlot(binding.slot)
                : binding.item.appearanceWardrobeSlotOverride;

            if (string.IsNullOrEmpty(slotName)) return false;

            avatar.SetSlot(slotName, recipeName);
            return true;
        }

        private void TryRebuildAvatarIfNeeded(DynamicCharacterAvatar avatar, bool changed, bool rebuildCharacter)
        {
            if (!changed || !rebuildCharacter || avatar == null || avatar.umaData == null) return;

            // Resolve UMA library references at runtime instead of relying on serialized cross-scene links.
            UmaGlobalLibraryService.TryBindAvatarLibrary(avatar, allowGlobalFallback: true);
            UmaAnimationControllerUtility.EnsureAnimationController(avatar);

            if (!Application.isPlaying)
            {
                // Keep editor guard semantics to avoid recursive rebuild loops.
                if (_isRebuilding) return;

#if UNITY_EDITOR
                if (!IsSafeToBuildAvatarInEditor(avatar))
                    return;
#endif

                _isRebuilding = true;

#if UNITY_EDITOR
                var originalRecreateAnimatorOnRaceChange = avatar.RecreateAnimatorOnRaceChange;
#endif

                try
                {
                    // In edit mode, avoid UMA's animator recreation path that can destroy Animator
                    // components in unsafe contexts during SetAnimatorController.
#if UNITY_EDITOR
                    avatar.RecreateAnimatorOnRaceChange = false;
#endif
                    avatar.BuildCharacter();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[EquipmentSystem] Editor UMA rebuild failed for '{name}'. " +
                        "BuildCharacter can fail in SetAnimatorController when editor-time animator recreation runs on asset contexts. " +
                        $"Details: {exception.Message}",
                        this);
                }
                finally
                {
#if UNITY_EDITOR
                    avatar.RecreateAnimatorOnRaceChange = originalRecreateAnimatorOnRaceChange;
#endif
                    _isRebuilding = false;
                }

                return;
            }

            avatar.BuildCharacter();
        }

#if UNITY_EDITOR
        private static bool IsSafeToBuildAvatarInEditor(DynamicCharacterAvatar avatar)
        {
            if (avatar == null) return false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return false;

            var avatarObject = avatar.gameObject;
            if (avatarObject == null) return false;

            if (!avatarObject.scene.IsValid() || !avatarObject.scene.isLoaded) return false;

            // Never run editor rebuilds on persistent assets/prefab assets where UMA may attempt destructive animator mutations.
            if (EditorUtility.IsPersistent(avatarObject) || PrefabUtility.IsPartOfPrefabAsset(avatarObject))
                return false;

            return true;
        }
#endif

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
                _ => string.Empty
            };
        }
    }
}
