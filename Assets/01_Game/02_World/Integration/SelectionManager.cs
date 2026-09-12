#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.Characters;
using Zombera.Factions;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Handles RTS-style unit selection:
    ///     left-click single select, drag-box multi-select, and ground-click deselect.
    /// </summary>
    public sealed partial class SelectionManager : MonoBehaviour
    {
        [Header("References")] [SerializeField] private Camera worldCamera;
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private SelectionBoxUI selectionBoxUi;
        [SerializeField] private PlayerControlModeCoordinator controlModeCoordinator;

        [Header("Selection Query")]
        [SerializeField] private LayerMask selectableMask = ~0;
        [SerializeField]
        private QueryTriggerInteraction selectableQueryTriggerInteraction = QueryTriggerInteraction.Collide;

        [SerializeField] private LayerMask attackTargetMask = ~0;
        [SerializeField] private bool requireHostileTargetsForAttack = true;

        [SerializeField] [Min(1f)] private float selectionRayDistance = 1000f;
        [SerializeField] [Min(4f)] private float dragThresholdPixels = 16f;
        [SerializeField] [Min(0f)] private float dragSelectionPaddingPixels = 8f;

        [Header("Selection Rules")] [SerializeField]
        private bool allowAdditiveSelection = true;

        [SerializeField] private bool useShiftAsAdditiveSelectionModifier = true;

        [SerializeField] private bool clearSelectionOnGroundClick = false;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private bool logMovementGroundingDiagnostics;

        [Header("Selection Visuals")] [SerializeField]
private bool updateSelectionVisuals = true;

        [SerializeField] private bool autoAddSelectionVisualWhenMissing = true;

        [Header("Input Actions (Optional)")] [SerializeField]
        private InputActionReference leftClickAction;

        [SerializeField] private InputActionReference pointerPositionAction;
        [SerializeField] private InputActionReference additiveSelectionAction;

        [Header("Hybrid Control")]
        [SerializeField] private bool hybridSingleCharacterAndRtsMode = true;
        [SerializeField] private bool autoSwitchToRtsWhenMultipleSelected = true;
        [SerializeField] [Min(2)] private int rtsMouseCaptureSelectionThreshold = 2;
        [SerializeField] private bool requireRtsModifierForMouseCapture = false;
        [SerializeField] private bool useAltAsRtsModifierFallback = true;
        [SerializeField] private InputActionReference rtsModifierAction;

        [Header("Legacy Input Interop")] [SerializeField]
        private bool overrideLegacyPlayerMouseInput = true;

        private static readonly List<RaycastResult> UiRaycastResults = new(16);
        private static PointerEventData s_uiPointerEventData;
        private static EventSystem s_uiPointerEventDataOwner;

        private readonly List<SquadMember> _dragSelectionBuffer = new(64);
        private readonly List<SquadMember> _selectionWorkingBuffer = new(64);
        private readonly HashSet<SquadMember> _selectionLookup = new();
        private readonly RaycastHit[] _selectionHits = new RaycastHit[64];
        private readonly RaycastHit[] _attackHits = new RaycastHit[64];

        private bool _dragCandidateActive;
        private bool _isDragging;
        private bool _hybridDragProbeActive;

        private Vector2 _dragStartScreen;
        private Vector2 _dragCurrentScreen;
        private Vector2 _hybridDragProbeStartScreen;

        private bool _enabledLeftClickAction;
        private bool _enabledPointerPositionAction;
        private bool _enabledAdditiveAction;
        private bool _enabledRtsModifierAction;
        private bool _legacyMouseOverrideApplied;

        private void OnEnable()
        {
            ResolveReferences();
            EnableOptionalActions();
            SubscribeSelectionEvents();
            ApplyLegacyMouseOverride(false, true);
            SyncSelectionVisuals();
        }

        private void OnDisable()
        {
            UnsubscribeSelectionEvents();
            DisableOptionalActions();
            ApplyLegacyMouseOverride(false, true);
            ResetDragState();

            if (selectionBoxUi != null) selectionBoxUi.Hide();
        }

        private void Update()
        {
            if (squadManager == null) return;

            TryHandleSelectAllShortcut();

            if (!TryReadPointerPosition(out var pointerScreenPosition)) return;
            if (!TryReadLeftClickState(out var wasPressed, out var wasReleased, out var isHeld)) return;

            UpdateHybridDragProbe(pointerScreenPosition, wasPressed, wasReleased, isHeld);

            var capturesRtsMouse = ShouldCaptureRtsMouseInput(pointerScreenPosition);
            ApplyLegacyMouseOverride(capturesRtsMouse);
            if (!capturesRtsMouse)
            {
                if (_dragCandidateActive || _isDragging)
                {
                    ResetDragState();
                    selectionBoxUi?.Hide();
                }

                return;
            }

            if (wasPressed && !_dragCandidateActive) BeginSelectionCandidate(pointerScreenPosition);

            if (!_dragCandidateActive) return;

            UpdateDragCandidate(pointerScreenPosition, isHeld);

            if (wasReleased) CompleteSelection(pointerScreenPosition);
        }

        private void TryHandleSelectAllShortcut()
        {
            if (squadManager == null || Keyboard.current == null) return;

            var ctrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;

            if (ctrlPressed && Keyboard.current.aKey.wasPressedThisFrame)
                squadManager.SelectAllMembers();
        }

        private void ResolveReferences()
        {
            if (squadManager == null)
                squadManager = SquadManager.Instance != null
                    ? SquadManager.Instance
                    : FindFirstObjectByType<SquadManager>();

            if (worldCamera == null) worldCamera = Camera.main;
            if (selectionBoxUi == null) selectionBoxUi = FindFirstObjectByType<SelectionBoxUI>();
            if (controlModeCoordinator == null)
                controlModeCoordinator = PlayerControlModeCoordinator.Instance != null
                    ? PlayerControlModeCoordinator.Instance
                    : FindFirstObjectByType<PlayerControlModeCoordinator>();
        }

        private void SubscribeSelectionEvents()
        {
            if (squadManager == null) return;
            squadManager.SelectionChanged += HandleExternalSelectionChanged;
        }

        private void UnsubscribeSelectionEvents()
        {
            if (squadManager == null) return;
            squadManager.SelectionChanged -= HandleExternalSelectionChanged;
        }

        private void HandleExternalSelectionChanged(IReadOnlyList<SquadMember> selectedMembers)
        {
            _ = selectedMembers;
            SyncSelectionVisuals();
        }

        private void ResetDragState()
        {
            _dragCandidateActive = false;
            _isDragging = false;
            _hybridDragProbeActive = false;
            _dragStartScreen = default;
            _dragCurrentScreen = default;
            _hybridDragProbeStartScreen = default;
        }
    }
}

