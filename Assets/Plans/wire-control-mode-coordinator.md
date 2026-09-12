# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with RTS-style squad control and third-person combat.
- Players: Single player
- Target Platform: PC (Windows)
- Render Pipeline: URP

# Game Mechanics
## Core Gameplay Loop
The player manages a squad in a zombie-infested world. Control transitions between single-unit control and RTS-style squad selection/commands.

## Controls and Input Methods
Uses the New Input System. The control mode is dynamically switched based on context (UI hover, selection count, hotkeys).

# UI
- Selection Box: Drawn when dragging the mouse for multi-selection.
- HUD: Displays unit status and control mode feedback (via logging for now).

# Key Asset & Context
- **PlayerControlModeCoordinator.cs**: Central hub for managing and logging the active control mode.
- **SelectionManager.cs**: Evaluates and sets the control mode based on selection state and modifiers.
- **PlayerInputController.cs**: Sets the control mode based on building state or UI interaction.
- **CommandManager.cs**: Listens to or references the control mode for order validation.

# Implementation Steps
1. **Initialize Coordinator in Scene**
   - Add the `PlayerControlModeCoordinator` component to the `[[SYSTEMS]/RTS Control Stack]` GameObject in the `World` scene.
   - Enable `logModeTransitions` on the component for diagnostic feedback.

2. **Wire SelectionManager**
   - Assign the `PlayerControlModeCoordinator` instance to the `controlModeCoordinator` field on the `SelectionManager` component (on `[RTS Control Stack]`).

3. **Wire CommandManager**
   - Assign the `PlayerControlModeCoordinator` instance to the `controlModeCoordinator` field on the `CommandManager` component (on `[RTS Control Stack]`).

4. **Wire PlayerInputController**
   - Assign the `PlayerControlModeCoordinator` instance to the `controlModeCoordinator` field on the `PlayerInputController` component (on `[Player_Preview]`).

5. **Validation of Wiring**
   - Run the `Tools/Zombera/RTS/Validate Control Wiring` menu item (or the logic within `RtsControlWiringValidator.cs`) to ensure no references remain null.

# Verification & Testing
1. **Startup Check**: Play the game from the `Boot` scene.
2. **Logging Verification**:
   - Hover over UI: Observe log `[PlayerControlMode] SingleUnit -> UiBlocked (source: EvaluateControlMode)`.
   - Select multiple units (if available): Observe log `[PlayerControlMode] SingleUnit -> SquadRts (source: ShouldCaptureRtsMouseInput)`.
   - Enter Build Mode: Observe log `[PlayerControlMode] ... -> BuildMode (source: EvaluateControlMode)`.
3. **Redundancy Check**: Ensure no logs are emitted if the mode is set to the same value (already handled by `if (currentMode == mode) return;`).
