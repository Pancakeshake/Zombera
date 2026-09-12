#region

using System;
using UnityEngine;

#endregion

namespace Zombera.Systems
{
    [Serializable]
    public struct CursorIconDefinition
    {
        public CursorIconIntent Intent;
        public Texture2D Texture;
        public Vector2 Hotspot;
        public bool AutoCenterWhenUnset;
        public bool AutoDetectWhenUnset;
        public CursorHotspotAutoAnchor AutoAnchor;

        public static CursorIconDefinition ForIntent(CursorIconIntent intent)
        {
            return new CursorIconDefinition
            {
                Intent = intent,
                AutoAnchor = intent == CursorIconIntent.Attack
                    ? CursorHotspotAutoAnchor.TopRight
                    : CursorHotspotAutoAnchor.TopLeft
            };
        }
    }
}
