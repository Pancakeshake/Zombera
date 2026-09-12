# Project Overview
- Game Title: Zombera
- Startup Flow: Boot -> MainMenu -> Loading -> World

# Root Cause Analysis
The startup flow is being bypassed due to two issues:
1. **Premature Fallback Loading**: `LoadingSceneController` triggers a fallback world load when the game starts if the `Loading` scene is present in the editor hierarchy. This happens because its `sceneLoaded` hook runs before `GameManager.Awake()`, leading it to believe no `GameManager` exists.
2. **Disabled Initialization**: The `GameManager` has `initializeOnStart` set to `false`, which prevents it from automatically transitioning from the `Boot` scene to the `MainMenu` scene.

# Implementation Steps

## 1. Robust Fallback Detection
- Update `LoadingSceneController.cs` to perform a more thorough check for the `GameManager` before triggering a fallback load. 
- Specifically, check for the existence of the `GameManager` component in any loaded scene if the static `Instance` is not yet set.

## 2. Restore GameManager Initialization
- Provide a utility script (or manual instruction) to set `initializeOnStart` to `true` on the `GameManager` prefab and scene instance.
- This ensures that when the game starts from the `Boot` scene, it correctly initializes systems and loads the Main Menu.

# Key Asset & Context
- `LoadingSceneController.cs`: The script responsible for the fallback load.
- `GameManager.cs`: The central coordinator.
- `[GameManager]` Prefab: `Assets/Prefabs/GameManager/[GameManager].prefab`.

# Implementation Details

## Step 1: Update `LoadingSceneController.cs`
Modify `HandleSceneLoaded` to:
```csharp
if (GameManager.Instance != null || _fallbackWorldLoadStarted) return;

// Add a check for the component in case Awake hasn't run yet
if (UnityEngine.Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include) != null) return;
```

## Step 2: Fix `GameManager` Settings
I will use a `RunCommand` to update the serialized property `initializeOnStart` to `true` on the prefab and in the `Boot` scene.

# Verification & Testing
- Open only the `Boot` scene in the editor.
- Press Play.
- Verify the game initializes and loads the Main Menu.
- (Test Case 2) Open `Boot` and `Loading` scenes in the editor.
- Press Play.
- Verify the game still goes to the Main Menu (the fallback should NOT trigger).
