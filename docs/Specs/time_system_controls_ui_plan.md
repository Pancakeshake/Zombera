# TIME SYSTEM CONTROLS + UI IMPLEMENTATION PLAN

System Name:
Time System Controls and HUD Integration

System Type:
Core System + Input + UI

---

# GOAL

Implement a production-ready time control flow for the prototype loop with:

- Pause
- Play (resume)
- 1x speed
- 2x speed
- 4x speed

The system must be easy to test, avoid current keybind conflicts, and use one authoritative source of truth for simulation speed.

---

# CURRENT PROJECT FINDINGS (SCAN SUMMARY)

1. Core time scaffolding already exists in `Assets/Core/TimeSystem.cs` with:
   - `SetTimeScale(float scale)`
   - `PauseGame()`
   - `ResumeGame()`
   - `TogglePause()`
2. `GameManager` already initializes and shuts down `TimeSystem`.
3. No gameplay input currently drives `TimeSystem`.
4. Debug slow-motion currently writes to `Time.timeScale` directly in `Assets/Debug/DebugManager.cs`, which can conflict with `TimeSystem`.
5. Existing keybind usage:
   - Squad commands: number keys 1-5 (`PlayerInputController`)
   - Combat: F and R
   - Menu/UI: Tab, Escape, F1-F8
6. HUD has two active patterns:
   - Panelized HUD flow via `HUDManager`
   - Runtime-built HUD flow via `WorldHUD`

Conclusion:
The project is ready for a proper time-controls implementation, but needs unified authority and conflict-free input/UI integration.

---

# SCOPE

In scope (V1):

- Pause/play control
- Speed presets: 1x, 2x, 4x
- Keyboard controls
- HUD controls with active-state feedback
- Event hooks for other systems and UI refresh

Out of scope (V1):

- Full day/night visual cycle
- Save/load of full time-of-day calendar state
- Per-system independent simulation channels

Optional V2 extension:

- Add a game clock (minutes/hours/day) and day/night integration using the same speed presets.

---

# TARGET DESIGN

## 1) Time authority model

`TimeSystem` is the only class allowed to write:

- `Time.timeScale`
- `Time.fixedDeltaTime`

Core fields to add:

- `float baseFixedDeltaTime = 0.02f`
- `float[] allowedSpeedPresets = { 1f, 2f, 4f }`
- `float lastNonZeroScale = 1f`
- `int currentPresetIndex`
- `bool isPaused`

Rules:

1. Pause sets active scale to 0 without losing `lastNonZeroScale`.
2. Resume restores `lastNonZeroScale` (default 1x).
3. Preset changes while paused only update `lastNonZeroScale`; active scale remains 0 until resume.
4. Every applied scale also sets `Time.fixedDeltaTime = baseFixedDeltaTime * scale` (or 0 when paused).

## 2) Public API surface

Recommended API additions in `TimeSystem`:

- `SetSpeedPreset(int presetIndex)`
- `SetSpeedScale(float scale)` (validated against allowed presets for now)
- `IncreaseSpeedStep()`
- `DecreaseSpeedStep()`
- `SetPaused(bool paused)`
- `GetCurrentPresetLabel()` (returns `"Pause"`, `"1x"`, `"2x"`, `"4x"`)

## 3) Event contract

Add strongly-typed events in `Assets/Core/GameEvents.cs`:

- `TimeScaleChangedEvent`
  - `float TimeScale`
  - `bool IsPaused`
  - `int PresetIndex`
  - `string PresetLabel`
- `PauseStateChangedEvent`
  - `bool IsPaused`

Publish from `TimeSystem` through `EventSystem.PublishGlobal(...)` after every state change.

---

# INPUT PLAN

## Proposed default controls (conflict-safe)

- `P`: Toggle pause/play
- `[`: Step speed down (4x -> 2x -> 1x)
- `]`: Step speed up (1x -> 2x -> 4x)
- `\\`: Reset to 1x

Rationale:

- Avoids conflicts with existing 1-5 squad commands.
- Avoids F-key debug mappings and Tab/Escape menu behavior.
- Matches strategy/management game style for speed stepping.

## Input implementation

Create `Assets/Systems/TimeInputController.cs`:

- Follows existing input style (`Keyboard.current` plus legacy fallback guards)
- Depends on `GameManager`/`TimeSystem` reference
- Can be disabled when menus block gameplay controls (future gating)

---

# UI PLAN

