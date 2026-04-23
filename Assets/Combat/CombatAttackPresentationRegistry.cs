using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

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
    /// Tracks per-encounter attack presentation selections so attack animation style
    /// can drive defender hit-reaction targeting.
    /// </summary>
    public static class CombatAttackPresentationRegistry
    {
        private readonly struct AttackExchangeKey : IEquatable<AttackExchangeKey>
        {
            public readonly int EncounterId;
            public readonly int AttackerInstanceId;
            public readonly int DefenderInstanceId;

            public AttackExchangeKey(int encounterId, int attackerInstanceId, int defenderInstanceId)
            {
                EncounterId = encounterId;
                AttackerInstanceId = attackerInstanceId;
                DefenderInstanceId = defenderInstanceId;
            }

            public bool Equals(AttackExchangeKey other)
            {
                return EncounterId == other.EncounterId
                    && AttackerInstanceId == other.AttackerInstanceId
                    && DefenderInstanceId == other.DefenderInstanceId;
            }

            public override bool Equals(object obj)
            {
                return obj is AttackExchangeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = EncounterId;
                    hash = (hash * 397) ^ AttackerInstanceId;
                    hash = (hash * 397) ^ DefenderInstanceId;
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

        private static readonly Dictionary<AttackExchangeKey, PendingSelection> PendingSelections
            = new Dictionary<AttackExchangeKey, PendingSelection>(64);

        private static readonly Dictionary<int, PendingReactionHint> PendingReactionHints
            = new Dictionary<int, PendingReactionHint>(64);

        private static readonly List<AttackExchangeKey> SelectionCleanupBuffer = new List<AttackExchangeKey>(32);
        private static readonly List<int> ReactionHintCleanupBuffer = new List<int>(32);

        public static void RegisterSelection(
            int encounterId,
            Unit attacker,
            Unit defender,
            CombatAttackStyle attackStyle,
            CombatReactionArea preferredReactionArea,
            float ttlSeconds = 1f)
        {
            if (encounterId <= 0 || attacker == null || defender == null)
            {
                return;
            }

            CleanupExpiredSelections();

            float safeTtl = Mathf.Max(0.05f, ttlSeconds);
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

            if (encounterId <= 0 || attacker == null || defender == null)
            {
                return false;
            }

            CleanupExpiredSelections();

            var key = new AttackExchangeKey(encounterId, attacker.GetInstanceID(), defender.GetInstanceID());
            if (!PendingSelections.TryGetValue(key, out PendingSelection pending))
            {
                return false;
            }

            PendingSelections.Remove(key);

            attackStyle = pending.AttackStyle;
            preferredReactionArea = NormalizeReactionArea(pending.PreferredReactionArea);
            return true;
        }

        public static void RegisterIncomingReactionHint(Unit defender, CombatReactionArea preferredReactionArea, float ttlSeconds = 0.5f)
        {
            if (defender == null)
            {
                return;
            }

            CleanupExpiredReactionHints();

            float safeTtl = Mathf.Max(0.05f, ttlSeconds);
            PendingReactionHints[defender.GetInstanceID()] = new PendingReactionHint
            {
                PreferredReactionArea = NormalizeReactionArea(preferredReactionArea),
                ExpiresAt = Time.time + safeTtl
            };
        }

        public static bool TryConsumeIncomingReactionHint(Unit defender, out CombatReactionArea preferredReactionArea)
        {
            preferredReactionArea = CombatReactionArea.Default;

            if (defender == null)
            {
                return false;
            }

            CleanupExpiredReactionHints();

            int defenderId = defender.GetInstanceID();
            if (!PendingReactionHints.TryGetValue(defenderId, out PendingReactionHint pending))
            {
                return false;
            }

            PendingReactionHints.Remove(defenderId);
            preferredReactionArea = NormalizeReactionArea(pending.PreferredReactionArea);
            return true;
        }

        public static void ClearEncounter(int encounterId)
        {
            if (encounterId <= 0 || PendingSelections.Count == 0)
            {
                return;
            }

            SelectionCleanupBuffer.Clear();
            foreach (KeyValuePair<AttackExchangeKey, PendingSelection> pair in PendingSelections)
            {
                if (pair.Key.EncounterId == encounterId)
                {
                    SelectionCleanupBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < SelectionCleanupBuffer.Count; i++)
            {
                PendingSelections.Remove(SelectionCleanupBuffer[i]);
            }
        }

        private static CombatReactionArea NormalizeReactionArea(CombatReactionArea preferredReactionArea)
        {
            return preferredReactionArea == CombatReactionArea.Default
                ? CombatReactionArea.Chest
                : preferredReactionArea;
        }

        private static void CleanupExpiredSelections()
        {
            if (PendingSelections.Count == 0)
            {
                return;
            }

            float now = Time.time;
            SelectionCleanupBuffer.Clear();

            foreach (KeyValuePair<AttackExchangeKey, PendingSelection> pair in PendingSelections)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    SelectionCleanupBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < SelectionCleanupBuffer.Count; i++)
            {
                PendingSelections.Remove(SelectionCleanupBuffer[i]);
            }
        }

        private static void CleanupExpiredReactionHints()
        {
            if (PendingReactionHints.Count == 0)
            {
                return;
            }

            float now = Time.time;
            ReactionHintCleanupBuffer.Clear();

            foreach (KeyValuePair<int, PendingReactionHint> pair in PendingReactionHints)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    ReactionHintCleanupBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < ReactionHintCleanupBuffer.Count; i++)
            {
                PendingReactionHints.Remove(ReactionHintCleanupBuffer[i]);
            }
        }
    }
}