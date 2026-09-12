namespace Zombera.Debugging
{
    /// <summary>
    ///     Cross-assembly read/write surface for the runtime debug manager.
    /// </summary>
    public interface IDebugManagerAccessor
    {
        bool DebugEnabled { get; }
        DebugSettings Settings { get; }
        bool IsDebugMenuVisible { get; }
        void RegisterDebugTool(IDebugTool tool);
        void UnregisterDebugTool(IDebugTool tool);
    }

    public static class DebugManagerAccessor
    {
        /// <summary>Set only by the owning DebugManager (World assembly) during its lifecycle.</summary>
        public static IDebugManagerAccessor Instance { get; set; }
    }
}
