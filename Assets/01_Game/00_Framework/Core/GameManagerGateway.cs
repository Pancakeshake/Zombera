using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    ///     Cross-assembly access to the active game session coordinator.
    /// </summary>
    public interface IGameManagerGateway
    {
        GameState CurrentState { get; }
        bool IsLoadingSession { get; }
        bool IsInitialized { get; }
        void SetGameState(GameState newState);
        void RegisterSystem<T>(T system) where T : Component;
        void LoadGame(string slotId);
        void InitializeSystems();
        void StartNewGame();
        void StartNewGame(WorldSessionRequest request);
        void QuitToMainMenu();
    }

    public static class GameManagerGateway
    {
        public static bool HasInstance => Instance != null;

        /// <summary>Set only by the owning GameManager (World assembly) during its lifecycle.</summary>
        public static IGameManagerGateway Instance { get; set; }
    }
}
