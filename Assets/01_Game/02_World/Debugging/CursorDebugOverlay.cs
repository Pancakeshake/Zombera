#region

using UnityEngine;
using Zombera.Debugging;

#endregion

namespace Zombera.Systems
{
    [AddComponentMenu("Zombera/Input/Cursor Debug Overlay")]
    [DisallowMultipleComponent]
    public sealed class CursorDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showCursorDebugReadout = true;
        [SerializeField] private bool showOnlyWhenDebugMenuVisible = true;
        [SerializeField] private Vector2 screenOffset = new(14f, 120f);

        private GUIStyle _labelStyle;
        private Vector2 _lastRawPointer;
        private Vector2 _lastGameplayPointer;

        private void Update()
        {
            CursorService.TryGetRawPointerScreenPosition(out _lastRawPointer);
            CursorService.TryGetGameplayPointerScreenPosition(out _lastGameplayPointer);
        }

        private void OnGUI()
        {
            if (!showCursorDebugReadout || !Application.isPlaying) return;

            if (showOnlyWhenDebugMenuVisible)
            {
                var debugManager = DebugManager.Instance;
                if (debugManager == null || !debugManager.IsDebugMenuVisible) return;
            }

            EnsureLabelStyle();

            var presentation = CursorService.CurrentPresentation;
            var calibration = CursorService.GetActiveCalibrationOffsetPixels();
            var delta = _lastGameplayPointer - _lastRawPointer;

            var text =
                "Cursor Debug\n" +
                $"icon={CursorService.ActiveIconIntent}\n" +
                $"visible={presentation.Visible} lock={presentation.LockMode}\n" +
                $"requests={CursorService.ActiveRequestCount}\n" +
                $"hotspot={CursorService.AppliedHotspot:0.#}\n" +
                $"raw=({_lastRawPointer.x:0}, {_lastRawPointer.y:0})\n" +
                $"pointer=({_lastGameplayPointer.x:0}, {_lastGameplayPointer.y:0})\n" +
                $"hotspotCal=({calibration.x:0.##}, {calibration.y:0.##})\n" +
                $"delta=({delta.x:0.##}, {delta.y:0.##})";

            var size = _labelStyle.CalcSize(new GUIContent(text));
            var rect = new Rect(screenOffset.x, screenOffset.y, size.x + 12f, size.y + 8f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), text, _labelStyle);
        }

        private void EnsureLabelStyle()
        {
            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = false,
                wordWrap = false
            };
            _labelStyle.normal.textColor = Color.white;
        }
    }
}
