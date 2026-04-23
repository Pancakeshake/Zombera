using UnityEngine;
using Zombera.Core;

namespace Zombera.World
{
    /// <summary>
    /// Coordinates world map spawning strategy.
    /// Prototype mode supports static map prefabs before full procedural generation.
    /// </summary>
    public sealed class MapSpawner : MonoBehaviour
    {
        [SerializeField] private bool usePrototypeStaticMap = true;
        [SerializeField] private GameObject staticMapPrefab;
        [SerializeField] private Transform mapRoot;

        [Header("Fallback")]
        [SerializeField] private bool createFallbackGroundWhenMissingPrefab = true;
        [SerializeField, Min(20f)] private float fallbackGroundSize = 1200f;
        [SerializeField] private float fallbackGroundY = 0f;
        [SerializeField] private Material fallbackGroundMaterial;

        private GameObject activeMapInstance;

        public void SpawnPrototypeMap()
        {
            if (!usePrototypeStaticMap)
            {
                return;
            }

            if (activeMapInstance != null)
            {
                return;
            }

            Transform resolvedMapRoot = ResolveMapRoot();

            if (staticMapPrefab != null)
            {
                activeMapInstance = Instantiate(staticMapPrefab, resolvedMapRoot);
                return;
            }

            if (!createFallbackGroundWhenMissingPrefab)
            {
                Debug.LogWarning("[MapSpawner] No static map prefab is assigned. World will have no visible terrain.", this);
                return;
            }

            // When MapMagic is present it owns all terrain geometry — skip the flat fallback ground
            // so the fake plane does not interfere with MapMagic tile placement or player spawning.
            MapMagic.Core.MapMagicObject mapMagicInScene = Object.FindFirstObjectByType<MapMagic.Core.MapMagicObject>();
            if (mapMagicInScene != null)
            {
                Debug.Log("[MapSpawner] MapMagic detected in scene — skipping fallback ground creation. MapMagic tiles are the world terrain.", this);
                return;
            }

            activeMapInstance = CreateRuntimeFallbackGround(resolvedMapRoot);
            Debug.LogWarning("[MapSpawner] Spawned runtime fallback ground because staticMapPrefab is unassigned.", this);

            // Procedural generation will replace this once ChunkGenerator is fully wired.
            // When usePrototypeStaticMap is false the World system calls ChunkLoader instead.
        }

        public void ClearMap()
        {
            if (activeMapInstance == null)
            {
                return;
            }

            Destroy(activeMapInstance);
            activeMapInstance = null;

            // Notify any registered chunk/prop listeners to return pooled objects.
            Zombera.Core.EventSystem.PublishGlobal(new MapClearedEvent());
        }

        private Transform ResolveMapRoot()
        {
            if (mapRoot != null)
            {
                return mapRoot;
            }

            GameObject runtimeMapRoot = GameObject.Find("RuntimeMap");
            if (runtimeMapRoot == null)
            {
                runtimeMapRoot = new GameObject("RuntimeMap");
            }

            mapRoot = runtimeMapRoot.transform;
            return mapRoot;
        }

        private GameObject CreateRuntimeFallbackGround(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "RuntimeFallbackGround";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0f, fallbackGroundY, 0f);

            float planeScale = Mathf.Max(1f, fallbackGroundSize / 10f);
            ground.transform.localScale = new Vector3(planeScale, 1f, planeScale);

            if (fallbackGroundMaterial != null)
            {
                Renderer renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = fallbackGroundMaterial;
                }
            }

            return ground;
        }
    }

    /// <summary>Published when the active map is cleared so pooled world props can be returned.</summary>
    public struct MapClearedEvent : IGameEvent { }
}