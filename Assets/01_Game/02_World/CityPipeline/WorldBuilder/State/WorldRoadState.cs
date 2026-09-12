using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public sealed class RoadState
    {
        public WorldEntityId id;
        public string sourceId = string.Empty;
        public RoadSourceKind sourceKind;
        public WorldEntityId regionId;
        public WorldEntityId settlementId;
        public int sourceRoadId;
        public RoadClass roadClass;
        public float widthMeters;
        public bool preserveWorldPath;
        public bool curvedMarkers;
        public List<Vector2> pointsXZ = new();
    }
}
