#region

using System;
using System.Linq;
using UnityEngine;

#endregion

namespace Zombera.Inventory
{
    public sealed partial class EquipmentSystem
    {
        private Transform ResolveSocket(EquipmentSlot slot, ItemDefinition item)
        {
            var socketName = ResolveSocketName(slot);
            if (!string.IsNullOrWhiteSpace(socketName))
            {
                var slotSocket = FindChildRecursive(transform, socketName);
                if (slotSocket != null) return slotSocket;
            }

            var itemId = item?.itemId;
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                var itemSocket = FindChildRecursive(transform, "Socket_" + itemId);
                if (itemSocket != null) return itemSocket;
            }

            var humanoidSocket = ResolveHumanoidBoneSocket(slot);
            if (humanoidSocket != null) return humanoidSocket;

            var namedSocket = ResolveNamedBoneSocket(slot);
            if (namedSocket != null) return namedSocket;

            if (slot is not (EquipmentSlot.LeftHand or EquipmentSlot.RightHand)) return transform;

            LogEquipmentWarning($"Could not resolve hand socket for {slot} on unit '{name}'.");
            return null;
        }

        private Transform ResolveHumanoidBoneSocket(EquipmentSlot slot)
        {
            var animator = _cachedAnimator != null ? _cachedAnimator : GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman || animator.avatar == null) return null;

            var bone = slot switch
            {
                EquipmentSlot.LeftHand => HumanBodyBones.LeftHand,
                EquipmentSlot.RightHand => HumanBodyBones.RightHand,
                EquipmentSlot.Head or EquipmentSlot.Face => HumanBodyBones.Head,
                EquipmentSlot.Chest => HumanBodyBones.Chest,
                EquipmentSlot.Back => HumanBodyBones.UpperChest,
                EquipmentSlot.Belt => HumanBodyBones.Hips,
                EquipmentSlot.Legs => HumanBodyBones.LeftUpperLeg,
                EquipmentSlot.Feet => HumanBodyBones.LeftFoot,
                _ => HumanBodyBones.LastBone
            };

            if (bone == HumanBodyBones.LastBone) return null;

            var boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform != null) return boneTransform;

            // UpperChest is not guaranteed on every humanoid rig.
            if (slot == EquipmentSlot.Back)
                boneTransform = animator.GetBoneTransform(HumanBodyBones.Chest)
                                ?? animator.GetBoneTransform(HumanBodyBones.Spine);

            return boneTransform;
        }

        private Transform ResolveNamedBoneSocket(EquipmentSlot slot)
        {
            var candidates = slot switch
            {
                EquipmentSlot.LeftHand => new[] { "LeftHand", "Hand_L", "hand_l", "mixamorig:LeftHand" },
                EquipmentSlot.RightHand => new[] { "RightHand", "Hand_R", "hand_r", "mixamorig:RightHand" },
                EquipmentSlot.Head or EquipmentSlot.Face => new[] { "Head", "head", "mixamorig:Head" },
                EquipmentSlot.Chest => new[] { "Chest", "Spine2", "spine_03", "mixamorig:Spine2" },
                EquipmentSlot.Back => new[] { "Spine", "Spine1", "spine_02", "mixamorig:Spine" },
                EquipmentSlot.Belt => new[] { "Hips", "Pelvis", "mixamorig:Hips" },
                EquipmentSlot.Legs => new[] { "LeftUpperLeg", "Thigh_L", "mixamorig:LeftUpLeg" },
                EquipmentSlot.Feet => new[] { "LeftFoot", "Foot_L", "mixamorig:LeftFoot" },
                _ => null
            };

            if (candidates == null) return null;

            return candidates
                .Select(candidate => FindChildRecursiveIgnoreCase(transform, candidate))
                .FirstOrDefault(match => match != null);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName)) return null;

            if (string.Equals(root.name, childName, StringComparison.Ordinal)) return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var match = FindChildRecursive(child, childName);
                if (match != null) return match;
            }

            return null;
        }

        private static Transform FindChildRecursiveIgnoreCase(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName)) return null;

            if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase)) return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var match = FindChildRecursiveIgnoreCase(child, childName);
                if (match != null) return match;
            }

            return null;
        }

        private static string ResolveSocketName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.LeftHand => "Socket_LeftHand",
                EquipmentSlot.RightHand => "Socket_RightHand",
                EquipmentSlot.Head => "Socket_Head",
                EquipmentSlot.Chest => "Socket_Chest",
                EquipmentSlot.Belt => "Socket_Belt",
                EquipmentSlot.Face => "Socket_Face",
                EquipmentSlot.Back => "Socket_Back",
                EquipmentSlot.Legs => "Socket_Legs",
                EquipmentSlot.Feet => "Socket_Feet",
                _ => null
            };
        }
    }
}
