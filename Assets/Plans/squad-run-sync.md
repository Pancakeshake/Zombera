# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with hybrid Direct/RTS controls.
- Players: Single player managing a squad.
- Target Platform: PC.
- Screen Orientation: Landscape.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
The player manages a group of survivors. They can control the "Player" character directly or select multiple units to issue group commands (RTS-style).
## Controls and Input Methods
- **Left-Click**: Select units (single/drag). In hybrid mode, should also Move selection to ground.
- **Right-Click**: Command selection to Move/Attack (handled by `CommandManager`).
- **Shift**: Toggle Sprint for the selection.
- **C/Z**: Toggle Posture (Crouch/Crawl) for the selection.

# UI
Standard RTS selection box and squad unit cards.

# Key Asset & Context
- `SelectionManager.cs`: Handles RTS selection and interprets ground clicks.
- `PlayerInputController.PostureSprint.cs`: Handles sprint/posture logic.
- `SquadManager.cs`: Central registry for squad members and selection.
- `UnitController.cs`: Low-level movement execution.

# Implementation Steps

## 1. Fix Movement for Multiple Selection (Left-Click)
Currently, when multiple units are selected, `SelectionManager` overrides `PlayerInputController`. However, `SelectionManager` only handles selection and skips issuing move orders on Left-Click.
- **Modify `Assets/01_Game/01_Core/RTS/SelectionManager.cs`**:
  - In `ApplySingleClickSelection`, detect when the pointer is on ground and a selection exists.
  - Call `squadManager.TryIssueOrderToSelection(SquadCommandType.Move, groundPoint)`.
  - This restores parity with single-unit "Click-to-Move" behavior for the entire group.

## 2. Sync Sprint State across Selection
- **Modify `Assets/01_Game/01_Core/Input/PlayerInputController.PostureSprint.cs`**:
  - Update `HandleSprintInput` to iterate through `squadManager.SelectedMembers`.
  - For each member, determine if they should sprint (based on global toggle, movement intent, and stamina) and call `member.UnitController.SetSprintActive()`.
  - Update `DisableSprint` to loop through the selection and call `SetSprintActive(false)`.

## 3. Sync Posture State across Selection
- **Modify `Assets/01_Game/01_Core/Input/PlayerInputController.PostureSprint.cs`**:
  - Update `ApplyPosture` to iterate through `squadManager.SelectedMembers`.
  - For each member:
    - Update `member.UnitStats.SetPostureState(state)`.
    - Update `member.UnitController.SetPostureSpeedMultiplier(speedMult)`.
    - Trigger the posture animation on the member's animator.

## 4. Ensure Robust Player Control Fallback
- Ensure that if `squadManager.SelectedMembers` is empty, the logic still applies to the local `unitController` (the player character) as a fallback, maintaining standard single-character controls.

# Verification & Testing
1. **Group Movement Test**: Select 2+ survivors. Left-click on the ground. Verify they all move to the clicked location in formation.
2. **Group Sprint Test**: With 2+ survivors selected and moving, press Shift. Verify all units transition to a run. Verify they stop running individually if they run out of stamina.
3. **Group Posture Test**: Select a squad. Press 'C'. Verify all units crouch and move at a reduced speed.
4. **Mixed Selection Test**: Select only the player. Verify Shift/C still work. Select only an NPC. Verify Shift/C work for them.
