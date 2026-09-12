using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Zombera.Combat
{
    internal static class ProjectilePoolRegistry
    {
        private const int DefaultPoolCapacity = 24;
        private const int DefaultPoolMaxSize = 192;

        private sealed class PoolHandle
        {
            public Transform Root;
            public IObjectPool<Projectile> Pool;
        }

        private static readonly Dictionary<int, PoolHandle> ProjectilePrefabPools = new();
        private static readonly Dictionary<int, PoolHandle> GameObjectPrefabPools = new();
        private static readonly Dictionary<string, PoolHandle> FactoryPools =
            new(StringComparer.Ordinal);

        private static Transform _globalRoot;

        public static Projectile SpawnFromProjectilePrefab(
            Projectile prefab,
            Vector3 position,
            Quaternion rotation)
        {
            if (prefab == null) return null;

            var key = prefab.GetInstanceID();
            var poolHandle = ResolveOrCreatePool(
                ProjectilePrefabPools,
                key,
                prefab.name,
                () =>
                {
                    var instance = Object.Instantiate(prefab, ResolveGlobalRoot());
                    if (instance != null) instance.gameObject.SetActive(false);
                    return instance;
                });

            return GetPooledProjectile(poolHandle, position, rotation);
        }

        public static Projectile SpawnFromGameObjectPrefab(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation)
        {
            if (prefab == null) return null;

            var key = prefab.GetInstanceID();
            var poolHandle = ResolveOrCreatePool(
                GameObjectPrefabPools,
                key,
                prefab.name,
                () =>
                {
                    var instanceObject = Object.Instantiate(prefab, ResolveGlobalRoot());
                    if (instanceObject == null) return null;

                    var projectile = instanceObject.GetComponent<Projectile>();
                    if (projectile == null) projectile = instanceObject.AddComponent<Projectile>();

                    instanceObject.SetActive(false);
                    return projectile;
                });

            return GetPooledProjectile(poolHandle, position, rotation);
        }

        public static Projectile SpawnFromFactory(
            string key,
            Func<Projectile> createProjectile,
            Vector3 position,
            Quaternion rotation)
        {
            if (string.IsNullOrWhiteSpace(key) || createProjectile == null) return null;

            var poolHandle = ResolveOrCreatePool(
                FactoryPools,
                key,
                key,
                () =>
                {
                    var projectile = createProjectile();
                    if (projectile != null) projectile.gameObject.SetActive(false);
                    return projectile;
                });

            return GetPooledProjectile(poolHandle, position, rotation);
        }

        private static Projectile GetPooledProjectile(PoolHandle poolHandle, Vector3 position, Quaternion rotation)
        {
            if (poolHandle == null) return null;

            var projectile = poolHandle.Pool.Get();
            if (projectile == null) return null;

            projectile.transform.SetPositionAndRotation(position, rotation);
            return projectile;
        }

        private static PoolHandle ResolveOrCreatePool(
            IDictionary<int, PoolHandle> pools,
            int key,
            string poolName,
            Func<Projectile> createProjectile)
        {
            if (pools.TryGetValue(key, out var existing)) return existing;

            var created = CreatePool(poolName, createProjectile);
            pools[key] = created;
            return created;
        }

        private static PoolHandle ResolveOrCreatePool(
            IDictionary<string, PoolHandle> pools,
            string key,
            string poolName,
            Func<Projectile> createProjectile)
        {
            if (pools.TryGetValue(key, out var existing)) return existing;

            var created = CreatePool(poolName, createProjectile);
            pools[key] = created;
            return created;
        }

        private static PoolHandle CreatePool(string poolName, Func<Projectile> createProjectile)
        {
            var poolRootObject = new GameObject($"ProjectilePool:{poolName}");
            poolRootObject.transform.SetParent(ResolveGlobalRoot(), false);
            Object.DontDestroyOnLoad(poolRootObject);

            var poolRoot = poolRootObject.transform;
            ObjectPool<Projectile> pool = null;
            pool = new ObjectPool<Projectile>(
                () => createProjectile(),
                projectile => OnGetFromPool(projectile, pool),
                projectile => OnReleaseToPool(projectile, poolRoot),
                OnDestroyPooledProjectile,
                false,
                DefaultPoolCapacity,
                DefaultPoolMaxSize);

            return new PoolHandle
            {
                Root = poolRoot,
                Pool = pool
            };
        }

        private static void OnGetFromPool(Projectile projectile, IObjectPool<Projectile> owningPool)
        {
            if (projectile == null) return;

            projectile.PrepareForPoolReuse(owningPool);
            projectile.transform.SetParent(null, true);
            projectile.gameObject.SetActive(true);
        }

        private static void OnReleaseToPool(Projectile projectile, Transform poolRoot)
        {
            if (projectile == null) return;

            projectile.transform.SetParent(poolRoot, false);
            projectile.gameObject.SetActive(false);
        }

        private static void OnDestroyPooledProjectile(Projectile projectile)
        {
            if (projectile == null) return;

            Object.Destroy(projectile.gameObject);
        }

        private static Transform ResolveGlobalRoot()
        {
            if (_globalRoot != null) return _globalRoot;

            var rootObject = new GameObject("ProjectilePoolRegistry");
            Object.DontDestroyOnLoad(rootObject);
            _globalRoot = rootObject.transform;
            return _globalRoot;
        }
    }
}
