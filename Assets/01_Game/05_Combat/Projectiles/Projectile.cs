#region

using UnityEngine;
using UnityEngine.Pool;
using Zombera.Characters;
using Zombera.Factions;

#endregion

namespace Zombera.Combat
{
    /// <summary>
    ///     Projectile shell that supports homing and ballistic flight with optional stick/embed impacts.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private float maxLifetime = 5f;
        [SerializeField] private LayerMask collisionMask;
        [SerializeField] [Min(0.01f)] private float collisionRadius = 0.12f;
        [SerializeField] [Min(0f)] private float defaultGravityScale = 1f;
        [SerializeField] [Min(0.1f)] private float defaultEmbeddedLifetimeSeconds = 10f;

        [Header("Impact Audio")] [SerializeField]
        private AudioClip enemyBodyHitClip;

        [SerializeField] [Range(0f, 1f)] private float enemyBodyHitVolume = 0.9f;

        private readonly RaycastHit[] _sphereCastHits = new RaycastHit[32];
        private bool _armedAttack;
        private float _damage;
        private float _destroyAtTime;
        private float _embeddedLifetimeSeconds;
        private bool _embedOnWorldImpact;
        private ProjectileFlightMode _flightMode = ProjectileFlightMode.Homing;
        private float _gravityScale = 1f;
        private bool _hasImpacted;
        private float _lifetime;
        private bool _shouldAwardWeightedStrengthXp;
        private GameObject _source;
        private UnitStats _sourceStats;
        private bool _stickToDamageableImpact;
        private bool _isDespawning;
        private IObjectPool<Projectile> _owningPool;

        private Transform _target;
        private IDamageable _targetDamageable;
        private Vector3 _velocity;

