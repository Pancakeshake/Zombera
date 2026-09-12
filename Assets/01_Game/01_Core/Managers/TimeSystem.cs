#region

using UnityEngine;

#endregion

namespace Zombera.Core
{
    /// <summary>
    ///     Controls simulation time scale and pause state.
    ///     Supports per-channel timescale modifiers (gameplay, UI, cutscene).
    ///     Pause *state* is owned by GameManager; this class only owns the timescale.
    ///     Callers outside GameManager should use the Request* methods, which route
    ///     through the game state machine when one is active.
    /// </summary>
    public sealed class TimeSystem : MonoBehaviour, IGameSystem
    {
        [SerializeField] private float defaultTimeScale = 1f;
        private float _channelCutscene = 1f;

        // Per-channel multipliers. Combined: Time.timeScale = gameplayChannel * globalScale.
        private float _channelGameplay = 1f;
        private float _channelUI = 1f;

        private float _lastUnpausedTimeScale = 1f;
        public bool IsPaused { get; private set; }
        public float CurrentTimeScale { get; private set; } = 1f;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            IsInitialized = true;
            _channelGameplay = 1f;
            _channelUI = 1f;
            _channelCutscene = 1f;
            _lastUnpausedTimeScale = Mathf.Max(0.1f, defaultTimeScale);
            SetTimeScale(defaultTimeScale);
        }

        public void Shutdown()
        {
            IsInitialized = false;
            _channelGameplay = 1f;
            _channelUI = 1f;
            _channelCutscene = 1f;
            _lastUnpausedTimeScale = 1f;
            SetTimeScale(1f);
        }

        public void SetTimeScale(float scale)
        {
            // Apply the gameplay channel multiplier on top of the requested base scale.
            var effective = Mathf.Clamp(scale * _channelGameplay, 0f, 10f);
            CurrentTimeScale = effective;
            Time.timeScale = effective;
            IsPaused = effective <= 0.0001f;

            if (!IsPaused) _lastUnpausedTimeScale = scale;
        }

        /// <summary>Sets the multiplier for a named channel. Reapplies the current base scale.</summary>
        public void SetChannelScale(TimeChannel channel, float multiplier)
        {
            multiplier = Mathf.Clamp(multiplier, 0f, 10f);

            switch (channel)
            {
                case TimeChannel.Gameplay: _channelGameplay = multiplier; break;
                case TimeChannel.UI: _channelUI = multiplier; break;
                case TimeChannel.Cutscene: _channelCutscene = multiplier; break;
                default: _channelGameplay = multiplier; break;
            }

            // Reapply using the last unpaused base so pause is preserved.
            SetTimeScale(IsPaused ? 0f : _lastUnpausedTimeScale);
        }

        public float GetChannelScale(TimeChannel channel)
        {
            return channel switch
            {
                TimeChannel.UI => _channelUI,
                TimeChannel.Cutscene => _channelCutscene,
                _ => _channelGameplay
            };
        }

        /// <summary>Timescale-only pause. Called by GameManager on state transitions.</summary>
        public void PauseGame()
        {
            SetTimeScale(0f);
        }

        /// <summary>Timescale-only resume. Called by GameManager on state transitions.</summary>
        public void ResumeGame()
        {
            var resumeScale = _lastUnpausedTimeScale > 0.0001f
                ? _lastUnpausedTimeScale
                : Mathf.Max(0.1f, defaultTimeScale);

            SetTimeScale(resumeScale);
        }

        /// <summary>
        ///     Pause request from gameplay/UI. Routes through the game state machine when one
        ///     is active (GameManager then drives <see cref="PauseGame" />); otherwise pauses locally.
        /// </summary>
        public void RequestPause()
        {
            var gateway = GameManagerGateway.Instance;
            if (gateway != null && gateway.CurrentState == GameState.Playing)
            {
                gateway.SetGameState(GameState.Paused);
                return;
            }

            PauseGame();
        }

        /// <summary>
        ///     Resume request from gameplay/UI. Routes through the game state machine when one
        ///     is active; otherwise resumes locally.
        /// </summary>
        public void RequestResume()
        {
            var gateway = GameManagerGateway.Instance;
            if (gateway != null && gateway.CurrentState == GameState.Paused)
            {
                gateway.SetGameState(GameState.Playing);
                return;
            }

            ResumeGame();
        }

        public void TogglePause()
        {
            if (IsPaused)
                RequestResume();
            else
                RequestPause();
        }
    }

    public enum TimeChannel
    {
        Gameplay,
        UI,
        Cutscene
    }
}