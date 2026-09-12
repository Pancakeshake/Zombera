#region

using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.Environment;

#endregion

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Top Bar Controller")]
    [DisallowMultipleComponent]
    public sealed class TopBarController : MonoBehaviour
    {
        // ── Serialized ────────────────────────────────────────────────────────

        [Header("Time Display")] [SerializeField]
        private TextMeshProUGUI dayTimeText;

        [Header("Time Buttons")] [SerializeField]
        private Button pauseButton;

        [SerializeField] private Button speed1XButton;
        [SerializeField] private Button speed2XButton;
        [SerializeField] private Button speed4XButton;

        [Header("Tab Buttons")] [SerializeField]
        private Button squadTabButton;

        [SerializeField] private Button inventoryTabButton;
        [SerializeField] private Button craftingTabButton;
        [SerializeField] private Button mapTabButton;
        [SerializeField] private Button missionsTabButton;
        [SerializeField] private Button formationsTabButton;
        [SerializeField] private Button jobsTabButton;
        [SerializeField] private Button factionsTabButton;

        [Header("Style")] [SerializeField] private Color tabNormalColor = new(0.12f, 0.13f, 0.16f, 1f);

        [SerializeField] private Color tabActiveColor = new(0.20f, 0.52f, 0.36f, 1f);
        [SerializeField] private Color speedNormalColor = new(0.14f, 0.15f, 0.18f, 1f);
        [SerializeField] private Color speedActiveColor = new(0.20f, 0.52f, 0.36f, 1f);
        private DayNightController _dayNight;

        // ── State ─────────────────────────────────────────────────────────────

        private WorldHUDController _hud;
        private float _speed = 1f;
        private TimeSystem _timeSystem;
        private int _lastDisplayedDay = -1;
        private int _lastDisplayedHour = -1;
        private int _lastDisplayedMinute = -1;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _hud = GetComponentInParent<WorldHUDController>();
        }

        private void Start()
        {
            _timeSystem = FindFirstObjectByType<TimeSystem>();
            _dayNight = DayNightController.Instance;

            ResolveTimeButtonsIfMissing();
            ResolveTabButtonsIfMissing();

            squadTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Squad));
            inventoryTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Inventory));
            craftingTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Crafting));
            mapTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Map));
            missionsTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Missions));
            formationsTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Formations));
            jobsTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Jobs));
            factionsTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Factions));

            pauseButton?.onClick.AddListener(TogglePause);
            speed1XButton?.onClick.AddListener(() => ApplySpeed(1f));
            speed2XButton?.onClick.AddListener(() => ApplySpeed(2f));
            speed4XButton?.onClick.AddListener(() => ApplySpeed(4f));

            RefreshSpeedHighlights();
        }

        private void ResolveTimeButtonsIfMissing()
        {
            var buttons = GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0) return;

            pauseButton ??= FindButton(buttons, "PauseButton", "Pause");
            speed1XButton ??= FindButton(buttons, "Speed1xButton", "Speed1XButton", "Speed1x", "Speed1X", "1x",
                "1X", "Btn_1x");
            speed2XButton ??= FindButton(buttons, "Speed2xButton", "Speed2XButton", "Speed2x", "Speed2X", "2x",
                "2X", "Btn_2x");
            speed4XButton ??= FindButton(buttons, "Speed4xButton", "Speed4XButton", "Speed4x", "Speed4X", "4x",
                "4X", "Btn_4x");
        }

        private void ResolveTabButtonsIfMissing()
        {
            var buttons = GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0) return;

            squadTabButton ??= FindButton(buttons, "Tab_Squad");
            inventoryTabButton ??= FindButton(buttons, "Tab_Inventory");
            craftingTabButton ??= FindButton(buttons, "Tab_Crafting");
            mapTabButton ??= FindButton(buttons, "Tab_Map");
            missionsTabButton ??= FindButton(buttons, "Tab_Missions");
            formationsTabButton ??= FindButton(buttons, "Tab_Formations");
            jobsTabButton ??= FindButton(buttons, "Tab_Jobs");
            factionsTabButton ??= FindButton(buttons, "Tab_Factions", "Tab_Faction");
        }

        private static Button FindButton(Button[] buttons, params string[] names)
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null) continue;

                var n = btn.name;
                for (var j = 0; j < names.Length; j++)
                {
                    if (string.Equals(n, names[j], StringComparison.Ordinal)) return btn;
                }
            }

            return null;
        }

        private void Update()
        {
            // Space = toggle pause
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
                TogglePause();

            // Refresh day-time label (string rebuilt only when the displayed minute changes).
            if (_dayNight == null) _dayNight = DayNightController.Instance;
            if (dayTimeText == null || _dayNight == null) return;

            var day = _dayNight.DayNumber;
            var h = Mathf.FloorToInt(_dayNight.CurrentHour);
            var m = Mathf.FloorToInt((_dayNight.CurrentHour - h) * 60f);

            if (day == _lastDisplayedDay && h == _lastDisplayedHour && m == _lastDisplayedMinute) return;

            _lastDisplayedDay = day;
            _lastDisplayedHour = h;
            _lastDisplayedMinute = m;
            dayTimeText.text = $"DAY {day}  |  {h:00}:{m:00}";
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by WorldHUDController whenever the active tab changes.</summary>
        public void SetActiveTabHighlight(WorldHUDController.TabId active)
        {
            SetTabColor(squadTabButton, active == WorldHUDController.TabId.Squad);
            SetTabColor(inventoryTabButton, active == WorldHUDController.TabId.Inventory);
            SetTabColor(craftingTabButton, active == WorldHUDController.TabId.Crafting);
            SetTabColor(mapTabButton, active == WorldHUDController.TabId.Map);
            SetTabColor(missionsTabButton, active == WorldHUDController.TabId.Missions);
            SetTabColor(formationsTabButton, active == WorldHUDController.TabId.Formations);
            SetTabColor(jobsTabButton, active == WorldHUDController.TabId.Jobs);
            SetTabColor(factionsTabButton, active == WorldHUDController.TabId.Factions);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void TogglePause()
        {
            if (_timeSystem == null) return;

            // Build placement should remain interactive; ignore pause requests while it is active.
            if (_hud != null && _hud.IsBuildPlacementModeActive && !_timeSystem.IsPaused)
            {
                RefreshSpeedHighlights();
                return;
            }

            _timeSystem.TogglePause();
            RefreshSpeedHighlights();
        }

        private void ApplySpeed(float s)
        {
            if (_timeSystem == null) return;
            _speed = s;
            if (_timeSystem.IsPaused) _timeSystem.RequestResume();
            _timeSystem.SetTimeScale(s);

            RefreshSpeedHighlights();
        }

        private void RefreshSpeedHighlights()
        {
            var paused = _timeSystem != null && _timeSystem.IsPaused;

            if (_timeSystem != null && !paused)
            {
                var actualSpeed = _timeSystem.CurrentTimeScale;
                if (Mathf.Abs(actualSpeed - 1f) < 0.05f) _speed = 1f;
                else if (Mathf.Abs(actualSpeed - 2f) < 0.05f) _speed = 2f;
                else if (Mathf.Abs(actualSpeed - 4f) < 0.05f) _speed = 4f;
                else _speed = actualSpeed;
            }

            SetSpeedColor(pauseButton, paused);
            SetSpeedColor(speed1XButton, !paused && Mathf.Approximately(_speed, 1f));
            SetSpeedColor(speed2XButton, !paused && Mathf.Approximately(_speed, 2f));
            SetSpeedColor(speed4XButton, !paused && Mathf.Approximately(_speed, 4f));
        }

        private void SetTabColor(Button btn, bool on)
        {
            if (!btn || btn.targetGraphic is not Image img) return;

            img.color = on ? tabActiveColor : tabNormalColor;
        }

        private void SetSpeedColor(Button btn, bool on)
        {
            if (!btn || btn.targetGraphic is not Image img) return;

            img.color = on ? speedActiveColor : speedNormalColor;
        }
    }
}