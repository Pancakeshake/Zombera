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

        private void Awake()
        {
            // Keep Awake lightweight. Heavy animator setup is done in OnEnable to avoid duplicate work.
            AutoResolveReferences();
            ApplyAnimatorRuntimeDefaults();
        }


        private void Update()
        {
            if (Time.time < _nextUpdateTime) return;

            UpdateLODThrottling();
            _nextUpdateTime = Time.time + _updateInterval;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();

                if (animator != null)
                {
                    EnsureInitializedForCurrentAnimator();
                    SyncDeadState();
                    SyncCombatState();
                }
            }
            else
            {
                var dead = unitHealth != null && unitHealth.IsDead;
                SyncDeadState();
                if (!dead)
                {
                    TryRestartNonLoopingLocomotionState();
                    DriveLocomotionParameters();
                }

                SyncCombatState();
            }

            if (!_combatTickSubscribed) TrySubscribeCombatTickEvents();
        }


        private void OnEnable()
        {
            AutoResolveReferences();
            CacheDeathPresentationReferences();
            RestoreAlivePresentationState();
            EnsureInitializedForCurrentAnimator();
            SubscribeHealthEvents();
            TrySubscribeCombatTickEvents();
            SyncDeadState();
            SyncCombatState();
        }


        private void OnDisable()
        {
            CancelPendingRagdoll();
            UnsubscribeHealthEvents();
            UnsubscribeCombatTickEvents();
        }


        private void OnDestroy()
        {
            UnsubscribeHealthEvents();
            UnsubscribeCombatTickEvents();
        }


        private void SyncDeadState()
        {
            if (animator == null || !_hasIsDeadBool) return;

            var isDead = unitHealth != null && unitHealth.IsDead;
            animator.SetBool(_isDeadHash, isDead);
        }


        private void SyncCombatState()
        {
            if (animator == null || !_hasIsInCombatBool) return;

            var isDead = unitHealth != null && unitHealth.IsDead;
            var isInCombat = false;

            if (!isDead && _zombieStateMachine != null)
            {
                var currentState = _zombieStateMachine.CurrentState;
                isInCombat = currentState == ZombieState.Chase
                             || currentState == ZombieState.Attack
                             || currentState == ZombieState.AttackDoor;
            }

            animator.SetBool(_isInCombatHash, isInCombat);
        }
    }
}
