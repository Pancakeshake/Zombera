#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

#endregion

namespace Zombera.AI
{
    /// <summary>
    ///     Makes a zombie react to NoiseEvents within hearing range.
    ///     Add alongside ZombieStateMachine on zombie prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseListener : MonoBehaviour
    {
        // Registered at enable so NoiseManager avoids GetComponent per zombie per noise event.
        private static readonly Dictionary<Unit, NoiseListener> ListenersByUnit = new();

        [SerializeField] [Min(0f)] private float hearingRadius = 20f;

        private ZombieStateMachine _stateMachine;
        private Unit _unit;

        public static bool TryGetForUnit(Unit unit, out NoiseListener listener)
        {
            if (unit != null) return ListenersByUnit.TryGetValue(unit, out listener);

            listener = null;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            ListenersByUnit.Clear();
        }

        private void Awake()
        {
            _stateMachine = GetComponent<ZombieStateMachine>();
            _unit = GetComponent<Unit>();
        }

        private void OnEnable()
        {
            if (_unit == null) _unit = GetComponent<Unit>();
            if (_unit != null) ListenersByUnit[_unit] = this;
        }

        private void OnDisable()
        {
            if (_unit != null && ListenersByUnit.TryGetValue(_unit, out var registered) && registered == this)
                ListenersByUnit.Remove(_unit);
        }

        /// <summary>Called by centralized NoiseManager for relevant zombies only.</summary>
        public void ReceiveDirectNoise(NoiseEvent evt)
        {
            OnNoise(evt);
        }

        private void OnNoise(NoiseEvent evt)
        {
            if (_stateMachine == null) return;

            // Already engaged — don't pull out of chase/attack.
            var state = _stateMachine.CurrentState;
            if (state is ZombieState.Chase or ZombieState.Attack) return;

            var distSqr = (evt.Position - transform.position).sqrMagnitude;
            var effectiveRadius = Mathf.Max(0f, Mathf.Min(hearingRadius, evt.Radius));
            if (distSqr > effectiveRadius * effectiveRadius) return;

            _stateMachine.SetInvestigateTarget(evt.Position);
            _stateMachine.SetState(ZombieState.Investigate);
        }
    }
}