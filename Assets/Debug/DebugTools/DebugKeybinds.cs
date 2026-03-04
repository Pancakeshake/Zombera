using UnityEngine;

namespace Zombera.Debugging.DebugTools
{
    /// <summary>
    /// Global debug keybinds.
    /// Required mappings:
    /// - F1: toggle debug menu
    /// - F2: toggle slow motion
    /// - F3: toggle AI debug visuals
    /// - F4: spawn zombie
    /// - F5: spawn survivor
    /// - F6: toggle god mode
    /// - F7: spawn zombie horde
    /// - F8: advance world simulation
    /// </summary>
    public sealed class DebugKeybinds : MonoBehaviour, IDebugTool
    {
        [Header("Keybinds")]
        [SerializeField] private KeyCode toggleMenuKey = KeyCode.F1;
        [SerializeField] private KeyCode toggleSlowMotionKey = KeyCode.F2;
        [SerializeField] private KeyCode toggleAIDebugKey = KeyCode.F3;
        [SerializeField] private KeyCode spawnZombieKey = KeyCode.F4;
        [SerializeField] private KeyCode spawnSurvivorKey = KeyCode.F5;
        [SerializeField] private KeyCode toggleGodModeKey = KeyCode.F6;
        [SerializeField] private KeyCode spawnHordeKey = KeyCode.F7;
        [SerializeField] private KeyCode advanceWorldSimulationKey = KeyCode.F8;

        [Header("Tool References")]
        [SerializeField] private SpawnDebugTools spawnDebugTools;
        [SerializeField] private WorldDebugTools worldDebugTools;
        [SerializeField] private AISimulationTools aiSimulationTools;

        public string ToolName => nameof(DebugKeybinds);
        public bool IsToolEnabled { get; private set; } = true;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public void SetToolEnabled(bool enabled)
        {
            IsToolEnabled = enabled;
        }

        private void Update()
        {
            if (!IsToolEnabled)
            {
                return;
            }

            if (Input.GetKeyDown(toggleMenuKey))
            {
                DebugManager.Instance?.ToggleDebugMenu();
            }

            if (Input.GetKeyDown(toggleSlowMotionKey))
            {
                DebugManager.Instance?.ToggleSlowMotion();
            }

            if (Input.GetKeyDown(toggleAIDebugKey))
            {
                DebugManager.Instance?.ToggleAIDebugVisuals();
            }

            if (Input.GetKeyDown(spawnZombieKey))
            {
                spawnDebugTools?.SpawnZombie();
            }

            if (Input.GetKeyDown(spawnSurvivorKey))
            {
                spawnDebugTools?.SpawnSurvivor();
            }

            if (Input.GetKeyDown(toggleGodModeKey))
            {
                DebugManager.Instance?.ToggleGodMode();
            }

            if (Input.GetKeyDown(spawnHordeKey))
            {
                spawnDebugTools?.SpawnZombieHorde();
            }

            if (Input.GetKeyDown(advanceWorldSimulationKey))
            {
                worldDebugTools?.AdvanceWorldSimulation();
            }
        }

        // TODO: Add runtime-rebind UI and persisted keybind profiles.
        // TODO: Add modifier key support for alternate debug actions.
    }
}