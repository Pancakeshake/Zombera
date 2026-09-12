namespace Zombera.Characters
{
    /// <summary>Play-mode MapMagic streaming profile used by PlayerSpawner / MapMagicStabilizer.</summary>
    public enum MapMagicPlayModeStreamingProfile
    {
        /// <summary>Single-tracker stabilization with optional frozen tile ring (reliable editor/debug).</summary>
        // Kept for serialized prefabs / inspector; production builds default to ProductionStreaming.
        // ReSharper disable once UnusedMember.Global
        SafeDebugStreaming,

        /// <summary>Infinite MapMagic expansion with retention margins for traversal (production procedural).</summary>
        ProductionStreaming
    }
}
