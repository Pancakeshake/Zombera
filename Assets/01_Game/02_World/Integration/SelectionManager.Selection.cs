using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Systems
{
    public sealed partial class SelectionManager
    {
        private void ApplySingleClickSelection(Vector2 pointerScreenPosition, bool additiveSelection)
        {
            if (!additiveSelection && TryHandleHostileClickAttack(pointerScreenPosition)) return;

            if (TryGetMemberUnderPointer(pointerScreenPosition, out var clickedMember))
            {
                if (!additiveSelection)
                {
                    _selectionWorkingBuffer.Clear();
                    _selectionWorkingBuffer.Add(clickedMember);
                    ApplySelection(_selectionWorkingBuffer);
                    return;
                }

                BuildSelectionBufferFromCurrent();

                var existingIndex = _selectionWorkingBuffer.IndexOf(clickedMember);
                if (existingIndex >= 0)
                    _selectionWorkingBuffer.RemoveAt(existingIndex);
                else
                    _selectionWorkingBuffer.Add(clickedMember);

                ApplySelection(_selectionWorkingBuffer);
                return;
            }

            // RTS semantic: left-clicking empty ground clears the active selection.
            if (!additiveSelection)
            {
                if (clearSelectionOnGroundClick)
                {
                    ClearSelection();
                }
                else if (hybridSingleCharacterAndRtsMode)
                {
                    // In hybrid mode, we issue a move order to the selection on ground click.
                    if (TryGetGroundPoint(pointerScreenPosition, out var groundPoint))
                    {
                        if (squadManager != null && squadManager.HasSelection)
                            squadManager.TryIssueOrderToSelection(SquadCommandType.Move, groundPoint);
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
                else
                {
                    ClearSelection();
                }
            }
        }

        private bool TryGetGroundPoint(Vector2 pointerScreenPosition, out Vector3 groundPoint)
        {
            groundPoint = default;
            if (!TryResolveSelectionCamera(out var selectionCamera)) return false;

            return RtsPointerQueryUtility.TryGetGroundPoint(
                selectionCamera,
                pointerScreenPosition,
                groundMask,
                selectionRayDistance,
                logMovementGroundingDiagnostics,
                out groundPoint);
        }

        private void ApplyDragSelection(bool additiveSelection)
        {
            if (!TryResolveSelectionCamera(out var selectionCamera)) return;

            var selectionRect = BuildPaddedSelectionRect(_dragStartScreen, _dragCurrentScreen);
            PopulateDragSelectionBuffer(selectionCamera, selectionRect);

            if (!additiveSelection)
            {
                ApplySelection(_dragSelectionBuffer);
                return;
            }

            BuildSelectionBufferFromCurrent();

            for (var i = 0; i < _dragSelectionBuffer.Count; i++)
            {
                var candidate = _dragSelectionBuffer[i];
                if (candidate == null || _selectionWorkingBuffer.Contains(candidate)) continue;

                _selectionWorkingBuffer.Add(candidate);
            }

            ApplySelection(_selectionWorkingBuffer);
        }

        private void BuildSelectionBufferFromCurrent()
        {
            _selectionWorkingBuffer.Clear();

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var member = selectedMembers[i];
                if (!IsSelectableMember(member)) continue;

                _selectionWorkingBuffer.Add(member);
            }
        }

        private void ApplySelection(IReadOnlyList<SquadMember> members)
        {
            squadManager.SetSelectedMembers(members);
            SyncSelectionVisuals();
        }

        private void ClearSelection()
        {
            squadManager.ClearSelection();
            SyncSelectionVisuals();
        }

        private void SyncSelectionVisuals()
        {
            if (!updateSelectionVisuals || squadManager == null) return;

            _selectionLookup.Clear();

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var selectedMember = selectedMembers[i];
                if (selectedMember == null) continue;

                _selectionLookup.Add(selectedMember);
            }

            var allMembers = squadManager.SquadMembers;
            for (var i = 0; i < allMembers.Count; i++)
            {
                var member = allMembers[i];
                if (member == null) continue;

                var visual = ResolveSelectionVisual(member);
                if (visual == null) continue;

                visual.SetSelected(_selectionLookup.Contains(member));
            }
        }

        private UnitSelectionVisual ResolveSelectionVisual(SquadMember member)
        {
            if (member == null) return null;

            var visual = member.GetComponent<UnitSelectionVisual>();
            if (visual != null || !autoAddSelectionVisualWhenMissing) return visual;

            return member.gameObject.AddComponent<UnitSelectionVisual>();
        }
    }
}
