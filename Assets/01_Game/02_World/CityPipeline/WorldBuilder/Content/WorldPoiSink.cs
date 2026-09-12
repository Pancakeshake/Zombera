using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Simple scene-visible POI sink that stores published wilderness POI records.</summary>
    [AddComponentMenu("Zombera/World/World Poi Sink")]
    [DisallowMultipleComponent]
    public sealed class WorldPoiSink : MonoBehaviour, IWorldPoiSink
    {
        private readonly List<WorldPoiRecord> _pois = new();

        public IReadOnlyList<WorldPoiRecord> Published => _pois;

        public void Publish(IReadOnlyList<WorldPoiRecord> pois)
        {
            _pois.Clear();
            if (pois == null) return;
            for (var i = 0; i < pois.Count; i++)
                _pois.Add(pois[i]);
        }

        public void Clear(WorldBuildScope scope)
        {
            _ = scope;
            _pois.Clear();
        }
    }
}
