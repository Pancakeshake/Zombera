using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Zombera.Core;
using Zombera.Environment;

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Top Bar Controller")]
    [DisallowMultipleComponent]
    public sealed class TopBarController : MonoBehaviour
    {
        // ── Serialized ────────────────────────────────────────────────────────

        [Header("Time Display")]
        [SerializeField] private TextMeshProUGUI dayTimeText;

        [Header("Time Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button speed1xButton;
        [SerializeField] private Button speed2xButton;
        [SerializeField] private Button speed4xButton;

        [Header("Tab Buttons")]
        [SerializeField] private Button squadTabButton;
        [SerializeField] private Button inventoryTabButton;
        [SerializeField] private Button craftingTabButton;
        [SerializeField] private Button mapTabButton;
        [SerializeField] private Button missionsTabButton;

        [Header("Style")]
        [SerializeField] private Color tabNormalColor    = new Color(0.12f, 0.13f, 0.16f, 1f);
        [SerializeField] private Color tabActiveColor    = new Color(0.20f, 0.52f, 0.36f, 1f);
        [SerializeField] private Color speedNormalColor  = new Color(0.14f, 0.15f, 0.18f, 1f);
        [SerializeField] private Color speedActiveColor  = new Color(0.20f, 0.52f, 0.36f, 1f);

        // ── State ─────────────────────────────────────────────────────────────

        private WorldHUDController _hud;
        private TimeSystem _timeSystem;
        private DayNightController _dayNight;
        private float _speed = 1f;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _hud = GetComponentInParent<WorldHUDController>();
        }

        private void Start()
        {
            _timeSystem = FindFirstObjectByType<TimeSystem>();
            _dayNight   = DayNightController.Instance;

            squadTabButton?.onClick.AddListener(    () => _hud?.OpenTab(WorldHUDController.TabId.Squad));
            inventoryTabButton?.onClick.AddListener(() => _hud?.OpenTab(WorldHUDController.TabId.Inventory));
            craftingTabButton?.onClick.AddListener( () => _hud?.OpenTab(WorldHUDController.TabId.Crafting));
            mapTabButton?.onClick.AddListener(      () => _hud?.OpenTab(WorldHUDController.TabId.Map));
            missionsTabButton?.onClick.AddListener( () => _hud?.OpenTab(WorldHUDController.TabId.Missions));

            pauseButton?.onClick.AddListener(TogglePause);
            speed1xButton?.onClick.AddListener(() => ApplySpeed(1f));
            speed2xButton?.onClick.AddListener(() => ApplySpeed(2f));
            speed4xButton?.onClick.AddListener(() => ApplySpeed(4f));

            RefreshSpeedHighlights();
        }

        private void Update()
        {
            // Space = toggle pause
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
                TogglePause();

            // Refresh day-time label
            if (_dayNight == null) _dayNight = DayNightController.Instance;
            if (dayTimeText != null && _dayNight != null)
            {
                int h = Mathf.FloorToInt(_dayNight.CurrentHour);
                int m = Mathf.FloorToInt((_dayNight.CurrentHour - h) * 60f);
                dayTimeText.text = $"DAY {_dayNight.DayNumber}  |  {h:00}:{m:00}";
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by WorldHUDController whenever the active tab changes.</summary>
        public void SetActiveTabHighlight(WorldHUDController.TabId active)
        {
            SetTabColor(squadTabButton,     active == WorldHUDController.TabId.Squad);
            SetTabColor(inventoryTabButton, active == WorldHUDController.TabId.Inventory);
            SetTabColor(craftingTabButton,  active == WorldHUDController.TabId.Crafting);
            SetTabColor(mapTabButton,       active == WorldHUDController.TabId.Map);
            SetTabColor(missionsTabButton,  active == WorldHUDController.TabId.Missions);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void TogglePause()
        {
            if (_timeSystem == null) return;
            if (_timeSystem.IsPaused) _timeSystem.ResumeGame();
            else _timeSystem.PauseGame();
            RefreshSpeedHighlights();
        }

        private void ApplySpeed(float s)
        {
            if (_timeSystem == null) return;
            _speed = s;
            if (_timeSystem.IsPaused) _timeSystem.ResumeGame();
            _timeSystem.SetTimeScale(s);
            RefreshSpeedHighlights();
        }

        private void RefreshSpeedHighlights()
        {
            bool paused = _timeSystem != null && _timeSystem.IsPaused;
            SetSpeedColor(pauseButton,   paused);
            SetSpeedColor(speed1xButton, !paused && Mathf.Approximately(_speed, 1f));
            SetSpeedColor(speed2xButton, !paused && Mathf.Approximately(_speed, 2f));
            SetSpeedColor(speed4xButton, !paused && Mathf.Approximately(_speed, 4f));
        }

        private void SetTabColor(Button btn, bool on)
        {
            if (!btn) return;
            if (btn.targetGraphic is Image img)
                img.color = on ? tabActiveColor : tabNormalColor;
        }

        private void SetSpeedColor(Button btn, bool on)
        {
            if (!btn) return;
            if (btn.targetGraphic is Image img)
                img.color = on ? speedActiveColor : speedNormalColor;
        }
    }
}
