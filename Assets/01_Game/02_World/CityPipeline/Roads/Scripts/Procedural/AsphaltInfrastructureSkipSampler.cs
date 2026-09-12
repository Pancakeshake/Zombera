using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Per-road tunnel/bridge filter for asphalt portal-to-portal and deck skips.
    ///     Uses span semantics (not road-bed core carve).
    /// </summary>
    internal sealed class AsphaltInfrastructureSkipSampler
    {
        private readonly List<MountainTunnel> _tunnels = new(4);
        private readonly List<ResolvedBridgeApproach> _bridges = new(4);
        private readonly float _lateralSlopMeters;

        public AsphaltInfrastructureSkipSampler(int roadId, Rect roadBounds, float lateralSlopMeters)
        {
            _lateralSlopMeters = Mathf.Max(0f, lateralSlopMeters);
            AddRelevantTunnels(roadId);
            AddRelevantBridges(roadId, roadBounds);
        }

        public bool HasSpans => _tunnels.Count > 0 || _bridges.Count > 0;
        public bool HasTunnels => _tunnels.Count > 0;
        public bool HasBridgeApproaches => _bridges.Count > 0;

        public bool ShouldSkip(Vector2 worldXZ)
        {
            for (var i = 0; i < _tunnels.Count; i++)
            {
                if (_tunnels[i].ContainsPointOnInteriorSpan(worldXZ, _lateralSlopMeters))
                    return true;
            }

            for (var i = 0; i < _bridges.Count; i++)
            {
                var bridge = _bridges[i];
                if (IsInsideOpenSpan(worldXZ, bridge.EntryXZ, bridge.ExitXZ, _lateralSlopMeters))
                    return true;
            }

            return false;
        }

        public float ResolveTunnelApproachHeight(
            Vector2 worldXZ,
            float terrainHeight,
            float approachMeters)
        {
            var bestWeight = 0f;
            var targetHeight = terrainHeight;
            var length = Mathf.Max(2f, approachMeters);
            for (var i = 0; i < _tunnels.Count; i++)
            {
                var tunnel = _tunnels[i];
                TryApplyApproach(
                    worldXZ,
                    tunnel.EntryXZ,
                    -tunnel.ForwardXZ,
                    tunnel.EntryWorldY,
                    length,
                    ref bestWeight,
                    ref targetHeight);
                TryApplyApproach(
                    worldXZ,
                    tunnel.ExitXZ,
                    tunnel.ForwardXZ,
                    tunnel.ExitWorldY,
                    length,
                    ref bestWeight,
                    ref targetHeight);
            }

            return Mathf.Lerp(terrainHeight, targetHeight, bestWeight);
        }

        public float ResolveBridgeApproachHeight(
            Vector2 worldXZ,
            float roadSurfaceHeight,
            float approachMeters)
        {
            var bestWeight = 0f;
            var targetHeight = roadSurfaceHeight;
            var length = Mathf.Max(2f, approachMeters);
            for (var i = 0; i < _bridges.Count; i++)
            {
                var bridge = _bridges[i];
                var forward = (bridge.ExitXZ - bridge.EntryXZ).normalized;
                TryApplyApproach(
                    worldXZ,
                    bridge.EntryXZ,
                    -forward,
                    bridge.EntryApproachWorld.y,
                    length,
                    ref bestWeight,
                    ref targetHeight);
                TryApplyApproach(
                    worldXZ,
                    bridge.ExitXZ,
                    forward,
                    bridge.ExitApproachWorld.y,
                    length,
                    ref bestWeight,
                    ref targetHeight);
            }

            return Mathf.Lerp(roadSurfaceHeight, targetHeight, bestWeight);
        }

        public float ResolveTunnelApproachWidth(
            Vector2 worldXZ,
            float roadWidth,
            float apertureWidth,
            float approachMeters)
        {
            var weight = 0f;
            var length = Mathf.Max(2f, approachMeters);
            for (var i = 0; i < _tunnels.Count; i++)
            {
                var tunnel = _tunnels[i];
                weight = Mathf.Max(weight, ApproachWeight(worldXZ, tunnel.EntryXZ, -tunnel.ForwardXZ, length));
                weight = Mathf.Max(weight, ApproachWeight(worldXZ, tunnel.ExitXZ, tunnel.ForwardXZ, length));
            }

            return Mathf.Lerp(roadWidth, Mathf.Min(roadWidth, apertureWidth), weight);
        }

        private float ApproachWeight(Vector2 point, Vector2 mouth, Vector2 outward, float approachMeters)
        {
            var delta = point - mouth;
            var along = Vector2.Dot(delta, outward);
            if (along < 0f || along > approachMeters)
                return 0f;
            var lateral = Mathf.Abs(delta.x * outward.y - delta.y * outward.x);
            return lateral <= _lateralSlopMeters ? 1f - along / approachMeters : 0f;
        }

        private void TryApplyApproach(
            Vector2 point,
            Vector2 mouth,
            Vector2 outward,
            float mouthHeight,
            float approachMeters,
            ref float bestWeight,
            ref float targetHeight)
        {
            var delta = point - mouth;
            var along = Vector2.Dot(delta, outward);
            if (along < 0f || along > approachMeters)
                return;
            var lateral = Mathf.Abs(delta.x * outward.y - delta.y * outward.x);
            if (lateral > _lateralSlopMeters)
                return;

            var weight = 1f - along / approachMeters;
            if (weight <= bestWeight)
                return;
            bestWeight = weight;
            targetHeight = mouthHeight;
        }

        private void AddRelevantTunnels(int roadId)
        {
            var active = MountainTunnelBuildCache.Active;
            if (active == null)
                return;

            for (var i = 0; i < active.Count; i++)
            {
                var tunnel = active[i];
                if (!IsFinite(tunnel.EntryXZ) || !IsFinite(tunnel.ExitXZ))
                    continue;
                if (tunnel.RoadId != 0 && roadId != 0 && tunnel.RoadId != roadId)
                    continue;
                _tunnels.Add(tunnel);
            }
        }

        private void AddRelevantBridges(int roadId, Rect roadBounds)
        {
            var resolved = WaterCrossingBuildCache.ResolvedApproaches;
            if (resolved != null && resolved.Count > 0)
            {
                AddResolvedBridges(resolved, roadId, roadBounds);
                return;
            }

            var active = WaterCrossingBuildCache.Active;
            if (active == null)
                return;

            for (var i = 0; i < active.Count; i++)
            {
                var crossing = active[i];
                if (crossing.Policy != WaterCrossingPolicy.Bridge &&
                    crossing.Policy != WaterCrossingPolicy.Causeway)
                    continue;
                if (!IsFinite(crossing.EntryXZ) || !IsFinite(crossing.ExitXZ))
                    continue;
                if (crossing.RoadId != 0 && roadId != 0 && crossing.RoadId != roadId)
                    continue;
                var bridge = new ResolvedBridgeApproach(
                    crossing.StableId,
                    crossing.RoadId,
                    crossing.RoadClass,
                    new Vector3(crossing.EntryXZ.x, crossing.DeckWorldY, crossing.EntryXZ.y),
                    new Vector3(crossing.ExitXZ.x, crossing.DeckWorldY, crossing.ExitXZ.y));
                if (ExpandedSpanBounds(bridge, _lateralSlopMeters).Overlaps(roadBounds))
                    _bridges.Add(bridge);
            }
        }

        private void AddResolvedBridges(
            IReadOnlyList<ResolvedBridgeApproach> approaches,
            int roadId,
            Rect roadBounds)
        {
            for (var i = 0; i < approaches.Count; i++)
            {
                var approach = approaches[i];
                if (!IsFinite(approach.EntryXZ) || !IsFinite(approach.ExitXZ))
                    continue;
                if (approach.RoadId != 0 && roadId != 0 && approach.RoadId != roadId)
                    continue;
                if (ExpandedSpanBounds(approach, _lateralSlopMeters).Overlaps(roadBounds))
                    _bridges.Add(approach);
            }
        }

        private static Rect ExpandedSpanBounds(
            ResolvedBridgeApproach bridge,
            float lateralSlopMeters)
        {
            var min = Vector2.Min(bridge.EntryXZ, bridge.ExitXZ);
            var max = Vector2.Max(bridge.EntryXZ, bridge.ExitXZ);
            var padding = Mathf.Max(0f, lateralSlopMeters);
            return Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
        }

        private static bool IsInsideOpenSpan(
            Vector2 point,
            Vector2 start,
            Vector2 end,
            float lateralSlopMeters)
        {
            var delta = end - start;
            var lengthSq = delta.sqrMagnitude;
            if (lengthSq < 0.0001f)
                return false;

            var t = Vector2.Dot(point - start, delta) / lengthSq;
            var endpointEpsilon = Mathf.Min(0.25f, 0.05f / Mathf.Sqrt(lengthSq));
            if (t <= endpointEpsilon || t >= 1f - endpointEpsilon)
                return false;
            var projected = start + delta * t;
            return Vector2.Distance(point, projected) <= lateralSlopMeters;
        }

        private static bool IsFinite(Vector2 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y));
    }
}
