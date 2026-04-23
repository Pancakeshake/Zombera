using System;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Combat
{
    /// <summary>
    /// Projectile shell that supports homing and ballistic flight with optional stick/embed impacts.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        private enum ProjectileFlightMode
        {
            Homing,
            Ballistic,
        }

        [SerializeField] private float speed = 20f;
        [SerializeField] private float maxLifetime = 5f;
        [SerializeField] private LayerMask collisionMask;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.12f;
        [SerializeField, Min(0f)] private float defaultGravityScale = 1f;
        [SerializeField, Min(0.1f)] private float defaultEmbeddedLifetimeSeconds = 10f;

        [Header("Impact Audio")]
        [SerializeField] private AudioClip enemyBodyHitClip;
        [SerializeField, Range(0f, 1f)] private float enemyBodyHitVolume = 0.9f;

        private Transform target;
        private IDamageable targetDamageable;
        private float damage;
        private GameObject source;
        private float lifetime;
        private UnitStats sourceStats;
        private bool shouldAwardWeightedStrengthXp;
        private bool armedAttack;
        private ProjectileFlightMode flightMode = ProjectileFlightMode.Homing;
        private Vector3 velocity;
        private float gravityScale = 1f;
        private bool stickToDamageableImpact;
        private bool embedOnWorldImpact;
        private float embeddedLifetimeSeconds;
        private bool hasImpacted;
        private float destroyAtTime;

        public void Initialize(Transform targetTransform, float baseDamage, GameObject owner)
        {
            UnitHealth unitHealth = targetTransform != null ? targetTransform.GetComponent<UnitHealth>() : null;
            Initialize(targetTransform, baseDamage, owner, unitHealth);
        }

        public void Initialize(Transform targetTransform, float baseDamage, GameObject owner, IDamageable damageableTarget)
        {
            target = targetTransform;
            damage = baseDamage;
            source = owner;
            targetDamageable = damageableTarget;
            lifetime = 0f;
            sourceStats = null;
            shouldAwardWeightedStrengthXp = false;
            armedAttack = true;
            flightMode = ProjectileFlightMode.Homing;
            velocity = Vector3.zero;
            gravityScale = Mathf.Max(0f, defaultGravityScale);
            stickToDamageableImpact = false;
            embedOnWorldImpact = false;
            embeddedLifetimeSeconds = Mathf.Max(0.1f, defaultEmbeddedLifetimeSeconds);
            hasImpacted = false;
            destroyAtTime = 0f;
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
            sourceStats = attackerStats;
            shouldAwardWeightedStrengthXp = awardWeightedStrengthXp;
            armedAttack = wasArmedAttack;
        }

        public void InitializeArc(
            Transform targetTransform,
            float baseDamage,
            GameObject owner,
            IDamageable damageableTarget,
            UnitStats attackerStats)
        {
            Initialize(targetTransform, baseDamage, owner, damageableTarget);
            sourceStats = attackerStats;
            shouldAwardWeightedStrengthXp = false;
            armedAttack = true;
            flightMode = ProjectileFlightMode.Ballistic;
            gravityScale = Mathf.Max(0f, defaultGravityScale);
            velocity = ResolveBallisticVelocity(targetTransform, 2f, gravityScale);

            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            }
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
            Initialize(targetTransform, baseDamage, owner, damageableTarget, attackerStats, awardWeightedStrengthXp, wasArmedAttack);

            flightMode = ProjectileFlightMode.Ballistic;
            gravityScale = Mathf.Max(0f, gravityMultiplier);
            velocity = ResolveBallisticVelocity(targetTransform, arcHeight, gravityScale);
            stickToDamageableImpact = stickToDamageable;
            embedOnWorldImpact = embedInEnvironment;
            embeddedLifetimeSeconds = Mathf.Max(0.1f, embedLifetimeSeconds);

            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            }
        }

        public void ConfigureImpactAudio(AudioClip bodyHitClip, float bodyHitVolume)
        {
            enemyBodyHitClip = bodyHitClip;
            enemyBodyHitVolume = Mathf.Clamp01(bodyHitVolume);
        }

        private void Update()
        {
            if (hasImpacted)
            {
                if (Time.time >= destroyAtTime)
                {
                    Destroy(gameObject);
                }

                return;
            }

            lifetime += Time.deltaTime;

            if (lifetime >= maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (flightMode == ProjectileFlightMode.Homing && target == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 previousPosition = transform.position;
            Vector3 nextPosition;

            if (flightMode == ProjectileFlightMode.Ballistic)
            {
                velocity += Physics.gravity * gravityScale * Time.deltaTime;
                nextPosition = transform.position + velocity * Time.deltaTime;

                if (velocity.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                }
            }
            else
            {
                Vector3 direction = (target.position - transform.position).normalized;
                float stepDistance = speed * Time.deltaTime;
                nextPosition = transform.position + direction * stepDistance;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                }
            }

            if (TryGetImpact(previousPosition, nextPosition, out RaycastHit hit, out IDamageable impactDamageable))
            {
                if (hit.collider != null)
                {
                    transform.position = hit.point;
                }
                else
                {
                    transform.position = nextPosition;
                }

                HandleImpact(impactDamageable, hit);
                return;
            }

            transform.position = nextPosition;

            if (flightMode == ProjectileFlightMode.Homing
                && target != null
                && Vector3.Distance(transform.position, target.position) <= collisionRadius)
            {
                HandleImpact(targetDamageable, default);
            }
        }

        private bool TryGetImpact(Vector3 start, Vector3 end, out RaycastHit nearestHit, out IDamageable impactDamageable)
        {
            nearestHit = default;
            impactDamageable = null;

            Vector3 travel = end - start;
            float distance = travel.magnitude;
            if (distance <= 0.0001f)
            {
                return false;
            }

            Vector3 direction = travel / distance;
            int mask = collisionMask.value != 0 ? collisionMask.value : Physics.DefaultRaycastLayers;
            RaycastHit[] hits = Physics.SphereCastAll(
                start,
                Mathf.Max(0.01f, collisionRadius),
                direction,
                distance,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, CompareByDistance);

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || IsSourceCollider(hit.collider))
                {
                    continue;
                }

                nearestHit = hit;
                impactDamageable = ResolveDamageable(hit.collider);
                return true;
            }

            return false;
        }

        private void HandleImpact(IDamageable impactDamageable, RaycastHit hit)
        {
            bool hitHasCollider = hit.collider != null;
            IDamageable resolvedDamageable = impactDamageable;

            // Only infer the designated target when this was a proximity impact,
            // not when we explicitly struck world geometry.
            if (!hitHasCollider && resolvedDamageable == null)
            {
                resolvedDamageable = targetDamageable;

                if (resolvedDamageable == null && target != null)
                {
                    resolvedDamageable = target.GetComponent<UnitHealth>();
                }
            }

            bool didDamage = false;
            if (resolvedDamageable != null && !resolvedDamageable.IsDead)
            {
                DamageSystem.ApplyDamage(resolvedDamageable, damage, source);
                didDamage = true;
            }

            if (didDamage && ShouldPlayEnemyBodyHitAudio(resolvedDamageable))
            {
                PlayEnemyBodyHitAudio(hit, resolvedDamageable);
            }

            if (didDamage && shouldAwardWeightedStrengthXp && sourceStats != null)
            {
                sourceStats.RecordWeightedCombatHit(armedAttack);
            }

            bool shouldStickToTarget = stickToDamageableImpact && didDamage;
            bool shouldEmbedInWorld = embedOnWorldImpact && !shouldStickToTarget && hitHasCollider;

            if (shouldStickToTarget || shouldEmbedInWorld)
            {
                Transform parent = shouldStickToTarget
                    ? ResolveImpactParentForDamageable(hit, resolvedDamageable)
                    : hit.collider.transform;

                StickAtImpact(parent, hit);
                return;
            }

            Destroy(gameObject);
        }

        private Transform ResolveImpactParentForDamageable(RaycastHit hit, IDamageable damageable)
        {
            if (hit.collider != null)
            {
                return hit.collider.transform;
            }

            if (damageable is Component damageableComponent)
            {
                return damageableComponent.transform;
            }

            if (target != null)
            {
                return target;
            }

            return null;
        }

        private void StickAtImpact(Transform parent, RaycastHit hit)
        {
            hasImpacted = true;
            destroyAtTime = Time.time + Mathf.Max(0.1f, embeddedLifetimeSeconds);

            if (hit.collider != null)
            {
                transform.position = hit.point;
                if (hit.normal.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
                }
            }

            if (parent != null)
            {
                transform.SetParent(parent, true);
            }

            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null)
            {
                ownCollider.enabled = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private Vector3 ResolveBallisticVelocity(Transform targetTransform, float arcHeight, float gravityMultiplier)
        {
            Vector3 fallbackDirection = transform.forward.sqrMagnitude > 0.0001f
                ? transform.forward.normalized
                : Vector3.forward;

            if (targetTransform == null)
            {
                return fallbackDirection * Mathf.Max(0.1f, speed) + Vector3.up * Mathf.Max(0f, arcHeight);
            }

            Vector3 toTarget = targetTransform.position - transform.position;
            Vector3 planar = new Vector3(toTarget.x, 0f, toTarget.z);
            float planarDistance = planar.magnitude;

            if (planarDistance <= 0.0001f)
            {
                return Vector3.up * Mathf.Max(0.1f, speed + arcHeight);
            }

            float travelTime = Mathf.Max(0.1f, planarDistance / Mathf.Max(0.1f, speed));
            Vector3 planarVelocity = planar / travelTime;

            float verticalVelocity = (toTarget.y / travelTime)
                + 0.5f * Mathf.Abs(Physics.gravity.y) * Mathf.Max(0f, gravityMultiplier) * travelTime
                + Mathf.Max(0f, arcHeight);

            return planarVelocity + Vector3.up * verticalVelocity;
        }

        private bool IsSourceCollider(Collider collider)
        {
            if (collider == null || source == null)
            {
                return false;
            }

            Transform sourceTransform = source.transform;
            Transform colliderTransform = collider.transform;
            return colliderTransform == sourceTransform || colliderTransform.IsChildOf(sourceTransform);
        }

        private static IDamageable ResolveDamageable(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            UnitHealth unitHealth = collider.GetComponentInParent<UnitHealth>();
            if (unitHealth != null)
            {
                return unitHealth;
            }

            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }

        private bool ShouldPlayEnemyBodyHitAudio(IDamageable damageable)
        {
            if (enemyBodyHitClip == null)
            {
                return false;
            }

            if (!(damageable is UnitHealth targetHealth))
            {
                return false;
            }

            Unit targetUnit = targetHealth.GetComponent<Unit>();
            if (targetUnit == null)
            {
                return false;
            }

            Unit sourceUnit = ResolveSourceUnit();
            if (sourceUnit == null)
            {
                return true;
            }

            return UnitFactionUtility.AreHostile(sourceUnit.Faction, targetUnit.Faction);
        }

        private Unit ResolveSourceUnit()
        {
            if (source == null)
            {
                return null;
            }

            Unit sourceUnit = source.GetComponent<Unit>();
            if (sourceUnit != null)
            {
                return sourceUnit;
            }

            return source.GetComponentInParent<Unit>();
        }

        private void PlayEnemyBodyHitAudio(RaycastHit hit, IDamageable damageable)
        {
            Vector3 audioPosition = transform.position;

            if (hit.collider != null)
            {
                audioPosition = hit.point;
            }
            else if (damageable is Component damageableComponent)
            {
                audioPosition = damageableComponent.transform.position;
            }

            AudioSource.PlayClipAtPoint(enemyBodyHitClip, audioPosition, Mathf.Clamp01(enemyBodyHitVolume));
        }

        private static int CompareByDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }
    }
}