# AI Plan: Production-Grade Cursor System (Scalable + Clean)

## 1) Objective
Evolve the current cursor stack into a production-grade subsystem with:
- single ownership of cursor state (visible/locked/icon/hotspot)
- one canonical pointer position API for gameplay/UI raycasts
- predictable behavior across gameplay, menus, build mode, and tests
- minimal per-frame overhead and no hidden side effects
- clear extension points for future cursor types and contexts

## 2) Current System Scan Summary

## 2.1 What is already strong
- A centralized manager exists: [Assets/01_Game/01_Core/Managers/CursorManager.cs](Assets/01_Game/01_Core/Managers/CursorManager.cs)
- A unified gameplay pointer API exists: `TryGetGameplayPointerScreenPosition(...)` in [Assets/01_Game/01_Core/Managers/CursorManager.cs](Assets/01_Game/01_Core/Managers/CursorManager.cs)
- Core consumers now call that API:
  - [Assets/01_Game/01_Core/RTS/SelectionManager.cs](Assets/01_Game/01_Core/RTS/SelectionManager.cs)
  - [Assets/01_Game/01_Core/RTS/CommandManager.cs](Assets/01_Game/01_Core/RTS/CommandManager.cs)
  - [Assets/01_Game/01_Core/Input/PlayerInputController.RebindableInput.cs](Assets/01_Game/01_Core/Input/PlayerInputController.RebindableInput.cs)
  - [Assets/01_Game/07_Building/Placement/BuildGhostPreview.cs](Assets/01_Game/07_Building/Placement/BuildGhostPreview.cs)
  - [Assets/01_Game/07_Building/Placement/BuildPlacementController.cs](Assets/01_Game/07_Building/Placement/BuildPlacementController.cs)
- UI input calibration is supported via processor registration in [Assets/01_Game/08_UI/Core/RuntimeUiEventSystemUtility.cs](Assets/01_Game/08_UI/Core/RuntimeUiEventSystemUtility.cs) and [Assets/01_Game/08_UI/Core/CursorCalibrationInputProcessor.cs](Assets/01_Game/08_UI/Core/CursorCalibrationInputProcessor.cs)

## 2.2 Primary risks to fix
- Cursor lock/visibility ownership conflict:
  - `CursorManager.Update()` currently forces `Cursor.visible = true` and `Cursor.lockState = None` every frame.
  - Menu code also toggles lock/visibility in [Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs](Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs).
  - Result: state tug-of-war risk and hard-to-debug cursor behavior.
- CursorManager currently mixes multiple responsibilities:
  - icon state and hover icon switching
  - calibration persistence and live calibration input
  - pointer API and fallback input reads
  - camera reference plumbing
- Runtime `GetPixels32()` hotspot auto-detection path exists and can be expensive if accidentally enabled in play.
- Global singleton pattern (`RuntimeInstance`) plus auto-add behavior in [Assets/01_Game/01_Core/Input/PlayerInputController.Initialization.cs](Assets/01_Game/01_Core/Input/PlayerInputController.Initialization.cs) can create lifecycle ambiguity in complex scene setups.

## 3) Target Architecture

## 3.1 Cursor ownership model
Create one authority for cursor state transitions:
- `CursorService` (new runtime service)
  - owns lock mode and visibility
  - owns icon set and active icon
  - owns hotspot resolution and calibration offset
  - exposes events for diagnostics/telemetry

Rule:
- no direct `Cursor.lockState`, `Cursor.visible`, or `Cursor.SetCursor` outside the service

## 3.2 Context-driven state machine
Use explicit context priorities instead of ad-hoc calls:
- Highest: cutscene/system override
- Menu/UI modal
- Build mode
- Combat hover targeting
- Default gameplay

Represent state as:
- `CursorContext`
- `CursorPresentation`
- priority resolver

## 3.3 Pointer API contract
Keep one canonical API and make all consumers use it:
- `TryGetGameplayPointerScreenPosition(...)`

Add explicit variants:
- gameplay pointer (calibrated)
- raw hardware pointer (debug-only)
- UI pointer (when required by specific UI systems)

## 3.4 Config and data separation
Move cursor asset/configuration to ScriptableObject:
- `CursorProfile`
  - default/attack/other icons
  - default hotspots
  - calibration enable flags
  - anchor and alpha thresholds

