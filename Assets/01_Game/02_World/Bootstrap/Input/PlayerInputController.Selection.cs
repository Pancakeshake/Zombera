#region

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;
using Zombera.Systems.Digging;
using Zombera.UI;

#endregion

namespace Zombera.Systems
{
    public sealed partial class PlayerInputController
    {

        private Vector2 _dragSelectCurrentScreen;
        private Vector2 _dragSelectStartScreen;

        private void HandleSquadSelectionInput(bool pointerOverUi)
        {
            if (squadManager == null) return;

            TryHandleSelectAllShortcut();

            if (!CanProcessDragSelectionInput()) return;

            if (!TryReadPointerScreenPosition(out var mouseScreenPosition)) return;
            if (!TryReadPrimaryMouseState(out var pressed, out var released, out var held)) return;
            var additiveSelection = IsAdditiveSelectionModifierActive();

            if (pressed) BeginDragSelectionCandidate(pointerOverUi, mouseScreenPosition);

            if (!_isDragSelectCandidateActive) return;

            UpdateDragSelectionProgress(mouseScreenPosition, held);

            if (released) CompleteDragSelection(mouseScreenPosition, pointerOverUi, additiveSelection);
        }


        private void TryHandleSelectAllShortcut()
        {
            if (WasActionPressedThisFrame(InputActionNameSelectAll))
            {
                squadManager.SelectAllMembers();
                return;
            }

            if (Keyboard.current == null) return;

            var ctrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            if (ctrlPressed && Keyboard.current.aKey.wasPressedThisFrame)
                squadManager.SelectAllMembers();
        }


        private bool CanProcessDragSelectionInput()
        {
            return enableDragBoxSquadSelection
                   && (Mouse.current != null || TryGetGameplayAction(InputActionNameLeftClick, out _));
        }


        private bool IsAdditiveSelectionModifierActive()
        {
            if (IsActionPressed(InputActionNameAdditiveSelectionModifier)) return true;

            if (!allowAdditiveSelectionModifier || Keyboard.current == null) return false;

            var shiftPressed = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            var ctrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            return shiftPressed || ctrlPressed;
        }


        private void BeginDragSelectionCandidate(bool pointerOverUi, Vector2 mouseScreenPosition)
        {
            if (pointerOverUi)
            {
                _isDragSelectCandidateActive = false;
                _isDragSelecting = false;
                return;
            }

            _isDragSelectCandidateActive = true;
            _isDragSelecting = false;
            _dragSelectStartScreen = mouseScreenPosition;
            _dragSelectCurrentScreen = mouseScreenPosition;
        }


        private void UpdateDragSelectionProgress(Vector2 mouseScreenPosition, bool held)
        {
            if (!held) return;

            _dragSelectCurrentScreen = mouseScreenPosition;
            if (_isDragSelecting)
            {
                if (selectionBoxUi != null)
                {
                    var rect = GetNormalizedScreenRect(_dragSelectStartScreen, _dragSelectCurrentScreen);
                    selectionBoxUi.ShowScreenRect(rect);
                }
                return;
            }

            var threshold = Mathf.Max(4f, dragSelectionThresholdPixels);
            if ((_dragSelectCurrentScreen - _dragSelectStartScreen).sqrMagnitude >= threshold * threshold)
                _isDragSelecting = true;
        }


        private void CompleteDragSelection(Vector2 mouseScreenPosition, bool pointerOverUi, bool additiveSelection)
        {
            _dragSelectCurrentScreen = mouseScreenPosition;

            if (_isDragSelecting)
            {
                SelectSquadMembersInDragRectangle(additiveSelection);
                SuppressNextLeftClickMoveRelease();
            }
            else
            {
                TryHandleSingleClickSquadSelection(pointerOverUi, additiveSelection);
            }

            _isDragSelectCandidateActive = false;
            _isDragSelecting = false;

            if (selectionBoxUi != null) selectionBoxUi.Hide();
        }


