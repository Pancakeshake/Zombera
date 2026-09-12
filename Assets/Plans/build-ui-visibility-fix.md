# Build UI Visibility Fix

## Project Overview
- **Game Title**: Zombera
- **Goal**: Ensure Build Controls and Build Command Strip are hidden when build mode is inactive.

## Implementation Steps

### Phase 1: Event-Driven UI Cleanup
1. **Update Transition Logic**: Ensure `HandleBuildModeVisibilityTransition` explicitly hides Build UI elements when exiting build mode.
   - **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.BuildHud.cs`
   - **Logic**: Call `ApplyBuildSearchFieldState(false)`, `ApplyBuildControlsPopupState(false)`, and `ApplyBuildCommandStripState(false)` inside `HandleBuildModeVisibilityTransition(false)`.
   - **Role**: developer
   - **Dependencies**: None

2. **Fix Tick Gating**: Remove the gating in `Update()` that prevents `TickBottomBuildModeState` from running when build mode is inactive, OR ensure the cleanup happens once.
   - **Alternative**: Keep the gating in `Lifecycle.cs` but ensure `HandleBuildModeVisibilityTransition` handles the final "off" state. This is cleaner.
   - **Role**: developer
   - **Dependencies**: Step 1

### Phase 2: Tab Interaction Fix
1. **Handle Tab Changes**: Ensure that when a tab (F1-F5) is opened while build mode is active, the Build UI elements are hidden.
   - **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.TabLayout.cs`
   - **Logic**: In `UpdateLayoutForTabState`, call `TickBottomBuildModeState()` or explicitly sync build UI visibility.
   - **Role**: developer
   - **Dependencies**: None

## Verification & Testing
- **Manual Test**: Open Build Mode (B). Verify popup and strip appear.
- **Manual Test**: Close Build Mode (Esc or B). Verify popup and strip disappear.
- **Manual Test**: Open Build Mode (B). Press F1. Verify popup and strip disappear while the Squad tab is open.
- **Manual Test**: Close Squad tab. Verify popup and strip reappear if build mode is still active.
