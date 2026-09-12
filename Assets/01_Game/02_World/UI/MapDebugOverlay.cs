using UnityEngine;
using Zombera.Core;
using Zombera.World;

namespace Zombera.UI
{
  /// <summary>
  ///     Optional runtime diagnostics for the world map stack.
  /// </summary>
  public sealed class MapDebugOverlay : MonoBehaviour
  {
    [SerializeField] private bool enableDebugOverlay;

    private MapStateService _mapStateService;
    private MapMarkerManager _mapMarkerManager;

    private void LateUpdate()
    {
      if (!enableDebugOverlay) return;
      EnsureReferences();
    }

    private void OnGUI()
    {
      if (!enableDebugOverlay) return;
      if (!IsWorldSessionActive()) return;

      EnsureReferences();
      if (_mapStateService == null) return;

      var regionName = _mapStateService.CurrentRegion != null
        ? _mapStateService.CurrentRegion.RegionId
        : "Unknown";
      var chunk = _mapStateService.CurrentPlayerChunk;
      var discovered = _mapStateService.DiscoveredChunks.Count;
      var pois = _mapStateService.Pois.Count;
      var markers = _mapMarkerManager != null ? _mapMarkerManager.VisibleMarkerCount : 0;

      var text =
        $"Map Debug\n" +
        $"Chunk: ({chunk.x}, {chunk.y})\n" +
        $"Region: {regionName}\n" +
        $"Discovered chunks: {discovered}\n" +
        $"POIs: {pois}\n" +
        $"Visible markers: {markers}";

      GUI.color = new Color(0f, 0f, 0f, 0.65f);
      GUI.Box(new Rect(12f, 120f, 240f, 124f), GUIContent.none);
      GUI.color = Color.white;
      GUI.Label(new Rect(20f, 128f, 220f, 112f), text);
    }

    private void EnsureReferences()
    {
      if (_mapStateService == null) _mapStateService = FindFirstObjectByType<MapStateService>();
      if (_mapMarkerManager == null)
        _mapMarkerManager = MapMarkerManager.Instance ?? FindFirstObjectByType<MapMarkerManager>();
    }

    private static bool IsWorldSessionActive()
    {
      return WorldSessionGate.IsWorldSessionActive;
    }
  }
}
