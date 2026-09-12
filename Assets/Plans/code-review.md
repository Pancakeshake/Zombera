# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with third-person combat, base building, and inventory management.
- Players: Single player (based on current controllers).
- Target Platform: PC (StandaloneWindows64).
- Render Pipeline: URP.

# Review of Scripts and Identified Issues

## 1. Critical Operational Issues
### MapMagic (ThirdParty) Errors
- **Issue**: 50+ continuous `NullReferenceException` logs from `Assets/ThirdParty/MapMagic/Terrains/Editor/FrameDraw.cs`.
- **Impact**: This likely breaks the terrain editing tools or prevents the world from rendering/generating correctly in the editor.
- **Recommendation**: Verify MapMagic configuration or check for missing terrain data in the current scene.

## 2. Architectural Concerns
### "God Classes" (High Complexity)
- **Identified Files**:
  - `EasyBuildRadialMenuInputBridge.cs` (2671 lines)
  - `GameManager.cs` (2669 lines)
  - `CharacterCreatorCustomizationController.cs` (2566 lines)
  - `PlayerInputController.cs` (2467 lines)
  - `PlayerSpawner.cs` (2342 lines)
- **Impact**: Violates Single Responsibility Principle. These files are extremely difficult to maintain, debug, and unit test.
- **Recommendation**: Decompose these into smaller, specialized components (e.g., move input handling logic out of `GameManager`, split `PlayerInputController` into specific action maps).

## 3. Performance Anti-Patterns
### Expensive Calls in Update
- **Issue**: Frequent use of `GetComponent`, `GetComponentsInChildren`, and `GameObject.Find` within `Update` or lifecycle methods that run often.
- **Examples**:
  - `CombatEncounterManager.cs`: `GetComponent<UnitStats>` and `GetComponent<UnitInventory>` called during combat resolution.
  - `PlayerInputController.cs`: `HasNearbyLiveEnemies()` called every frame.
- **Impact**: Frame rate drops and CPU spikes, especially as the number of active units increases.
- **Recommendation**: Cache component references in `Awake` or `Start`. Use event-based logic instead of polling status every frame.

### Reflection & Dynamic Invocation
- **Issue**: Usage of `method.Invoke` in `BuildPlacementController.cs`, `GameManager.cs`, and `EasyBuildRadialMenuInputBridge.cs`.
- **Impact**: Reflection is significantly slower than direct calls and lacks compile-time safety.
- **Recommendation**: Use interfaces, delegates, or the `CoreEventBus` for decoupled communication instead of reflection.

## 4. Fragility & Maintenance
### Hierarchy Dependencies
- **Issue**: Many scripts rely on `GameObject.Find("RuntimeWorldSystems")` or similar hardcoded strings to find managers.
- **Impact**: Renaming objects in the hierarchy will cause silent failures or exceptions.
- **Recommendation**: Use a Singleton pattern for managers or inject dependencies through `GameManager` or a Service Locator.

### Hardcoded Tags and Layers
- **Issue**: `MinimapController.cs` uses `GameObject.FindWithTag`.
- **Impact**: If the tag is not defined in the Project Settings, the game will crash or fail to function.
- **Recommendation**: Use a SerializedField for the tag or find the camera through a dedicated reference.

### Legacy UI (OnGUI)
- **Issue**: Usage of `OnGUI()` in `AnimationTestControllerUI.cs`, `DayNightController.cs`, and `PlayerInputController.cs`.
- **Impact**: `OnGUI` is inefficient and creates garbage every frame.
- **Recommendation**: Transition these debug or runtime UIs to UGUI or UI Toolkit.

## 5. Summary Table

| Category | Issue Count (Est.) | Severity | Priority |
| :--- | :--- | :--- | :--- |
| Console Errors | 50+ | High | High |
| God Classes | 5+ | Medium | Medium |
| Update Performance | 20+ | Medium | Medium |
| Fragile Wiring | 15+ | Medium | Medium |
| Log Spam | 400+ | Low | Low |

# Implementation Steps (Recommendations)
1. **Fix MapMagic Errors**: Resolve the terrain editor exceptions.
2. **Cache References**: Perform a pass on all scripts to move `GetComponent` calls from `Update` to `Awake`/`Start`.
3. **Refactor GameManager**: Extract specific manager logic (e.g., Save/Load, Scene Transition) into dedicated classes.
4. **Replace Reflection**: Convert `method.Invoke` patterns to interface-based callbacks or events.
5. **Hierarchy Cleanup**: Replace `GameObject.Find` with explicit references or a Service Locator pattern.

# Verification & Testing
- **Editor Console**: Verify 0 errors/warnings on scene start.
- **Profiling**: Use Unity Profiler to ensure `Update` times for `PlayerInputController` and `UnitController` are below 0.5ms.
- **Hierarchy Test**: Rename "RuntimeWorldSystems" and verify that systems still initialize correctly (proving dependency injection works).
