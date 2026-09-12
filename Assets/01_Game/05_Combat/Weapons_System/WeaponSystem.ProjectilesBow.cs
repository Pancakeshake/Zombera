#region

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using Zombera.Characters;
using Zombera.Systems;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

#endregion

namespace Zombera.Combat
{
    public sealed partial class WeaponSystem
    {
        private static readonly Color DefaultRuntimeBowProjectileColor = new(0.58f, 0.42f, 0.24f, 1f);

        private readonly struct BowProjectileSettings
        {
            public readonly float ArcHeight;
            public readonly float GravityScale;
            public readonly float EmbeddedLifetimeSeconds;
            public readonly bool StickToTargets;
            public readonly bool EmbedInEnvironment;

            public BowProjectileSettings(
                float arcHeight,
                float gravityScale,
                float embeddedLifetimeSeconds,
                bool stickToTargets,
                bool embedInEnvironment)
            {
                ArcHeight = arcHeight;
                GravityScale = gravityScale;
                EmbeddedLifetimeSeconds = embeddedLifetimeSeconds;
                StickToTargets = stickToTargets;
                EmbedInEnvironment = embedInEnvironment;
            }
        }

        private BowSubsystem Bow => _bowSubsystem ??= new BowSubsystem(this);

        private BowProjectileSettings ResolveBowProjectileSettings()
        {
            var bowData = GetActiveBowWeaponData();
            if (bowData == null)
                return new BowProjectileSettings(1.2f, 1.35f, 18f, true, true);

            return new BowProjectileSettings(
                Mathf.Max(0f, bowData.projectileArcHeight),
                Mathf.Max(0.1f, bowData.projectileGravityScale),
                Mathf.Max(0.1f, bowData.projectileEmbeddedLifetimeSeconds),
                bowData.projectilesStickToTargets,
                bowData.projectilesEmbedInEnvironment);
        }

        private float ResolveShotNoiseRadius(bool isBowShot)
        {
            if (equippedWeapon != null && equippedWeapon.effectiveRange > 0f)
                return equippedWeapon.effectiveRange * (isBowShot ? 1.1f : 2f);

            return isBowShot ? 14f : 30f;
        }

        private bool CanBowDamageTargetAtDistance(float distanceMeters)
        {
            return Bow.CanDamageTargetAtDistance(distanceMeters);
        }

        private void ResolveProjectileSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            var resolvedMuzzlePoint = EnsureMuzzlePoint();
            if (resolvedMuzzlePoint != null)
            {
                spawnPosition = resolvedMuzzlePoint.position;
                spawnRotation = resolvedMuzzlePoint.rotation;
                return;
            }

            var forward = transform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            spawnPosition = transform.position + forward * 0.45f + Vector3.up * 1.35f;
            spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private Projectile CreateRuntimeBowProjectileFallback(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            var poolKey = $"RuntimeBowProjectile:{GetInstanceID()}";
            return ProjectilePoolRegistry.SpawnFromFactory(
                poolKey,
                CreateRuntimeBowProjectileFallbackInstance,
                spawnPosition,
                spawnRotation);
        }

        private Projectile CreateRuntimeBowProjectileFallbackInstance()
        {
            var bowData = GetActiveBowWeaponData();
            var thickness = bowData != null ? bowData.runtimeProjectileThickness : 0.025f;
            var length = bowData != null ? bowData.runtimeProjectileLength : 0.35f;
            var color = bowData != null ? bowData.runtimeProjectileColor : DefaultRuntimeBowProjectileColor;

            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projectileObject.name = "RuntimeBowProjectile";
            projectileObject.transform.localScale = new Vector3(
                Mathf.Max(0.01f, thickness),
                Mathf.Max(0.01f, thickness),
                Mathf.Max(0.05f, length));

            var projectileCollider = projectileObject.GetComponent<Collider>();
            if (projectileCollider != null) Destroy(projectileCollider);

            var material = GetRuntimeBowProjectileMaterial(color);

            var meshRenderer = projectileObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                if (material != null)
                    meshRenderer.sharedMaterial = material;
                else
                    meshRenderer.material.color = color;
            }

            var trail = projectileObject.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.startWidth = Mathf.Max(0.005f, thickness * 0.9f);
            trail.endWidth = Mathf.Max(0.001f, thickness * 0.15f);
            trail.minVertexDistance = 0.01f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            var startColor = color;
            startColor.a = 0.85f;
            var endColor = color;
            endColor.a = 0f;
            trail.startColor = startColor;
            trail.endColor = endColor;

            if (material != null) trail.sharedMaterial = material;

            return projectileObject.AddComponent<Projectile>();
        }

        private Projectile CreateBowProjectileInstance(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            return Bow.CreateProjectileInstance(spawnPosition, spawnRotation);
        }

