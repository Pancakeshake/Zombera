#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#endregion

namespace Zombera.Inventory
{
    public sealed partial class EquipmentSystem
    {
        private enum RendererClassification
        {
            HiddenByPartRule,
            BaseBody,
            EquippedVisual,
            Other
        }

        private void ApplyVisualAttachment(EquipmentSlot slot, ItemDefinition item)
        {
            if (item?.equippedVisualPrefab == null) return;

            var isSkinned = item.equippedVisualPrefab.GetComponentInChildren<SkinnedMeshRenderer>() != null;
            var socket = ResolveSocket(slot, item);

            if (!isSkinned && socket == null)
            {
                LogEquipmentWarning($"Failed to resolve socket for slot {slot} on unit '{name}'.");
                return;
            }

            RemoveVisualAttachment(slot);

            // Skinned meshes are parented to root to avoid socket offsets; static meshes are parented to socket.
            var parent = isSkinned ? transform : socket;
            var visualInstance = Instantiate(item.equippedVisualPrefab, parent);
            visualInstance.name = item.equippedVisualPrefab.name + "_Equipped";

            if (isSkinned)
            {
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
                visualInstance.transform.localScale = Vector3.one;
                DisableEmbeddedAnimationComponents(visualInstance);
                RemapSkinnedMesh(visualInstance);
            }
            else
            {
                visualInstance.transform.localPosition = item.equippedVisualLocalPosition;
                visualInstance.transform.localRotation = Quaternion.Euler(item.equippedVisualLocalEulerAngles);

                var configuredScale = item.equippedVisualLocalScale;
                if (Mathf.Abs(configuredScale.x) < 0.0001f
                    || Mathf.Abs(configuredScale.y) < 0.0001f
                    || Mathf.Abs(configuredScale.z) < 0.0001f)
                    configuredScale = Vector3.one;

                visualInstance.transform.localScale = configuredScale;
            }

            _equippedVisualInstances[slot] = visualInstance;
            LogEquipmentInfo($"Successfully equipped visual '{visualInstance.name}' (Skinned: {isSkinned}) for slot {slot} on unit '{name}'.");
        }

        private static void DisableEmbeddedAnimationComponents(GameObject visualInstance)
        {
            if (visualInstance == null) return;

            var embeddedAnimators = visualInstance.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < embeddedAnimators.Length; i++)
            {
                var embeddedAnimator = embeddedAnimators[i];
                if (embeddedAnimator == null) continue;
                embeddedAnimator.enabled = false;
            }

            var embeddedLegacyAnimations = visualInstance.GetComponentsInChildren<UnityEngine.Animation>(true);
            for (var i = 0; i < embeddedLegacyAnimations.Length; i++)
            {
                var embeddedLegacyAnimation = embeddedLegacyAnimations[i];
                if (embeddedLegacyAnimation == null) continue;
                embeddedLegacyAnimation.enabled = false;
            }
        }

        private void RemapSkinnedMesh(GameObject visualInstance)
        {
            var skinnedRenderers = visualInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skinnedRenderers == null || skinnedRenderers.Length == 0) return;

            var animator = _cachedAnimator != null ? _cachedAnimator : GetComponentInChildren<Animator>();
            if (animator == null) return;

