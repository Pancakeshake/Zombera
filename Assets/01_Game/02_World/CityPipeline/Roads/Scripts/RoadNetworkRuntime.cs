using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public sealed class RoadNetworkRuntime
    {
        public int Seed { get; }
        public IReadOnlyList<RoadPolyline> Roads => _roads;
        public IReadOnlyList<TownNode> TownNodes => _townNodes;

        private readonly List<RoadPolyline> _roads = new();
        private readonly List<TownNode> _townNodes = new();

        public RoadNetworkRuntime(int seed)
        {
            Seed = seed;
        }

        public void AddRoad(RoadPolyline road)
        {
            if (road == null || road.pointsXZ == null || road.pointsXZ.Count < 2) return;
            _roads.Add(road);
        }

        public void AddTown(TownNode town)
        {
            if (town == null) return;
            _townNodes.Add(town);
        }

        public void RemoveRoad(RoadPolyline road)
        {
            _roads.Remove(road);
        }

        public void Clear()
        {
            _roads.Clear();
            _townNodes.Clear();
        }
    }

    [System.Serializable]
    public sealed class TownNode
    {
        public int id;
        public Vector2 positionXZ;
        public TownType type;
        public float radius;
        public string prefabName; // Optional override
        public string displayName; // Seeded city name from CityNames
        public bool hasMarket;
        public Vector2 marketPositionXZ;

        public TownNode(int id, Vector2 position, TownType type, float radius = 50f)
        {
            this.id = id;
            this.positionXZ = position;
            this.type = type;
            this.radius = radius;
        }
    }
}
