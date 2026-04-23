using UnityEngine;
using Zombera.Core;

namespace Zombera.AI
{
    /// <summary>
    /// Makes a zombie react to NoiseEvents within hearing range.
    /// Add alongside ZombieStateMachine on zombie prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseListener : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float hearingRadius = 20f;

        private ZombieStateMachine stateMachine;

        private void Awake()
        {
            stateMachine = GetComponent<ZombieStateMachine>();
        }

        private void Start()
        {
            EventSystem.Instance?.Subscribe<NoiseEvent>(OnNoise);
        }

        private void OnDestroy()
        {
            EventSystem.Instance?.Unsubscribe<NoiseEvent>(OnNoise);
        }

        private void OnNoise(NoiseEvent evt)
        {
            if (stateMachine == null) return;

            // Already engaged — don't pull out of chase/attack.
            ZombieState state = stateMachine.CurrentState;
            if (state == ZombieState.Chase || state == ZombieState.Attack) return;

            float distSqr = (evt.Position - transform.position).sqrMagnitude;
            float effectiveRadius = Mathf.Max(0f, Mathf.Min(hearingRadius, evt.Radius));
            if (distSqr > effectiveRadius * effectiveRadius) return;

            stateMachine.SetInvestigateTarget(evt.Position);
            stateMachine.SetState(ZombieState.Investigate);
        }
    }
}