        private void Update()
        {
            if (_hasImpacted)
            {
                if (Time.time >= _destroyAtTime) Despawn();

                return;
            }

            _lifetime += Time.deltaTime;

            if (_lifetime >= maxLifetime || (_flightMode == ProjectileFlightMode.Homing && _target == null))
            {
                Despawn();
                return;
            }

            var previousPosition = transform.position;
            Vector3 nextPosition;

            if (_flightMode == ProjectileFlightMode.Ballistic)
            {
                var scaledGravity = Physics.gravity * _gravityScale;
                _velocity += scaledGravity * Time.deltaTime;
                nextPosition = transform.position + _velocity * Time.deltaTime;

                if (_velocity.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);
            }
            else
            {
                var direction = (_target.position - transform.position).normalized;
                var stepDistance = speed * Time.deltaTime;
                nextPosition = transform.position + direction * stepDistance;

                if (direction.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }

            if (TryGetImpact(previousPosition, nextPosition, out var hit, out var impactDamageable))
            {
                transform.position = hit.collider != null ? hit.point : nextPosition;

                HandleImpact(impactDamageable, hit);
                return;
            }

            transform.position = nextPosition;

            if (_flightMode != ProjectileFlightMode.Homing
                || _target == null
                || Vector3.Distance(transform.position, _target.position) > collisionRadius)
                return;

            HandleImpact(_targetDamageable, default);
        }

        public void Initialize(Transform targetTransform, float baseDamage, GameObject owner)
        {
            var unitHealth = targetTransform != null ? targetTransform.GetComponent<UnitHealth>() : null;
            Initialize(targetTransform, baseDamage, owner, unitHealth);
        }

        public void Initialize(Transform targetTransform, float baseDamage, GameObject owner,
            IDamageable damageableTarget)
        {
            _isDespawning = false;
            _target = targetTransform;
            _damage = baseDamage;
            _source = owner;
            _targetDamageable = damageableTarget;
            _lifetime = 0f;
            _sourceStats = null;
            _shouldAwardWeightedStrengthXp = false;
            _armedAttack = true;
            _flightMode = ProjectileFlightMode.Homing;
            _velocity = Vector3.zero;
            _gravityScale = Mathf.Max(0f, defaultGravityScale);
            _stickToDamageableImpact = false;
            _embedOnWorldImpact = false;
            _embeddedLifetimeSeconds = Mathf.Max(0.1f, defaultEmbeddedLifetimeSeconds);
            _hasImpacted = false;
            _destroyAtTime = 0f;
        }

        public void PrepareForPoolReuse(IObjectPool<Projectile> owningPool)
        {
            _owningPool = owningPool;
            _isDespawning = false;
            _hasImpacted = false;
            _destroyAtTime = 0f;
            _lifetime = 0f;
            _target = null;
            _targetDamageable = null;
            _source = null;
            _sourceStats = null;
            _velocity = Vector3.zero;

            var ownCollider = GetComponent<Collider>();
            if (ownCollider != null) ownCollider.enabled = true;

            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        public void Initialize(
            Transform targetTransform,
            float baseDamage,
            GameObject owner,
            IDamageable damageableTarget,
            UnitStats attackerStats,
            bool awardWeightedStrengthXp,
            bool wasArmedAttack)
        {
            Initialize(targetTransform, baseDamage, owner, damageableTarget);
            _sourceStats = attackerStats;
            _shouldAwardWeightedStrengthXp = awardWeightedStrengthXp;
            _armedAttack = wasArmedAttack;
        }

        public void InitializeArc(
            Transform targetTransform,
            float baseDamage,
            GameObject owner,
            IDamageable damageableTarget,
            UnitStats attackerStats)
        {
            Initialize(targetTransform, baseDamage, owner, damageableTarget);
            _sourceStats = attackerStats;
            _shouldAwardWeightedStrengthXp = false;
            _armedAttack = true;
            _flightMode = ProjectileFlightMode.Ballistic;
            _gravityScale = Mathf.Max(0f, defaultGravityScale);
            _velocity = ResolveBallisticVelocity(targetTransform, 2f, _gravityScale);

            if (_velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);
        }

        public void InitializeBallistic(
            Transform targetTransform,
            float baseDamage,
            GameObject owner,
            IDamageable damageableTarget,
            UnitStats attackerStats,
            bool awardWeightedStrengthXp,
            bool wasArmedAttack,
            float arcHeight,
            float gravityMultiplier,
            bool stickToDamageable,
            bool embedInEnvironment,
            float embedLifetimeSeconds)
        {
            Initialize(targetTransform, baseDamage, owner, damageableTarget, attackerStats, awardWeightedStrengthXp,
                wasArmedAttack);

            _flightMode = ProjectileFlightMode.Ballistic;
            _gravityScale = Mathf.Max(0f, gravityMultiplier);
            _velocity = ResolveBallisticVelocity(targetTransform, arcHeight, _gravityScale);
            _stickToDamageableImpact = stickToDamageable;
            _embedOnWorldImpact = embedInEnvironment;
            _embeddedLifetimeSeconds = Mathf.Max(0.1f, embedLifetimeSeconds);

            if (_velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);
        }

        public void ConfigureImpactAudio(AudioClip bodyHitClip, float bodyHitVolume)
        {
            enemyBodyHitClip = bodyHitClip;
            enemyBodyHitVolume = Mathf.Clamp01(bodyHitVolume);
        }

        private bool TryGetImpact(Vector3 start, Vector3 end, out RaycastHit nearestHit,
            out IDamageable impactDamageable)
        {
            nearestHit = default;
            impactDamageable = null;

            var travel = end - start;
            var distance = travel.magnitude;
            if (distance <= 0.0001f) return false;

            var direction = travel / distance;
            var mask = collisionMask.value != 0 ? collisionMask.value : Physics.DefaultRaycastLayers;
            var hitCount = Physics.SphereCastNonAlloc(
                start,
                Mathf.Max(0.01f, collisionRadius),
                direction,
                _sphereCastHits,
                distance,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0) return false;

            var hasBestHit = false;
            var bestHit = default(RaycastHit);
            var bestDistance = float.MaxValue;
            var processedHits = 0;
            foreach (var hit in _sphereCastHits)
            {
                if (processedHits++ >= hitCount) break;
                if (hit.collider == null || IsSourceCollider(hit.collider)) continue;

                if (hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                bestHit = hit;
                hasBestHit = true;
            }

            if (!hasBestHit) return false;

            nearestHit = bestHit;
            impactDamageable = ResolveDamageable(nearestHit.collider);
            return true;
        }

        private void HandleImpact(IDamageable impactDamageable, RaycastHit hit)
        {
            var hitHasCollider = hit.collider != null;
            var resolvedDamageable = impactDamageable;

            // Only infer the designated target when this was a proximity impact,
            // not when we explicitly struck world geometry.
            if (!hitHasCollider && resolvedDamageable == null)
                resolvedDamageable = _targetDamageable ?? _target?.GetComponent<UnitHealth>();

            var didDamage = false;
            if (resolvedDamageable is { IsDead: false })
            {
                DamageSystem.ApplyDamage(resolvedDamageable, _damage, _sourceStats, DamageType.Ranged, _source);
                didDamage = true;
            }

            if (didDamage && ShouldPlayEnemyBodyHitAudio(resolvedDamageable))
                PlayEnemyBodyHitAudio(hit, resolvedDamageable);

            if (didDamage && _shouldAwardWeightedStrengthXp && _sourceStats != null)
                _sourceStats.RecordWeightedCombatHit(_armedAttack);

            var shouldStickToTarget = _stickToDamageableImpact && didDamage;
            var shouldEmbedInWorld = _embedOnWorldImpact && !shouldStickToTarget && hitHasCollider;

            if (!shouldStickToTarget && !shouldEmbedInWorld)
            {
                Despawn();
                return;
            }

            var parent = shouldStickToTarget
                ? ResolveImpactParentForDamageable(hit, resolvedDamageable)
                : hit.collider.transform;

            StickAtImpact(parent, hit);
        }

        private Transform ResolveImpactParentForDamageable(RaycastHit hit, IDamageable damageable)
        {
            if (hit.collider is { } hitCollider) return hitCollider.transform;

            if (damageable is Component damageableComponent) return damageableComponent.transform;

            return _target;
        }

        private void StickAtImpact(Transform parent, RaycastHit hit)
        {
            _hasImpacted = true;
            _destroyAtTime = Time.time + Mathf.Max(0.1f, _embeddedLifetimeSeconds);

            if (hit.collider != null)
            {
                transform.position = hit.point;
                if (hit.normal.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
            }

            if (parent != null) transform.SetParent(parent, true);

            var ownCollider = GetComponent<Collider>();
            if (ownCollider != null) ownCollider.enabled = false;

            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private Vector3 ResolveBallisticVelocity(Transform targetTransform, float arcHeight, float gravityMultiplier)
        {
            var fallbackDirection = transform.forward.sqrMagnitude > 0.0001f
                ? transform.forward.normalized
                : Vector3.forward;

            if (targetTransform == null)
                return fallbackDirection * Mathf.Max(0.1f, speed) + Vector3.up * Mathf.Max(0f, arcHeight);

            var toTarget = targetTransform.position - transform.position;
            var planar = new Vector3(toTarget.x, 0f, toTarget.z);
            var planarDistance = planar.magnitude;

            if (planarDistance <= 0.0001f) return Vector3.up * Mathf.Max(0.1f, speed + arcHeight);

            var travelTime = Mathf.Max(0.1f, planarDistance / Mathf.Max(0.1f, speed));
            var planarVelocity = planar / travelTime;

            var verticalVelocity = toTarget.y / travelTime
                                   + 0.5f * Mathf.Abs(Physics.gravity.y) * Mathf.Max(0f, gravityMultiplier) * travelTime
                                   + Mathf.Max(0f, arcHeight);

            return planarVelocity + Vector3.up * verticalVelocity;
        }

        private bool IsSourceCollider(Collider hitCollider)
        {
            if (hitCollider == null || _source == null) return false;

            var sourceTransform = _source.transform;
            var colliderTransform = hitCollider.transform;
            return colliderTransform == sourceTransform || colliderTransform.IsChildOf(sourceTransform);
        }

        private static IDamageable ResolveDamageable(Collider hitCollider)
        {
            if (hitCollider == null) return null;

            // Try fast component check first.
            if (hitCollider.TryGetComponent(out IDamageable damageable))
                return damageable;

            // Then check parent hierarchy.
            var unitHealth = hitCollider.GetComponentInParent<UnitHealth>();
            if (unitHealth != null) return unitHealth;

            return hitCollider.GetComponentInParent<IDamageable>();
        }

        private bool ShouldPlayEnemyBodyHitAudio(IDamageable damageable)
        {
            if (enemyBodyHitClip == null) return false;

            if (damageable is not UnitHealth targetHealth
                || targetHealth.GetComponent<Unit>() is not { } targetUnit)
                return false;

            var sourceUnit = ResolveSourceUnit();
            return sourceUnit == null || FactionManager.AreUnitsHostile(sourceUnit, targetUnit);
        }

        private Unit ResolveSourceUnit()
        {
            if (_source == null) return null;

            return _source.GetComponent<Unit>() ?? _source.GetComponentInParent<Unit>();
        }

        private void PlayEnemyBodyHitAudio(RaycastHit hit, IDamageable damageable)
        {
            var audioPosition = transform.position;
            if (hit.collider != null)
                audioPosition = hit.point;
            else if (damageable is Component damageableComponent)
                audioPosition = damageableComponent.transform.position;

            AudioSource.PlayClipAtPoint(enemyBodyHitClip, audioPosition, Mathf.Clamp01(enemyBodyHitVolume));
        }

        private void Despawn()
        {
            if (_isDespawning) return;

            _isDespawning = true;
            if (_owningPool != null)
            {
                _owningPool.Release(this);
                return;
            }

            Destroy(gameObject);
        }

        private enum ProjectileFlightMode
        {
            Homing,
            Ballistic
        }
    }
}