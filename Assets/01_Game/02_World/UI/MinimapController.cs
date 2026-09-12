#region

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.World;

#endregion

namespace Zombera.UI
{
    /// <summary>
    ///     Controls minimap rendering references and marker UI state.
    /// </summary>
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Root")] [SerializeField] private RectTransform panelRoot;

        [SerializeField] private Image panelBackground;

        [Header("Map")] [SerializeField] private RawImage minimapImage;

        [SerializeField] private TextMeshProUGUI regionLabelText;

        [Header("Zoom")] [SerializeField] private Slider zoomSlider;

        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 2f;

        private MinimapOverlayRenderer _overlayRenderer;
        private MinimapFogOverlay _fogOverlay;
        private MapStateService _mapStateService;

        public bool IsInitialized { get; private set; }
        public float CurrentZoom { get; private set; } = 1f;

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized) return;

            if (panelRoot == null) panelRoot = transform as RectTransform;

            SetZoom(1f);
            SetRegionLabel("Unknown Region");

            IsInitialized = true;
            BindMinimapCamera();
            EnsureFogOverlay();
            EnsureOverlayRenderer();
        }

        private void LateUpdate()
        {
            if (!IsWorldSessionActive()) return;

            EnsureRuntimeReferences();
            RefreshRegionLabelFromMapState();
        }

        private void EnsureFogOverlay()
        {
            if (minimapImage == null) return;

            _fogOverlay = minimapImage.GetComponent<MinimapFogOverlay>();
            if (_fogOverlay == null)
                _fogOverlay = minimapImage.gameObject.AddComponent<MinimapFogOverlay>();

            _fogOverlay.Initialize(minimapImage.rectTransform);
        }

        private void EnsureOverlayRenderer()
        {
            if (minimapImage == null) return;

            _overlayRenderer = minimapImage.GetComponent<MinimapOverlayRenderer>();
            if (_overlayRenderer == null)
                _overlayRenderer = minimapImage.gameObject.AddComponent<MinimapOverlayRenderer>();

            _overlayRenderer.Initialize(minimapImage.rectTransform);
        }

        private void EnsureRuntimeReferences()
        {
            if (_mapStateService == null) _mapStateService = FindFirstObjectByType<MapStateService>();
        }

        private void RefreshRegionLabelFromMapState()
        {
            if (_mapStateService == null) return;

            var region = _mapStateService.CurrentRegion;
            var regionName = region != null && !string.IsNullOrWhiteSpace(region.RegionId)
                ? region.RegionId
                : "Unknown Region";
            SetRegionLabel(regionName);
        }

        private static bool IsWorldSessionActive()
        {
            return WorldSessionGate.IsWorldSessionActive;
        }

        private void BindMinimapCamera()
        {
            // Find a Camera tagged "MinimapCamera" and assign its render texture to the UI.
            var minimapCam = TryFindMinimapCamera();

            if (minimapCam != null && minimapImage != null && minimapCam.targetTexture != null)
                minimapImage.texture = minimapCam.targetTexture;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetRenderTexture(RenderTexture minimapRenderTexture)
        {
            if (minimapImage != null) minimapImage.texture = minimapRenderTexture;
        }

        public void SetRegionLabel(string regionName)
        {
            if (regionLabelText != null)
                regionLabelText.text = string.IsNullOrWhiteSpace(regionName) ? "Unknown Region" : regionName;
        }

        public void SetZoom(float zoom)
        {
            CurrentZoom = Mathf.Clamp(zoom, minZoom, maxZoom);

            if (zoomSlider != null) zoomSlider.value = CurrentZoom;

            // Push zoom level to the orthographic size of the minimap camera.
            var minimapCam = TryFindMinimapCamera();

            if (minimapCam != null && minimapCam.orthographic)
                minimapCam.orthographicSize = 40f / Mathf.Max(0.01f, CurrentZoom);
        }

        private static Camera TryFindMinimapCamera()
        {
            const string minimapCameraTag = "MinimapCamera";

            try
            {
                var tagged = GameObject.FindWithTag(minimapCameraTag);
                return tagged != null ? tagged.GetComponent<Camera>() : null;
            }
            catch
            {
                // Tag doesn't exist in Tags & Layers. Prefer SetRenderTexture() wiring instead of hard failure.
                return null;
            }
        }
    }
}