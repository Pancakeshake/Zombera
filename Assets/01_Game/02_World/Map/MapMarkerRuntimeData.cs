using System;
using UnityEngine;

namespace Zombera.World
{
    [Serializable]
    public struct MapMarkerRuntimeData
    {
        public string markerId;
        public MapMarkerType type;
        public Vector3 worldPosition;
        public string label;
        public Sprite icon;
        public float headingDegrees;

        public bool IsValid => !string.IsNullOrWhiteSpace(markerId);

        public MapMarkerRuntimeData(
            string markerId,
            MapMarkerType type,
            Vector3 worldPosition,
            string label = "",
            Sprite icon = null,
            float headingDegrees = 0f)
        {
            this.markerId = markerId;
            this.type = type;
            this.worldPosition = worldPosition;
            this.label = label ?? string.Empty;
            this.icon = icon;
            this.headingDegrees = headingDegrees;
        }
    }
}
