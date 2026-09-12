#region

using System;
using UnityEngine;

#endregion

namespace Zombera.Systems
{
    public static partial class CursorService
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float TransitionLogThrottleSeconds = 0.25f;
        private static float _nextTransitionLogAt;
#endif

        public static event Action<CursorPresentation, CursorPresentation> PresentationChanged;

        private static void LogPresentationTransition(CursorPresentation previous, CursorPresentation next)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.unscaledTime < _nextTransitionLogAt) return;

            _nextTransitionLogAt = Time.unscaledTime + TransitionLogThrottleSeconds;
            Debug.Log(
                $"[CursorService] Presentation {FormatPresentation(previous)} -> {FormatPresentation(next)} " +
                $"(requests={ActiveRequests.Count}, icon={ActiveIconIntent})");
#endif
            PresentationChanged?.Invoke(previous, next);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string FormatPresentation(CursorPresentation presentation)
        {
            return $"visible={presentation.Visible}, lock={presentation.LockMode}";
        }
#endif
    }
}
