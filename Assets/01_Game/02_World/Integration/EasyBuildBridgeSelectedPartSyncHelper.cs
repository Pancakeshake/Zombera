using System;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    internal delegate bool TryResolveRadialPartReferenceDelegate(
        int hudItemIndex,
        out string partReference,
        bool includeLegacyFallback);

    internal readonly struct SelectedPartSyncContext
    {
        internal readonly Func<int, bool> TrySelectPartByHudIndex;
        internal readonly Func<int> GetBuildItemCount;
        internal readonly TryResolveRadialPartReferenceDelegate TryResolveRadialPartReferenceForHudIndex;
        internal readonly Func<object> ResolveBuildingManagerInstance;
        internal readonly Func<object, int> GetManagerPartCount;
        internal readonly Func<object, int, object> GetManagerPartByIndex;
        internal readonly Func<object, string> ResolvePartIdentifier;
        internal readonly Action<string, string, int, int> LogFilteredPlacementDiagnostic;
        internal readonly float EnsureSelectedPartDiagnosticsIntervalSeconds;

        internal SelectedPartSyncContext(
            Func<int, bool> trySelectPartByHudIndex,
            Func<int> getBuildItemCount,
            TryResolveRadialPartReferenceDelegate tryResolveRadialPartReferenceForHudIndex,
            Func<object> resolveBuildingManagerInstance,
            Func<object, int> getManagerPartCount,
            Func<object, int, object> getManagerPartByIndex,
            Func<object, string> resolvePartIdentifier,
            Action<string, string, int, int> logFilteredPlacementDiagnostic,
            float ensureSelectedPartDiagnosticsIntervalSeconds)
        {
            TrySelectPartByHudIndex = trySelectPartByHudIndex;
            GetBuildItemCount = getBuildItemCount;
            TryResolveRadialPartReferenceForHudIndex = tryResolveRadialPartReferenceForHudIndex;
            ResolveBuildingManagerInstance = resolveBuildingManagerInstance;
            GetManagerPartCount = getManagerPartCount;
            GetManagerPartByIndex = getManagerPartByIndex;
            ResolvePartIdentifier = resolvePartIdentifier;
            LogFilteredPlacementDiagnostic = logFilteredPlacementDiagnostic;
            EnsureSelectedPartDiagnosticsIntervalSeconds = ensureSelectedPartDiagnosticsIntervalSeconds;
        }
    }

    internal static class EasyBuildBridgeSelectedPartSyncHelper
    {
        internal static void EnsureSelectedPart(
            object controller,
            in SelectedPartSyncContext ctx,
            ref int currentHudSelectionIndex,
            ref string lastEnsureSelectedPartReference,
            ref int lastEnsureSelectedHudIndex,
            ref float nextEnsureSelectedPartDiagnosticAt)
        {
            var selectedPart = EasyBuildBridgeReflectionHelper.GetMemberValue(controller, "SelectedPart");
            if (selectedPart == null)
            {
                if (ctx.TrySelectPartByHudIndex(0))
                    currentHudSelectionIndex = 0;

                return;
            }

            var selectedPartReference = ctx.ResolvePartIdentifier(selectedPart);
            if (string.IsNullOrWhiteSpace(selectedPartReference))
                return;

            var shouldLogEnsureState = ShouldLogEnsureSelectedPartState(
                selectedPartReference,
                currentHudSelectionIndex,
                ctx.EnsureSelectedPartDiagnosticsIntervalSeconds,
                ref lastEnsureSelectedPartReference,
                ref lastEnsureSelectedHudIndex,
                ref nextEnsureSelectedPartDiagnosticAt);

            if (shouldLogEnsureState)
            {
                ctx.LogFilteredPlacementDiagnostic(
                    "EnsureSelectedPart.current",
                    selectedPartReference,
                    currentHudSelectionIndex,
                    -1);
            }

            if (currentHudSelectionIndex >= 0
                && ctx.TryResolveRadialPartReferenceForHudIndex(
                    currentHudSelectionIndex,
                    out var currentReference,
                    false)
                && string.Equals(currentReference, selectedPartReference, StringComparison.OrdinalIgnoreCase))
            {
                if (shouldLogEnsureState)
                {
                    ctx.LogFilteredPlacementDiagnostic(
                        "EnsureSelectedPart.aligned",
                        selectedPartReference,
                        currentHudSelectionIndex,
                        -1);
                }

                return;
            }

            if (TryResolveHudIndexForPartReference(selectedPartReference, ctx, out var resolvedHudIndex))
            {
                currentHudSelectionIndex = resolvedHudIndex;
                ctx.LogFilteredPlacementDiagnostic(
                    "EnsureSelectedPart.syncedHudIndex",
                    selectedPartReference,
                    resolvedHudIndex,
                    -1);
            }
        }

        private static bool ShouldLogEnsureSelectedPartState(
            string selectedPartReference,
            int currentHudSelectionIndex,
            float ensureSelectedPartDiagnosticsIntervalSeconds,
            ref string lastEnsureSelectedPartReference,
            ref int lastEnsureSelectedHudIndex,
            ref float nextEnsureSelectedPartDiagnosticAt)
        {
            var now = Time.unscaledTime;
            var selectionChanged = !string.Equals(
                                      lastEnsureSelectedPartReference,
                                      selectedPartReference,
                                      StringComparison.OrdinalIgnoreCase)
                                  || lastEnsureSelectedHudIndex != currentHudSelectionIndex;

            if (!selectionChanged && now < nextEnsureSelectedPartDiagnosticAt)
                return false;

            lastEnsureSelectedPartReference = selectedPartReference;
            lastEnsureSelectedHudIndex = currentHudSelectionIndex;
            nextEnsureSelectedPartDiagnosticAt = now + Mathf.Max(0.1f, ensureSelectedPartDiagnosticsIntervalSeconds);
            return true;
        }

        private static bool TryResolveHudIndexForPartReference(
            string partReference,
            in SelectedPartSyncContext ctx,
            out int hudIndex)
        {
            hudIndex = -1;
            if (string.IsNullOrWhiteSpace(partReference)) return false;

            return TryResolveHudIndexFromSlots(partReference, ctx, out hudIndex)
                   || TryResolveHudIndexFromManagerFallback(partReference, ctx, out hudIndex);
        }

        private static bool TryResolveHudIndexFromSlots(
            string partReference,
            in SelectedPartSyncContext ctx,
            out int hudIndex)
        {
            hudIndex = -1;

            var slotCount = ctx.GetBuildItemCount();
            if (slotCount <= 0) return false;

            for (var index = 0; index < slotCount; index++)
            {
                if (!ctx.TryResolveRadialPartReferenceForHudIndex(index, out var candidateReference, false))
                    continue;

                if (!string.Equals(candidateReference, partReference, StringComparison.OrdinalIgnoreCase))
                    continue;

                hudIndex = index;
                ctx.LogFilteredPlacementDiagnostic("ResolveHudIndex.slotMatch", partReference, hudIndex, -1);
                return true;
            }

            return false;
        }

        private static bool TryResolveHudIndexFromManagerFallback(
            string partReference,
            in SelectedPartSyncContext ctx,
            out int hudIndex)
        {
            hudIndex = -1;

            var manager = ctx.ResolveBuildingManagerInstance();
            if (manager == null) return false;

            var partCount = ctx.GetManagerPartCount(manager);
            if (partCount <= 0) return false;

            for (var index = 0; index < partCount; index++)
            {
                var candidatePart = ctx.GetManagerPartByIndex(manager, index);
                var candidateReference = ctx.ResolvePartIdentifier(candidatePart);
                if (!string.Equals(candidateReference, partReference, StringComparison.OrdinalIgnoreCase))
                    continue;

                hudIndex = index;
                ctx.LogFilteredPlacementDiagnostic("ResolveHudIndex.managerFallback", partReference, hudIndex, index);
                return true;
            }

            return false;
        }
    }
}