## Visual controls

Add a compact time control strip with:

- Pause button
- Play button
- 1x button
- 2x button
- 4x button
- Current speed label

Behavior:

1. Active preset is highlighted.
2. Pause state visually overrides preset highlight.
3. Buttons trigger `TimeSystem` API only (never write `Time.timeScale` directly).
4. UI updates from time events, not polling.

## Integration strategy

Phase A (low risk):

- Create `Assets/UI/HUD/TimeControlsPanelController.cs` as an independent HUD module.
- Instantiate/wire through `HUDManager` (panelized flow).

Phase B (runtime-built HUD support):

- Add a matching time cluster in `WorldHUD.BuildHUD()` and wire to the same controller logic or shared binder methods.

This keeps both HUD pipelines consistent while avoiding a large immediate refactor.

---

# DEBUG SYSTEM ALIGNMENT

Current risk:

- `DebugManager.ToggleSlowMotion()` sets `Time.timeScale` directly and can desync UI/time state.

Plan:

1. Route debug slow motion through `TimeSystem` instead of direct `Time.*` writes.
2. Add a debug-only temporary preset if needed (example: 0.2x) without breaking normal gameplay presets.
3. Keep debug key (`F2`) behavior the same from user perspective.

---

# PHASED IMPLEMENTATION PLAN

## Phase 1: Core stabilization

Tasks:

- Extend `TimeSystem` with preset model and pause-resume restore behavior.
- Add events in `GameEvents` and publish from `TimeSystem`.
- Ensure `fixedDeltaTime` is always synced.

Acceptance criteria:

- Pause -> Resume restores previous preset.
- 1x/2x/4x correctly apply to simulation.
- No direct `Time.timeScale` writes remain outside `TimeSystem` (except approved temporary debug exceptions during transition).

## Phase 2: Input controls

Tasks:

- Add `TimeInputController` and wire default keys.
- Add serialization for key overrides in inspector.
- Respect Input System + legacy fallback pattern.

Acceptance criteria:

- `P`, `[`, `]`, `\\` work in World scene.
- No conflict with squad, combat, or debug keybinds.

## Phase 3: HUD controls

Tasks:

- Add time control panel prefab/runtime panel.
- Bind panel actions to `TimeSystem` API.
- Subscribe to time events and reflect state.

Acceptance criteria:

- Clicking pause/play/1x/2x/4x updates simulation and UI state instantly.
- UI remains accurate when time changes from keyboard or debug inputs.

## Phase 4: Debug integration cleanup

Tasks:

- Update debug slow motion path to call `TimeSystem`.
- Verify F2 still toggles expected behavior.

Acceptance criteria:

- Debug slow motion no longer desyncs HUD indicators.
- Returning from debug mode preserves normal time preset flow.

## Phase 5: Validation + playtest pass

Tasks:

- Run boot -> main menu -> world smoke test.
- Validate combat, movement, and world simulation at each speed.
- Validate pause behavior during combat and digging.

Acceptance criteria:

- No stuck pause state.
- No soft lock from speed changes.
- World simulation ticks remain stable at 1x/2x/4x.

---

# TEST CHECKLIST

Functional:

1. Enter World scene at 1x.
2. Press `]` twice -> confirm 2x then 4x.
3. Press `[` twice -> confirm back to 2x then 1x.
4. Press `P` -> pause (0x).
5. Press `P` again -> resume previous speed.
6. Click HUD buttons for each state and verify same behavior.

Integration:

1. During pause, ensure movement/combat input does not progress gameplay.
2. Ensure UI animations requiring unscaled time still animate as intended.
3. Validate debug F2 path does not break time HUD state.

Regression:

1. Squad hotkeys 1-5 still issue commands.
2. Combat F/R still function.
3. Debug F1-F8 behavior remains unchanged.

---

# RISK NOTES

1. `WorldManager` and other systems use `Time.deltaTime`; high multipliers can amplify simulation bursts. Keep V1 capped at 4x.
2. UI update loops that rely on scaled time can freeze on pause; use unscaled time where UI should keep animating.
3. Future save/load should decide whether speed preset persists per profile or resets to 1x.

---

# RECOMMENDED IMPLEMENTATION ORDER

1. Core `TimeSystem` preset + events
2. Input controller
3. HUD time controls
4. Debug slow-motion reroute
5. Playtest checklist pass

This order gives fast usable controls early while minimizing cross-system regression risk.