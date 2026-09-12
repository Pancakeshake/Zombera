using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.Characters;
using Zombera.Factions;

namespace Zombera.Systems
{
    public sealed partial class SelectionManager
    {
        private bool TryHandleHostileClickAttack(Vector2 pointerScreenPosition)
        {
            if (squadManager == null || !squadManager.HasSelection) return false;
            if (!TryGetAttackTargetUnderPointer(pointerScreenPosition, out var targetHealth)) return false;

            var focusPoint = targetHealth.transform.position;
            if (squadManager.TryIssueAttackOrderToSelection(targetHealth, focusPoint)) return true;

            squadManager.IssueAttackOrder(targetHealth, focusPoint);
            return true;
        }

        private bool TryGetAttackTargetUnderPointer(Vector2 pointerScreenPosition, out UnitHealth targetHealth)
        {
            targetHealth = null;
            if (!TryResolveSelectionCamera(out var selectionCamera)) return false;

            if (!RtsPointerQueryUtility.TryGetNearestUnitHealthUnderPointer(
                    selectionCamera,
                    pointerScreenPosition,
                    attackTargetMask,
                    selectableQueryTriggerInteraction,
                    selectionRayDistance,
                    _attackHits,
                    out var nearestTarget))
                return false;

            var sourceUnit = ResolveSelectionUnit();
            if (!IsValidAttackTarget(nearestTarget, sourceUnit)) return false;

            targetHealth = nearestTarget;
            return true;
        }

        private bool IsValidAttackTarget(UnitHealth candidateHealth, Unit sourceUnit)
        {
            if (candidateHealth == null || candidateHealth.IsDead) return false;

            var targetUnit = candidateHealth.GetComponentInParent<Unit>();
            if (targetUnit == null) return false;
            if (IsSelectedSquadUnit(targetUnit)) return false;

            if (!requireHostileTargetsForAttack) return true;

            if (sourceUnit == null) return true;
            return FactionManager.AreUnitsHostile(sourceUnit, targetUnit);
        }

        private bool IsSelectedSquadUnit(Unit targetUnit)
        {
            if (targetUnit == null || squadManager == null) return false;

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var member = selectedMembers[i];
                if (member?.Unit == targetUnit) return true;
            }

            return false;
        }

        private Unit ResolveSelectionUnit()
        {
            if (squadManager == null) return null;

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var member = selectedMembers[i];
                if (member?.Unit != null) return member.Unit;
            }