            var characterBones = CollectCharacterBonesForRemap();
            for (var i = 0; i < skinnedRenderers.Length; i++)
                RemapSkinnedRenderer(skinnedRenderers[i], animator, characterBones, visualInstance);
        }

        private List<Transform> CollectCharacterBonesForRemap()
        {
            var allTransforms = GetComponentsInChildren<Transform>(true);
            var characterBones = new List<Transform>(allTransforms.Length);

            foreach (var transformCandidate in allTransforms)
            {
                if (IsTransformPartOfEquippedVisual(transformCandidate)) continue;
                characterBones.Add(transformCandidate);
            }

            return characterBones;
        }

        private static bool IsTransformPartOfEquippedVisual(Transform transformCandidate)
        {
            var parent = transformCandidate.parent;
            while (parent != null)
            {
                if (parent.name.EndsWith("_Equipped", StringComparison.Ordinal)) return true;
                parent = parent.parent;
            }

            return false;
        }

        private void RemapSkinnedRenderer(
            SkinnedMeshRenderer skinnedRenderer,
            Animator animator,
            List<Transform> characterBones,
            GameObject visualInstance)
        {
            if (skinnedRenderer == null) return;

            // Safety: reset SMR local transform to avoid authored offsets causing failures.
            skinnedRenderer.transform.localRotation = Quaternion.identity;
            skinnedRenderer.transform.localPosition = Vector3.zero;

            var newBones = new Transform[skinnedRenderer.bones.Length];
            for (var i = 0; i < skinnedRenderer.bones.Length; i++)
            {
                var boneName = skinnedRenderer.bones[i].name;
                var targetBone = characterBones.Find(candidate => candidate.name == boneName);
                newBones[i] = targetBone;

                if (newBones[i] == null)
                    LogEquipmentWarning($"Could not find bone '{boneName}' on character skeleton for remapping '{visualInstance.name}'.");
            }

            skinnedRenderer.bones = newBones;
            skinnedRenderer.rootBone = ResolveRemappedRootBone(animator, characterBones);
        }

        private static Transform ResolveRemappedRootBone(Animator animator, List<Transform> characterBones)
        {
            if (animator.isHuman && animator.avatar != null)
                return animator.GetBoneTransform(HumanBodyBones.Hips) ?? animator.transform;

            // Fallback to name-based root-bone lookup when humanoid mapping is unavailable.
            return characterBones.Find(candidate =>
                       candidate.name.Contains("Hips", StringComparison.Ordinal)
                       || candidate.name.Contains("pelvis", StringComparison.Ordinal))
                   ?? animator.transform;
        }

        private void RemoveVisualAttachment(EquipmentSlot slot)
        {
            if (!_equippedVisualInstances.TryGetValue(slot, out var existing) || existing == null)
                // Keep previous behavior: if dictionary is out of sync, bail and rely on full rebuild cleanup.
                return;

            DestroyVisualInstance(existing);
            _equippedVisualInstances.Remove(slot);
        }

        private void RefreshBodyVisibility()
        {
            var partsToHide = BuildHiddenBodyPartSet();
            var allRenderers = GetComponentsInChildren<Renderer>(true);

            for (var i = 0; i < allRenderers.Length; i++)
            {
                var renderer = allRenderers[i];
                if (renderer == null) continue;

                var classification = ClassifyRenderer(renderer, partsToHide);
                ApplyRendererVisibility(renderer.gameObject, classification);
            }
        }

        private HashSet<string> BuildHiddenBodyPartSet()
        {
            var partsToHide = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in equippedItems)
            {
                if (binding.item == null || binding.item.hiddenBodyParts == null) continue;

                foreach (var part in binding.item.hiddenBodyParts)
                {
                    if (string.IsNullOrWhiteSpace(part)) continue;
                    partsToHide.Add(part.Trim());
                }
            }

            return partsToHide;
        }

        private RendererClassification ClassifyRenderer(Renderer renderer, ISet<string> partsToHide)
        {
            var gameObject = renderer.gameObject;
            var meshName = gameObject.name;

            if (partsToHide.Contains(meshName))
                return RendererClassification.HiddenByPartRule;

            if (meshName.StartsWith("Body_", StringComparison.OrdinalIgnoreCase)
                && !IsTransformPartOfEquippedVisual(gameObject.transform))
                return RendererClassification.BaseBody;

            if (IsTransformPartOfEquippedVisual(gameObject.transform))
                return RendererClassification.EquippedVisual;

            return RendererClassification.Other;
        }

        private static void ApplyRendererVisibility(GameObject gameObject, RendererClassification classification)
        {
            switch (classification)
            {
                case RendererClassification.HiddenByPartRule:
                    gameObject.SetActive(false);
                    break;
                case RendererClassification.BaseBody:
                case RendererClassification.EquippedVisual:
                    gameObject.SetActive(true);
                    break;
                case RendererClassification.Other:
                default:
                    break;
            }
        }

        private void ClearAllEquippedVisuals()
        {
            foreach (var instance in _equippedVisualInstances.Values)
            {
                if (instance != null)
                    DestroyVisualInstance(instance);
            }

            _equippedVisualInstances.Clear();
            RemoveStrayEquippedVisualChildren();
        }

        private void RemoveStrayEquippedVisualChildren()
        {
            // Robustness: clean leftover equipped visuals if dictionary references are stale after reload.
            var children = GetComponentsInChildren<Transform>(true);
            for (var i = children.Length - 1; i >= 0; i--)
            {
                var child = children[i];
                if (child == null) continue;

                var childName = child.name;
                if (!childName.EndsWith("_Equipped", StringComparison.Ordinal)
                    && !string.Equals(childName, "CorrectionProxy_180", StringComparison.Ordinal))
                    continue;

                DestroyVisualInstance(child.gameObject);
            }
        }

        private static void DestroyVisualInstance(GameObject visualObject)
        {
            if (visualObject == null) return;

            if (Application.isPlaying)
            {
                Destroy(visualObject);
                return;
            }

#if UNITY_EDITOR
            // Safety: ensure we never destroy an asset reference while cleaning hierarchy objects.
            if (!UnityEditor.AssetDatabase.Contains(visualObject))
                DestroyImmediate(visualObject);
#else
            DestroyImmediate(visualObject);
#endif
        }

        private void RebuildEquippedVisuals()
        {
            ClearAllEquippedVisuals();

            foreach (var binding in equippedItems.Where(binding => binding.item != null))
            {
                // Hand slots are managed by WeaponSystem to support holster testing flow.
                if (binding.slot == EquipmentSlot.LeftHand || binding.slot == EquipmentSlot.RightHand)
                    continue;

                try
                {
                    ApplyVisualAttachment(binding.slot, binding.item);
                }
                catch (Exception exception)
                {
                    LogEquipmentError($"Failed to attach visual for {binding.item.name}: {exception.Message}");
                }
            }

            RefreshBodyVisibility();
        }

        private void LogEquipmentInfo(string message)
        {
            Debug.Log($"[EquipmentSystem] {message}", this);
        }

        private void LogEquipmentWarning(string message)
        {
            Debug.LogWarning($"[EquipmentSystem] {message}", this);
        }

        private void LogEquipmentError(string message)
        {
            Debug.LogError($"[EquipmentSystem] {message}", this);
        }
    }
}
