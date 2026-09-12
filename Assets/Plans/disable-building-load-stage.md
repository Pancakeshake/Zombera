# Project Overview
- **Game Title**: Zombera
- **Objective**: Disable procedural building spawning and remove the "Buildings" stage from the loading screen to streamline the main game flow.

# Implementation Steps

## 1. Skip Buildings Loading Stage in GameManager
- **File**: `Assets/01_Game/01_Core/Managers/GameManager.WorldSession.cs`
- **Action**: Remove or comment out the `yield return WaitForWorldDependencyLoadingStage` block for `WorldLoadingStage.Buildings`. This prevents the loading screen from waiting for or displaying the building spawning status.
- **Assigned role**: developer

## 2. Adjust Loading Progress Values
- **File**: `Assets/01_Game/01_Core/Managers/GameManager.Helpers.cs`
- **Action**: Update `GetLoadingStageProgressEnd` for `Roads` and `GetLoadingStageProgressStart` for `Players` to close the gap left by removing the `Buildings` stage.
    - Set `WorldLoadingStage.Roads` End to `0.940f`.
    - Set `WorldLoadingStage.Players` Start to `0.940f`.
- **Assigned role**: developer

# Verification & Testing
1. **Loading Sequence**: Start the game from the `Boot` scene. Observe the loading screen.
2. **UI Check**: Verify that "Spawning buildings..." no longer appears in the loading status text.
3. **Progress Bar**: Ensure the progress bar moves smoothly from "Roads" directly to "Players" without jumping or stalling at 89%.
4. **Console Logs**: Verify that `[GameManager] Loading stage 'Buildings' started/completed` logs no longer appear (or verify they are bypassed).
