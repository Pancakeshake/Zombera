using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Data;
using Zombera.Systems;

namespace Zombera.Combat
{
    /// <summary>
    /// Handles weapon equip/use flow for firearms, bows, melee, and throwables.
    /// </summary>
    public sealed class WeaponSystem : MonoBehaviour
    {
        [SerializeField] private WeaponData equippedWeapon;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Projectile projectilePrefab;
        [Header("Close-Range Hit Validation")]
        [SerializeField] private bool requireFacingForCloseRangeHits = true;
        [SerializeField, Range(0f, 180f)] private float closeRangeFacingAngleDegrees = 65f;
        [SerializeField, Min(0.1f)] private float unarmedRange = 2.25f;
        [SerializeField, Min(0.1f)] private float meleeRange = 1.5f;

        [Header("Knockback")]
        [SerializeField, Min(0f)] private float knockbackForce = 6f;

        [Header("Bow Projectile")]
        [SerializeField, Min(0f)] private float bowProjectileArcHeight = 1.2f;
        [SerializeField, Min(0.1f)] private float bowProjectileGravityScale = 1.35f;
        [SerializeField, Min(0.1f)] private float bowProjectileEmbeddedLifetimeSeconds = 18f;
        [SerializeField] private bool bowProjectilesStickToTargets = true;
        [SerializeField] private bool bowProjectilesEmbedInEnvironment = true;
        [SerializeField, Min(1f)] private float bowMaximumRangeMeters = 100f;
        [SerializeField, Range(0f, 1f)] private float bowHitChanceAtMaxRange = 0.35f;
        [SerializeField] private GameObject bowArrowVisualPrefab;
        [SerializeField] private bool preferBowArrowVisualPrefab = true;
        [SerializeField] private bool enableRuntimeBowProjectileFallback = true;
        [SerializeField, Min(0.01f)] private float runtimeBowProjectileThickness = 0.025f;
        [SerializeField, Min(0.05f)] private float runtimeBowProjectileLength = 0.35f;
        [SerializeField] private Color runtimeBowProjectileColor = new Color(0.58f, 0.42f, 0.24f, 1f);

        [Header("Bow Audio")]
        [SerializeField] private AudioClip bowReleaseArrowClip;
        [SerializeField, Range(0f, 1f)] private float bowReleaseArrowVolume = 0.9f;
        [SerializeField] private AudioClip arrowHitBodyClip;
        [SerializeField, Range(0f, 1f)] private float arrowHitBodyVolume = 0.9f;

    #if UNITY_EDITOR
        private const string DefaultBowArrowVisualPrefabPath = "Assets/ThirdParty/Free medieval weapons/Prefabs/Arrow.prefab";
        private const string DefaultBowReleaseArrowClipPath = "Assets/Universal Sound FX/WEAPONS/Bow_Arrow/BOW_Release_Arrow_mono.wav";
        private const string DefaultArrowHitBodyClipPath = "Assets/Universal Sound FX/WEAPONS/Bow_Arrow/ARROW_Hit_Body_mono.wav";
    #endif

        private static readonly string[] FallbackMuzzleSearchNames =
        {
            "Muzzle",
            "Socket_RightHand",
            "RightHand",
            "Hand_R",
            "mixamorig:RightHand"
        };

        private Unit ownerUnit;
        private UnitStats ownerStats;
        private UnitInventory ownerInventory;
        private FogOfWarVisionSource ownerVisionSource;
        private Material runtimeBowProjectileMaterial;
        private int currentAmmo;

        public WeaponData EquippedWeapon => equippedWeapon;
        public int CurrentAmmo => currentAmmo;
        public int MagazineSize => equippedWeapon != null ? equippedWeapon.magazineSize : 0;
        public bool NeedsReload => equippedWeapon != null && equippedWeapon.magazineSize > 0 && currentAmmo <= 0;
        public bool IsRangedWeaponEquipped => equippedWeapon != null && IsRangedCategory(equippedWeapon.weaponCategory);
        public bool IsBowEquipped => equippedWeapon != null && equippedWeapon.weaponCategory == WeaponCategory.Bow;
        public Unit OwnerUnit => ownerUnit;
        public UnitStats OwnerStats => ownerStats;

