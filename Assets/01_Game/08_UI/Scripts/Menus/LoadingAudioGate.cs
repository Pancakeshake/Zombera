#region

using UnityEngine;

#endregion

namespace Zombera.UI.Menus
{
    /// <summary>
    ///     Global audio pause gate for loading transitions.
    ///     Uses reference counting so overlapping loading flows remain muted until all complete.
    /// </summary>
    public static class LoadingAudioGate
    {
        private static int _holdCount;
        private static bool _previousPauseState;

        public static void Acquire()
        {
            if (_holdCount == 0)
            {
                _previousPauseState = AudioListener.pause;
                AudioListener.pause = true;
            }

            _holdCount++;
        }

        public static void Release()
        {
            if (_holdCount <= 0) return;

            _holdCount--;
            if (_holdCount > 0) return;

            AudioListener.pause = _previousPauseState;
            _holdCount = 0;
        }

        public static void Reset()
        {
            _holdCount = 0;
            AudioListener.pause = _previousPauseState;
        }
    }
}