        private void SelectSquadMembersInDragRectangle(bool additiveSelection)
        {
            if (squadManager == null) return;

            if (!TryResolveSelectionCamera(out var selectionCamera)) return;

            var selectionRect = BuildPaddedDragSelectionRect();
            PopulateDragSelectionBuffer(selectionCamera, selectionRect);

            if (additiveSelection)
            {
                MergeDragSelectionWithCurrentSelection();
                squadManager.SetSelectedMembers(_selectionMergeBuffer);
                return;
            }

            squadManager.SetSelectedMembers(_dragSelectionBuffer);
        }


        private void MergeDragSelectionWithCurrentSelection()
        {
            _selectionMergeBuffer.Clear();

            if (squadManager != null)
                AppendSelectableUniqueMembers(squadManager.SelectedMembers);

            AppendSelectableUniqueMembers(_dragSelectionBuffer);
        }


        private void AppendSelectableUniqueMembers(IReadOnlyList<SquadMember> sourceMembers)
        {
            if (sourceMembers == null) return;

            for (var i = 0; i < sourceMembers.Count; i++)
            {
                var member = sourceMembers[i];
                if (member == null || !member.IsAvailableForOrders()) continue;
                if (_selectionMergeBuffer.Contains(member)) continue;

                _selectionMergeBuffer.Add(member);
            }
        }


        private void TryHandleSingleClickSquadSelection(bool pointerOverUi, bool additiveSelection)
        {
            if (!enableClickSquadSelection || pointerOverUi || squadManager == null) return;

            if (TryGetSquadMemberUnderCursor(out var clickedMember))
            {
                ApplySingleClickSquadSelection(clickedMember, additiveSelection);
                SuppressNextLeftClickMoveRelease();
            }
        }


        private void ApplySingleClickSquadSelection(SquadMember clickedMember, bool additiveSelection)
        {
            if (clickedMember == null || squadManager == null || !clickedMember.IsAvailableForOrders()) return;

            _selectionMergeBuffer.Clear();

            if (!additiveSelection)
            {
                _selectionMergeBuffer.Add(clickedMember);
                squadManager.SetSelectedMembers(_selectionMergeBuffer);
                return;
            }

            var selectedMembers = squadManager.SelectedMembers;
            var wasAlreadySelected = false;

            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var selectedMember = selectedMembers[i];
                if (selectedMember == null || !selectedMember.IsAvailableForOrders()) continue;

                if (selectedMember == clickedMember)
                {
                    wasAlreadySelected = true;
                    continue;
                }

                if (_selectionMergeBuffer.Contains(selectedMember)) continue;
                _selectionMergeBuffer.Add(selectedMember);
            }

            if (!wasAlreadySelected) _selectionMergeBuffer.Add(clickedMember);

            squadManager.SetSelectedMembers(_selectionMergeBuffer);
        }


        private bool TryGetSquadMemberUnderCursor(out SquadMember member)
        {
            member = null;
            if (!TryGetUnitHealthUnderCursor(out var unitHealth) || unitHealth == null) return false;

            var unit = unitHealth.GetComponentInParent<Unit>();
            if (unit == null) return false;

            member = unit.GetComponent<SquadMember>();

            if (member == null && unit.Role is UnitRole.Player or UnitRole.SquadMember or UnitRole.Survivor)
            {
                member = unit.gameObject.AddComponent<SquadMember>();
                member.RefreshReferences();
                squadManager?.RegisterMember(member);
            }

            return member != null && member.IsAvailableForOrders();
        }


        private bool TryResolveSelectionCamera(out Camera selectionCamera)
        {
            selectionCamera = worldCamera != null ? worldCamera : Zombera.Core.CameraRegistry.Main;
            return selectionCamera != null;
        }