Benefits:
- easier tuning per platform/theme
- no hardcoded texture wiring in scene instances

## 4) Refactor Plan (Phased)

### Phase 1: Stabilize ownership (must-do first)
1. Introduce `CursorService` facade and route all existing cursor calls through it.
2. Remove per-frame forced lock/visible writes from `CursorManager.Update()`.
3. Replace direct lock/visible writes in menu/gameplay with service requests:
   - [Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs](Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs)
4. Keep behavior parity by mapping current menu/gameplay states to service contexts.

Acceptance:
- no cursor tug-of-war between pause menu and gameplay
- lock/visible transitions deterministic and reversible

### Phase 2: Split responsibilities for maintainability
1. Keep lightweight `CursorManager` as compatibility shim (temporary).
2. Extract calibration persistence into `CursorCalibrationStore`.
3. Extract hotspot resolution into `CursorHotspotResolver`.
4. Extract icon selection logic into `CursorIconResolver`.
5. Keep `CursorManager` forwarding only; deprecate direct usage.

Acceptance:
- each class has one clear responsibility
- unit-testable non-MonoBehaviour logic for hotspot and calibration

### Phase 3: Input pipeline hardening
1. Standardize pointer reads:
   - migrate all pointer consumers to service API
   - ensure zero direct `Mouse.current.position` reads in gameplay path
2. Validate UI processor integration and avoid double-application of calibration.
3. Add optional runtime warning when raw pointer APIs are used in gameplay assemblies.

Acceptance:
- consistent hit-point alignment across RTS, combat, and building
- no duplicate calibration offsets

### Phase 4: Data-driven scalability
1. Add `CursorProfile` asset and bind via bootstrap.
2. Add support for extensible cursor intents:
   - attack
   - interact
   - invalid placement
   - loot
   - dialogue
3. Add theme/platform override hooks (desktop now, future console/cloud).

Acceptance:
- adding new cursor types requires data changes plus small resolver extension only

### Phase 5: Diagnostics + quality gates
1. Add lightweight debug overlay (dev-only):
   - active context
   - active icon id
   - lock/visible state
   - calibrated vs raw pointer delta
2. Add structured logs for state transitions (throttled).
3. Add regression checks in smoke tests for menu/gameplay/build transitions.

Acceptance:
- fast root-cause diagnosis for cursor mismatches
- repeatable validation path before release

## 5) Cleanup Backlog (Concrete)
- Replace direct cursor state writes outside service:
  - [Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs](Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs)
  - [Assets/01_Game/00_Framework/Testing/ClothingTestCameraController.cs](Assets/01_Game/00_Framework/Testing/ClothingTestCameraController.cs) (test-only path; isolate)
- Centralize cursor manager auto-creation policy in one bootstrap path:
  - [Assets/01_Game/01_Core/Input/PlayerInputController.Initialization.cs](Assets/01_Game/01_Core/Input/PlayerInputController.Initialization.cs)
- Ensure all gameplay pointer reads route through one API (already mostly done, keep as enforced rule).

## 6) Performance and Reliability Standards
- No per-frame texture readbacks in runtime cursor path.
- No per-frame state writes unless state actually changed.
- No repeated `FindFirstObjectByType` in hot loops.
- Input reads must be null-safe for both Input System and legacy fallback.
- Cursor transitions should be idempotent and event-driven.

## 7) Test Matrix
- Gameplay free-look + RTS selection + right-click command.
- Build mode ghost placement + UI-over-pointer suppression.
- Pause open/close while moving mouse quickly.
- Scene load transitions (menu -> world -> pause -> world).
- Attack hover cursor switching on hostile targets.
- Calibration persistence across restart.

## 8) Delivery Sequence Recommendation
1. Phase 1 and Phase 3 together in one PR (stability first).
2. Phase 2 in a follow-up refactor PR (no behavior change).
3. Phase 4 and Phase 5 as productization PR.

This keeps risk low while moving quickly to production-grade behavior.

## 9) Immediate Next Step
Implement Phase 1 skeleton first:
- introduce cursor state request API
- reroute pause menu lock/visible calls
- remove forced lock/visible writes from `CursorManager.Update()`
- verify no behavior regressions in world + pause flow