using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    [AddComponentMenu("Zombera/World/World Nature Placer")]
    public sealed partial class WorldNaturePlacer : MonoBehaviour, IWorldNaturePlacer
    {
        [SerializeField] private Transform _root;

        private readonly List<GameObject> _spawned = new();

        public void Clear(WorldBuildScope scope)
        {
            for (var i = 0; i < _spawned.Count; i++)
                DestroySpawned(_spawned[i]);

            _spawned.Clear();

            if (_root != null)
            {
                for (var i = _root.childCount - 1; i >= 0; i--)
                    DestroySpawned(_root.GetChild(i).gameObject);
            }

            ClearTerrainTreesInScope(scope.BoundsXZ);
        }

        private static void DestroySpawned(GameObject go)
        {
            if (go == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(go);
            else
#endif
                Destroy(go);
        }

        private void EnsureRoot()
        {
            if (_root != null)
                return;

            var go = new GameObject("NatureRoot");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }
    }
}
