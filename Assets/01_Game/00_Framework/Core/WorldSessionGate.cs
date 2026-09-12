using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    ///     Cached "is a world session active" flag, updated once per game-state change by the
    ///     GameManager state machine. Replaces per-frame GameManager.Instance.CurrentState polling
    ///     in HUD/minimap/map LateUpdate loops.
    /// </summary>
    public static class WorldSessionGate
    {
        /// <summary>True while the game state is LoadingWorld, Playing, or Paused.</summary>
        public static bool IsWorldSessionActive { get; private set; }

        /// <summary>
        ///     World systems (city/roads streaming) may run when a session is active.
        ///     Scenes without a GameManager (dev/prototype) are treated as always allowed.
        /// </summary>
        public static bool IsAllowed =>
            !GameManagerGateway.HasInstance || IsWorldSessionActive;

        /// <summary>
        ///     Gameplay-only gate (Playing/Paused). Dev scenes without a GameManager are allowed.
        /// </summary>
        public static bool IsPlayingOrPaused
        {
            get
            {
                if (!GameManagerGateway.HasInstance)
                    return true;

                var state = GameManagerGateway.Instance.CurrentState;
                return state is GameState.Playing or GameState.Paused;
            }
        }

        /// <summary>Called by GameManager on every state transition (single publisher).</summary>
        public static void NotifyStateChanged(GameState state)
        {
            IsWorldSessionActive = state == GameState.LoadingWorld
                                   || state == GameState.Playing
                                   || state == GameState.Paused;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            IsWorldSessionActive = false;
        }
    }
}
