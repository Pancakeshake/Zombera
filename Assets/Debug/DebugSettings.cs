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

        [Header("Tuning")]
        [Range(0.05f, 1f)] public float slowMotionScale = 0.2f;

        // TODO: Add per-system debug channels (combat, world, squad, save).
        // TODO: Add profile presets for lightweight and full debug sessions.
    }
}