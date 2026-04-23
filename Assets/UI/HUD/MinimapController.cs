using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    /// <summary>
    /// Controls minimap rendering references and marker UI state.
    /// </summary>
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;

        [Header("Map")]
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private TextMeshProUGUI regionLabelText;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private RectTransform squadMarkersRoot;

        [Header("Zoom")]
        [SerializeField] private Slider zoomSlider;
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 2f;

        public bool IsInitialized { get; private set; }
        public float CurrentZoom { get; private set; } = 1f;

        private HUDManager hudManager;

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized)
            {
                return;
            }

            hudManager = manager;

            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            SetZoom(1f);
            SetRegionLabel("Unknown Region");

            IsInitialized = true;
            BindMinimapCamera();
        }

        private void BindMinimapCamera()
        {
            // Find a Camera tagged "MinimapCamera" and assign its render texture to the UI.
            Camera minimapCam = GameObject.FindWithTag("MinimapCamera")?.GetComponent<Camera>();

            if (minimapCam != null && minimapImage != null && minimapCam.targetTexture != null)
            {
                minimapImage.texture = minimapCam.targetTexture;
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetRenderTexture(RenderTexture minimapRenderTexture)
        {
            if (minimapImage != null)
            {
                minimapImage.texture = minimapRenderTexture;
            }
        }

        public void SetRegionLabel(string regionName)
        {
            if (regionLabelText != null)
            {
                regionLabelText.text = string.IsNullOrWhiteSpace(regionName) ? "Unknown Region" : regionName;
            }
        }

        public void SetZoom(float zoom)
        {
            CurrentZoom = Mathf.Clamp(zoom, minZoom, maxZoom);

            if (zoomSlider != null)
            {
                zoomSlider.value = CurrentZoom;
            }

            // Push zoom level to the orthographic size of the minimap camera.
            Camera minimapCam = GameObject.FindWithTag("MinimapCamera")?.GetComponent<Camera>();

            if (minimapCam != null && minimapCam.orthographic)
            {
                minimapCam.orthographicSize = 40f / Mathf.Max(0.01f, CurrentZoom);
            }
        }

        public void SetPlayerRotation(float yRotationDegrees)
        {
            if (playerMarker != null)
            {
                playerMarker.localRotation = Quaternion.Euler(0f, 0f, -yRotationDegrees);
            }
        }

        public void SetMarkersVisible(bool visible)
        {
            if (squadMarkersRoot != null)
            {
                squadMarkersRoot.gameObject.SetActive(visible);
            }
        }
    }
}