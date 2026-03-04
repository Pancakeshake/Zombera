using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Debugging.DebugLogging;
using Zombera.Debugging.DebugTools;

namespace Zombera.Debugging.DebugMenu
{
    /// <summary>
    /// UI controller for the runtime debug menu.
    /// Responsibilities:
    /// - Open/close menu
    /// - Bind debug action buttons
    /// - Forward UI actions to debug tools
    /// </summary>
    public sealed class DebugMenuController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private CanvasGroup menuCanvasGroup;

        [Header("Buttons")]
        [SerializeField] private Button spawnZombieButton;
        [SerializeField] private Button spawnSurvivorButton;
        [SerializeField] private Button spawnLootButton;
        [SerializeField] private Button spawnHordeButton;
        [SerializeField] private Button teleportPlayerButton;
        [SerializeField] private Button toggleAIButton;
        [SerializeField] private Button toggleGodModeButton;
        [SerializeField] private Button toggleSlowMotionButton;
        [SerializeField] private Button advanceWorldTimeButton;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Tool References")]
        [SerializeField] private SpawnDebugTools spawnDebugTools;
        [SerializeField] private WorldDebugTools worldDebugTools;
        [SerializeField] private AISimulationTools aiSimulationTools;

        public bool IsMenuVisible { get; private set; }

        private DebugManager debugManager;

        public void Initialize(DebugManager manager)
        {
            debugManager = manager;

            if (menuRoot == null)
            {
                menuRoot = gameObject;
            }

            BindButtons();
            SetMenuVisible(false);
        }

        public void SetMenuVisible(bool visible)
        {
            IsMenuVisible = visible;

            if (menuRoot != null)
            {
                menuRoot.SetActive(visible);
            }

            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = visible ? 1f : 0f;
                menuCanvasGroup.interactable = visible;
                menuCanvasGroup.blocksRaycasts = visible;
            }
        }

        public void ToggleMenuVisible()
        {
            SetMenuVisible(!IsMenuVisible);
        }

        private void BindButtons()
        {
            BindButton(spawnZombieButton, () => spawnDebugTools?.SpawnZombie());
            BindButton(spawnSurvivorButton, () => spawnDebugTools?.SpawnSurvivor());
            BindButton(spawnLootButton, () => spawnDebugTools?.SpawnLootContainer());
            BindButton(spawnHordeButton, () => spawnDebugTools?.SpawnZombieHorde());
            BindButton(teleportPlayerButton, () => worldDebugTools?.TeleportPlayerToDebugTarget());
            BindButton(toggleAIButton, () => aiSimulationTools?.ToggleAISimulation());
            BindButton(toggleGodModeButton, () => debugManager?.ToggleGodMode());
            BindButton(toggleSlowMotionButton, () => debugManager?.ToggleSlowMotion());
            BindButton(advanceWorldTimeButton, () => worldDebugTools?.AdvanceWorldSimulation());
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(callback);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }

            DebugLogger.Log(LogCategory.Debug, message, this);
        }

        // TODO: Add tabbed sections for AI, World, Spawn, Performance.
        // TODO: Add runtime value editing for debug settings and stress test parameters.
    }
}