using UnityEngine;

namespace Zombera.Debugging
{
    /// <summary>
    /// Global debug feature flags and tuning values.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Debug/Debug Settings", fileName = "DebugSettings")]
    public sealed class DebugSettings : ScriptableObject
    {
        [Header("Visual Flags")]
        public bool showAIStates = true;
        public bool showDetectionRadius = true;
        public bool showPathfinding = true;

        [Header("Tool Toggles")]
        public bool enableSpawnTools = true;
        public bool enableGodMode;
        public bool enableSlowMotion;

        [Header("Developer Spawn Cheats")]
        [Tooltip("When debug mode is active, spawn the player with all configured item definitions in inventory.")]
        public bool enableDevSpawnFullInventory = true;

        [Header("Tuning")]
        [Range(0.05f, 1f)] public float slowMotionScale = 0.2f;

        [Header("Per-System Debug Channels")]
        public bool debugCombat;
        public bool debugWorld;
        public bool debugSquad;
        public bool debugSave;

        [Header("Profile Presets")]
        public bool useLightweightProfile;

        public void ApplyLightweightProfile()
        {
            showAIStates = false;
            showDetectionRadius = false;
            showPathfinding = false;
            debugCombat = false;
            debugWorld = false;
            debugSquad = false;
            debugSave = false;
        }

        public void ApplyFullDebugProfile()
        {
            showAIStates = true;
            showDetectionRadius = true;
            showPathfinding = true;
            debugCombat = true;
            debugWorld = true;
            debugSquad = true;
            debugSave = true;
        }
    }
}