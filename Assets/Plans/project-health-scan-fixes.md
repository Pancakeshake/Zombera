# Project Optimization and Bug Fix Plan

## Project Overview
- **Game Title:** Zombera
- **High-Level Concept:** Survival RPG with zombies, building, and inventory management.
- **Render Pipeline:** URP
- **Core Systems:** UMA Character System, New Input System, NavMesh AI.

## Scanning Results: Issues & Improvements

### 1. UMA Character System Failure (Critical)
**Issue:** The project console reports several errors: `Error loading recipe: HumanMale Base Recipe Object reference not set to an instance of an object`. Investigation reveals that the `UMAGlobalLibrary` is missing from the project's AssetDatabase, which prevents UMA from resolving slot and overlay names.
**Impact:** Characters (Player and Zombies) fail to render or render incorrectly (invisible/broken meshes).

### 2. Cross-Scene Reference Warnings
**Issue:** `Player_Preview` in the `MainMenu` scene has a direct reference to `UMA_GLIB` in the `Boot` scene.
**Impact:** This triggers Unity warnings and causes errors if `Boot` is not loaded, or if the reference is lost during scene transitions. It violates the decoupling of scenes.

### 3. Rendering Limitations
**Issue:** Shadow distance is currently set to 40 meters.
**Impact:** Significant shadow popping in open-world environments, reducing immersion.

### 4. Legacy System Maintenance
**Issue:** Scripts like `SpawnAppearanceStylingService` and `GameManager.Appearance` are managing "Legacy" preview roots (`UMA_GLIB`) which are causing the aforementioned cross-scene issues.
**Impact:** Cluttered codebase and scene hierarchy.

---

## Implementation Steps

### Phase 1: Fixing UMA Initialization
1. **Regenerate UMA Global Library**:
   - Create a new `UMAGlobalLibrary` asset if it's truly missing.
   - Run the UMA "Library Indexing" to ensure `HumanMale Base Recipe` and its dependencies (slots, overlays) are registered.
2. **Verify Recipe Loading**:
   - Update `AppearanceProfileService.cs` to handle cases where the library might be temporarily unavailable during initialization.

### Phase 2: Decoupling Scene References
1. **Automate UMA Library Binding**:
   - Modify `UmaGlobalLibraryService` to find the `UMA_GLIB` or equivalent library by name or via a singleton rather than a direct scene reference.
   - Clear the hard-coded reference in `MainMenu.unity`'s `Player_Preview` object.
2. **Sanitize Scene Transitions**:
   - Update `GameManager.Appearance.cs` to ensure that when additive scenes load, they dynamically register their preview requirements.

### Phase 3: Visual & Performance Tuning
1. **Shadow Distance Update**:
   - Increase `shadowDistance` in the `PC` Quality Setting from 40 to 120.
   - Adjust Shadow Cascades to ensure resolution remains high near the player.
2. **Boot Optimization**:
   - Run the existing `SceneBootRebuildTool` on `Boot` and `MainMenu` to apply conservative fixes (disabling `playOnAwake` where unnecessary).

### Phase 4: Data Validation
1. **Weapon Recipe Audit**:
   - Check `Katana`, `SledgeHammer`, and other weapon assets to ensure their `appearanceWardrobeRecipe` fields are correctly populated if they are intended to modify the character's appearance.

---

## Key Assets & Context
- `Assets/UMA/Content/Core/HumanMale/RaceData/Human Male.asset` (Source of truth for the race)
- `Assets/01_Game/01_Core/Managers/GameManager.Appearance.cs` (Handles scene cleanup)
- `Assets/01_Game/03_Characters/Scripts/SpawnAppearanceStylingService.cs` (Handles character spawning logic)

## Verification & Testing
1. **Character Rendering Test**: Start the game from the `Boot` scene. Verify that the player character in the `MainMenu` (Character Creator) renders correctly without UMA errors.
2. **Scene Transition Test**: Navigate from `MainMenu` to a gameplay world and back. Ensure no "Missing Reference" or "Cross-Scene Reference" warnings appear.
3. **Visual Quality Check**: In a daylight scene, check for shadow popping. Shadows should now extend significantly further into the distance.
4. **Console Cleanliness**: Ensure the console is free of `NullReferenceException` during the first 60 seconds of gameplay.
