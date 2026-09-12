#region

using UnityEngine;
using Zombera.Core;
using Zombera.Debugging.DebugLogging;
using Zombera.World;

#endregion

namespace Zombera.Debugging.DebugTools
{
    /// <summary>
    ///     Debug helpers for world simulation and player relocation testing.
    /// </summary>
    public sealed class WorldDebugTools : MonoBehaviour, IDebugTool
    {
        [Header("References")] [SerializeField]
        private Transform playerTransform;

        [SerializeField] private Transform teleportTarget;

        [Header("Simulation")] [SerializeField]
        private float manualSimulationStep = 10f;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public string ToolName => nameof(WorldDebugTools);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
        }

        public void AdvanceWorldSimulation()
        {
            if (!IsToolEnabled) return;

            CoreEventBus.PublishGlobal(new WorldSimulationTickEvent
            {
                DeltaTime = manualSimulationStep,
                PlayerPosition = playerTransform != null ? playerTransform.position : Vector3.zero
            });

            DebugLogger.Log(LogCategory.World, "Manual world simulation step requested", this);
        }

        public void TeleportPlayerToDebugTarget()
        {
            if (!IsToolEnabled) return;

            if (playerTransform == null || teleportTarget == null)
            {
                DebugLogger.LogWarning(LogCategory.World, "Teleport request skipped (missing references)", this);
                return;
            }

            // Route through the WorldManager if one is available so transition effects
            // and chunk streaming are triggered correctly.
            var worldManager = FindFirstObjectByType<WorldManager>();

            if (worldManager != null)
                worldManager.TeleportPlayer(playerTransform, teleportTarget.position);
            else
                playerTransform.position = teleportTarget.position;

            DebugLogger.Log(LogCategory.World, "Player teleported to debug target", this);
        }
    }
}