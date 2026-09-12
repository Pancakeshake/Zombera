#region

using System.Runtime.CompilerServices;
using UnityEngine;

#endregion

namespace Zombera.Systems
{
    public enum PlayerControlModeSource
    {
        Unknown,
        PlayerInput,
        SelectionManager,
        CommandManager
    }

    [DisallowMultipleComponent]
    public sealed class PlayerControlModeCoordinator : MonoBehaviour
    {
        private static PlayerControlModeCoordinator _instance;

        [SerializeField] private PlayerControlMode currentMode = PlayerControlMode.SingleUnit;
        [Header("Diagnostics")] [SerializeField] private bool logModeTransitions;

        private int _lastRequestFrame = -1;
        private int _lastRequestPriority = int.MinValue;

        public static PlayerControlModeCoordinator Instance => _instance;
        public PlayerControlMode CurrentMode => currentMode;

        public bool IsRtsModeActive => currentMode == PlayerControlMode.SquadRts;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void RequestMode(
            PlayerControlMode mode,
            PlayerControlModeSource source = PlayerControlModeSource.Unknown,
            [CallerMemberName] string caller = null)
        {
            var frame = Time.frameCount;
            if (_lastRequestFrame != frame)
            {
                _lastRequestFrame = frame;
                _lastRequestPriority = int.MinValue;
            }

            var requestedPriority = GetModePriority(mode);
            if (requestedPriority < _lastRequestPriority) return;

            _lastRequestPriority = requestedPriority;

            if (currentMode == mode) return;

            var previousMode = currentMode;
            currentMode = mode;

            if (!logModeTransitions) return;

            Debug.Log($"[PlayerControlMode] {previousMode} -> {mode} (source: {source}, caller: {caller})", this);
        }

        private static int GetModePriority(PlayerControlMode mode)
        {
            return mode switch
            {
                PlayerControlMode.BuildMode => 400,
                PlayerControlMode.SquadRts => 300,
                PlayerControlMode.UiBlocked => 200,
                PlayerControlMode.SingleUnit => 100,
                _ => 0
            };
        }
    }
}
