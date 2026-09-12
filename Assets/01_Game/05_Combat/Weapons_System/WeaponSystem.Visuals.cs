#region

using System;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Zombera.Data;
using Zombera.Inventory;

#endregion

namespace Zombera.Combat
{
    public sealed partial class WeaponSystem
    {
        public void RebuildVisuals()
        {
            ClearVisuals();

            TryAttachVisual(WeaponSlot.Hand_R, slotHandR);
            TryAttachVisual(WeaponSlot.Hand_L, slotHandL);
            TryAttachVisual(WeaponSlot.Back, slotBack);
            TryAttachVisual(WeaponSlot.Hip_R, slotHipR);
            TryAttachVisual(WeaponSlot.Hip_L, slotHipL);
            TryAttachVisual(WeaponSlot.Chest, slotChest);
        }

        private void ClearVisuals()
        {
            foreach (var instance in _visualInstances.Values)
            {
                if (instance == null) continue;

                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
#if UNITY_EDITOR
                    if (!AssetDatabase.Contains(instance))
                        DestroyImmediate(instance);
#else
                    DestroyImmediate(instance);
#endif
                }
            }

            _visualInstances.Clear();

            // Cleanup any stray objects in case dictionary references are lost.
            var children = GetComponentsInChildren<Transform>(true);
            for (var i = children.Length - 1; i >= 0; i--)
            {
                if (children[i] == null || !children[i].name.EndsWith("_HolsterView")) continue;

                if (Application.isPlaying)
                {
                    Destroy(children[i].gameObject);
                }
                else
                {
#if UNITY_EDITOR
                    if (!AssetDatabase.Contains(children[i].gameObject))
                        DestroyImmediate(children[i].gameObject);
#else
                    DestroyImmediate(children[i].gameObject);
#endif
                }
            }
        }

        private void TryAttachVisual(WeaponSlot slot, ItemDefinition item)
        {
            if (item == null || item.equippedVisualPrefab == null) return;

            var socket = ResolveSocketForSlot(slot);
            if (socket == null) return;

            var visualInstance = Instantiate(item.equippedVisualPrefab, socket);
            visualInstance.name = item.equippedVisualPrefab.name + "_HolsterView";

            var offset = item.slotOffsets?.FirstOrDefault(o => o.slot == slot)
                         ?? WeaponVisualOffset.Default(slot);

            visualInstance.transform.localPosition = offset.position;
            visualInstance.transform.localRotation = Quaternion.Euler(offset.rotation);
            visualInstance.transform.localScale = offset.scale;

            _visualInstances[slot] = visualInstance;
        }

        private Transform ResolveSocketForSlot(WeaponSlot slot)
        {
            var animator = GetComponentInChildren<Animator>();
            Transform boneTransform = null;

            if (animator != null && animator.isHuman && animator.avatar != null)
            {
                var bone = slot switch
                {
                    WeaponSlot.Hand_R => HumanBodyBones.RightHand,
                    WeaponSlot.Hand_L => HumanBodyBones.LeftHand,
                    WeaponSlot.Back => HumanBodyBones.UpperChest,
                    WeaponSlot.Hip_R => HumanBodyBones.RightUpperLeg,
                    WeaponSlot.Hip_L => HumanBodyBones.LeftUpperLeg,
                    WeaponSlot.Chest => HumanBodyBones.Chest,
                    _ => HumanBodyBones.LastBone
                };

                if (bone != HumanBodyBones.LastBone)
                    boneTransform = animator.GetBoneTransform(bone);
            }

            if (boneTransform == null)
                boneTransform = ResolveNamedBoneSocket(slot);

            return boneTransform ?? transform;
        }

        private Transform ResolveNamedBoneSocket(WeaponSlot slot)
        {
            var candidates = slot switch
            {
                WeaponSlot.Hand_R => new[] { "RightHand", "Hand_R", "hand_r", "mixamorig:RightHand" },
                WeaponSlot.Hand_L => new[] { "LeftHand", "Hand_L", "hand_l", "mixamorig:LeftHand" },
                WeaponSlot.Back => new[] { "Spine", "Spine1", "spine_02", "mixamorig:Spine" },
                WeaponSlot.Hip_R => new[] { "RightUpLeg", "Thigh_R", "mixamorig:RightUpLeg" },
                WeaponSlot.Hip_L => new[] { "LeftUpLeg", "Thigh_L", "mixamorig:LeftUpLeg" },
                WeaponSlot.Chest => new[] { "Chest", "Spine2", "spine_03", "mixamorig:Spine2" },
                _ => null
            };

            if (candidates == null) return null;

            foreach (var candidate in candidates)
            {
                var match = FindChildRecursiveIgnoreCase(transform, candidate);
                if (match != null) return match;
            }

            return null;
        }

        public void EquipWeapon(WeaponData weaponData)
        {
            equippedWeapon = weaponData;
            CurrentAmmo = weaponData != null ? weaponData.magazineSize : 0;

            // Notify the animator so equip/holster blends play on the owning unit.
            var animator = GetComponentInChildren<Animator>();
            if (animator == null) return;

            var hasWeapon = weaponData != null;
            if (HasAnimatorParameter(animator, "HasWeapon", AnimatorControllerParameterType.Bool))
                animator.SetBool(HasWeaponAnimatorParameterHash, hasWeapon);

            if (hasWeapon && HasAnimatorParameter(animator, "Equip", AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(EquipAnimatorParameterHash);
        }

        [ContextMenu("Swap Weapon")]
        public void SwapWeapon()
        {
            var temp = equippedWeapon;
            EquipWeapon(secondaryWeapon);
            secondaryWeapon = temp;
        }

        private static bool HasAnimatorParameter(
            Animator animator,
            string parameterName,
            AnimatorControllerParameterType expectedType)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName)) return false;

            return animator.parameters.Any(parameter =>
                parameter.type == expectedType &&
                string.Equals(parameter.name, parameterName, StringComparison.Ordinal));
        }

        private Transform EnsureMuzzlePoint()
        {
            if (muzzlePoint != null) return muzzlePoint;

            foreach (var fallbackMuzzleSearchName in FallbackMuzzleSearchNames)
            {
                var match = FindChildRecursiveIgnoreCase(transform, fallbackMuzzleSearchName);
                if (match == null) continue;

                muzzlePoint = match;
                return muzzlePoint;
            }

            var animator = GetComponentInChildren<Animator>();
            if (animator is not { isHuman: true }) return null;

            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return null;

            muzzlePoint = hand;
            return muzzlePoint;
        }

        private static Transform FindChildRecursiveIgnoreCase(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName)) return null;

            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.name, childName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
