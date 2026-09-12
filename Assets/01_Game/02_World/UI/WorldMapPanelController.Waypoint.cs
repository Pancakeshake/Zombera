using UnityEngine;
using UnityEngine.EventSystems;

namespace Zombera.UI
{
  public sealed partial class WorldMapPanelController
  {
    internal void HandleMapScroll(PointerEventData eventData)
    {
      if (_mapStateService == null) return;

      var zoomDelta = eventData.scrollDelta.y * 0.08f;
      var nextZoom = Mathf.Clamp(CurrentZoom + zoomDelta, MinZoom, MaxZoom);
      ApplyView(nextZoom, CurrentPan);
    }

    internal void HandleMapDrag(PointerEventData eventData)
    {
      if (_mapStateService == null) return;

      var viewSize = ScaledViewWorldSize;
      var rect = MapRect;
      if (rect.width <= 0f || rect.height <= 0f) return;

      var worldPerPixelX = viewSize / rect.width;
      var worldPerPixelY = viewSize / rect.height;
      var delta = eventData.delta;
      var pan = CurrentPan;
      pan.x -= delta.x * worldPerPixelX;
      pan.y -= delta.y * worldPerPixelY;
      ApplyView(CurrentZoom, pan);
    }

    internal void HandleMapClick(PointerEventData eventData)
    {
      if (_mapStateService == null) return;
      if (!TryScreenPointToWorld(eventData.position, out var worldPosition)) return;

      _mapStateService.SetWaypoint(worldPosition);
    }
  }

  internal sealed class MapPanelInputRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler, IPointerClickHandler
  {
    private WorldMapPanelController _controller;

    public void Initialize(WorldMapPanelController controller)
    {
      _controller = controller;
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
      _controller?.HandleMapDrag(eventData);
    }

    public void OnScroll(PointerEventData eventData)
    {
      _controller?.HandleMapScroll(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
      if (eventData.dragging) return;
      _controller?.HandleMapClick(eventData);
    }
  }
}
