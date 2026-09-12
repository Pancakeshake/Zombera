using System;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    [Serializable]
    public sealed class WorldPoiRecord
    {
        public ulong StableId;
        public string EntryId;
        public Vector2 PositionXZ;
        public float YawDegrees;
        public Vector2 FootprintMeters;
        public string MapMarkerId;
    }
}
