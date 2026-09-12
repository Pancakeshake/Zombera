using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgePlacementDiagnosticsHelper
    {
        [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Shared bridge diagnostic call-sites pass a stable tuple of stage/filter/throttle inputs.")]
        internal static bool ShouldEmitFilteredPlacementDiagnostic(
            string stage,
            string partReference,
            int hudIndex,
            int managerIndex,
            string placementDiagnosticsPartFilter,
            float placementDiagnosticsRepeatIntervalSeconds,
            IDictionary<string, float> nextPlacementDiagnosticAtByKey,
            string displayToken)
        {
            if (!ShouldTracePartReference(partReference, placementDiagnosticsPartFilter, displayToken))
                return false;

            var key = $"{stage}|{partReference}|{hudIndex}|{managerIndex}";
            var repeatInterval = Mathf.Max(0.1f, placementDiagnosticsRepeatIntervalSeconds);
            var now = Time.unscaledTime;
            if (nextPlacementDiagnosticAtByKey.TryGetValue(key, out var nextAt) && now < nextAt)
                return false;

            nextPlacementDiagnosticAtByKey[key] = now + repeatInterval;
            return true;
        }

        internal static bool ShouldTracePartReference(
            string partReference,
            string placementDiagnosticsPartFilter,
            params string[] alternateTokens)
        {
            if (MatchesDiagnosticFilter(partReference, placementDiagnosticsPartFilter)) return true;

            if (alternateTokens == null) return false;

            for (var i = 0; i < alternateTokens.Length; i++)
            {
                if (MatchesDiagnosticFilter(alternateTokens[i], placementDiagnosticsPartFilter))
                    return true;
            }

            return false;
        }

        private static bool MatchesDiagnosticFilter(string token, string placementDiagnosticsPartFilter)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            if (string.IsNullOrWhiteSpace(placementDiagnosticsPartFilter)) return true;

            if (token.IndexOf(placementDiagnosticsPartFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var normalizedPartReference = EasyBuildBridgeTextHelper.NormalizeDiagnosticToken(token);
            var normalizedFilter = EasyBuildBridgeTextHelper.NormalizeDiagnosticToken(placementDiagnosticsPartFilter);
            if (string.IsNullOrWhiteSpace(normalizedPartReference) || string.IsNullOrWhiteSpace(normalizedFilter))
                return false;

            if (normalizedPartReference.Contains(normalizedFilter))
                return true;

            if (normalizedFilter.Contains("barrell")
                && normalizedPartReference.Contains(normalizedFilter.Replace("barrell", "barrel")))
                return true;

            if (normalizedFilter.Contains("barrel")
                && normalizedPartReference.Contains(normalizedFilter.Replace("barrel", "barrell")))
                return true;

            return false;
        }
    }
}
