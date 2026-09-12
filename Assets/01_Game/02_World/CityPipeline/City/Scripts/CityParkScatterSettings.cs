using System;
using UnityEngine;

namespace Zombera.World.City
{
    [Serializable]
    public sealed class CityParkScatterSettings
    {
        [Min(0)] public int treeCountMin = 6;
        [Min(0)] public int treeCountMax = 18;
        [Min(0)] public int benchCountMin = 1;
        [Min(0)] public int benchCountMax = 4;
        [Min(0.5f)] public float loopPathWidthMeters = 1.4f;
        [Min(0.5f)] public float loopPathInsetMeters = 3f;
        [Min(0f)] public float surfaceLiftMeters = 0.05f;

        public void Clamp()
        {
            treeCountMax = Mathf.Max(treeCountMin, treeCountMax);
            benchCountMax = Mathf.Max(benchCountMin, benchCountMax);
        }
    }
}