        private void Awake()
        {
            ResolveOwnerReferences();
            EnsureMuzzlePoint();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (bowArrowVisualPrefab == null)
            {
                bowArrowVisualPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBowArrowVisualPrefabPath);
            }

            if (bowReleaseArrowClip == null)
            {
                bowReleaseArrowClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultBowReleaseArrowClipPath);
            }

            if (arrowHitBodyClip == null)
            {
                arrowHitBodyClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultArrowHitBodyClipPath);
            }
        }
#endif

        public void EquipWeapon(WeaponData weaponData)
        {
            equippedWeapon = weaponData;
            currentAmmo = weaponData != null ? weaponData.magazineSize : 0;

            // Notify the animator so equip/holster blends play on the owning unit.
            Animator animator = GetComponentInChildren<Animator>();

            if (animator != null)
            {
                bool hasWeapon = weaponData != null;
                if (HasAnimatorParameter(animator, "HasWeapon", AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool("HasWeapon", hasWeapon);
                }

                if (hasWeapon && HasAnimatorParameter(animator, "Equip", AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger("Equip");
                }
            }
        }

        private static bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType expectedType)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == expectedType && string.Equals(parameter.name, parameterName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryAttackTarget(IDamageable target)
        {
            ResolveOwnerReferences();

            if (target == null || target.IsDead)
            {
                return false;
            }

            if (equippedWeapon == null)
            {
                // Unarmed fallback: bare-hands melee at close range.
                return TryApplyCloseRangeHit(target, 10f, unarmedRange, armedAttack: false);
            }

            switch (equippedWeapon.weaponCategory)
            {
                case WeaponCategory.Pistol:
                case WeaponCategory.Rifle:
                case WeaponCategory.Shotgun:
                case WeaponCategory.Bow:
                    return FireProjectileAt(target);
                case WeaponCategory.Melee:
                    bool meleeHit = TryApplyCloseRangeHit(target, equippedWeapon.baseDamage, ResolveMeleeRange(), armedAttack: true);
                    if (meleeHit)
                    {
                        EventSystem.PublishGlobal(new NoiseEvent
                        {
                            Position = transform.position,
                            Radius = meleeRange * 3f,
                            NoiseType = NoiseType.Generic,
                            Source = gameObject
                        });
                    }
                    return meleeHit;
                case WeaponCategory.Throwable:
                    ThrowAt(target);
                    break;
            }

            return true;
        }

        public bool TryAttackTarget(UnitHealth target)
        {
            return TryAttackTarget((IDamageable)target);
        }

        public void Reload()
        {
            if (equippedWeapon != null && equippedWeapon.magazineSize > 0)
            {
                currentAmmo = equippedWeapon.magazineSize;
            }
        }

        private bool FireProjectileAt(IDamageable target)
        {
            bool isBowShot = equippedWeapon != null && equippedWeapon.weaponCategory == WeaponCategory.Bow;
            bool bowCanDamageTarget = true;

            // Ranged weapons consume one round per shot; block if empty.
            if (equippedWeapon != null && equippedWeapon.magazineSize > 0)
            {
                if (currentAmmo <= 0) return false;
                currentAmmo--;
            }

            float scaledDamage = ResolveRangedScaledDamage(equippedWeapon.baseDamage);
            GameObject sourceObject = ResolveDamageSourceObject();

            // Emit a noise event so nearby zombies investigate the shot.
            EventSystem.PublishGlobal(new NoiseEvent
            {
                Position = transform.position,
                Radius = equippedWeapon.effectiveRange > 0f
                    ? equippedWeapon.effectiveRange * (isBowShot ? 1.1f : 2f)
                    : (isBowShot ? 14f : 30f),
                NoiseType = isBowShot ? NoiseType.Generic : NoiseType.Gunshot,
                Source = sourceObject
            });

            // Range-accuracy check:
            // - Bow: 0..maxRange always allowed with linear distance falloff in hit chance.
            // - Other ranged: retain effective-range over-penalty behavior.
            if (target is Component rangeCheckComponent)
            {
                float dist = Vector3.Distance(transform.position, rangeCheckComponent.transform.position);

                if (isBowShot)
                {
                    float maxRange = ResolveBowMaximumRangeMeters();
                    if (dist > maxRange)
                    {
                        bowCanDamageTarget = false;
                    }

                    if (bowCanDamageTarget)
                    {
                        float hitChance = ResolveBowDistanceHitChance(dist, maxRange);
                        if (UnityEngine.Random.value > hitChance)
                        {
                            bowCanDamageTarget = false;
                        }
                    }
                }
                else
                {
                    float effectiveRange = GetEffectiveRangedRange();
                    if (dist > effectiveRange)
                    {
                        float overRangeFraction = (dist - effectiveRange) / Mathf.Max(1f, effectiveRange);
                        float hitChancePenalty = Mathf.Clamp01(overRangeFraction);
                        float skillBonus = ownerStats != null ? ownerStats.GetShootingHitChanceBonus() : 0f;
                        float hitChance = Mathf.Clamp01(1f - hitChancePenalty + skillBonus);
                        if (UnityEngine.Random.value > hitChance)
                        {
                            return false; // legacy non-bow miss behavior
                        }
                    }
                }
            }

            if (isBowShot)
            {
                TryPlayBowReleaseAudio();
            }

            if (!(target is Component targetComponent))
            {
                if (isBowShot && !bowCanDamageTarget)
                {
                    return true;
                }

                DamageSystem.ApplyDamage(target, scaledDamage, sourceObject);
                TryAwardWeightedCombatStrengthXp(armedAttack: true);
                ownerStats?.RecordRangedHit();
                return true;
            }

            ResolveProjectileSpawnPose(out Vector3 projectileSpawnPosition, out Quaternion projectileSpawnRotation);

            Projectile projectile = null;
            if (isBowShot)
            {
                projectile = CreateBowProjectileInstance(projectileSpawnPosition, projectileSpawnRotation);
            }
            else if (projectilePrefab != null)
            {
                projectile = Instantiate(projectilePrefab, projectileSpawnPosition, projectileSpawnRotation);
            }

            if (projectile == null)
            {
                if (isBowShot && !bowCanDamageTarget)
                {
                    return true;
                }

                DamageSystem.ApplyDamage(target, scaledDamage, sourceObject);
                TryAwardWeightedCombatStrengthXp(armedAttack: true);
                ownerStats?.RecordRangedHit();
                return true;
            }

            if (isBowShot)
            {
                ConfigureBowProjectileAudio(projectile);
            }

            if (isBowShot)
            {
                if (bowCanDamageTarget)
                {
                    projectile.InitializeBallistic(
                        targetComponent.transform,
                        scaledDamage,
                        sourceObject,
                        target,
                        ownerStats,
                        ShouldAwardWeightedStrengthXp(),
                        true,
                        bowProjectileArcHeight,
                        bowProjectileGravityScale,
                        bowProjectilesStickToTargets,
                        bowProjectilesEmbedInEnvironment,
                        bowProjectileEmbeddedLifetimeSeconds);
                }
                else
                {
                    projectile.InitializeBallistic(
                        null,
                        0f,
                        sourceObject,
                        null,
                        ownerStats,
                        false,
                        true,
                        bowProjectileArcHeight,
                        bowProjectileGravityScale,
                        false,
                        bowProjectilesEmbedInEnvironment,
                        bowProjectileEmbeddedLifetimeSeconds);
                }
            }
            else
            {
                projectile.Initialize(targetComponent.transform, scaledDamage, sourceObject, target, ownerStats, ShouldAwardWeightedStrengthXp(), true);
            }

            if (!isBowShot || bowCanDamageTarget)
            {
                ownerStats?.RecordRangedHit();
            }

            return true;
        }

        private float ResolveBowDistanceHitChance(float distanceMeters, float maxRange)
        {
            float normalizedDistance = Mathf.Clamp01(distanceMeters / Mathf.Max(1f, maxRange));
            float minChanceAtMaxRange = Mathf.Clamp01(bowHitChanceAtMaxRange);
            float distanceChance = Mathf.Lerp(1f, minChanceAtMaxRange, normalizedDistance);
            float skillBonus = ownerStats != null ? ownerStats.GetShootingHitChanceBonus() : 0f;
            return Mathf.Clamp01(distanceChance + skillBonus);
        }

        private float ResolveBowMaximumRangeMeters()
        {
            float configuredMaxRange = Mathf.Max(1f, bowMaximumRangeMeters);

            ResolveOwnerReferences();
            if (ownerUnit == null || ownerUnit.Role != UnitRole.Player)
            {
                return configuredMaxRange;
            }

            if (ownerVisionSource == null)
            {
                ownerVisionSource = ownerUnit.GetComponent<FogOfWarVisionSource>();
                if (ownerVisionSource == null)
                {
                    ownerVisionSource = ownerUnit.GetComponentInChildren<FogOfWarVisionSource>();
                }
            }

            if (ownerVisionSource == null)
            {
                return configuredMaxRange;
            }

            float perceptionMaxRange = Mathf.Max(1f, ownerVisionSource.MaximumVisionRangeMeters);
            return perceptionMaxRange;
        }

        private void ResolveProjectileSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            Transform resolvedMuzzlePoint = EnsureMuzzlePoint();
            if (resolvedMuzzlePoint != null)
            {
                spawnPosition = resolvedMuzzlePoint.position;
                spawnRotation = resolvedMuzzlePoint.rotation;
                return;
            }

            Vector3 forward = transform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            spawnPosition = transform.position + forward * 0.45f + Vector3.up * 1.35f;
            spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private Transform EnsureMuzzlePoint()
        {
            if (muzzlePoint != null)
            {
                return muzzlePoint;
            }

            for (int i = 0; i < FallbackMuzzleSearchNames.Length; i++)
            {
                Transform match = FindChildRecursiveIgnoreCase(transform, FallbackMuzzleSearchNames[i]);
                if (match != null)
                {
                    muzzlePoint = match;
                    return muzzlePoint;
                }
            }

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    muzzlePoint = hand;
                    return muzzlePoint;
                }
            }

            return null;
        }

        private static Transform FindChildRecursiveIgnoreCase(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform candidate = children[i];
                if (candidate != null && string.Equals(candidate.name, childName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private Projectile CreateRuntimeBowProjectileFallback(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projectileObject.name = "RuntimeBowProjectile";
            projectileObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            projectileObject.transform.localScale = new Vector3(
                Mathf.Max(0.01f, runtimeBowProjectileThickness),
                Mathf.Max(0.01f, runtimeBowProjectileThickness),
                Mathf.Max(0.05f, runtimeBowProjectileLength));

            Collider projectileCollider = projectileObject.GetComponent<Collider>();
            if (projectileCollider != null)
            {
                Destroy(projectileCollider);
            }

            Material material = GetRuntimeBowProjectileMaterial();

            MeshRenderer renderer = projectileObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
                else
                {
                    renderer.material.color = runtimeBowProjectileColor;
                }
            }

            TrailRenderer trail = projectileObject.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.startWidth = Mathf.Max(0.005f, runtimeBowProjectileThickness * 0.9f);
            trail.endWidth = Mathf.Max(0.001f, runtimeBowProjectileThickness * 0.15f);
            trail.minVertexDistance = 0.01f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Color startColor = runtimeBowProjectileColor;
            startColor.a = 0.85f;
            Color endColor = runtimeBowProjectileColor;
            endColor.a = 0f;
            trail.startColor = startColor;
            trail.endColor = endColor;

            if (material != null)
            {
                trail.sharedMaterial = material;
            }

            return projectileObject.AddComponent<Projectile>();
        }

        private Projectile CreateBowProjectileInstance(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (preferBowArrowVisualPrefab)
            {
                Projectile projectileFromVisualPrefab = TryCreateBowProjectileFromVisualPrefab(spawnPosition, spawnRotation);
                if (projectileFromVisualPrefab != null)
                {
                    return projectileFromVisualPrefab;
                }
            }

            if (projectilePrefab != null)
            {
                return Instantiate(projectilePrefab, spawnPosition, spawnRotation);
            }

            if (!preferBowArrowVisualPrefab)
            {
                Projectile projectileFromVisualPrefab = TryCreateBowProjectileFromVisualPrefab(spawnPosition, spawnRotation);
                if (projectileFromVisualPrefab != null)
                {
                    return projectileFromVisualPrefab;
                }
            }

            if (enableRuntimeBowProjectileFallback)
            {
                return CreateRuntimeBowProjectileFallback(spawnPosition, spawnRotation);
            }

            return null;
        }

        private Projectile TryCreateBowProjectileFromVisualPrefab(Vector3 spawnPosition, Quaternion spawnRotation)
        {
#if UNITY_EDITOR
            if (bowArrowVisualPrefab == null)
            {
                bowArrowVisualPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBowArrowVisualPrefabPath);
            }
#endif

            if (bowArrowVisualPrefab == null)
            {
                return null;
            }

            GameObject projectileObject = Instantiate(bowArrowVisualPrefab, spawnPosition, spawnRotation);
            if (projectileObject == null)
            {
                return null;
            }

            Projectile projectile = projectileObject.GetComponent<Projectile>();
            if (projectile == null)
            {
                projectile = projectileObject.AddComponent<Projectile>();
            }

            return projectile;
        }

        private void TryPlayBowReleaseAudio()
        {
            if (bowReleaseArrowClip == null)
            {
                return;
            }

            Transform resolvedMuzzlePoint = EnsureMuzzlePoint();
            Vector3 audioPosition = resolvedMuzzlePoint != null ? resolvedMuzzlePoint.position : transform.position;
            AudioSource.PlayClipAtPoint(bowReleaseArrowClip, audioPosition, Mathf.Clamp01(bowReleaseArrowVolume));
        }

        private void ConfigureBowProjectileAudio(Projectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.ConfigureImpactAudio(arrowHitBodyClip, Mathf.Clamp01(arrowHitBodyVolume));
        }

        private Material GetRuntimeBowProjectileMaterial()
        {
            if (runtimeBowProjectileMaterial != null)
            {
                return runtimeBowProjectileMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            runtimeBowProjectileMaterial = new Material(shader)
            {
                color = runtimeBowProjectileColor
            };
            return runtimeBowProjectileMaterial;
        }

        private void ThrowAt(IDamageable target)
        {
            if (target == null)
            {
                return;
            }

            GameObject sourceObject = ResolveDamageSourceObject();

            if (projectilePrefab == null || muzzlePoint == null)
            {
                // No throwable prefab configured: fall back to instant-hit.
                DamageSystem.ApplyDamage(target, equippedWeapon?.baseDamage ?? 5f, sourceObject);
                return;
            }

            if (!(target is Component targetComponent))
            {
                DamageSystem.ApplyDamage(target, equippedWeapon?.baseDamage ?? 5f, sourceObject);
                return;
            }

            // Spawn the throwable and initialize with an arc trajectory.
            Projectile thrown = Instantiate(projectilePrefab, muzzlePoint.position, Quaternion.identity);
            thrown.InitializeArc(targetComponent.transform, equippedWeapon?.baseDamage ?? 5f, sourceObject, target, ownerStats);
        }

        private bool TryApplyCloseRangeHit(IDamageable target, float damage, float maxRange, bool armedAttack)
        {
            float scaledDamage = ResolveMeleeScaledDamage(damage);
            GameObject sourceObject = ResolveDamageSourceObject();

            if (!(target is Component targetComponent))
            {
                DamageSystem.ApplyDamage(target, scaledDamage, DamageType.Melee, sourceObject);
                TryAwardWeightedCombatStrengthXp(armedAttack);
                return true;
            }

            Vector3 toTarget = targetComponent.transform.position - transform.position;
            toTarget.y = 0f;

            float effectiveRange = Mathf.Max(0.1f, maxRange);
            if (toTarget.sqrMagnitude > effectiveRange * effectiveRange)
            {
                return true;
            }

            if (requireFacingForCloseRangeHits && !IsFacingTarget(toTarget))
            {
                return true;
            }

            DamageSystem.ApplyDamage(target, scaledDamage, DamageType.Melee, sourceObject);
            TryAwardWeightedCombatStrengthXp(armedAttack);
            TryApplyKnockback(target);
            return true;
        }

        private void TryApplyKnockback(IDamageable target)
        {
            if (ownerStats == null || !(target is Component targetComp))
            {
                return;
            }

            float knockbackChance = ownerStats.GetMeleeKnockbackChance() + ownerStats.GetStrengthKnockbackChanceBonus();

            if (Random.value > knockbackChance)
            {
                return;
            }

            Rigidbody rb = targetComp.GetComponent<Rigidbody>();

            if (rb == null || rb.isKinematic)
            {
                return;
            }

            Vector3 dir = (targetComp.transform.position - transform.position);
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                rb.AddForce(dir.normalized * knockbackForce, ForceMode.Impulse);
            }
        }

        private float ResolveStrengthScaledDamage(float baseDamage)
        {
            ResolveOwnerReferences();

            if (ownerStats == null) return Mathf.Max(0f, baseDamage);
            return ownerStats.ApplyStrengthDamageScaling(baseDamage);
        }

        private float ResolveRangedScaledDamage(float baseDamage)
        {
            ResolveOwnerReferences();

            if (ownerStats == null) return Mathf.Max(0f, baseDamage);
            return ownerStats.ApplyShootingDamageScaling(baseDamage);
        }

        private float ResolveMeleeScaledDamage(float baseDamage)
        {
            ResolveOwnerReferences();

            if (ownerStats == null) return Mathf.Max(0f, baseDamage);
            return ownerStats.ApplyMeleeDamageScaling(baseDamage);
        }

        private bool ShouldAwardWeightedStrengthXp()
        {
            ResolveOwnerReferences();

            if (ownerStats == null || ownerInventory == null)
            {
                return false;
            }

            return ownerStats.IsHeavyCarry(ownerInventory.CarryRatio);
        }

        private void TryAwardWeightedCombatStrengthXp(bool armedAttack)
        {
            ResolveOwnerReferences();

            if (ownerStats == null || !ShouldAwardWeightedStrengthXp())
            {
                return;
            }

            ownerStats.RecordWeightedCombatHit(armedAttack);
        }

        private GameObject ResolveDamageSourceObject()
        {
            ResolveOwnerReferences();
            return ownerUnit != null ? ownerUnit.gameObject : gameObject;
        }

        private bool IsFacingTarget(Vector3 toTarget)
        {
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 direction = toTarget.normalized;
            float requiredDot = Mathf.Cos(Mathf.Clamp(closeRangeFacingAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);
            return Vector3.Dot(forward, direction) >= requiredDot;
        }

        private float ResolveMeleeRange()
        {
            if (equippedWeapon == null)
            {
                return meleeRange;
            }

            float configuredRange = equippedWeapon.effectiveRange;
            if (configuredRange <= 0f)
            {
                return meleeRange;
            }

            return Mathf.Min(meleeRange, configuredRange);
        }

        private void ResolveOwnerReferences()
        {
            if (ownerUnit == null)
            {
                ownerUnit = GetComponent<Unit>();
                if (ownerUnit == null)
                {
                    ownerUnit = GetComponentInParent<Unit>();
                }

                ownerVisionSource = null;
            }

            if (ownerStats == null)
            {
                ownerStats = ownerUnit != null ? ownerUnit.Stats : null;
                if (ownerStats == null)
                {
                    ownerStats = GetComponent<UnitStats>();
                    if (ownerStats == null)
                    {
                        ownerStats = GetComponentInParent<UnitStats>();
                    }
                }
            }

            if (ownerInventory == null)
            {
                ownerInventory = ownerUnit != null ? ownerUnit.Inventory : null;
                if (ownerInventory == null)
                {
                    ownerInventory = GetComponent<UnitInventory>();
                    if (ownerInventory == null)
                    {
                        ownerInventory = GetComponentInParent<UnitInventory>();
                    }
                }
            }

            if (ownerVisionSource == null)
            {
                ownerVisionSource = GetComponent<FogOfWarVisionSource>();
                if (ownerVisionSource == null)
                {
                    ownerVisionSource = GetComponentInChildren<FogOfWarVisionSource>();
                }
            }
        }

        /// <summary>Effective ranged range scaled by the owner's Shooting skill.</summary>
        public float GetEffectiveRangedRange()
        {
            float baseRange = equippedWeapon != null ? equippedWeapon.effectiveRange : 20f;
            if (ownerStats == null) return baseRange;
            return baseRange * ownerStats.GetShootingEffectiveRangeMultiplier();
        }

        public static bool IsRangedCategory(WeaponCategory category)
        {
            return category == WeaponCategory.Pistol
                || category == WeaponCategory.Rifle
                || category == WeaponCategory.Shotgun
                || category == WeaponCategory.Bow;
        }
    }

    public enum WeaponCategory
    {
        Pistol,
        Rifle,
        Shotgun,
        Melee,
        Throwable,
        Bow
    }
}