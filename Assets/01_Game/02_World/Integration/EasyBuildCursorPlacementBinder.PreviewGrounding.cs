#region

using System;
using UnityEngine;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildCursorPlacementBinder
    {
        private void FixActivePlacementPreviews()
        {
            var selectedPartReference = TryResolveSelectedPartReferenceFromController();
            var selectedPartDisplayName = TryResolveSelectedPartDisplayNameFromController();

            if (!TryResolvePlacementPartsProvider(out var managerInstance, out var placementState))
            {
                LogPreviewScanDiagnostic("Preview.scan.noProvider", selectedPartReference,
                    $"selectedDisplay={selectedPartDisplayName ?? "-"}");
                return;
            }

            var parts =
                InvokeMethodWithReturn(managerInstance, "GetPartsByState", placementState) as
                    System.Collections.IEnumerable;
            if (parts == null)
            {
                LogPreviewScanDiagnostic("Preview.scan.noEnumerable", selectedPartReference,
                    $"selectedDisplay={selectedPartDisplayName ?? "-"}");
                return;
            }

            var totalParts = 0;
            var activeParts = 0;
            var matchedParts = 0;

            foreach (var part in parts)
            {
                totalParts++;

                var component = part as Component;
                if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy)
                    continue;

                activeParts++;
                if (FixSinglePreviewPart(component, selectedPartReference, selectedPartDisplayName))
                    matchedParts++;
            }

            LogPreviewScanDiagnostic("Preview.scan", selectedPartReference,
                $"selectedDisplay={selectedPartDisplayName ?? "-"}, total={totalParts}, active={activeParts}, matched={matchedParts}");
        }

        private bool TryResolvePlacementPartsProvider(out object managerInstance, out object placementState)
        {
            managerInstance = null;
            placementState = null;

            _cachedBuildingManagerType ??= FindType(BuildingManagerTypeName);
            _cachedBuildingPartStateType ??= FindType(BuildingPartStateTypeName);

            if (_cachedBuildingManagerType == null || _cachedBuildingPartStateType == null)
                return false;

            _cachedBuildingManagerInstance ??= GetStaticMemberValue(_cachedBuildingManagerType, "Instance");
            if (_cachedBuildingManagerInstance == null)
                return false;

            _cachedPlacementStateEnum ??= Enum.Parse(_cachedBuildingPartStateType, "Placement");

            managerInstance = _cachedBuildingManagerInstance;
            placementState = _cachedPlacementStateEnum;
            return true;
        }

        private bool FixSinglePreviewPart(Component part, string selectedPartReference, string selectedPartDisplayName)
        {
            if (!TryResolvePreviewGroundingContext(
                    part,
                    selectedPartReference,
                    selectedPartDisplayName,
                    out var diagnosticContext,
                    out var mask))
                return false;

            if (!TryResolvePreviewGroundSample(part, mask, diagnosticContext, out var groundY, out var lowest))
                return true;

            var required = ResolvePreviewRequiredLowestY(part, groundY, diagnosticContext);

            LogPreviewSampleIfNeeded(diagnosticContext, mask,
                groundY, lowest, required, part.transform.position);

            return ApplyPreviewVerticalAdjustment(part, lowest, required, diagnosticContext);
        }

        private bool TryResolvePreviewGroundingContext(
            Component part,
            string selectedPartReference,
            string selectedPartDisplayName,
            out PreviewDiagnosticContext diagnosticContext,
            out LayerMask mask)
        {
            diagnosticContext = default;
            mask = default;

            if (!IsActivePreviewPart(part)) return false;

            var partReference = ResolvePartReference(part);
            var previewObjectName = part.gameObject.name;
            diagnosticContext = BuildPreviewDiagnosticContext(
                partReference,
                previewObjectName,
                selectedPartReference,
                selectedPartDisplayName);

            var settings = GetMemberValue(GetMemberValue(part, "PlacementSystem"), "Settings");
            if (settings != null)
                NormalizePreviewSettings(settings);

            mask = ResolvePreviewMaskWithFallback(settings);
            return true;
        }

        private bool TryResolvePreviewGroundSample(
            Component part,
            LayerMask mask,
            PreviewDiagnosticContext diagnosticContext,
            out float groundY,
            out float lowest)
        {
            lowest = GetPreviewLowestWorldY(part);

            var groundYHit = GetGroundYUnderPreview(part.transform.position, mask);
            if (groundYHit.HasValue)
            {
                groundY = groundYHit.Value;
                return true;
            }

            groundY = 0f;
            LogPlacementDiagnostic(
                "Preview.noGroundHit",
                diagnosticContext.DiagnosticPartReference,
                -1,
                $"{diagnosticContext.ReferenceDetail}, mask={mask.value}, position={FormatVector3(part.transform.position)}");
            return false;
        }

        private float ResolvePreviewRequiredLowestY(
            Component part,
            float groundY,
            PreviewDiagnosticContext diagnosticContext)
        {
            var required = groundY + previewGroundClearance;
            if (autoSnapFoundationHeights && diagnosticContext.IsFoundation)
                required = ResolveFoundationRequiredLowestY(part, required);

            return required;
        }

        private static bool IsActivePreviewPart(Component part)
        {
            return part != null && part.gameObject != null && part.gameObject.activeInHierarchy;
        }

        private readonly struct PreviewDiagnosticContext
        {
            public readonly bool ShouldLogPart;
            public readonly string DiagnosticPartReference;
            public readonly string ReferenceDetail;
            public readonly bool IsFoundation;

            public PreviewDiagnosticContext(bool shouldLogPart, string diagnosticPartReference,
                string referenceDetail, bool isFoundation)
            {
                ShouldLogPart = shouldLogPart;
                DiagnosticPartReference = diagnosticPartReference;
                ReferenceDetail = referenceDetail;
                IsFoundation = isFoundation;
            }
        }

        private PreviewDiagnosticContext BuildPreviewDiagnosticContext(string partReference, string previewObjectName,
            string selectedPartReference, string selectedPartDisplayName)
        {
            var shouldLogPart = ShouldLogPlacementDiagnosticForPart(
                partReference,
                previewObjectName,
                selectedPartReference,
                selectedPartDisplayName);

            var diagnosticPartReference = shouldLogPart
                ? ResolveDiagnosticPartReference(
                    partReference,
                    previewObjectName,
                    selectedPartReference,
                    selectedPartDisplayName)
                : ResolvePartLabelForChecks(partReference, previewObjectName, selectedPartReference,
                    selectedPartDisplayName);

            var referenceDetail = string.Equals(diagnosticPartReference, partReference, StringComparison.OrdinalIgnoreCase)
                ? $"previewRef={partReference}"
                : $"previewRef={partReference}, selectedRef={diagnosticPartReference}";

            var isFoundation = IsFoundationPlacementPreview(partReference, previewObjectName, selectedPartReference,
                selectedPartDisplayName);

            return new PreviewDiagnosticContext(shouldLogPart, diagnosticPartReference, referenceDetail, isFoundation);
        }

        private LayerMask ResolvePreviewMaskWithFallback(object settings)
        {
            var mask = ResolvePreviewGroundMask(settings);
            return mask.value == 0 ? previewGroundFallbackMask : mask;
        }

        private void LogPreviewSampleIfNeeded(PreviewDiagnosticContext diagnosticContext, LayerMask mask,
            float groundY, float lowest, float required, Vector3 position)
        {
            if (!diagnosticContext.ShouldLogPart) return;

            LogPlacementDiagnostic("Preview.sample", diagnosticContext.DiagnosticPartReference, -1,
                $"{diagnosticContext.ReferenceDetail}, foundation={diagnosticContext.IsFoundation}, mask={mask.value}, groundY={groundY:0.###}, lowest={lowest:0.###}, required={required:0.###}, position={FormatVector3(position)}");
        }

        private bool ApplyPreviewVerticalAdjustment(Component part, float lowest, float required,
            PreviewDiagnosticContext diagnosticContext)
        {
            if (!autoSnapFoundationHeights || !diagnosticContext.IsFoundation)
                return ApplyNonFoundationPreviewAdjustment(part, lowest, required, diagnosticContext);

            return ApplyFoundationPreviewAdjustment(part, lowest, required, diagnosticContext);
        }

        private bool ApplyNonFoundationPreviewAdjustment(Component part, float lowest, float required,
            PreviewDiagnosticContext diagnosticContext)
        {
            if (lowest >= required) return true;

            var deltaUp = required - lowest;
            var previousPosition = part.transform.position;
            var adjustedPosition = part.transform.position;
            adjustedPosition.y += deltaUp;
            part.transform.position = adjustedPosition;

            if (diagnosticContext.ShouldLogPart)
                LogPlacementDiagnostic("Preview.adjustedY", diagnosticContext.DiagnosticPartReference, -1,
                    $"{diagnosticContext.ReferenceDetail}, deltaY={deltaUp:0.###}, lowest={lowest:0.###}, required={required:0.###}, from={FormatVector3(previousPosition)}, to={FormatVector3(adjustedPosition)}");

            return true;
        }

        private bool ApplyFoundationPreviewAdjustment(Component part, float lowest, float required,
            PreviewDiagnosticContext diagnosticContext)
        {
            var delta = required - lowest;
            if (Mathf.Abs(delta) <= 0.0005f)
                return true;

            var previousPosition = part.transform.position;
            var adjustedPosition = part.transform.position;
            adjustedPosition.y += delta;
            part.transform.position = adjustedPosition;

            if (diagnosticContext.ShouldLogPart)
                LogPlacementDiagnostic("Preview.adjustedY", diagnosticContext.DiagnosticPartReference, -1,
                    $"{diagnosticContext.ReferenceDetail}, foundation={diagnosticContext.IsFoundation}, deltaY={delta:0.###}, lowest={lowest:0.###}, required={required:0.###}, from={FormatVector3(previousPosition)}, to={FormatVector3(adjustedPosition)}");

            return true;
        }

    }
}
