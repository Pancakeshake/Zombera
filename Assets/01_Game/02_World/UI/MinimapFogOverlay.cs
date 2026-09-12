using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.World;

namespace Zombera.UI
{
    /// <summary>
    ///     Renders discovered-chunk fog above the minimap camera quad and below marker overlays.
    /// </summary>
    public sealed class MinimapFogOverlay : MonoBehaviour
    {
        [SerializeField] private RectTransform minimapImageRect;

        private MapStateService _mapStateService;
        private RawImage _fogImage;

        public void Initialize(RectTransform minimapRect)
        {
            minimapImageRect = minimapRect;
            EnsureFogImage();
            EnsureRuntimeReferences();
        }

        private void LateUpdate()
        {
            if (!IsWorldSessionActive()) return;

            EnsureFogImage();
            EnsureRuntimeReferences();
            RefreshFogTexture();
        }

        private void EnsureFogImage()
        {
            if (_fogImage != null || minimapImageRect == null) return;

            var fogRoot = new GameObject("MinimapFogOverlay", typeof(RectTransform)).GetComponent<RectTransform>();
            fogRoot.SetParent(minimapImageRect, false);
            fogRoot.SetAsFirstSibling();
            fogRoot.anchorMin = Vector2.zero;
            fogRoot.anchorMax = Vector2.one;
            fogRoot.offsetMin = Vector2.zero;
            fogRoot.offsetMax = Vector2.zero;

            _fogImage = fogRoot.gameObject.AddComponent<RawImage>();
            _fogImage.raycastTarget = false;
            _fogImage.color = new Color(1f, 1f, 1f, 0.88f);
        }

        private void EnsureRuntimeReferences()
        {
            if (_mapStateService == null) _mapStateService = FindFirstObjectByType<MapStateService>();
        }

        private void RefreshFogTexture()
        {
            if (_fogImage == null || _mapStateService == null) return;

            var fog = _mapStateService.FogTexture;
            if (_fogImage.texture != fog) _fogImage.texture = fog;
        }

        private static bool IsWorldSessionActive()
        {
            return WorldSessionGate.IsWorldSessionActive;
        }
    }
}
