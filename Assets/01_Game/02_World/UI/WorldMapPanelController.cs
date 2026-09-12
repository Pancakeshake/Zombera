using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.World;

namespace Zombera.UI
{
  /// <summary>
  ///     Full-screen world map panel with fog overlay, pan/zoom, waypoint placement, and marker filters.
  /// </summary>
  public sealed partial class WorldMapPanelController : MonoBehaviour
  {
    [SerializeField] [Min(32f)] private float viewWorldSize = 256f;
    [SerializeField] [Min(1f)] private float minZoom = 0.5f;
    [SerializeField] [Min(1f)] private float maxZoom = 4f;

    private MapStateService _mapStateService;
    private MapMarkerManager _mapMarkerManager;
    private RectTransform _hostRoot;
    private RawImage _fogImage;
    private RawImage _baseImage;
    private RectTransform _markerOverlayRoot;
    private TMP_Text _regionLabel;
    private bool _isBuilt;

    public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground)
    {
      if (host == null || _isBuilt) return;

      _hostRoot = host;
      ClearChildren(host);

      var background = CreateStretchImage(host, "Background", panelBackground, new Color(0.10f, 0.11f, 0.12f, 0.98f));

      var header = CreateRect("Header", host);
      Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -8f), new Vector2(-8f, -52f));
      AddImage(header, new Color(0.16f, 0.17f, 0.18f, 0.98f), panelBackground);

      _regionLabel = CreateText(header, "Unknown Region", 18f, font, TextAlignmentOptions.MidlineLeft);
      Stretch(_regionLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(12f, 0f), Vector2.zero);

      var hint = CreateText(header, "Drag pan · Scroll zoom · Click waypoint", 13f, font, TextAlignmentOptions.MidlineRight);
      Stretch(hint.rectTransform, new Vector2(0.55f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-12f, 0f));
      hint.color = new Color(0.72f, 0.75f, 0.78f, 0.9f);

      var mapFrame = CreateRect("MapFrame", host);
      Stretch(mapFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 58f), new Vector2(-8f, -8f));
      AddImage(mapFrame, new Color(0.05f, 0.06f, 0.07f, 1f), panelBackground);

      var mapViewport = CreateRect("MapViewport", mapFrame);
      Stretch(mapViewport, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

      _baseImage = CreateStretchRawImage(mapViewport, "BaseMap", new Color(0.12f, 0.14f, 0.16f, 1f));
      _fogImage = CreateStretchRawImage(mapViewport, "FogMap", Color.white);
      _fogImage.color = new Color(1f, 1f, 1f, 0.92f);

      _markerOverlayRoot = CreateRect("MarkerOverlay", mapViewport);
      Stretch(_markerOverlayRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      var inputRelay = mapViewport.gameObject.AddComponent<MapPanelInputRelay>();
      inputRelay.Initialize(this);

      BuildMarkerFilterBar(mapFrame, font, panelBackground);
      EnsureRuntimeReferences();
      _isBuilt = true;
    }

    public void Initialize()
    {
      if (_hostRoot == null) _hostRoot = transform as RectTransform;
      if (!_isBuilt && _hostRoot != null)
      {
        var font = TMP_Settings.defaultFontAsset;
        Build(_hostRoot, font, null);
      }

      EnsureRuntimeReferences();
    }

    private void OnEnable()
    {
      EnsureRuntimeReferences();
      if (_mapMarkerManager != null) _mapMarkerManager.MarkersChanged += HandleMarkersChanged;
    }

    private void OnDisable()
    {
      if (_mapMarkerManager != null) _mapMarkerManager.MarkersChanged -= HandleMarkersChanged;
    }

    private void LateUpdate()
    {
      if (!IsWorldSessionActive()) return;
      if (!_isBuilt) return;

      RefreshFogTexture();
      RefreshRegionLabel();
      RefreshMarkerOverlay();
    }

    private void EnsureRuntimeReferences()
    {
      if (_mapStateService == null) _mapStateService = FindFirstObjectByType<MapStateService>();
      if (_mapMarkerManager == null)
        _mapMarkerManager = MapMarkerManager.Instance ?? FindFirstObjectByType<MapMarkerManager>();
    }

    private void RefreshFogTexture()
    {
      if (_fogImage == null || _mapStateService == null) return;

      var fog = _mapStateService.FogTexture;
      if (_fogImage.texture != fog) _fogImage.texture = fog;
    }

    private void RefreshRegionLabel()
    {
      if (_regionLabel == null || _mapStateService == null) return;

      var region = _mapStateService.CurrentRegion;
      var regionName = region != null && !string.IsNullOrWhiteSpace(region.RegionId)
        ? region.RegionId
        : "Unknown Region";
      _regionLabel.text = regionName;
    }

    internal float CurrentZoom => _mapStateService != null ? _mapStateService.MapZoom : 1f;
    internal Vector2 CurrentPan => _mapStateService != null ? _mapStateService.MapPan : Vector2.zero;
    internal float ViewWorldSize => viewWorldSize;
    internal float MinZoom => minZoom;
    internal float MaxZoom => maxZoom;

    internal Vector3 MapCenterWorld
    {
      get
      {
        if (_mapStateService == null) return Vector3.zero;

        var player = _mapStateService.CurrentPlayerWorldPosition;
        var pan = _mapStateService.MapPan;
        return new Vector3(player.x + pan.x, 0f, player.z + pan.y);
      }
    }

    internal float ScaledViewWorldSize => viewWorldSize / Mathf.Max(0.01f, CurrentZoom);

    internal Rect MapRect => _fogImage != null ? _fogImage.rectTransform.rect : Rect.zero;

    internal void ApplyView(float zoom, Vector2 pan)
    {
      _mapStateService?.SetView(
        Mathf.Clamp(zoom, minZoom, maxZoom),
        pan);
    }

    internal bool TryScreenPointToWorld(Vector2 screenPoint, out Vector3 worldPosition)
    {
      worldPosition = default;
      if (_fogImage == null) return false;

      var rectTransform = _fogImage.rectTransform;
      if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, null, out var local))
        return false;

      return TryLocalPointToWorld(local, rectTransform.rect, out worldPosition);
    }

    internal bool TryLocalPointToWorld(Vector2 localPoint, Rect mapRect, out Vector3 worldPosition)
    {
      var viewSize = ScaledViewWorldSize;
      var center = MapCenterWorld;
      var normalized = new Vector2(
        localPoint.x / Mathf.Max(1f, mapRect.width * 0.5f),
        localPoint.y / Mathf.Max(1f, mapRect.height * 0.5f));
      worldPosition = new Vector3(
        center.x + normalized.x * viewSize,
        0f,
        center.z + normalized.y * viewSize);
      return true;
    }

    internal bool TryWorldToLocalPoint(Vector3 worldPosition, out Vector2 localPoint)
    {
      localPoint = default;
      if (_fogImage == null) return false;

      var mapRect = _fogImage.rectTransform.rect;
      var viewSize = ScaledViewWorldSize;
      var center = MapCenterWorld;
      var offset = new Vector2(worldPosition.x - center.x, worldPosition.z - center.z);
      localPoint = new Vector2(
        offset.x / Mathf.Max(0.01f, viewSize) * mapRect.width * 0.5f,
        offset.y / Mathf.Max(0.01f, viewSize) * mapRect.height * 0.5f);
      return true;
    }

    private static bool IsWorldSessionActive()
    {
      return WorldSessionGate.IsWorldSessionActive;
    }

    private void HandleMarkersChanged()
    {
      RefreshMarkerOverlay();
    }

    private static void ClearChildren(RectTransform root)
    {
      for (var i = root.childCount - 1; i >= 0; i--)
        Destroy(root.GetChild(i).gameObject);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
      rect.anchorMin = anchorMin;
      rect.anchorMax = anchorMax;
      rect.offsetMin = offsetMin;
      rect.offsetMax = offsetMax;
    }

    private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
    {
      var image = rect.gameObject.AddComponent<Image>();
      image.color = color;
      if (sprite != null)
      {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
      }

      return image;
    }

    private static Image CreateStretchImage(RectTransform parent, string name, Sprite sprite, Color color)
    {
      var rect = CreateRect(name, parent);
      Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      return AddImage(rect, color, sprite);
    }

    private static RawImage CreateStretchRawImage(RectTransform parent, string name, Color color)
    {
      var rect = CreateRect(name, parent);
      Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      var raw = rect.gameObject.AddComponent<RawImage>();
      raw.color = color;
      return raw;
    }

    private static TMP_Text CreateText(RectTransform parent, string text, float size, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
      var rect = CreateRect("Text", parent);
      Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
      tmp.text = text;
      tmp.fontSize = size;
      tmp.alignment = alignment;
      if (font != null) tmp.font = font;
      tmp.raycastTarget = false;
      return tmp;
    }
  }
}
