#region

using UnityEngine;

#endregion

namespace Zombera.Systems
{
    public enum PlayerControlMode
    {
        SingleUnit,
        SquadRts,
        BuildMode,
        UiBlocked
    }

    public readonly struct PlayerControlModeContext
    {
        public readonly bool BuildModeActive;
        public readonly bool PointerOverUi;
        public readonly bool HybridModeEnabled;
        public readonly bool AutoSwitchToRtsWhenMultipleSelected;
        public readonly int SelectedCount;
        public readonly int RtsSelectionThreshold;
        public readonly bool RequireRtsModifier;
        public readonly bool RtsModifierActive;

        public PlayerControlModeContext(
            bool buildModeActive,
            bool pointerOverUi,
            bool hybridModeEnabled,
            bool autoSwitchToRtsWhenMultipleSelected,
            int selectedCount,
            int rtsSelectionThreshold,
            bool requireRtsModifier,
            bool rtsModifierActive)
        {
            BuildModeActive = buildModeActive;
            PointerOverUi = pointerOverUi;
            HybridModeEnabled = hybridModeEnabled;
            AutoSwitchToRtsWhenMultipleSelected = autoSwitchToRtsWhenMultipleSelected;
            SelectedCount = selectedCount;
            RtsSelectionThreshold = rtsSelectionThreshold;
            RequireRtsModifier = requireRtsModifier;
            RtsModifierActive = rtsModifierActive;
        }
    }

    public static class PlayerControlModePolicy
    {
        public static PlayerControlMode Evaluate(in PlayerControlModeContext context)
        {
            if (context.BuildModeActive) return PlayerControlMode.BuildMode;
            if (context.PointerOverUi) return PlayerControlMode.UiBlocked;

            if (!context.HybridModeEnabled) return PlayerControlMode.SquadRts;

            var threshold = Mathf.Max(2, context.RtsSelectionThreshold);
            if (context.AutoSwitchToRtsWhenMultipleSelected && context.SelectedCount >= threshold)
                return PlayerControlMode.SquadRts;

            if (context.RequireRtsModifier && context.RtsModifierActive)
                return PlayerControlMode.SquadRts;

            return PlayerControlMode.SingleUnit;
        }

        public static bool ShouldCaptureRtsMouseInput(in PlayerControlModeContext context)
        {
            return Evaluate(context) == PlayerControlMode.SquadRts;
        }
    }
}
