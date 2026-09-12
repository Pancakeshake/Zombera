using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    public static partial class CityDistrictClusterPlanner
    {
        private const int DefaultParkBlockCount = 2;

        private static void AssignParkBlocks(List<BlockNode> nodes, System.Random rng, int seed, int maxBlocks = -1)
        {
            var target = Mathf.Clamp(DefaultParkBlockCount + (MixSeed(seed, 77) % 2), 1, 4);
            if (maxBlocks >= 0)
                target = Mathf.Min(target, maxBlocks);
            if (target <= 0)
                return;

            var candidates = new List<BlockNode>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.district != CityDistrictType.Mixed)
                    continue;
                candidates.Add(node);
            }

            if (candidates.Count == 0)
                return;

            candidates.Sort((a, b) => b.perimeter.CompareTo(a.perimeter));
            var placed = 0;
            for (var i = 0; i < candidates.Count && placed < target; i++)
            {
                var pickIndex = i == 0 ? 0 : rng.Next(candidates.Count);
                var pick = candidates[pickIndex];
                if (pick.district != CityDistrictType.Mixed)
                    continue;

                pick.district = CityDistrictType.Park;
                pick.clusterName = "Park_" + (placed + 1).ToString("00");
                candidates.RemoveAt(pickIndex);
                placed++;
            }
        }
    }
}