        private Projectile TryCreateBowProjectileFromVisualPrefab(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            var bowData = GetActiveBowWeaponData();

#if UNITY_EDITOR
            if (bowData != null && bowData.arrowVisualPrefab == null)
                bowData.arrowVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBowArrowVisualPrefabPath);
#endif

            var arrowVisualPrefab = bowData != null ? bowData.arrowVisualPrefab : null;
            if (arrowVisualPrefab == null) return null;

            return ProjectilePoolRegistry.SpawnFromGameObjectPrefab(arrowVisualPrefab, spawnPosition, spawnRotation);
        }

        private void TryPlayBowReleaseAudio()
        {
            Bow.TryPlayReleaseAudio();
        }

        private void ConfigureBowProjectileAudio(Projectile projectile)
        {
            Bow.ConfigureProjectileAudio(projectile);
        }

        private Material GetRuntimeBowProjectileMaterial(Color color)
        {
            if (_runtimeBowProjectileMaterial != null)
            {
                _runtimeBowProjectileMaterial.color = color;
                return _runtimeBowProjectileMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            _runtimeBowProjectileMaterial = new Material(shader)
            {
                color = color
            };

            return _runtimeBowProjectileMaterial;
        }

        private sealed class BowSubsystem
        {
            private readonly WeaponSystem _owner;

            public BowSubsystem(WeaponSystem owner)
            {
                _owner = owner;
            }

            public bool CanDamageTargetAtDistance(float distanceMeters)
            {
                var maxRange = ResolveMaximumRangeMeters();
                if (distanceMeters > maxRange) return false;

                var hitChance = ResolveDistanceHitChance(distanceMeters, maxRange);
                return Random.value <= hitChance;
            }

            public float ResolveDistanceHitChance(float distanceMeters, float maxRange)
            {
                var bowData = _owner.GetActiveBowWeaponData();
                var normalizedDistance = Mathf.Clamp01(distanceMeters / Mathf.Max(1f, maxRange));
                var minChanceAtMaxRange = Mathf.Clamp01(bowData != null ? bowData.hitChanceAtMaxRange : 0.35f);
                var distanceChance = Mathf.Lerp(1f, minChanceAtMaxRange, normalizedDistance);
                var skillBonus = _owner.OwnerStats != null ? _owner.OwnerStats.GetShootingHitChanceBonus() : 0f;
                return Mathf.Clamp01(distanceChance + skillBonus);
            }

            public float ResolveMaximumRangeMeters()
            {
                var bowData = _owner.GetActiveBowWeaponData();
                return Mathf.Max(1f, bowData != null ? bowData.maximumRangeMeters : 100f);
            }

            public Projectile CreateProjectileInstance(Vector3 spawnPosition, Quaternion spawnRotation)
            {
                var bowData = _owner.GetActiveBowWeaponData();
                var preferVisualPrefab = bowData == null || bowData.preferArrowVisualPrefab;
                var allowRuntimeFallback = bowData == null || bowData.enableRuntimeProjectileFallback;

                if (preferVisualPrefab)
                {
                    var projectileFromVisualPrefab = _owner.TryCreateBowProjectileFromVisualPrefab(
                        spawnPosition,
                        spawnRotation);
                    if (projectileFromVisualPrefab != null) return projectileFromVisualPrefab;
                }

                if (_owner.projectilePrefab != null)
                    return ProjectilePoolRegistry.SpawnFromProjectilePrefab(
                        _owner.projectilePrefab,
                        spawnPosition,
                        spawnRotation);

                if (!preferVisualPrefab)
                {
                    var projectileFromVisualPrefab = _owner.TryCreateBowProjectileFromVisualPrefab(
                        spawnPosition,
                        spawnRotation);
                    if (projectileFromVisualPrefab != null) return projectileFromVisualPrefab;
                }

                return allowRuntimeFallback
                    ? _owner.CreateRuntimeBowProjectileFallback(spawnPosition, spawnRotation)
                    : null;
            }

            public void TryPlayReleaseAudio()
            {
                var bowData = _owner.GetActiveBowWeaponData();
                if (bowData == null || bowData.releaseArrowClip == null) return;

                var resolvedMuzzlePoint = _owner.EnsureMuzzlePoint();
                var audioPosition = resolvedMuzzlePoint != null ? resolvedMuzzlePoint.position : _owner.transform.position;
                AudioSource.PlayClipAtPoint(
                    bowData.releaseArrowClip,
                    audioPosition,
                    Mathf.Clamp01(bowData.releaseArrowVolume));
            }

            public void ConfigureProjectileAudio(Projectile projectile)
            {
                if (projectile == null) return;

                var bowData = _owner.GetActiveBowWeaponData();
                if (bowData == null) return;

                projectile.ConfigureImpactAudio(bowData.hitBodyClip, Mathf.Clamp01(bowData.hitBodyVolume));
            }
        }
    }
}
