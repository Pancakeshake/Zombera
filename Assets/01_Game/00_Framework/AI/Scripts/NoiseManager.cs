#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;

// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable Unity.PerformanceCriticalCodeInvocation

#endregion

namespace Zombera.AI
{
    public sealed class NoiseManager : MonoBehaviour
    {
        private readonly List<Unit> _nearbyBuffer = new();
        public static NoiseManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            CoreEventBus.Instance?.Subscribe<NoiseEvent>(OnNoisePublished);
        }

        private void OnDestroy()
        {
            CoreEventBus.Instance?.Unsubscribe<NoiseEvent>(OnNoisePublished);
        }

        private void OnNoisePublished(NoiseEvent evt)
        {
            if (UnitManager.Instance == null) return;

            // Query spatial grid for units within radius
            UnitManager.Instance.FindNearbyUnits(evt.Position, evt.Radius, _nearbyBuffer);

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var unit in _nearbyBuffer)
            {
                if (unit == null || unit.Role != UnitRole.Zombie) continue;

                if (NoiseListener.TryGetForUnit(unit, out var listener)) listener.ReceiveDirectNoise(evt);
            }
        }
    }
}