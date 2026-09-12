#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.AI
{
    public sealed partial class ZombieAnimationController
    {

        private void AutoResolveReferences()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (unit == null || unit.gameObject.scene != gameObject.scene) unit = GetComponent<Unit>();

            if (unitHealth == null || unitHealth.gameObject.scene != gameObject.scene)
                unitHealth = GetComponent<UnitHealth>();
        }


        private void CacheDeathPresentationReferences()
        {
            if (_zombieAi == null || _zombieAi.gameObject.scene != gameObject.scene)
                _zombieAi = GetComponent<ZombieController>();

            if (_zombieStateMachine == null || _zombieStateMachine.gameObject.scene != gameObject.scene)
                _zombieStateMachine = GetComponent<ZombieStateMachine>();

            if (_unitController == null || _unitController.gameObject.scene != gameObject.scene)
                _unitController = GetComponent<UnitController>();

            if (_navMeshAgent == null || _navMeshAgent.gameObject.scene != gameObject.scene)
                _navMeshAgent = GetComponent<NavMeshAgent>();

            if (_mainCollider == null || _mainCollider.gameObject.scene != gameObject.scene)
                _mainCollider = GetComponent<Collider>();

            if (_rootRigidbody == null || _rootRigidbody.gameObject.scene != gameObject.scene)
                _rootRigidbody = GetComponent<Rigidbody>();

            CacheRagdollRig();
        }


        private void CacheRagdollRig()
        {
            if (_ragdollRigCached) return;

            _ragdollRigCached = true;

            var allRigidbodies = GetComponentsInChildren<Rigidbody>(true);
            var ragdollBodies = new List<Rigidbody>();

            for (var i = 0; i < allRigidbodies.Length; i++)
            {
                var body = allRigidbodies[i];

                if (body == null || body == _rootRigidbody) continue;

                ragdollBodies.Add(body);
            }

            _ragdollRigidbodies = ragdollBodies.ToArray();

            var allColliders = GetComponentsInChildren<Collider>(true);
            var ragdollBodyColliders = new List<Collider>();

            for (var i = 0; i < allColliders.Length; i++)
            {
                var childCollider = allColliders[i];

                if (childCollider == null || childCollider == _mainCollider) continue;

                var attachedBody = childCollider.attachedRigidbody;
                if (attachedBody == null || attachedBody == _rootRigidbody) continue;

                ragdollBodyColliders.Add(childCollider);
            }

            _ragdollColliders = ragdollBodyColliders.ToArray();
            _hasRagdollRig = _ragdollRigidbodies.Length > 0;

            SetRagdollEnabled(false);
        }


        private void SetRagdollEnabled(bool isEnabled)
        {
            if (!_hasRagdollRig) return;

            for (var i = 0; i < _ragdollRigidbodies.Length; i++)
            {
                var body = _ragdollRigidbodies[i];
                if (body == null) continue;

                body.isKinematic = !isEnabled;
                body.useGravity = isEnabled;
                body.detectCollisions = isEnabled;

                if (isEnabled)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            for (var i = 0; i < _ragdollColliders.Length; i++)
            {
                var ragdollCollider = _ragdollColliders[i];
                if (ragdollCollider == null) continue;

                ragdollCollider.enabled = isEnabled;
            }
        }


        private void RestoreAlivePresentationState()
        {
            _hasAppliedDeathPresentation = false;
            CancelPendingRagdoll();

            if (animator != null && !animator.enabled) animator.enabled = true;

            if (_mainCollider != null && !_mainCollider.enabled) _mainCollider.enabled = true;

            SetRagdollEnabled(false);
        }


        private void CancelPendingRagdoll()
        {
            if (_pendingRagdollCoroutine == null) return;

            StopCoroutine(_pendingRagdollCoroutine);
            _pendingRagdollCoroutine = null;
        }


        private static string NormalizeClipLookupName(string rawName)
        {
            return ClipNameTokenUtility.Normalize(rawName);
        }


        private static void AddUnique(List<AnimationClip> destination, AnimationClip clip)
        {
            if (destination == null || clip == null || destination.Contains(clip)) return;

            destination.Add(clip);
        }


        private static void AddRangeUnique(List<AnimationClip> destination, AnimationClip[] clips)
        {
            if (destination == null || clips == null) return;

            for (var i = 0; i < clips.Length; i++) AddUnique(destination, clips[i]);
        }


        private static bool ContainsAny(string value, params string[] tokens)
        {
            return ClipNameTokenUtility.Matches(value, tokens);
        }


        private static class ClipNameTokenUtility
        {
            public static string Normalize(string rawName)
            {
                if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

                var trimmed = rawName.Trim();
                var pipeIndex = trimmed.LastIndexOf('|');
                if (pipeIndex >= 0 && pipeIndex < trimmed.Length - 1) trimmed = trimmed[(pipeIndex + 1)..];

                var builder = new StringBuilder(trimmed.Length);
                for (var i = 0; i < trimmed.Length; i++)
                {
                    var character = char.ToLowerInvariant(trimmed[i]);
                    if (char.IsLetterOrDigit(character)) builder.Append(character);
                }

                var normalized = builder.ToString();
                const string armaturePrefix = "armature";
                if (normalized.StartsWith(armaturePrefix, StringComparison.Ordinal))
                    normalized = normalized[armaturePrefix.Length..];

                return normalized;
            }

            public static bool Matches(string value, params string[] tokens)
            {
                if (string.IsNullOrEmpty(value) || tokens == null) return false;

                var normalizedValue = value.ToLowerInvariant();

                for (var i = 0; i < tokens.Length; i++)
                {
                    var token = tokens[i];
                    if (!string.IsNullOrEmpty(token) && normalizedValue.Contains(token.ToLowerInvariant())) return true;
                }

                return false;
            }
        }


        private void TryPlayFallbackState(string stateName, bool triggerWasSet)
        {
            if (!CanQueryAnimatorState() || string.IsNullOrWhiteSpace(stateName)) return;

            if (!forceStateFallbackWhenTriggerSet && triggerWasSet) return;

            var fadeSeconds = Mathf.Max(0f, fallbackCrossFadeSeconds);
            const int baseLayer = 0;

            var stateHash = Animator.StringToHash(stateName);
            if (animator.HasState(baseLayer, stateHash))
            {
                animator.CrossFadeInFixedTime(stateHash, fadeSeconds, baseLayer);
                return;
            }

            var qualifiedStateName = "Base Layer." + stateName;
            var qualifiedHash = Animator.StringToHash(qualifiedStateName);
            if (animator.HasState(baseLayer, qualifiedHash))
                animator.CrossFadeInFixedTime(qualifiedHash, fadeSeconds, baseLayer);
        }
    }
}
