#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.Combat
{
    /// <summary>
    ///     Per-encounter tactical tick timing service.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatTickScheduler : MonoBehaviour
    {
        [SerializeField] private float tickIntervalSeconds = 1.65f;
        [SerializeField] [Range(0f, 1f)] private float newEncounterOffsetRange = 0.8f;

        private readonly Dictionary<int, float> _elapsedByEncounter = new();

        public float TickIntervalSeconds => Mathf.Max(0.05f, tickIntervalSeconds);

        public void RegisterEncounter(int encounterId)
        {
            if (encounterId <= 0) return;

            // Seed with a random offset so multiple encounters don't all tick on the same frame.
            var offset = Random.Range(0f, TickIntervalSeconds * Mathf.Clamp01(newEncounterOffsetRange));
            _elapsedByEncounter[encounterId] = offset;
        }

        public void UnregisterEncounter(int encounterId)
        {
            if (encounterId <= 0) return;

            _elapsedByEncounter.Remove(encounterId);
        }

        public bool ShouldTick(int encounterId, float deltaTime)
        {
            if (!_elapsedByEncounter.TryGetValue(encounterId, out var elapsed)) return false;

            elapsed += Mathf.Max(0f, deltaTime);

            if (elapsed < TickIntervalSeconds)
            {
                _elapsedByEncounter[encounterId] = elapsed;
                return false;
            }

            _elapsedByEncounter[encounterId] = elapsed - TickIntervalSeconds;
            return true;
        }

        public void ResetEncounterTimer(int encounterId)
        {
            if (!_elapsedByEncounter.ContainsKey(encounterId)) return;

            _elapsedByEncounter[encounterId] = 0f;
        }
    }
}