#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.Systems
{
    public readonly struct CursorIconResolveResult
    {
        public readonly CursorIconIntent Intent;
        public readonly Texture2D Texture;
        public readonly Vector2 Hotspot;

        public CursorIconResolveResult(CursorIconIntent intent, Texture2D texture, Vector2 hotspot)
        {
            Intent = intent;
            Texture = texture;
            Hotspot = hotspot;
        }
    }

    public sealed class CursorIconCatalog
    {
        private readonly Dictionary<CursorIconIntent, CursorIconDefinition> _definitions = new();

        public void SetDefinition(in CursorIconDefinition definition)
        {
            _definitions[definition.Intent] = definition;
        }

        public bool TryGetDefinition(CursorIconIntent intent, out CursorIconDefinition definition)
        {
            return _definitions.TryGetValue(intent, out definition);
        }
    }

    public sealed class CursorIconResolver
    {
        private CursorIconCatalog _catalog;
        private CursorHotspotResolver _hotspotResolver;
        private float _alphaThreshold = 0.2f;

        public void Configure(CursorIconCatalog catalog, CursorHotspotResolver hotspotResolver, float alphaThreshold)
        {
            _catalog = catalog;
            _hotspotResolver = hotspotResolver;
            _alphaThreshold = alphaThreshold;
        }

        public CursorIconResolveResult Resolve(CursorIconIntent intent)
        {
            if (_catalog == null || _hotspotResolver == null || !_catalog.TryGetDefinition(intent, out var definition))
                return new CursorIconResolveResult(intent, null, Vector2.zero);

            var hotspot = _hotspotResolver.Resolve(new CursorHotspotResolveRequest(
                definition.Texture,
                definition.Hotspot,
                definition.AutoCenterWhenUnset,
                definition.AutoDetectWhenUnset,
                definition.AutoAnchor,
                _alphaThreshold));

            hotspot = CursorHotspotResolver.ClampHotspot(definition.Texture, hotspot);
            return new CursorIconResolveResult(intent, definition.Texture, hotspot);
        }
    }
}
