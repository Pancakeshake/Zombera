using UnityEngine;

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

        private GameObject activeMapInstance;

        public void SpawnPrototypeMap()
        {
            if (!usePrototypeStaticMap)
            {
                return;
            }

            if (activeMapInstance != null || staticMapPrefab == null)
            {
                return;
            }

            activeMapInstance = Instantiate(staticMapPrefab, mapRoot);

            // TODO: Replace with procedural map generation entry point.
        }

        public void ClearMap()
        {
            if (activeMapInstance == null)
            {
                return;
            }

            Destroy(activeMapInstance);
            activeMapInstance = null;

            // TODO: Clear spawned map chunks and pooled world props.
        }
    }
}