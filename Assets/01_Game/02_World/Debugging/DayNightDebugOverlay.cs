#region

using UnityEngine;
using Zombera.Core;
using Zombera.Debugging;

#endregion

namespace Zombera.Environment
{
    [AddComponentMenu("Zombera/Environment/Day Night Debug Overlay")]
    [DisallowMultipleComponent]
    public sealed class DayNightDebugOverlay : MonoBehaviour
    {
        [SerializeField] private DayNightController dayNightController;
        [SerializeField] private bool showTimeDebugReadout;
        [SerializeField] private bool showTimeDebugReadoutOnlyWhenDebugMenuVisible = true;
        [SerializeField] private Vector2 debugReadoutScreenOffset = new(14f, 14f);

        [SerializeField] [HideInInspector] private bool legacySettingsImported;

        private GUIStyle _debugLabelStyle;
        private TimeSystem _timeSystem;

        private void Awake()
        {
            ResolveController();
        }

        private void OnGUI()
        {
            if (!showTimeDebugReadout || !Application.isPlaying) return;

            if (showTimeDebugReadoutOnlyWhenDebugMenuVisible)
            {
                var debugManager = DebugManager.Instance;
                if (debugManager == null || !debugManager.IsDebugMenuVisible) return;
            }

            ResolveController();
            if (dayNightController == null) return;

            if (_timeSystem == null)
                _timeSystem = FindFirstObjectByType<TimeSystem>();

            var timeScale = _timeSystem != null ? _timeSystem.CurrentTimeScale : 1f;
            var enviroTimeText = "n/a";

            if (dayNightController.TryReadEnviroTime(out var enviroTime))
                enviroTimeText = DayNightClockLogic.FormatHour(enviroTime);

            _debugLabelStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8)
            };

            var panelRect = new Rect(debugReadoutScreenOffset.x, debugReadoutScreenOffset.y, 290f, 96f);
            var text =
                $"<b>Time Debug</b>\nDayNight Hour: {DayNightClockLogic.FormatHour(dayNightController.CurrentHour)}\nEnviro TOD: {enviroTimeText}\nTime Scale: x{timeScale:0.##}";

            GUI.Box(panelRect, text, _debugLabelStyle);
        }

        internal void TryImportLegacySettings(bool showReadout, bool onlyWhenDebugMenuVisible, Vector2 screenOffset)
        {
            if (legacySettingsImported) return;

            showTimeDebugReadout = showReadout;
            showTimeDebugReadoutOnlyWhenDebugMenuVisible = onlyWhenDebugMenuVisible;
            debugReadoutScreenOffset = screenOffset;
            legacySettingsImported = true;
        }

        private void ResolveController()
        {
            if (dayNightController != null) return;

            dayNightController = GetComponent<DayNightController>();
            if (dayNightController == null)
                dayNightController = DayNightController.Instance;
        }
    }
}
