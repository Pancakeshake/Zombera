using UnityEngine;

namespace Zombera.Core
{
    public struct GamePausedEvent : IGameEvent { }
    public struct GameResumedEvent : IGameEvent { }

    /// <summary>
    /// Controls simulation time scale and pause state.
    /// Supports per-channel timescale modifiers (gameplay, UI, cutscene).
    /// </summary>
    public sealed class TimeSystem : MonoBehaviour, IGameSystem
    {
        [SerializeField] private float defaultTimeScale = 1f;

        private float lastUnpausedTimeScale = 1f;

        // Per-channel multipliers. Combined: Time.timeScale = gameplayChannel * globalScale.
        private float channelGameplay = 1f;
        private float channelUI = 1f;
        private float channelCutscene = 1f;

        public bool IsInitialized { get; private set; }
        public bool IsPaused { get; private set; }
        public float CurrentTimeScale { get; private set; } = 1f;

        public void Initialize()
        {
            IsInitialized = true;
            channelGameplay = 1f;
            channelUI = 1f;
            channelCutscene = 1f;
            lastUnpausedTimeScale = Mathf.Max(0.1f, defaultTimeScale);
            SetTimeScale(defaultTimeScale);
        }

        public void Shutdown()
        {
            IsInitialized = false;
            channelGameplay = 1f;
            channelUI = 1f;
            channelCutscene = 1f;
            lastUnpausedTimeScale = 1f;
            SetTimeScale(1f);

            EventSystem.Instance?.Unsubscribe<GamePausedEvent>(OnExternalPause);
            EventSystem.Instance?.Unsubscribe<GameResumedEvent>(OnExternalResume);
        }

        public void SetTimeScale(float scale)
        {
            // Apply the gameplay channel multiplier on top of the requested base scale.
            float effective = Mathf.Clamp(scale * channelGameplay, 0f, 10f);
            CurrentTimeScale = effective;
            Time.timeScale = effective;
            IsPaused = effective <= 0.0001f;

            if (!IsPaused)
            {
                lastUnpausedTimeScale = scale;
            }
        }

        /// <summary>Sets the multiplier for a named channel. Reapplies the current base scale.</summary>
        public void SetChannelScale(TimeChannel channel, float multiplier)
        {
            multiplier = Mathf.Clamp(multiplier, 0f, 10f);

            switch (channel)
            {
                case TimeChannel.Gameplay: channelGameplay = multiplier; break;
                case TimeChannel.UI: channelUI = multiplier; break;
                case TimeChannel.Cutscene: channelCutscene = multiplier; break;
            }

            // Reapply using the last unpaused base so pause is preserved.
            SetTimeScale(IsPaused ? 0f : lastUnpausedTimeScale);
        }

        public float GetChannelScale(TimeChannel channel)
        {
            return channel switch
            {
                TimeChannel.UI => channelUI,
                TimeChannel.Cutscene => channelCutscene,
                _ => channelGameplay,
            };
        }

        public void PauseGame()
        {
            SetTimeScale(0f);

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                GameManager.Instance.SetGameState(GameState.Paused);
            }

            EventSystem.PublishGlobal(new GamePausedEvent());
        }

        public void ResumeGame()
        {
            float resumeScale = lastUnpausedTimeScale > 0.0001f
                ? lastUnpausedTimeScale
                : Mathf.Max(0.1f, defaultTimeScale);

            SetTimeScale(resumeScale);

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
            }

            EventSystem.PublishGlobal(new GameResumedEvent());
        }

        public void TogglePause()
        {
            if (IsPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        private void OnExternalPause(GamePausedEvent _) { /* handled by GameManager */ }
        private void OnExternalResume(GameResumedEvent _) { /* handled by GameManager */ }
    }

    public enum TimeChannel
    {
        Gameplay,
        UI,
        Cutscene,
    }
}