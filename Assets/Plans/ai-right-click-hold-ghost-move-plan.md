# AI Plan: Hold Right Click for Ghost Move Preview, Release to Commit

## 1) Goal
Implement RTS move input so that:
1. Holding right click shows ghost destination positions for selected units.
2. Releasing right click commits the move order to the currently previewed ghost positions.
3. No movement is issued on initial right-click press.

## 2) Current State (Scan Summary)
- `CommandManager` currently issues commands on `WasPressedThisFrame`.
- `RtsPointerQueryUtility` already resolves grounded movement points via `MovementDestinationResolver`.
- `SelectionManager` already owns hybrid RTS mouse capture and can issue left-click move in hybrid mode.
- Group slot/deconfliction logic already exists in `SquadMoveDeconfliction` and `CommandSystem`.
- A temporary marker system (`SquadCommandVisualizer`) exists, but it is event-driven and short-lived, not a continuous hold-preview system.

## 3) UX Spec (Production Behavior)

### Input behavior
1. Right click pressed:
   - Enter preview mode if RTS mouse input is active and selection exists.
   - Do not issue move/attack yet.
2. Right click held:
   - Continuously update grounded target under cursor.
   - Continuously recompute per-unit ghost slot positions.
3. Right click released:
   - If preview has a valid destination, issue move command once.
   - If release is over invalid ground, cancel preview and issue nothing.
4. Cancel cases:
   - Pointer over UI while held or on release: cancel.
   - Selection cleared while held: cancel.
   - Mode change out of RTS while held: cancel.

### Attack behavior
Use a thresholded split:
1. Quick tap on hostile target: preserve attack-on-right-click.
2. Hold for move preview duration: force move-preview mode (no attack).

Recommended default:
- `holdToPreviewThresholdSeconds = 0.10f`

## 4) Architecture

### 4.1 Add a dedicated preview controller
Create a new runtime component:
- `Assets/01_Game/01_Core/RTS/RightClickMoveGhostPreviewController.cs`

Responsibilities:
1. Manage right-click hold state machine.
2. Resolve target point each frame using `RtsPointerQueryUtility`.
3. Build preview slots for selected members.
4. Render/update pooled ghost objects.
5. Emit commit callback on release.

This keeps `CommandManager` focused on command routing and prevents input/render/state coupling in one class.

### 4.2 CommandManager integration
Update `CommandManager`:
1. Replace immediate `WasPressedThisFrame` dispatch path with hold-state path.
2. Add callbacks from preview controller:
   - `OnPreviewCommitMove(Vector3 destination)`
   - `OnPreviewCancel()`
3. Keep attack command path for quick tap hostile target behavior.

### 4.3 Slot computation reuse
Avoid copy-pasting slot logic.

Preferred extraction:
1. Add a non-issuing API in `SquadMoveDeconfliction`, e.g.:
   - `ResolveGroupMoveSlots(...) -> IReadOnlyList<Vector3>`
2. Use the same settings as runtime move dispatch to guarantee preview == commit result.

If extraction is too invasive for first pass, mirror only the minimal deterministic subset and schedule refactor immediately after.

## 5) Ghost Visualization Plan

### 5.1 Rendering approach
Phase 1 (fast, reliable):
1. Use pooled primitive capsules/cylinders per selected unit.
2. Tint color:
   - green: valid slot
   - red: invalid destination (global invalid)
3. Add simple facing arrow/line toward group forward.

Phase 2 (optional visual polish):
1. Replace primitive ghosts with lightweight unit silhouette meshes.
2. Keep pooled instances and shared material.

### 5.2 Performance constraints
1. Pool once, never instantiate/destroy every frame.
2. Cap max visualized ghosts (for example 24).
3. Update at a fixed preview tick (for example 20-30 Hz) if needed.

## 6) State Machine

States:
1. `Idle`
2. `PressedPending` (pressed, deciding tap vs hold)
3. `PreviewActive`
4. `CommitPending` (release this frame)
5. `Cancelled`

Transitions:
1. `Idle -> PressedPending` on right press with valid RTS context.
2. `PressedPending -> PreviewActive` after hold threshold OR immediate if configured.
3. `PressedPending -> CommitPending` on early release (tap behavior).
4. `PreviewActive -> CommitPending` on release with valid destination.
5. `PreviewActive -> Cancelled` on invalid context/mode/UI.
6. `CommitPending -> Idle` after one command dispatch.
7. `Cancelled -> Idle` after cleanup.

## 7) File-Level Change Plan

### New files
1. `Assets/01_Game/01_Core/RTS/RightClickMoveGhostPreviewController.cs`
2. `Assets/01_Game/01_Core/RTS/RightClickMoveGhostPool.cs` (optional helper)

### Existing files to modify
1. `Assets/01_Game/01_Core/RTS/CommandManager.cs`
   - switch to right-click pressed/released/held handling
   - integrate preview controller
   - maintain quick-tap attack path
2. `Assets/01_Game/01_Core/Interaction/SquadMoveDeconfliction.cs`
   - expose non-issuing slot resolution API
3. `Assets/01_Game/01_Core/Interaction/CommandSystem.cs`
   - optionally expose deconfliction settings accessor for preview parity
4. `Assets/01_Game/01_Core/RTS/SelectionManager.cs`
   - no core logic change expected; only coordination if capture conflicts appear

## 8) Edge Cases
1. Selection changes during hold: rebuild ghost pool and slots safely.
2. Unit dies/unavailable during hold: exclude from preview; commit only for valid orderable members.
3. Cursor leaves valid terrain: keep ghosts hidden/red and suppress commit.
4. Right-click begins over hostile target then drags to ground: follow hold-threshold rule (prefer preview mode after threshold).
5. Build mode or UI modal appears mid-hold: cancel preview immediately.

## 9) Validation Checklist

### Functional
1. Hold right click on terrain shows ghost positions for all selected units.
2. Releasing right click issues exactly one move command to previewed positions.
3. No movement issued on initial press.
4. Quick tap hostile target still issues attack (if configured).
5. Quick tap ground can either issue immediate move or require minimal hold, based on chosen policy.

### Stability
1. No ghost leaks (pool returns to hidden state on cancel/disable/scene unload).
2. No null exceptions when selection changes during hold.
3. No FPS spikes when holding with large selections.

### Grounding parity
1. Previewed slots match final committed slot positions.
2. Slots stay terrain-grounded on uneven terrain.

## 10) Rollout Strategy
1. Ship behind feature flag in `CommandManager`:
   - `enableRightClickHoldGhostMove = true`
2. Keep legacy press-to-move path as fallback for one sprint.
3. Add debug toggle to display slot IDs and validity for QA.
4. Remove legacy path after QA signoff.

## 11) Recommendation
Best production option is to keep existing NavMesh + grounding pipeline, and add a dedicated hold-preview controller that computes the same deconflicted slots used by final move commit. This gives predictable behavior, minimal regression risk, and clean separation of concerns.