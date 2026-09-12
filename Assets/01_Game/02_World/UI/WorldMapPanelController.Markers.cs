using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.World;

namespace Zombera.UI
{
  public sealed partial class WorldMapPanelController
  {
    private readonly List<RectTransform> _markerDots = new();
    private readonly List<MapMarkerRuntimeData> _markerScratch = new();

    private RectTransform _filterBarRoot;

    private void BuildMarkerFilterBar(RectTransform mapFrame, TMP_FontAsset font, Sprite panelBackground)
    {
      _filterBarRoot = CreateRect("FilterBar", mapFrame);
      Stretch(_filterBarRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 6f), new Vector2(-6f, 46f));
      AddImage(_filterBarRoot, new Color(0.14f, 0.15f, 0.16f, 0.96f), panelBackground);

      var layout = _filterBarRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
      layout.padding = new RectOffset(6, 6, 4, 4);
      layout.spacing = 6f;
      layout.childAlignment = TextAnchor.MiddleLeft;
      layout.childControlWidth = false;
      layout.childControlHeight = true;
      layout.childForceExpandWidth = false;
      layout.childForceExpandHeight = true;

      CreateFilterToggle("Player", MapMarkerType.Player, font);
      CreateFilterToggle("Squad", MapMarkerType.Squad, font);
      CreateFilterToggle("Mission", MapMarkerType.Mission, font);
      CreateFilterToggle("POI", MapMarkerType.POI, font);
      CreateFilterToggle("Waypoint", MapMarkerType.Waypoint, font);
    }

    private void CreateFilterToggle(string label, MapMarkerType type, TMP_FontAsset font)
    {
      var buttonRoot = CreateRect(label + "Filter", _filterBarRoot);
      var layout = buttonRoot.gameObject.AddComponent<LayoutElement>();
      layout.minWidth = 88f;
      layout.preferredHeight = 30f;

      var background = AddImage(buttonRoot, ResolveMarkerColor(type, 0.85f), null);
      var button = buttonRoot.gameObject.AddComponent<Button>();
      button.targetGraphic = background;
      button.onClick.AddListener(() => ToggleMarkerType(type, background));

      var text = CreateText(buttonRoot, label, 13f, font, TextAlignmentOptions.Center);
      text.color = Color.white;
      text.fontStyle = FontStyles.Bold;
    }

    private void ToggleMarkerType(MapMarkerType type, Image background)
    {
      if (_mapMarkerManager == null) return;

      var nextVisible = !_mapMarkerManager.IsMarkerTypeVisible(type);
      _mapMarkerManager.SetMarkerTypeVisible(type, nextVisible);
      background.color = nextVisible
        ? ResolveMarkerColor(type, 0.85f)
        : new Color(0.22f, 0.24f, 0.27f, 0.75f);
      RefreshMarkerOverlay();
    }

    private void RefreshMarkerOverlay()
    {
      if (_markerOverlayRoot == null || _mapMarkerManager == null) return;

      var markers = _mapMarkerManager.GetVisibleMarkers(_markerScratch);
      EnsureMarkerDotPool(markers.Count);

      for (var i = 0; i < _markerDots.Count; i++)
        _markerDots[i].gameObject.SetActive(false);

      for (var i = 0; i < markers.Count; i++)
      {
        if (!TryWorldToLocalPoint(markers[i].worldPosition, out var local)) continue;

        var dot = _markerDots[i];
        dot.gameObject.SetActive(true);
        dot.anchoredPosition = local;

        var image = dot.GetComponent<Image>();
        if (image != null) image.color = ResolveMarkerColor(markers[i].type, 1f);

        if (markers[i].type == MapMarkerType.Player)
          dot.localRotation = Quaternion.Euler(0f, 0f, -markers[i].headingDegrees);
        else
          dot.localRotation = Quaternion.identity;
      }
    }

    private void EnsureMarkerDotPool(int requiredCount)
    {
      while (_markerDots.Count < requiredCount)
      {
        var dot = CreateRect("Marker", _markerOverlayRoot);
        dot.sizeDelta = new Vector2(10f, 10f);
        var image = dot.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        _markerDots.Add(dot);
      }
    }

    private static Color ResolveMarkerColor(MapMarkerType type, float alpha)
    {
      var color = type switch
      {
        MapMarkerType.Player => new Color(0.25f, 0.75f, 1f, alpha),
        MapMarkerType.Squad => new Color(0.35f, 0.9f, 0.45f, alpha),
        MapMarkerType.Mission => new Color(1f, 0.78f, 0.2f, alpha),
        MapMarkerType.POI => new Color(0.85f, 0.55f, 0.95f, alpha),
        MapMarkerType.Waypoint => new Color(1f, 0.35f, 0.35f, alpha),
        MapMarkerType.WorldEvent => new Color(1f, 0.45f, 0.2f, alpha),
        _ => new Color(0.8f, 0.8f, 0.8f, alpha)
      };
      return color;
    }
  }
}
