#region

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zombera.Debugging.DebugLogging;
using Zombera.Debugging.DebugTools;

#endregion

namespace Zombera.Debugging.DebugMenu
{
    /// <summary>
    ///     UI controller for the runtime debug menu.
    ///     Responsibilities:
    ///     - Open/close menu
    ///     - Bind debug action buttons
    ///     - Forward UI actions to debug tools
    /// </summary>
    public sealed class DebugMenuController : MonoBehaviour
    {
        [Header("Root")] [SerializeField] private GameObject menuRoot;

        [SerializeField] private CanvasGroup menuCanvasGroup;

        [Header("Buttons")] [SerializeField] private Button spawnZombieButton;

        [SerializeField] private Button toggleMenuButton;
        [SerializeField] private Button spawnSurvivorButton;
        [SerializeField] private Button spawnLootButton;
        [SerializeField] private Button spawnHordeButton;
        [SerializeField] private Button teleportPlayerButton;
        [SerializeField] private Button toggleAIButton;
        [SerializeField] private Button toggleGodModeButton;
        [SerializeField] private Button toggleSlowMotionButton;
        [SerializeField] private Button advanceWorldTimeButton;
        [SerializeField] private Button addThreeHoursButton;
        [SerializeField] private Button subtractThreeHoursButton;
        [SerializeField] private Button runFullDiagnosticsButton;

        [Header("Status")] [SerializeField] private TextMeshProUGUI statusText;

        [Header("Sections")] [SerializeField] private string defaultSectionTag;

        [Header("Tool References")] [SerializeField]
        private SpawnDebugTools spawnDebugTools;

        [SerializeField] private WorldDebugTools worldDebugTools;
        [SerializeField] private AISimulationTools aiSimulationTools;
        [SerializeField] private QuickTimeDebugMenu quickTimeDebugMenu;
        [SerializeField] private DebugSettings debugSettings;

        private DebugManager _debugManager;

        public bool IsMenuVisible { get; private set; }

        public void Initialize(DebugManager manager)
        {
            _debugManager = manager;

            if (quickTimeDebugMenu == null)
                quickTimeDebugMenu = manager != null
                    ? manager.GetComponent<QuickTimeDebugMenu>()
                    : FindFirstObjectByType<QuickTimeDebugMenu>();

            if (menuRoot == null) menuRoot = gameObject;

            BindButtons();
            SetMenuVisible(false);

            if (!string.IsNullOrEmpty(defaultSectionTag))
            {
                HideSection(defaultSectionTag);
                ShowSection(defaultSectionTag);
            }

            if (debugSettings != null) SetFloatSetting("slowMotionScale", debugSettings.slowMotionScale);
        }

        public void SetMenuVisible(bool visible)
        {
            IsMenuVisible = visible;

            if (menuRoot != null) menuRoot.SetActive(visible);

            if (menuCanvasGroup == null) return;

            menuCanvasGroup.alpha = visible ? 1f : 0f;
            menuCanvasGroup.interactable = visible;
            menuCanvasGroup.blocksRaycasts = visible;
        }

        public void ToggleMenuVisible()
        {
            SetMenuVisible(!IsMenuVisible);
        }

        private void BindButtons()
        {
            BindButton(toggleMenuButton, ToggleMenuVisible);
            BindButton(spawnZombieButton, () => spawnDebugTools?.SpawnZombie());
            BindButton(spawnSurvivorButton, () => spawnDebugTools?.SpawnSurvivor());
            BindButton(spawnLootButton, () => spawnDebugTools?.SpawnLootContainer());
            BindButton(spawnHordeButton, () => spawnDebugTools?.SpawnZombieHorde());
            BindButton(teleportPlayerButton, () => worldDebugTools?.TeleportPlayerToDebugTarget());
            BindButton(toggleAIButton, () => aiSimulationTools?.ToggleAISimulation());
            BindButton(toggleGodModeButton, () => _debugManager?.ToggleGodMode());
            BindButton(toggleSlowMotionButton, () => _debugManager?.ToggleSlowMotion());
            BindButton(advanceWorldTimeButton, () => worldDebugTools?.AdvanceWorldSimulation());
            BindButton(addThreeHoursButton, HandleAddThreeHours);
            BindButton(subtractThreeHoursButton, HandleSubtractThreeHours);
            BindButton(runFullDiagnosticsButton, HandleRunFullDiagnostics);
        }

        private void HandleAddThreeHours()
        {
            if (quickTimeDebugMenu == null)
            {
                SetStatusText("Quick time tool not found.");
                return;
            }

            var changed = quickTimeDebugMenu.AddThreeHours();
            SetStatusText(changed ? "Advanced world time by +3 hours." : "Unable to advance time (+3h).");
        }

        private void HandleSubtractThreeHours()
        {
            if (quickTimeDebugMenu == null)
            {
                SetStatusText("Quick time tool not found.");
                return;
            }

            var changed = quickTimeDebugMenu.SubtractThreeHours();
            SetStatusText(changed ? "Rewound world time by -3 hours." : "Unable to rewind time (-3h).");
        }

        private void HandleRunFullDiagnostics()
        {
            if (quickTimeDebugMenu == null)
            {
                SetStatusText("Quick diagnostics tool not found.");
                return;
            }

            var summary = quickTimeDebugMenu.RunFullDiagnostics();
            SetStatusText(string.IsNullOrWhiteSpace(summary) ? "Diagnostics completed." : summary);
        }

        private void SetStatusText(string message)
        {
            if (statusText != null)
                statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }

        private static void BindButton(Button button, UnityAction callback)
        {
            if (button == null) return;

            button.onClick.AddListener(callback);
        }

        // Menu section helpers — wire buttons to these from editor-created tab panels.
        // Each section tag matches a GameObject child named e.g. "Tab_AI", "Tab_World".
        public void ShowSection(string sectionTag)
        {
            if (string.IsNullOrEmpty(sectionTag)) return;

            var section = transform.Find(sectionTag);

            if (section != null) section.gameObject.SetActive(true);
        }

        public void HideSection(string sectionTag)
        {
            var section = transform.Find(sectionTag);

            if (section != null) section.gameObject.SetActive(false);
        }

        /// <summary>Applies a float debug setting at runtime by name.</summary>
        public void SetFloatSetting(string settingName, float value)
        {
            if (debugSettings == null) return;

            switch (settingName)
            {
                case "slowMotionScale":
                    debugSettings.slowMotionScale = Mathf.Clamp(value, 0.05f, 1f);
                    break;
                default:
                    DebugLogger.LogWarning(LogCategory.Debug, $"Unknown setting name: {settingName}", this);
                    break;
            }
        }
    }
}