using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    /// Controls simulation time scale and pause state.
    /// </summary>
    public sealed class TimeSystem : MonoBehaviour, IGameSystem
    {
        [SerializeField] private float defaultTimeScale = 1f;

        public bool IsInitialized { get; private set; }
        public bool IsPaused { get; private set; }
        public float CurrentTimeScale { get; private set; } = 1f;

        public void Initialize()
        {
            IsInitialized = true;
            SetTimeScale(defaultTimeScale);
            IsPaused = false;

            // TODO: Restore time settings from save/profile data if needed.
        }

        public void Shutdown()
        {
            IsInitialized = false;
            SetTimeScale(1f);
            IsPaused = false;

            // TODO: Unsubscribe from global events.
        }

        public void SetTimeScale(float scale)
        {
            CurrentTimeScale = Mathf.Clamp(scale, 0f, 10f);
            Time.timeScale = CurrentTimeScale;

            // TODO: Support per-system timescale channels (gameplay/UI/cutscene).
        }

        public void PauseGame()
        {
            IsPaused = true;
            SetTimeScale(0f);

            // TODO: Broadcast pause event through EventSystem.
        }

        public void ResumeGame()
        {
            IsPaused = false;
            SetTimeScale(defaultTimeScale);

            // TODO: Restore previous timescale modifiers after pause.
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
    }
}