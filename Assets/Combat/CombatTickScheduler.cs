using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Combat
{
    /// <summary>
    /// Per-encounter tactical tick timing service.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatTickScheduler : MonoBehaviour
    {
        [SerializeField] private float tickIntervalSeconds = 1.65f;
        [SerializeField, Range(0f, 1f)] private float newEncounterOffsetRange = 0.8f;

        private readonly Dictionary<int, float> elapsedByEncounter = new Dictionary<int, float>();

        public float TickIntervalSeconds => Mathf.Max(0.05f, tickIntervalSeconds);

        public void RegisterEncounter(int encounterId)
        {
            if (encounterId <= 0)
            {
                return;
            }

            // Seed with a random offset so multiple encounters don't all tick on the same frame.
            float offset = Random.Range(0f, TickIntervalSeconds * Mathf.Clamp01(newEncounterOffsetRange));
            elapsedByEncounter[encounterId] = offset;
        }

        public void UnregisterEncounter(int encounterId)
        {
            if (encounterId <= 0)
            {
                return;
            }

            elapsedByEncounter.Remove(encounterId);
        }

        public bool ShouldTick(int encounterId, float deltaTime)
        {
            if (!elapsedByEncounter.TryGetValue(encounterId, out float elapsed))
            {
                return false;
            }

            elapsed += Mathf.Max(0f, deltaTime);

            if (elapsed < TickIntervalSeconds)
            {
                elapsedByEncounter[encounterId] = elapsed;
                return false;
            }

            elapsedByEncounter[encounterId] = elapsed - TickIntervalSeconds;
            return true;
        }

        public void ResetEncounterTimer(int encounterId)
        {
            if (!elapsedByEncounter.ContainsKey(encounterId))
            {
                return;
            }

            elapsedByEncounter[encounterId] = 0f;
        }
    }
}