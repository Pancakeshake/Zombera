# Project Overview
- Game Title: Zombera
- Goal: Create a dedicated bootstrap script to satisfy gameplay logic and allow the World HUD to spawn.

# Implementation Steps

## 1. Create Test Gameplay Bootstrap Script
Create a script that ensures the `GameManager` is fully initialized in a "Playing" state and that the `WorldHUD` is spawned.
- **File**: `Assets/Scripts/Testing/TestGameplayBootstrap.cs`
- **Logic**:
    - Reference the `[GameManager]` prefab and `HUD` prefab.
    - In `Awake()`:
        - Instantiate the `[GameManager]` prefab if `GameManager.Instance` is missing.
        - Call `GameManager.Instance.InitializeSystems()`.
        - Set `GameManager.Instance.SetGameState(GameState.Playing)`.
        - Instantiate the `HUD` prefab (containing `WorldHUD`) if `WorldHUD` is missing.

# Key Assets & Context
- **Prefab Paths**: 
    - `Assets/Prefabs/GameManager/[GameManager].prefab`
    - `Assets/Prefabs/UI/HUD/HUD.prefab`

# Verification & Testing
1. **Script Creation**: Verify the script compiles correctly.
2. **Manual Test**: Add the script to a GameObject in a test scene, assign the prefabs, and enter Play Mode.
3. **Behavior Check**: Verify the `GameManager` is initialized and state is `Playing`. Verify the `HUD` appears.
