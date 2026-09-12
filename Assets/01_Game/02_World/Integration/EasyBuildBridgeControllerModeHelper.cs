using System;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeControllerModeHelper
    {
        internal static bool TrySetControllerModeCandidates(
            string[] modeNames,
            Func<string, bool> trySetControllerMode,
            Action<string> bridgeLog)
        {
            if (modeNames == null || modeNames.Length == 0) return false;
            if (trySetControllerMode == null) return false;

            for (var i = 0; i < modeNames.Length; i++)
            {
                var candidate = modeNames[i];
                if (string.IsNullOrWhiteSpace(candidate)) continue;

                if (!trySetControllerMode(candidate)) continue;

                bridgeLog?.Invoke($"Controller mode set to {candidate}");
                return true;
            }

            bridgeLog?.Invoke($"Controller mode switch failed. Candidates=[{string.Join(", ", modeNames)}]");
            return false;
        }
    }
}
