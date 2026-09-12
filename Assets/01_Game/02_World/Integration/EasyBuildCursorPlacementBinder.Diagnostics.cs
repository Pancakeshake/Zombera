#region

using UnityEngine;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildCursorPlacementBinder
    {
        private void LogPlacementDiagnostic(string stage, string partReference, int hudIndex, string details)
        {
            if (!enablePlacementDiagnosticsLogs) return;
            if (!EasyBuildBridgePlacementDiagnosticsHelper.ShouldEmitFilteredPlacementDiagnostic(
                    stage,
                    partReference,
                    hudIndex,
                    -1,
                    placementDiagnosticsPartFilter,
                    placementDiagnosticsRepeatIntervalSeconds,
                    _nextPlacementDiagnosticAtByKey,
                    null))
                return;

            if (string.IsNullOrWhiteSpace(details))
            {
                Debug.Log($"[EasyBuildCursorBinder] [PlacementDiag] {stage}: partRef={partReference}, hudIndex={hudIndex}", this);
                return;
            }

            Debug.Log($"[EasyBuildCursorBinder] [PlacementDiag] {stage}: partRef={partReference}, hudIndex={hudIndex}, {details}",
                this);
        }

        private bool ShouldLogPlacementDiagnosticForPart(string partReference, params string[] alternateTokens)
        {
            return EasyBuildBridgePlacementDiagnosticsHelper.ShouldTracePartReference(
                partReference,
                placementDiagnosticsPartFilter,
                alternateTokens);
        }

        private static string ResolvePartReference(Component part)
        {
            if (part == null) return null;

            return GetMemberValue(part, "PrefabId") as string
                   ?? GetMemberValue(part, "PartReference") as string
                   ?? GetMemberValue(part, "ID") as string
                   ?? part.gameObject.name;
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
