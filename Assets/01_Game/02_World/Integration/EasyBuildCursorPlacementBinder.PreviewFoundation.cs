using System;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildCursorPlacementBinder
    {
        private float ResolveFoundationRequiredLowestY(Component previewPart, float terrainRequiredLowestY)
        {
            var snappedTerrainRequiredY = SnapFoundationLowestYToStep(terrainRequiredLowestY);
            if (TryGetNearbyFoundationLowestY(previewPart, out var nearbyFoundationLowestY))
                return Mathf.Max(snappedTerrainRequiredY, nearbyFoundationLowestY);

            return snappedTerrainRequiredY;
        }

        private float SnapFoundationLowestYToStep(float value)
        {
            var step = Mathf.Max(0.01f, foundationHeightStepMeters);
            return Mathf.Ceil(value / step) * step;
        }

        private bool TryGetNearbyFoundationLowestY(Component previewPart, out float lowestY)
        {
            lowestY = 0f;

            if (!CanProbeNearbyFoundations(previewPart))
                return false;

            var center = previewPart.transform.position;
            var hitCount = ProbeNearbyFoundationColliders(center);
            if (hitCount <= 0)
                return false;

            return TryResolveClosestFoundationLowestY(previewPart.transform.root, center, hitCount, out lowestY);
        }

        private bool CanProbeNearbyFoundations(Component previewPart)
        {
            return previewPart != null && foundationNeighborProbeRadius > 0f;
        }

        private int ProbeNearbyFoundationColliders(Vector3 center)
        {
            return Physics.OverlapSphereNonAlloc(
                center,
                foundationNeighborProbeRadius,
                _foundationNeighborProbeHits,
                foundationNeighborProbeMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool TryResolveClosestFoundationLowestY(Transform previewRoot, Vector3 center, int hitCount,
            out float lowestY)
        {
            lowestY = 0f;

            var found = false;
            var bestSqrDistance = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var hit = TakeFoundationProbeHit(i);
                if (!IsFoundationProbeCandidate(hit, previewRoot)) continue;

                var sqrDistance = (hit.bounds.center - center).sqrMagnitude;
                if (!IsCloserFoundationCandidate(found, sqrDistance, bestSqrDistance)) continue;

                found = true;
                bestSqrDistance = sqrDistance;
                lowestY = hit.bounds.min.y;
            }

            return found;
        }

        private Collider TakeFoundationProbeHit(int index)
        {
            var hit = _foundationNeighborProbeHits[index];
            _foundationNeighborProbeHits[index] = null;
            return hit;
        }

        private static bool IsFoundationProbeCandidate(Collider hit, Transform previewRoot)
        {
            if (hit == null) return false;

            if (previewRoot != null && hit.transform.IsChildOf(previewRoot))
                return false;

            return IsFoundationPlacementPreview(
                hit.gameObject.name,
                hit.transform.parent != null ? hit.transform.parent.name : null,
                hit.transform.root != null ? hit.transform.root.name : null);
        }

        private static bool IsCloserFoundationCandidate(bool found, float sqrDistance, float bestSqrDistance)
        {
            return !found || sqrDistance < bestSqrDistance;
        }

        private static bool IsFoundationPlacementPreview(params string[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
                return false;

            for (var i = 0; i < tokens.Length; i++)
                if (ContainsFoundationToken(tokens[i]))
                    return true;

            return false;
        }

        private static bool ContainsFoundationToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.IndexOf("foundation", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolvePartLabelForChecks(params string[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
                return string.Empty;

            for (var i = 0; i < tokens.Length; i++)
                if (!string.IsNullOrWhiteSpace(tokens[i]))
                    return tokens[i];

            return string.Empty;
        }

        private string ResolveDiagnosticPartReference(string previewPartReference, string previewObjectName,
            string selectedPartReference, string selectedPartDisplayName)
        {
            if (ShouldLogPlacementDiagnosticForPart(previewPartReference, previewObjectName))
                return previewPartReference;

            if (ShouldLogPlacementDiagnosticForPart(selectedPartReference, selectedPartDisplayName))
                return selectedPartReference;

            return previewPartReference;
        }

        private void LogPreviewScanDiagnostic(string stage, string selectedPartReference, string details)
        {
            if (!enablePreviewScanDiagnosticsLogs) return;

            var now = Time.unscaledTime;
            if (now < _nextPreviewScanDiagnosticAt) return;

            var selectedPartDisplayName = TryResolveSelectedPartDisplayNameFromController();
            if (!ShouldLogPlacementDiagnosticForPart(selectedPartReference, selectedPartDisplayName)) return;

            _nextPreviewScanDiagnosticAt = now + Mathf.Max(0.1f, previewScanDiagnosticsIntervalSeconds);

            if (string.IsNullOrWhiteSpace(details))
            {
                Debug.Log($"[EasyBuildCursorBinder] [PlacementDiag] {stage}: partRef={selectedPartReference}", this);
                return;
            }

            Debug.Log($"[EasyBuildCursorBinder] [PlacementDiag] {stage}: partRef={selectedPartReference}, {details}",
                this);
        }

        private void NormalizePreviewSettings(object settings)
        {
            if (forceGroundingOnPreviewSettings)
                SetMemberValue(settings, "PreviewForceGrounding", true);

            if (clampNegativePreviewYOffset)
            {
                var offsetRaw = GetMemberValue(settings, "PreviewOffsetPosition");
                if (offsetRaw is Vector3 offset && offset.y < 0f)
                {
                    offset.y = 0f;
                    SetMemberValue(settings, "PreviewOffsetPosition", offset);
                }
            }

            if (!expandGroundingMaskToCommonGroundLayers) return;

            var existingMask = ResolvePreviewGroundMask(settings);
            var extraMask = LayerMask.GetMask("Ground", "Terrain", "Default");
            var merged = existingMask.value | extraMask;
            if (merged == existingMask.value) return;

            var outMask = (LayerMask)merged;
            SetMemberValue(settings, "PreviewGroundingLayer", outMask);
        }

        private LayerMask ResolvePreviewGroundMask(object settings)
        {
            if (settings == null) return previewGroundFallbackMask;

            var rawMask = GetMemberValue(settings, "PreviewGroundingLayer")
                          ?? GetMemberValue(settings, "GroundingLayer")
                          ?? GetMemberValue(settings, "GroundMask")
                          ?? GetMemberValue(settings, "PlacementGroundMask");

            if (rawMask is LayerMask layerMask)
                return layerMask;

            if (rawMask is int intMask)
                return (LayerMask)intMask;

            return previewGroundFallbackMask;
        }

        private static float? GetGroundYUnderPreview(Vector3 aroundPosition, LayerMask mask)
        {
            var rayOrigin = aroundPosition + Vector3.up * 512f;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out var hit, 2048f, mask, QueryTriggerInteraction.Ignore))
                return null;

            return hit.point.y;
        }

        private static float GetPreviewLowestWorldY(Component part)
        {
            var renderers = part.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return part.transform.position.y;

            var lowest = float.PositiveInfinity;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r.bounds.min.y < lowest) lowest = r.bounds.min.y;
            }

            return float.IsPositiveInfinity(lowest) ? part.transform.position.y : lowest;
        }

    }
}
