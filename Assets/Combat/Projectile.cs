using UnityEngine;
using Zombera.Characters;

namespace Zombera.Combat
{
    /// <summary>
    /// Simple projectile shell that moves toward a target and applies damage on impact.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private float maxLifetime = 5f;

        private Transform target;
        private IDamageable targetDamageable;
        private float damage;
        private GameObject source;
        private float lifetime;

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
        }

        private void Update()
        {
            lifetime += Time.deltaTime;

            if (lifetime >= maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 direction = (target.position - transform.position).normalized;
            transform.position += direction * speed * Time.deltaTime;

            // TODO: Replace with proper collision/raycast logic.
            if (Vector3.Distance(transform.position, target.position) <= 0.2f)
            {
                if (targetDamageable != null)
                {
                    DamageSystem.ApplyDamage(targetDamageable, damage, source);
                }
                else
                {
                    UnitHealth unitHealth = target.GetComponent<UnitHealth>();

                    if (unitHealth != null)
                    {
                        DamageSystem.ApplyDamage(unitHealth, damage, source);
                    }
                }

                Destroy(gameObject);
            }
        }
    }
}