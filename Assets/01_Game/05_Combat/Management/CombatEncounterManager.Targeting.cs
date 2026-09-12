using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

namespace Zombera.Combat
{
    public sealed partial class CombatEncounterManager
    {
        // ──────────────────────────────────────────────
        //  Survivor targeting + state validation
        // ──────────────────────────────────────────────

        private bool ShouldHoldSurvivorAttackOnCurrentDefender(Unit survivor)
        {
            if (!lockUnarmedSurvivorCounterattacksToFocusTarget || survivor == null) return false;

            return survivor.Role == UnitRole.Player && survivor.Faction == UnitFaction.Survivor;
        }

        private bool IsPreferredFocusOpponent(Unit survivor, Unit candidateOpponent)
        {
            if (survivor == null || candidateOpponent == null || !candidateOpponent.IsAlive) return false;

            var survivorForward = survivor.transform.forward;
            survivorForward.y = 0f;
            if (survivorForward.sqrMagnitude <= 0.0001f)
                survivorForward = Vector3.forward;
            else
                survivorForward.Normalize();

            var focusDotThreshold = Mathf.Cos(
                Mathf.Clamp(survivorCounterattackFocusAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);

            Unit bestOpponent = null;
            var bestDot = -1f;
            var bestDistanceSqr = float.MaxValue;

            foreach (var pair in _encountersById)
            {
                var state = pair.Value;
                if (state == null || state.UnitA == null || state.UnitB == null) continue;

                Unit opponent = null;
                if (state.UnitA == survivor)
                    opponent = state.UnitB;
                else if (state.UnitB == survivor) opponent = state.UnitA;

                if (opponent == null || !opponent.IsAlive) continue;

                var toOpponent = opponent.transform.position - survivor.transform.position;
                toOpponent.y = 0f;

                var dot = 1f;
                var distanceSqr = toOpponent.sqrMagnitude;
                if (distanceSqr > 0.0001f) dot = Vector3.Dot(survivorForward, toOpponent.normalized);

                if (dot < focusDotThreshold) continue;

                var isBetter = dot > bestDot + 0.001f
                               || (Mathf.Abs(dot - bestDot) <= 0.001f && distanceSqr < bestDistanceSqr);

                if (!isBetter) continue;

                bestDot = dot;
                bestDistanceSqr = distanceSqr;
                bestOpponent = opponent;
            }

            return bestOpponent != null && bestOpponent == candidateOpponent;
        }

        private bool IsSurvivorAlreadyAttackingElsewhere(Unit survivor, int exceptEncounterId)
        {
            foreach (var pair in _encountersById)
            {
                if (pair.Key != exceptEncounterId && pair.Value.CurrentAttacker == survivor)
                    return true;
            }

            return false;
        }

        private static bool IsCombatAllowedForCurrentState()
        {
            if (GameManagerGateway.Instance is not { CurrentState: var state }) return true;

            return state != GameState.LoadingWorld;
        }
    }
}
