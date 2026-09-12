#region

using System.Collections.Generic;
using UnityEngine;

// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable Unity.PerformanceCriticalCodeInvocation

#endregion

namespace Zombera.AI
{
    /// <summary>
    ///     Distributes AI ticks across frames to prevent CPU spikes when many zombies are active.
    /// </summary>
    public sealed class AITickManager : MonoBehaviour
    {
        [SerializeField] private int ticksPerFrame = 20;

        private readonly List<ZombieController> _zombies = new();
        private readonly List<float> _nextTickTimes = new();
        private readonly HashSet<ZombieController> _registeredZombies = new();
        private int _currentIndex;
        // ReSharper disable once MemberCanBePrivate.Global
        public static AITickManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            PruneNullEntries();

            var count = _zombies.Count;
            if (count == 0) return;

            // Round-robin over registered zombies, but only tick those whose
            // per-zombie interval has elapsed. Caps work at ticksPerFrame while
            // never visiting an entry more than once per frame.
            var now = Time.time;
            var ticksProcessed = 0;
            var examined = 0;

            while (ticksProcessed < ticksPerFrame && examined < count)
            {
                if (_currentIndex >= _zombies.Count)
                    _currentIndex = 0;

                var zombie = _zombies[_currentIndex];
                if (zombie != null && now >= _nextTickTimes[_currentIndex] &&
                    zombie.enabled && zombie.gameObject.activeInHierarchy)
                {
                    zombie.ForceTickAI();
                    _nextTickTimes[_currentIndex] = now + Mathf.Max(0.05f, zombie.AITickInterval);
                    ticksProcessed++;
                }

                _currentIndex++;
                examined++;
            }
        }

        // ReSharper disable once UnusedMember.Global
        public void Register(ZombieController zombie)
        {
            if (zombie == null) return;

            if (!_registeredZombies.Add(zombie)) return;

            _zombies.Add(zombie);
            // Stagger the first tick so batches spawned together don't tick in lockstep.
            _nextTickTimes.Add(Time.time + Random.value * Mathf.Max(0.05f, zombie.AITickInterval));
        }

        // ReSharper disable once UnusedMember.Global
        public void Unregister(ZombieController zombie)
        {
            if (zombie == null) return;

            if (!_registeredZombies.Remove(zombie)) return;

            var index = _zombies.IndexOf(zombie);

            if (index != -1)
            {
                _zombies.RemoveAt(index);
                _nextTickTimes.RemoveAt(index);

                // If we removed a zombie at or before the current distribution index,
                // shift the index back to ensure the next zombie in line isn't skipped.
                if (index <= _currentIndex && _currentIndex > 0) _currentIndex--;
            }
        }

        private void PruneNullEntries()
        {
            if (_zombies.Count == 0) return;

            for (var i = _zombies.Count - 1; i >= 0; i--)
            {
                if (_zombies[i] != null) continue;

                _zombies.RemoveAt(i);
                _nextTickTimes.RemoveAt(i);

                if (i <= _currentIndex && _currentIndex > 0) _currentIndex--;
            }

            _registeredZombies.RemoveWhere(static zombie => zombie == null);
        }
    }
}