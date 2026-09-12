#region

using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.AI
{
    /// <summary>
    ///     On damage, applies a short NavMesh knockback away from the attacker using
    ///     <see cref="UnitController.BeginDodgeStep" />, then pushes nearby zombies (domino-style).
    ///     Requires <see cref="UnitController" /> with a valid NavMeshAgent (same as normal zombie locomotion).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitHealth))]
    [RequireComponent(typeof(UnitController))]
    public sealed class ZombieKnockbackSpread : MonoBehaviour
    {
        private const int OverlapBufferSize = 24;

        [Header("Knockback from hit")]
        [SerializeField] [Min(0f)] private float knockDistancePerDamageUnit = 0.025f;

        [SerializeField] [Min(0f)] private float knockDistanceMin = 0.12f;

        [SerializeField] [Min(0f)] private float knockDistanceMax = 1.1f;

        [SerializeField] [Min(0.05f)] private float knockDurationSeconds = 0.22f;

        [Header("Chain (domino)")]
        [SerializeField] [Min(0f)] private float chainOverlapRadius = 0.85f;

        [SerializeField] [Range(0f, 1f)] private float chainDistanceMultiplier = 0.45f;

        [SerializeField] [Min(0)] private int maxChainDepth = 2;

        [SerializeField] private LayerMask chainPhysicsMask = ~0;

        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        private UnitHealth _health;
        private Unit _unit;
        private UnitController _unitController;

        private void Awake()
        {
            _health = GetComponent<UnitHealth>();
            _unit = GetComponent<Unit>();
            _unitController = GetComponent<UnitController>();
        }

        private void OnEnable()
        {
            if (_health == null) _health = GetComponent<UnitHealth>();
            if (_health != null) _health.DamagedWithSource += OnDamagedWithSource;
        }

        private void OnDisable()
        {
            if (_health != null) _health.DamagedWithSource -= OnDamagedWithSource;
        }

        private void OnDamagedWithSource(float damageAmount, GameObject source)
        {
            if (_health == null || _health.IsDead || damageAmount <= 0f) return;

            if (!IsThisAZombie()) return;

            if (_unitController == null) _unitController = GetComponent<UnitController>();

            if (_unitController == null) return;

            var dir = PlanarAwayFrom(source);
            if (dir.sqrMagnitude < 0.0001f) return;

            var dist = Mathf.Clamp(damageAmount * knockDistancePerDamageUnit, knockDistanceMin, knockDistanceMax);

            ReceiveKnockImpulse(dir, dist, 0);
        }

        private bool IsThisAZombie()
        {
            if (_unit == null) _unit = GetComponent<Unit>();
            if (_unit != null && _unit.Role == UnitRole.Zombie) return true;

            return GetComponent<ZombieController>() != null;
        }

        /// <summary>Push direction is planar; distance is total dodge-step length.</summary>
        private void ReceiveKnockImpulse(Vector3 planarDir, float distance, int chainDepth)
        {
            if (_health == null || _health.IsDead) return;

            if (chainDepth > maxChainDepth) return;

            if (_unitController == null) _unitController = GetComponent<UnitController>();

            if (_unitController == null) return;

            planarDir.y = 0f;
            if (planarDir.sqrMagnitude < 0.0001f) return;

            planarDir.Normalize();

            _unitController.BeginDodgeStep(planarDir, distance, knockDurationSeconds);

            if (chainDepth >= maxChainDepth) return;

            SpreadToNearbyZombies(distance, chainDepth);
        }

        private void SpreadToNearbyZombies(float appliedDistance, int chainDepth)
        {
            var center = transform.position + Vector3.up * 0.35f;
            var radius = Mathf.Max(0.05f, chainOverlapRadius);
            var hitCount = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, chainPhysicsMask,
                QueryTriggerInteraction.Ignore);

            var nextDistance = appliedDistance * chainDistanceMultiplier;
            if (nextDistance < knockDistanceMin * 0.25f) return;

            for (var i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;

                var otherKb = col.GetComponentInParent<ZombieKnockbackSpread>();
                if (otherKb == null || ReferenceEquals(otherKb, this)) continue;

                var otherHealth = otherKb._health;
                if (otherHealth == null || otherHealth.IsDead) continue;

                var toOther = otherKb.transform.position - transform.position;
                toOther.y = 0f;

                if (toOther.sqrMagnitude < 0.01f) continue;

                toOther.Normalize();

                otherKb.ReceiveKnockImpulse(toOther, nextDistance, chainDepth + 1);
            }
        }

        private Vector3 PlanarAwayFrom(GameObject source)
        {
            if (source == null) return Vector3.zero;

            var delta = transform.position - source.transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude >= 0.0001f ? delta.normalized : Vector3.zero;
        }
    }
}
