using System;
using UnityEngine;

namespace Zombera.World
{
    [Serializable]
    public struct MapPoiRuntimeData
    {
        public string poiId;
        public string label;
        public Vector3 worldPosition;

        public MapPoiRuntimeData(string poiId, string label, Vector3 worldPosition)
        {
            this.poiId = poiId;
            this.label = label;
            this.worldPosition = worldPosition;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(poiId);
    }
}
