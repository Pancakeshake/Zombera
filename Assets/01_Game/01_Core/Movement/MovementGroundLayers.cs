#region

using UnityEngine;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Layer policy for movement ground raycasts. Excludes UI/preview layers from broad defaults.
    /// </summary>
    public static class MovementGroundLayers
    {
        private static LayerMask? _cachedDefaultMask;

        public static LayerMask DefaultMovementGroundMask => Resolve(default, default);

        public static LayerMask Resolve(LayerMask configuredMask, LayerMask profileMask = default)
        {
            if (configuredMask.value != 0 && configuredMask.value != -1)
                return configuredMask;

            if (profileMask.value != 0)
                return profileMask;

            if (_cachedDefaultMask.HasValue) return _cachedDefaultMask.Value;

            var named = LayerMask.GetMask("Ground", "Terrain", "Default");
            if (named != 0)
            {
                _cachedDefaultMask = named;
                return named;
            }

            var exclude = LayerMask.GetMask(
                "UI",
                "Ignore Raycast",
                "CharacterPreview",
                "TransparentFX",
                "Water",
                "Socket");
            _cachedDefaultMask = ~exclude;
            return _cachedDefaultMask.Value;
        }
    }
}
