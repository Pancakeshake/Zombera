#region

using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

#endregion

// ReSharper disable InvertIf

namespace Zombera.World
{
    /// <summary>
    ///     Coordinates world map spawning strategy.
    ///     Prototype mode supports static map prefabs before full procedural generation.
    /// </summary>
    public sealed class MapSpawner : MonoBehaviour
    {
        [SerializeField] private bool usePrototypeStaticMap = true;
        [SerializeField] private GameObject staticMapPrefab;
        [SerializeField] private Transform mapRoot;

        [Header("Fallback")] [SerializeField] private bool createFallbackGroundWhenMissingPrefab = true;

        [SerializeField] [Min(20f)] private float fallbackGroundSize = 1200f;
        [SerializeField] private float fallbackGroundY;
        [SerializeField] private Material fallbackGroundMaterial;

        private GameObject _activeMapInstance;

        public void SpawnPrototypeMap()
        {
            if (!usePrototypeStaticMap) return;

            if (_activeMapInstance != null) return;

            var resolvedMapRoot = ResolveMapRoot();

            if (staticMapPrefab != null)
            {
                _activeMapInstance = Instantiate(staticMapPrefab, resolvedMapRoot);
                return;
            }

            if (!createFallbackGroundWhenMissingPrefab)
            {
                Debug.LogWarning("[MapSpawner] No static map prefab is assigned. World will have no visible terrain.",
                    this);
                return;
            }

            // When a tile stream / generation backend is present it owns terrain geometry.
            var hasTileStream = FindFirstObjectByType<WorldTileStreamSource>() != null;
            if (!hasTileStream)
            {
                _activeMapInstance = CreateRuntimeFallbackGround(resolvedMapRoot);
                Debug.LogWarning("[MapSpawner] Spawned runtime fallback ground because staticMapPrefab is unassigned.",
                    this);
                return;
            }

            Debug.Log(
                "[MapSpawner] World tile stream detected — skipping fallback ground creation.",
                this);
        }

        public void ClearMap()
        {
            if (_activeMapInstance == null) return;

            Destroy(_activeMapInstance);
            _activeMapInstance = null;

            CoreEventBus.PublishGlobal(new MapClearedEvent());
        }


        private Transform ResolveMapRoot()
        {
            if (mapRoot != null) return mapRoot;

            var runtimeMapRoot = GameObject.Find("RuntimeMap");
            if (runtimeMapRoot == null) runtimeMapRoot = new GameObject("RuntimeMap");

            mapRoot = runtimeMapRoot.transform;
            return mapRoot;
        }

        private GameObject CreateRuntimeFallbackGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "RuntimeFallbackGround";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0f, fallbackGroundY, 0f);

            var planeScale = Mathf.Max(1f, fallbackGroundSize / 10f);
            ground.transform.localScale = new Vector3(planeScale, 1f, planeScale);

            if (fallbackGroundMaterial != null)
            {
                var groundRenderer = ground.GetComponent<Renderer>();
                if (groundRenderer != null) groundRenderer.sharedMaterial = fallbackGroundMaterial;
            }

            return ground;
        }
    }

    /// <summary>Published when the active map is cleared so pooled world props can be returned.</summary>
    public struct MapClearedEvent : IGameEvent
    {
    }
}