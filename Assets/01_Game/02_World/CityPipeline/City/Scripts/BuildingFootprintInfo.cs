using UnityEngine;

namespace Zombera.World.City
{
    public struct BuildingFootprintInfo
    {
        public float WidthMeters;
        public float DepthMeters;
        public Vector2 CenterOffsetXZ;
        public bool IsValid;

        public static BuildingFootprintInfo Invalid => new() { IsValid = false };
    }
}
