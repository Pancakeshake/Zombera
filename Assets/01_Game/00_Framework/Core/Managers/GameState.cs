namespace Zombera.Core
{
    /// <summary>
    ///     High-level game lifecycle states.
    /// </summary>
    public enum GameState
    {
        Booting,
        MainMenu,
        LoadingWorld,
        Playing,
        Paused
    }
}