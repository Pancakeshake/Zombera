#if UNITY_EDITOR
#region

using UnityEditor;
using UnityEngine;

#endregion

namespace Zombera.Inventory
{
    public sealed partial class EquipmentSystem
    {
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            // Use delayCall to avoid hierarchy edits during serialization/physics/update loops.
            if (_editorDelayedRebuildQueued) return;

            _editorDelayedRebuildQueued = true;
            EditorApplication.delayCall += RunDelayedEditorRebuild;
        }

        private void RunDelayedEditorRebuild()
        {
            if (this == null) return;
            _editorDelayedRebuildQueued = false;

            // Extra guard for editor: avoid rebuilds while scripts/assets are updating.
            if (EditorApplication.isUpdating || EditorApplication.isCompiling) return;
            if (EditorUtility.IsPersistent(this)) return;
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;

            SyncExplicitFieldsToList();
            RebuildEquippedVisuals();
            RefreshWeaponFromEquipment();
            SyncEquipmentAppearanceFromEquipment();
        }

        private void SyncExplicitFieldsToList()
        {
            // One-way sync from explicit editor fields into internal slot bindings.
            UpdateSlotFromField(EquipmentSlot.Head, headItem);
            UpdateSlotFromField(EquipmentSlot.Chest, chestItem);
            UpdateSlotFromField(EquipmentSlot.Back, backItem);
            UpdateSlotFromField(EquipmentSlot.Legs, legsItem);
            UpdateSlotFromField(EquipmentSlot.Feet, feetItem);
            UpdateSlotFromField(EquipmentSlot.LeftHand, leftHandItem);
            UpdateSlotFromField(EquipmentSlot.RightHand, rightHandItem);
        }

        private void UpdateSlotFromField(EquipmentSlot slot, ItemDefinition fieldItem)
        {
            var existing = GetEquippedItem(slot);
            if (existing == fieldItem) return;

            if (fieldItem == null)
                Unequip(slot);
            else
                Equip(slot, fieldItem);
        }
    }
}
#endif
