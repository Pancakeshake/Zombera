using UnityEngine;

namespace Zombera.World.Roads
{
    public static class RoadNetworkGenerator
    {
        public static RoadNetworkRuntime Generate(int seed, RoadNetworkSettings settings)
        {
            settings ??= ScriptableObject.CreateInstance<RoadNetworkSettings>();

            var network = new RoadNetworkRuntime(seed);
            var rng = new System.Random(seed);

            var ringRadius = 1800f;
            var center = Vector2.zero;

            var ring = new RoadPolyline
            {
                id = 100,
                roadClass = RoadClass.Highway,
                widthMeters = settings.highwayWidth
            };

            const int ringPoints = 64;
            for (var i = 0; i <= ringPoints; i++)
            {
                var t = i / (float)ringPoints;
                var a = t * Mathf.PI * 2f;
                var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ringRadius;
                ring.pointsXZ.Add(p);
            }

            network.AddRoad(ring);

            const int spokeCount = 10;
            for (var i = 0; i < spokeCount; i++)
            {
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var start = center + dir * 80f;
                var end = center + dir * ringRadius;

                var spoke = new RoadPolyline
                {
                    id = 1000 + i,
                    roadClass = RoadClass.Arterial,
                    widthMeters = settings.arterialWidth
                };

                spoke.pointsXZ.Add(start);
                spoke.pointsXZ.Add(Vector2.Lerp(start, end, 0.35f) + new Vector2(-dir.y, dir.x) * 60f * Mathf.Sin(i));
                spoke.pointsXZ.Add(end);

                network.AddRoad(spoke);
            }

            return network;
        }
    }
}
