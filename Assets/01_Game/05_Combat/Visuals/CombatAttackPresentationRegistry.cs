#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Combat
{
    public enum CombatAttackStyle
    {
        Unknown = 0,
        Jab = 1,
        Cross = 2,
        Hook = 3,
        Uppercut = 4,
        Knee = 5,
        LowKick = 6,
        Combo = 7
    }

    public enum CombatReactionArea
    {
        Default = 0,
        Chest = 1,
        Head = 2,
        ShoulderLeft = 3,
        ShoulderRight = 4,
        Stomach = 5,
        Legs = 6
    }

    /// <summary>
    ///     Tracks per-encounter attack presentation selections so attack animation style
    ///     can drive defender hit-reaction targeting.
    /// </summary>
    public static class CombatAttackPresentationRegistry
    {
        private static readonly Dictionary<AttackExchangeKey, PendingSelection> PendingSelections = new(64);

        private static readonly Dictionary<int, PendingReactionHint> PendingReactionHints = new(64);

        private static readonly List<AttackExchangeKey> SelectionCleanupBuffer = new(32);
        private static readonly List<int> ReactionHintCleanupBuffer = new(32);

        public static void RegisterSelection(
            int encounterId,
            Unit attacker,
            Unit defender,
            CombatAttackStyle attackStyle,
            CombatReactionArea preferredReactionArea,
            float ttlSeconds = 1f)
        {
            if (encounterId <= 0 || attacker == null || defender == null) return;

            CleanupExpiredSelections();

            var safeTtl = Mathf.Max(0.05f, ttlSeconds);
            var key = new AttackExchangeKey(encounterId, attacker.GetInstanceID(), defender.GetInstanceID());

            PendingSelections[key] = new PendingSelection
            {
                AttackStyle = attackStyle,
                PreferredReactionArea = NormalizeReactionArea(preferredReactionArea),
                ExpiresAt = Time.time + safeTtl
            };
        }

        public static bool TryConsumeSelection(
            int encounterId,
            Unit attacker,
            Unit defender,
            out CombatAttackStyle attackStyle,
            out CombatReactionArea preferredReactionArea)
        {
            attackStyle = CombatAttackStyle.Unknown;
            preferredReactionArea = CombatReactionArea.Chest;

            if (encounterId <= 0 || attacker == null || defender == null) return false;

            CleanupExpiredSelections();

            var key = new AttackExchangeKey(encounterId, attacker.GetInstanceID(), defender.GetInstanceID());
            if (!PendingSelections.Remove(key, out var pending)) return false;

            attackStyle = pending.AttackStyle;
            preferredReactionArea = NormalizeReactionArea(pending.PreferredReactionArea);
            return true;
        }

        public static void RegisterIncomingReactionHint(Unit defender, CombatReactionArea preferredReactionArea,
            float ttlSeconds = 0.5f)
        {
            if (defender == null) return;

            CleanupExpiredReactionHints();

            var safeTtl = Mathf.Max(0.05f, ttlSeconds);
            PendingReactionHints[defender.GetInstanceID()] = new PendingReactionHint
            {
                PreferredReactionArea = NormalizeReactionArea(preferredReactionArea),
                ExpiresAt = Time.time + safeTtl
            };
        }

        public static bool TryConsumeIncomingReactionHint(Unit defender, out CombatReactionArea preferredReactionArea)
        {
            preferredReactionArea = CombatReactionArea.Default;

            if (defender == null) return false;

            CleanupExpiredReactionHints();

            var defenderId = defender.GetInstanceID();
            if (!PendingReactionHints.Remove(defenderId, out var pending)) return false;
            preferredReactionArea = NormalizeReactionArea(pending.PreferredReactionArea);
            return true;
        }

        public static void ClearEncounter(int encounterId)
        {
            if (encounterId <= 0 || PendingSelections.Count == 0) return;

            SelectionCleanupBuffer.Clear();
            foreach (var pair in PendingSelections)
                if (pair.Key.EncounterId == encounterId)
                    SelectionCleanupBuffer.Add(pair.Key);

            foreach (var key in SelectionCleanupBuffer) PendingSelections.Remove(key);
        }

        private static CombatReactionArea NormalizeReactionArea(CombatReactionArea preferredReactionArea)
        {
            return preferredReactionArea == CombatReactionArea.Default
                ? CombatReactionArea.Chest
                : preferredReactionArea;
        }

        private static void CleanupExpiredSelections()
        {
            if (PendingSelections.Count == 0) return;

            var now = Time.time;
            SelectionCleanupBuffer.Clear();

            foreach (var pair in PendingSelections)
                if (pair.Value.ExpiresAt <= now)
                    SelectionCleanupBuffer.Add(pair.Key);

            foreach (var key in SelectionCleanupBuffer) PendingSelections.Remove(key);
        }

        private static void CleanupExpiredReactionHints()
        {
            if (PendingReactionHints.Count == 0) return;

            var now = Time.time;
            ReactionHintCleanupBuffer.Clear();

            foreach (var pair in PendingReactionHints)
                if (pair.Value.ExpiresAt <= now)
                    ReactionHintCleanupBuffer.Add(pair.Key);

            foreach (var key in ReactionHintCleanupBuffer)
                PendingReactionHints.Remove(key);
        }

        private readonly struct AttackExchangeKey : IEquatable<AttackExchangeKey>
        {
            public readonly int EncounterId;
            private readonly int _attackerInstanceId;
            private readonly int _defenderInstanceId;

            public AttackExchangeKey(int encounterId, int attackerInstanceId, int defenderInstanceId)
            {
                EncounterId = encounterId;
                _attackerInstanceId = attackerInstanceId;
                _defenderInstanceId = defenderInstanceId;
            }

            public bool Equals(AttackExchangeKey other)
            {
                return EncounterId == other.EncounterId
                       && _attackerInstanceId == other._attackerInstanceId
                       && _defenderInstanceId == other._defenderInstanceId;
            }

            public override bool Equals(object obj)
            {
                return obj is AttackExchangeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = EncounterId;
                    hash = (hash * 397) ^ _attackerInstanceId;
                    hash = (hash * 397) ^ _defenderInstanceId;
                    return hash;
                }
            }
        }

        private struct PendingSelection
        {
            public CombatAttackStyle AttackStyle;
            public CombatReactionArea PreferredReactionArea;
            public float ExpiresAt;
        }

        private struct PendingReactionHint
        {
            public CombatReactionArea PreferredReactionArea;
            public float ExpiresAt;
        }
    }
}