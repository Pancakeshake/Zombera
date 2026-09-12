namespace Zombera.Debugging
{
    /// <summary>
    ///     Shared interface for modular debug tools.
    /// </summary>
    public interface IDebugTool
    {
        // ReSharper disable once UnusedMember.Global
        string ToolName { get; }

        // ReSharper disable once UnusedMemberInSuper.Global
        bool IsToolEnabled { get; }
        void SetToolEnabled(bool isEnabled);
    }
}