            return null;
        }

        private void PopulateDragSelectionBuffer(Camera selectionCamera, Rect selectionRect)
        {
            _dragSelectionBuffer.Clear();
            var members = squadManager.SquadMembers;

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (!IsSelectableMember(member)) continue;
                if (!IsMemberInSelectionRect(member, selectionCamera, selectionRect)) continue;

                _dragSelectionBuffer.Add(member);
            }
        }

        private static bool IsMemberInSelectionRect(SquadMember member, Camera selectionCamera, Rect selectionRect)
        {
            if (!TryResolveMemberRoot(member, out var root)) return false;

            if (TryGetMemberScreenRect(root, selectionCamera, out var memberRect))
                return selectionRect.Overlaps(memberRect, true);

            var screenPoint = selectionCamera.WorldToScreenPoint(root.position);
            return screenPoint.z > 0f && selectionRect.Contains(new Vector2(screenPoint.x, screenPoint.y));
        }

        private static bool TryResolveMemberRoot(SquadMember member, out Transform root)
        {
            root = null;
            if (member == null) return false;

            root = member.Unit != null ? member.Unit.transform : member.transform;
            return root != null;
        }

        private static bool TryGetMemberScreenRect(Transform root, Camera selectionCamera, out Rect screenRect)
        {
            screenRect = default;
            if (root == null || selectionCamera == null) return false;

            var renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers == null || renderers.Length == 0) return false;

            var hasPoint = false;
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer == null || !renderer.enabled) continue;

                var bounds = renderer.bounds;
                for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    var corner = BuildBoundsCorner(bounds, cornerIndex);
                    var screenPoint = selectionCamera.WorldToScreenPoint(corner);
                    if (screenPoint.z <= 0f) continue;

                    if (!hasPoint)
                    {
                        minX = maxX = screenPoint.x;
                        minY = maxY = screenPoint.y;
                        hasPoint = true;
                        continue;
                    }

                    if (screenPoint.x < minX) minX = screenPoint.x;
                    if (screenPoint.x > maxX) maxX = screenPoint.x;
                    if (screenPoint.y < minY) minY = screenPoint.y;
                    if (screenPoint.y > maxY) maxY = screenPoint.y;
                }
            }

            if (!hasPoint) return false;

            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static Vector3 BuildBoundsCorner(Bounds bounds, int cornerIndex)
        {
            var center = bounds.center;
            var extents = bounds.extents;

            return new Vector3(
                center.x + ((cornerIndex & 1) == 0 ? -extents.x : extents.x),
                center.y + ((cornerIndex & 2) == 0 ? -extents.y : extents.y),
                center.z + ((cornerIndex & 4) == 0 ? -extents.z : extents.z));
        }

        private bool TryGetMemberUnderPointer(Vector2 pointerScreenPosition, out SquadMember selectedMember)
        {
            selectedMember = null;
            if (!TryResolveSelectionCamera(out var selectionCamera)) return false;

            var ray = selectionCamera.ScreenPointToRay(pointerScreenPosition);

            var hitCount = Physics.RaycastNonAlloc(
                ray,
                _selectionHits,
                Mathf.Max(1f, selectionRayDistance),
                selectableMask,
                selectableQueryTriggerInteraction);

            if (hitCount <= 0) return false;

            var nearestDistance = float.MaxValue;
            for (var i = 0; i < Mathf.Min(hitCount, _selectionHits.Length); i++)
            {
                var hit = _selectionHits[i];
                _selectionHits[i] = default;

                if (hit.collider == null) continue;

                var candidate = hit.collider.GetComponentInParent<SquadMember>();
                if (!IsSelectableMember(candidate)) continue;
                if (!IsRegisteredSquadMember(candidate)) continue;
                if (hit.distance >= nearestDistance) continue;

                nearestDistance = hit.distance;
                selectedMember = candidate;
            }

            return selectedMember != null;
        }

        private bool TryResolveSelectionCamera(out Camera selectionCamera)
        {
            selectionCamera = worldCamera != null ? worldCamera : Zombera.Core.CameraRegistry.Main;
            return selectionCamera != null;
        }

        private static Rect GetNormalizedScreenRect(Vector2 start, Vector2 end)
        {
            var minX = Mathf.Min(start.x, end.x);
            var minY = Mathf.Min(start.y, end.y);
            var maxX = Mathf.Max(start.x, end.x);
            var maxY = Mathf.Max(start.y, end.y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private Rect BuildPaddedSelectionRect(Vector2 start, Vector2 end)
        {
            var rect = GetNormalizedScreenRect(start, end);
            var padding = Mathf.Max(0f, dragSelectionPaddingPixels);
            if (padding <= 0f) return rect;

            rect.xMin -= padding;
            rect.yMin -= padding;
            rect.xMax += padding;
            rect.yMax += padding;
            return rect;
        }

        private bool IsRegisteredSquadMember(SquadMember member)
        {
            if (squadManager == null || member == null) return false;

            var members = squadManager.SquadMembers;
            for (var i = 0; i < members.Count; i++)
                if (members[i] == member)
                    return true;

            return false;
        }

        private static bool IsSelectableMember(SquadMember member)
        {
            return member != null && member.IsAvailableForOrders();
        }

        private static bool IsPointerOverUi(Vector2 pointerScreenPosition)
        {
            var uiEventSystem = EventSystem.current;
            if (uiEventSystem == null || !uiEventSystem.isActiveAndEnabled) return false;

            if (Mouse.current != null && uiEventSystem.IsPointerOverGameObject()) return true;

            if (s_uiPointerEventData == null || s_uiPointerEventDataOwner != uiEventSystem)
            {
                s_uiPointerEventDataOwner = uiEventSystem;
                s_uiPointerEventData = new PointerEventData(uiEventSystem);
            }

            s_uiPointerEventData.Reset();
            s_uiPointerEventData.position = pointerScreenPosition;

            UiRaycastResults.Clear();
            uiEventSystem.RaycastAll(s_uiPointerEventData, UiRaycastResults);
            return UiRaycastResults.Count > 0;
        }
    }
}
