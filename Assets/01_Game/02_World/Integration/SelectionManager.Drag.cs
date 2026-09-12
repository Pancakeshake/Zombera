using UnityEngine;
using UnityEngine.InputSystem;

namespace Zombera.Systems
{
    public sealed partial class SelectionManager
    {
        private void UpdateHybridDragProbe(
            Vector2 pointerScreenPosition,
            bool wasPressed,
            bool wasReleased,
            bool isHeld)
        {
            if (!hybridSingleCharacterAndRtsMode || !autoSwitchToRtsWhenMultipleSelected)
            {
                _hybridDragProbeActive = false;
                return;
            }

            var selectionThreshold = Mathf.Max(2, rtsMouseCaptureSelectionThreshold);
            var capturesFromSelectionCount = autoSwitchToRtsWhenMultipleSelected
                                             && squadManager != null
                                             && squadManager.SelectedMembers.Count >= selectionThreshold;

            if (capturesFromSelectionCount || _dragCandidateActive || _isDragging)
            {
                _hybridDragProbeActive = false;
                return;
            }

            if (wasPressed)
            {
                if (IsPointerOverUi(pointerScreenPosition))
                {
                    _hybridDragProbeActive = false;
                    return;
                }

                _hybridDragProbeActive = true;
                _hybridDragProbeStartScreen = pointerScreenPosition;
                return;
            }

            if (!_hybridDragProbeActive) return;

            if (wasReleased || !isHeld)
            {
                _hybridDragProbeActive = false;
                return;
            }

            var threshold = Mathf.Max(4f, dragThresholdPixels);
            if ((pointerScreenPosition - _hybridDragProbeStartScreen).sqrMagnitude < threshold * threshold) return;

            _hybridDragProbeActive = false;
            _dragCandidateActive = true;
            _isDragging = false;
            _dragStartScreen = _hybridDragProbeStartScreen;
            _dragCurrentScreen = pointerScreenPosition;
        }

        private void BeginSelectionCandidate(Vector2 pointerScreenPosition)
        {
            if (IsPointerOverUi(pointerScreenPosition))
            {
                ResetDragState();
                return;
            }

            _dragCandidateActive = true;
            _isDragging = false;
            _dragStartScreen = pointerScreenPosition;
            _dragCurrentScreen = pointerScreenPosition;
        }

        private void UpdateDragCandidate(Vector2 pointerScreenPosition, bool isHeld)
        {
            if (!isHeld) return;

            _dragCurrentScreen = pointerScreenPosition;
            if (_isDragging)
            {
                selectionBoxUi?.ShowScreenRect(GetNormalizedScreenRect(_dragStartScreen, _dragCurrentScreen));
                return;
            }

            var threshold = Mathf.Max(4f, dragThresholdPixels);
            if ((_dragCurrentScreen - _dragStartScreen).sqrMagnitude < threshold * threshold) return;

            _isDragging = true;
            selectionBoxUi?.ShowScreenRect(GetNormalizedScreenRect(_dragStartScreen, _dragCurrentScreen));
        }

        private void CompleteSelection(Vector2 pointerScreenPosition)
        {
            _dragCurrentScreen = pointerScreenPosition;

            var additiveSelection = IsAdditiveSelectionActive();

            if (_isDragging)
                ApplyDragSelection(additiveSelection);
            else
                ApplySingleClickSelection(pointerScreenPosition, additiveSelection);

            ResetDragState();
            if (selectionBoxUi != null) selectionBoxUi.Hide();
        }

        private bool IsAdditiveSelectionActive()
        {
            if (!allowAdditiveSelection) return false;

            if (additiveSelectionAction?.action != null) return additiveSelectionAction.action.IsPressed();

            if (!useShiftAsAdditiveSelectionModifier || Keyboard.current == null) return false;

            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
    }
}
