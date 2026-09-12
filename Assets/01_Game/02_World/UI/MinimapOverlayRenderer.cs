using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.World;

namespace Zombera.UI
{
  /// <summary>
  ///     Draws map markers above the minimap camera quad without altering the minimap render pipeline.
  /// </summary>
  public sealed class MinimapOverlayRenderer : MonoBehaviour
  {
    [SerializeField] private RectTransform minimapImageRect;
    [SerializeField] [Min(32f)] private float minimapWorldRadius = 48f;

    private MapMarkerManager _mapMarkerManager;
    private MapStateService _mapStateService;
    private RectTransform _overlayRoot;
    private readonly List<RectTransform> _markerDots = new();
    private readonly List<MapMarkerRuntimeData> _markerScratch = new();

    public void Initialize(RectTransform minimapRect)
    {
      minimapImageRect = minimapRect;
      EnsureOverlayRoot();
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
      EnsureOverlayRoot();
      EnsureRuntimeReferences();
      RefreshMarkers();
    }

    private void EnsureOverlayRoot()
    {
      if (_overlayRoot != null || minimapImageRect == null) return;

      _overlayRoot = new GameObject("MinimapMarkerOverlay", typeof(RectTransform)).GetComponent<RectTransform>();
      _overlayRoot.SetParent(minimapImageRect, false);
      _overlayRoot.anchorMin = Vector2.zero;
      _overlayRoot.anchorMax = Vector2.one;
      _overlayRoot.offsetMin = Vector2.zero;
      _overlayRoot.offsetMax = Vector2.zero;
    }

    private void EnsureRuntimeReferences()
    {
      if (_mapStateService == null) _mapStateService = FindFirstObjectByType<MapStateService>();
      if (_mapMarkerManager == null)
        _mapMarkerManager = MapMarkerManager.Instance ?? FindFirstObjectByType<MapMarkerManager>();
    }

    private void HandleMarkersChanged()
    {
      RefreshMarkers();
    }

    private void RefreshMarkers()
    {
      if (_overlayRoot == null || _mapMarkerManager == null || _mapStateService == null) return;

      var markers = _mapMarkerManager.GetVisibleMarkers(_markerScratch);
      EnsureMarkerDotPool(markers.Count);

      for (var i = 0; i < _markerDots.Count; i++)
        _markerDots[i].gameObject.SetActive(false);

      var center = _mapStateService.CurrentPlayerWorldPosition;
      var mapRect = minimapImageRect.rect;

      for (var i = 0; i < markers.Count; i++)
      {
        var marker = markers[i];
        var offset = new Vector2(marker.worldPosition.x - center.x, marker.worldPosition.z - center.z);
        var normalized = offset / Mathf.Max(1f, minimapWorldRadius);
        if (normalized.sqrMagnitude > 1f) continue;

        var dot = _markerDots[i];
        dot.gameObject.SetActive(true);
        dot.anchoredPosition = new Vector2(
          normalized.x * mapRect.width * 0.5f,
          normalized.y * mapRect.height * 0.5f);

        var image = dot.GetComponent<Image>();
        if (image != null) image.color = ResolveMarkerColor(marker.type);

        dot.localRotation = marker.type == MapMarkerType.Player
          ? Quaternion.Euler(0f, 0f, -marker.headingDegrees)
          : Quaternion.identity;
      }
    }

    private void EnsureMarkerDotPool(int requiredCount)
    {
      while (_markerDots.Count < requiredCount)
      {
        var dot = new GameObject("Marker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        dot.SetParent(_overlayRoot, false);
        dot.sizeDelta = new Vector2(8f, 8f);
        var image = dot.GetComponent<Image>();
        image.raycastTarget = false;
        _markerDots.Add(dot);
      }
    }

    private static Color ResolveMarkerColor(MapMarkerType type)
    {
      return type switch
      {
        MapMarkerType.Player => new Color(0.25f, 0.75f, 1f, 1f),
        MapMarkerType.Squad => new Color(0.35f, 0.9f, 0.45f, 1f),
        MapMarkerType.Mission => new Color(1f, 0.78f, 0.2f, 1f),
        MapMarkerType.POI => new Color(0.85f, 0.55f, 0.95f, 1f),
        MapMarkerType.Waypoint => new Color(1f, 0.35f, 0.35f, 1f),
        MapMarkerType.WorldEvent => new Color(1f, 0.45f, 0.2f, 1f),
        _ => new Color(0.8f, 0.8f, 0.8f, 1f)
      };
    }

    private static bool IsWorldSessionActive()
    {
      return WorldSessionGate.IsWorldSessionActive;
    }
  }
}
