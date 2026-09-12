namespace Zombera.Core
{
    /// <summary>
    ///     Shared interface for top-level game systems.
    /// </summary>
    public interface IGameSystem
    {
        bool IsInitialized { get; }
        void Initialize();
        void Shutdown();
    }
}