        private Rect BuildPaddedDragSelectionRect()
        {
            var selectionRect = GetNormalizedScreenRect(_dragSelectStartScreen, _dragSelectCurrentScreen);
            var padding = Mathf.Max(0f, dragSelectionScreenPaddingPixels);
            if (padding <= 0f) return selectionRect;

            selectionRect.xMin -= padding;
            selectionRect.yMin -= padding;
            selectionRect.xMax += padding;
            selectionRect.yMax += padding;
            return selectionRect;
        }


        private void PopulateDragSelectionBuffer(Camera selectionCamera, Rect selectionRect)
        {
            _dragSelectionBuffer.Clear();

            var members = squadManager.SquadMembers;
            foreach (var member in members)
                if (IsMemberInSelectionRect(member, selectionCamera, selectionRect))
                    _dragSelectionBuffer.Add(member);
        }


        private static bool IsMemberInSelectionRect(SquadMember member, Camera selectionCamera, Rect selectionRect)
        {
            if (member == null || !member.IsAvailableForOrders()) return false;

            var memberTransform = member.Unit != null
                ? member.Unit.transform
                : member.transform;

            if (memberTransform == null) return false;

            if (TryGetMemberScreenRect(member, selectionCamera, out var memberScreenRect))
                return selectionRect.Overlaps(memberScreenRect, true);

            var screenPosition = selectionCamera.WorldToScreenPoint(memberTransform.position);
            return screenPosition.z > 0f &&
                   selectionRect.Contains(new Vector2(screenPosition.x, screenPosition.y));
        }


        private static bool TryGetMemberScreenRect(SquadMember member, Camera camera, out Rect screenRect)
        {
            screenRect = default;
            if (!TryResolveMemberRoot(member, camera, out var root)) return false;

            var renderers = root.GetComponentsInChildren<Renderer>(false);
            var hasAnyPoint = false;
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            foreach (var renderer in renderers)
                TryAccumulateRendererBounds(
                    renderer,
                    camera,
                    ref hasAnyPoint,
                    ref minX,
                    ref minY,
                    ref maxX,
                    ref maxY);

            if (!hasAnyPoint) return TryCreateFallbackScreenRect(root, camera, out screenRect);

            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }


        private static bool TryResolveMemberRoot(SquadMember member, Camera camera, out Transform root)
        {
            root = null;
            if (member == null || camera == null) return false;

            root = member.Unit != null ? member.Unit.transform : member.transform;
            return root != null;
        }


        private static void TryAccumulateRendererBounds(
            Renderer renderer,
            Camera camera,
            ref bool hasAnyPoint,
            ref float minX,
            ref float minY,
            ref float maxX,
            ref float maxY)
        {
            if (renderer == null || !renderer.enabled) return;

            var bounds = renderer.bounds;
            for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                var corner = BuildBoundsCorner(bounds, cornerIndex);
                var screenPoint = camera.WorldToScreenPoint(corner);
                if (screenPoint.z <= 0f) continue;

                UpdateScreenBounds(screenPoint, ref hasAnyPoint, ref minX, ref minY, ref maxX, ref maxY);
            }
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


        private static void UpdateScreenBounds(
            Vector3 screenPoint,
            ref bool hasAnyPoint,
            ref float minX,
            ref float minY,
            ref float maxX,
            ref float maxY)
        {
            if (!hasAnyPoint)
            {
                minX = maxX = screenPoint.x;
                minY = maxY = screenPoint.y;
                hasAnyPoint = true;
                return;
            }

            if (screenPoint.x < minX) minX = screenPoint.x;
            if (screenPoint.x > maxX) maxX = screenPoint.x;
            if (screenPoint.y < minY) minY = screenPoint.y;
            if (screenPoint.y > maxY) maxY = screenPoint.y;
        }


        private static bool TryCreateFallbackScreenRect(Transform root, Camera camera, out Rect screenRect)
        {
            screenRect = default;

            var fallbackPoint = camera.WorldToScreenPoint(root.position);
            if (fallbackPoint.z <= 0f) return false;

            screenRect = new Rect(fallbackPoint.x, fallbackPoint.y, 1f, 1f);
            return true;
        }
    }
}
