using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Places bridge / causeway deck prefabs at resolved water crossings.</summary>
    public sealed class RoadCrossingPlacer
    {
        private readonly List<GameObject> _spawned = new();

        public void Place(
            IReadOnlyList<WaterCrossing> crossings,
            Transform parent,
            GameObject bridgePrefab,
            GameObject causewayPrefab)
        {
            Clear();
            if (crossings == null) return;

            for (var i = 0; i < crossings.Count; i++)
            {
                var crossing = crossings[i];
                GameObject prefab = null;
                if (crossing.Policy == WaterCrossingPolicy.Bridge) prefab = bridgePrefab;
                else if (crossing.Policy == WaterCrossingPolicy.Causeway) prefab = causewayPrefab;
                if (prefab == null) continue;

                var instance = Object.Instantiate(prefab, parent);
                var mid = crossing.PositionXZ;
                instance.transform.position = new Vector3(mid.x, crossing.DeckWorldY, mid.y);
                var dir = crossing.ExitXZ - crossing.EntryXZ;
                if (dir.sqrMagnitude > 0.001f)
                    instance.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                _spawned.Add(instance);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Object.Destroy(_spawned[i]);
            }

            _spawned.Clear();
        }
    }
}
