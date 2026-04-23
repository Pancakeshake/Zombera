using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Data;

namespace Zombera.Inventory
{
    /// <summary>
    /// Handles item equipment and slot assignment.
    /// </summary>
    public sealed class EquipmentSystem : MonoBehaviour
    {
        [SerializeField] private List<EquipmentSlotBinding> equippedItems = new List<EquipmentSlotBinding>();
        [SerializeField] private UnitStats unitStats;
        [SerializeField] private WeaponSystem weaponSystem;

        public IReadOnlyList<EquipmentSlotBinding> EquippedItems => equippedItems;
        public event Action OnEquipmentChanged;

        private readonly Dictionary<EquipmentSlot, GameObject> equippedVisualInstances = new Dictionary<EquipmentSlot, GameObject>();

        private void Awake()
        {
            if (unitStats == null)
            {
                unitStats = GetComponent<UnitStats>();
            }

            if (weaponSystem == null)
            {
                weaponSystem = GetComponent<WeaponSystem>();
            }

            RebuildEquippedVisuals();
            RefreshWeaponFromEquipment();
        }

        public bool Equip(EquipmentSlot slot, ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return false;
            }

            EquipmentSlot resolvedSlot = ResolveRequestedSlot(slot, itemDefinition);

            // Keep one equipped binding per item; move between slots when needed.
            RemoveDuplicateItemBindings(itemDefinition, resolvedSlot);

            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].slot != resolvedSlot)
                {
                    continue;
                }

                bool replacingDifferentItem = equippedItems[i].item != itemDefinition;

                // Remove bonuses for the item being replaced.
                if (replacingDifferentItem)
                {
                    RemoveStatBonuses(equippedItems[i].item);
                }

                RemoveVisualAttachment(resolvedSlot);

                EquipmentSlotBinding binding = equippedItems[i];
                binding.item = itemDefinition;
                equippedItems[i] = binding;

                if (replacingDifferentItem)
                {
                    ApplyStatBonuses(itemDefinition);
                }

                ApplyVisualAttachment(resolvedSlot, itemDefinition);
                RefreshWeaponFromEquipment();
                OnEquipmentChanged?.Invoke();
                return true;
            }

            equippedItems.Add(new EquipmentSlotBinding(resolvedSlot, itemDefinition));
            ApplyStatBonuses(itemDefinition);
            ApplyVisualAttachment(resolvedSlot, itemDefinition);
            RefreshWeaponFromEquipment();
            OnEquipmentChanged?.Invoke();
            return true;
        }

        public bool Unequip(EquipmentSlot slot)
        {
            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].slot == slot)
                {
                    RemoveStatBonuses(equippedItems[i].item);
                    equippedItems.RemoveAt(i);
                    RemoveVisualAttachment(slot);
                    RefreshWeaponFromEquipment();
                    OnEquipmentChanged?.Invoke();
                    return true;
                }
            }

            return false;
        }

        private static EquipmentSlot ResolveRequestedSlot(EquipmentSlot requestedSlot, ItemDefinition item)
        {
            if (item != null && item.enforceSpecificEquipSlot)
            {
                return item.forcedEquipSlot;
            }

            return requestedSlot;
        }

        private void RemoveDuplicateItemBindings(ItemDefinition item, EquipmentSlot keepSlot)
        {
            for (int i = equippedItems.Count - 1; i >= 0; i--)
            {
                EquipmentSlotBinding binding = equippedItems[i];
                if (binding.item != item || binding.slot == keepSlot)
                {
                    continue;
                }

                RemoveStatBonuses(binding.item);
                RemoveVisualAttachment(binding.slot);
                equippedItems.RemoveAt(i);
            }
        }

        public ItemDefinition GetEquippedItem(EquipmentSlot slot)
        {
            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].slot == slot)
                {
                    return equippedItems[i].item;
                }
            }

            return null;
        }

        private void ApplyVisualAttachment(EquipmentSlot slot, ItemDefinition item)
        {
            if (item == null || item.equippedVisualPrefab == null)
            {
                return;
            }

            Transform socket = ResolveSocket(slot, item);

            if (socket == null)
            {
                return;
            }

            RemoveVisualAttachment(slot);

            GameObject visualInstance = Instantiate(item.equippedVisualPrefab, socket);
            visualInstance.name = item.equippedVisualPrefab.name + "_Equipped";
            visualInstance.transform.localPosition = item.equippedVisualLocalPosition;
            visualInstance.transform.localRotation = Quaternion.Euler(item.equippedVisualLocalEulerAngles);

            Vector3 configuredScale = item.equippedVisualLocalScale;
            if (Mathf.Abs(configuredScale.x) < 0.0001f
                || Mathf.Abs(configuredScale.y) < 0.0001f
                || Mathf.Abs(configuredScale.z) < 0.0001f)
            {
                configuredScale = Vector3.one;
            }

            visualInstance.transform.localScale = configuredScale;
            equippedVisualInstances[slot] = visualInstance;
        }

        private void RemoveVisualAttachment(EquipmentSlot slot)
        {
            if (!equippedVisualInstances.TryGetValue(slot, out GameObject existing) || existing == null)
            {
                equippedVisualInstances.Remove(slot);
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(existing);
            }
            else
            {
                DestroyImmediate(existing);
            }

            equippedVisualInstances.Remove(slot);
        }

        private Transform ResolveSocket(EquipmentSlot slot, ItemDefinition item)
        {
            string socketName = ResolveSocketName(slot);
            if (!string.IsNullOrWhiteSpace(socketName))
            {
                Transform slotSocket = FindChildRecursive(transform, socketName);
                if (slotSocket != null)
                {
                    return slotSocket;
                }
            }

            if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
            {
                Transform itemSocket = FindChildRecursive(transform, "Socket_" + item.itemId);
                if (itemSocket != null)
                {
                    return itemSocket;
                }
            }

            Transform humanoidSocket = ResolveHumanoidBoneSocket(slot);
            if (humanoidSocket != null)
            {
                return humanoidSocket;
            }

            Transform namedSocket = ResolveNamedBoneSocket(slot);
            if (namedSocket != null)
            {
                return namedSocket;
            }

            if (slot == EquipmentSlot.LeftHand || slot == EquipmentSlot.RightHand)
            {
                Debug.LogWarning($"[EquipmentSystem] Could not resolve hand socket for {slot}; skipping equipped visual attachment on '{name}'.", this);
                return null;
            }

            return transform;
        }

        private Transform ResolveHumanoidBoneSocket(EquipmentSlot slot)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                return null;
            }

            HumanBodyBones bone = slot switch
            {
                EquipmentSlot.LeftHand => HumanBodyBones.LeftHand,
                EquipmentSlot.RightHand => HumanBodyBones.RightHand,
                EquipmentSlot.Head => HumanBodyBones.Head,
                EquipmentSlot.Face => HumanBodyBones.Head,
                EquipmentSlot.Chest => HumanBodyBones.Chest,
                EquipmentSlot.Back => HumanBodyBones.UpperChest,
                EquipmentSlot.Belt => HumanBodyBones.Hips,
                EquipmentSlot.Legs => HumanBodyBones.LeftUpperLeg,
                EquipmentSlot.Feet => HumanBodyBones.LeftFoot,
                _ => HumanBodyBones.LastBone,
            };

            if (bone == HumanBodyBones.LastBone)
            {
                return null;
            }

            Transform boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform != null)
            {
                return boneTransform;
            }

            // UpperChest is not guaranteed on every humanoid rig.
            if (slot == EquipmentSlot.Back)
            {
                boneTransform = animator.GetBoneTransform(HumanBodyBones.Chest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            }

            return boneTransform;
        }

        private Transform ResolveNamedBoneSocket(EquipmentSlot slot)
        {
            string[] candidates = slot switch
            {
                EquipmentSlot.LeftHand => new[] { "LeftHand", "Hand_L", "hand_l", "mixamorig:LeftHand" },
                EquipmentSlot.RightHand => new[] { "RightHand", "Hand_R", "hand_r", "mixamorig:RightHand" },
                EquipmentSlot.Head => new[] { "Head", "head", "mixamorig:Head" },
                EquipmentSlot.Face => new[] { "Head", "head", "mixamorig:Head" },
                EquipmentSlot.Chest => new[] { "Chest", "Spine2", "spine_03", "mixamorig:Spine2" },
                EquipmentSlot.Back => new[] { "Spine", "Spine1", "spine_02", "mixamorig:Spine" },
                EquipmentSlot.Belt => new[] { "Hips", "Pelvis", "mixamorig:Hips" },
                EquipmentSlot.Legs => new[] { "LeftUpperLeg", "Thigh_L", "mixamorig:LeftUpLeg" },
                EquipmentSlot.Feet => new[] { "LeftFoot", "Foot_L", "mixamorig:LeftFoot" },
                _ => null,
            };

            if (candidates == null)
            {
                return null;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                Transform match = FindChildRecursiveIgnoreCase(transform, candidates[i]);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            if (string.Equals(root.name, childName, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindChildRecursiveIgnoreCase(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform match = FindChildRecursiveIgnoreCase(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static string ResolveSocketName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.LeftHand:
                    return "Socket_LeftHand";
                case EquipmentSlot.RightHand:
                    return "Socket_RightHand";
                case EquipmentSlot.Head:
                    return "Socket_Head";
                case EquipmentSlot.Chest:
                    return "Socket_Chest";
                case EquipmentSlot.Belt:
                    return "Socket_Belt";
                case EquipmentSlot.Face:
                    return "Socket_Face";
                case EquipmentSlot.Back:
                    return "Socket_Back";
                case EquipmentSlot.Legs:
                    return "Socket_Legs";
                case EquipmentSlot.Feet:
                    return "Socket_Feet";
                default:
                    return null;
            }
        }

        private void RebuildEquippedVisuals()
        {
            equippedVisualInstances.Clear();

            for (int i = 0; i < equippedItems.Count; i++)
            {
                EquipmentSlotBinding binding = equippedItems[i];
                if (binding.item == null)
                {
                    continue;
                }

                ApplyVisualAttachment(binding.slot, binding.item);
            }
        }

        private void RefreshWeaponFromEquipment()
        {
            if (weaponSystem == null)
            {
                return;
            }

            WeaponData desiredWeapon = ResolveEquippedWeaponData();
            if (weaponSystem.EquippedWeapon == desiredWeapon)
            {
                return;
            }

            weaponSystem.EquipWeapon(desiredWeapon);
        }

        private WeaponData ResolveEquippedWeaponData()
        {
            ItemDefinition rightHandItem = GetEquippedItem(EquipmentSlot.RightHand);
            if (rightHandItem != null && rightHandItem.equippedWeaponData != null)
            {
                return rightHandItem.equippedWeaponData;
            }

            ItemDefinition leftHandItem = GetEquippedItem(EquipmentSlot.LeftHand);
            if (leftHandItem != null && leftHandItem.equippedWeaponData != null)
            {
                return leftHandItem.equippedWeaponData;
            }

            for (int i = 0; i < equippedItems.Count; i++)
            {
                ItemDefinition item = equippedItems[i].item;
                if (item != null && item.equippedWeaponData != null)
                {
                    return item.equippedWeaponData;
                }
            }

            return null;
        }

        private void ApplyStatBonuses(ItemDefinition item)
        {
            if (unitStats == null || item?.statBonuses == null) return;
            for (int i = 0; i < item.statBonuses.Length; i++)
                unitStats.AddEquipmentBonus(item.statBonuses[i].skill, item.statBonuses[i].flatBonus);
        }

        private void RemoveStatBonuses(ItemDefinition item)
        {
            if (unitStats == null || item?.statBonuses == null) return;
            for (int i = 0; i < item.statBonuses.Length; i++)
                unitStats.RemoveEquipmentBonus(item.statBonuses[i].skill, item.statBonuses[i].flatBonus);
        }
    }

    [Serializable]
    public struct EquipmentSlotBinding
    {
        public EquipmentSlot slot;
        public ItemDefinition item;

        public EquipmentSlotBinding(EquipmentSlot equipmentSlot, ItemDefinition itemDefinition)
        {
            slot = equipmentSlot;
            item = itemDefinition;
        }
    }

    public enum EquipmentSlot
    {
        // Legacy values retained for backward compatibility.
        PrimaryWeapon = 0,
        SecondaryWeapon = 1,
        Head = 2,
        Body = 3,
        Utility = 4,

        // Canonical silhouette layout names.
        LeftHand = PrimaryWeapon,
        RightHand = SecondaryWeapon,
        Chest = Body,
        Belt = Utility,
        Face = 5,
        Back = 6,
        Legs = 7,
        Feet = 8
    }
}