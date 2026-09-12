# Portrait Studio + Save Slot Screenshot Plan (Project-Aligned)

## Scope
Create a reliable portrait pipeline and save-slot visual previews using the systems that already exist in this repo.

## Current State (Verified)

### Portrait Studio (already implemented)
- Runtime portrait system exists in `Assets/01_Game/08_UI/Scripts/SquadManagement/PortraitStudioManager.cs`.
- Squad UI already provisions and uses Portrait Studio in:
    - `Assets/01_Game/08_UI/Scripts/SquadManagement/ZomberaSquadManagementUI.cs`
    - `Assets/01_Game/08_UI/Scripts/SquadManagement/ZomberaSquadManagementUI.Utility.cs`
    - `Assets/01_Game/08_UI/Scripts/SquadManagement/ZomberaSquadManagementUI.SelectionInventorySkills.cs`
- `PortraitStudioManager` already:
    - syncs appearance via `AppearanceProfileService`
    - waits for UMA `CharacterUpdated`
    - renders to a dedicated `RenderTexture` via `RenderOnce()`.

### Save Slot Preview UI (partially implemented)
- Save slot UI already supports preview image display and metadata:
    - `Assets/01_Game/08_UI/Scripts/Menus/SaveGameMenuController.cs`
    - `Assets/01_Game/08_UI/Scripts/Menus/SaveSlotItem.cs`
- Save metadata already has `screenshotBase64` in:
    - `Assets/01_Game/01_Core/Systems/SaveSystem.cs` (`SaveMetadata`).
- Missing piece: no gameplay screenshot capture pipeline currently writes `screenshotBase64` during save.

## Part 1: Portrait Studio Hardening Plan

### Objective
Stabilize and formalize the existing Portrait Studio flow so it remains correct across player appearance/equipment changes and save/load transitions.

### 1. Runtime configuration validation
- Validate `PortraitStudio.prefab` wiring at startup and log actionable errors if missing:
    - `studioAvatar`
    - `studioCamera`
    - `portraitRT`
    - `portraitAnchor`
- File: `Assets/01_Game/08_UI/Scripts/SquadManagement/PortraitStudioManager.cs`.

### 2. Appearance + equipment sync guarantees
- Keep appearance sync through `AppearanceProfileService` as primary path.
- Add explicit equipment refresh hook after sync if visuals lag behind UMA updates.
- Ensure sync succeeds for player and squad members from live unit context.
- Files:
    - `Assets/01_Game/08_UI/Scripts/SquadManagement/PortraitStudioManager.cs`
    - `Assets/01_Game/08_UI/Scripts/SquadManagement/ZomberaSquadManagementUI.SelectionInventorySkills.cs`.

### 3. Portrait refresh triggers (project-specific)
- Character creation finalize:
    - Trigger refresh after `CharacterCreatorController` applies selected profile.
    - Files: `Assets/01_Game/08_UI/Scripts/Menus/CharacterCreatorController*.cs`.
- Equipment changes:
    - Subscribe to `EquipmentSystem.OnEquipmentChanged` for selected unit and re-sync portrait.
    - Files: `Assets/01_Game/06_Inventory/Equipment/EquipmentSystem*.cs`, Squad UI scripts.
- Save/load completion:
    - Trigger portrait refresh after `SaveManager.LoadGame(...)` restore completes.
    - Files: `Assets/01_Game/09_SaveSystem/Core/SaveManager.cs`, Squad UI wiring.

### 4. Lifecycle and memory safety
- Reuse one runtime Portrait Studio instance (already intended via singleton + fallback instantiation).
- Prevent duplicate runtime prefab spawn in edge scene transitions.
- Add explicit cleanup for temporary portrait textures if any runtime conversion path is added.

### Portrait validation checklist
- Portrait camera renders expected model framing.
- Studio clone/avatar reflects live appearance and equipment.
- No missing materials/shader pink meshes.
- Refresh works after character creation, equipment updates, save/load.
- No repeated `PortraitStudio_RuntimeInstance` objects across scene changes.

## Part 2: Save Slot Screenshot Preview Plan

### Objective
Implement automatic screenshot capture on save, persist it, and display it in save slots with portrait/placeholder fallback.

### 1. Add save screenshot capture service
- New runtime utility (proposed):
    - `Assets/01_Game/09_SaveSystem/Core/SaveScreenshotService.cs`
- Responsibilities:
    - capture active gameplay camera (or fallback camera)
    - resize/compress to thumbnail
    - encode to PNG bytes
    - convert to Base64 (for existing `screenshotBase64` field)
    - optional: write PNG file to disk and store path if later schema migration adds `screenshotPath`.

### 2. Hook screenshot generation into save flow
- Inject capture in `SaveManager.PopulateMetadata(...)` before `saveSystem.SaveGameData(...)`.
- Write captured preview into `saveData.metadata.screenshotBase64`.
- File: `Assets/01_Game/09_SaveSystem/Core/SaveManager.cs`.

### 3. Fallback order for preview display
- In save menu rendering (`SaveGameMenuController`):
    1. Use `metadata.screenshotBase64` if valid.
    2. Fallback to portrait snapshot source (see note below).
    3. Fallback to default placeholder.
- File: `Assets/01_Game/08_UI/Scripts/Menus/SaveGameMenuController.cs`.

### 4. Portrait fallback implementation option
- If no screenshot exists, generate fallback from:
    - active portrait `RenderTexture` from `PortraitStudioManager` when available.
- This can be converted once per slot load and cached.

### 5. Performance and caching
- Add texture cache by `slotId` in `SaveGameMenuController` to avoid repeated Base64 decode churn.
- Dispose temporary textures/sprites on menu close or list refresh.

### 6. Delete/rename behavior
- Current save metadata updates already run through `SaveSystem.DeleteSave` and `SaveSystem.RenameSave`.
- If file-based thumbnails are introduced later, extend these methods to delete/rename companion image files.

## Data Model Alignment

### Existing save metadata fields (already available)
- `slotId`
- `slotName`
- `timestamp`
- `playTimeSeconds`
- `dayNumber`
- `locationName`
- `difficulty`
- `progressPercent`
- `gameVersion`
- `screenshotBase64`
- `recentActivity`

### Proposed additions (optional future)
- `screenshotPath` (disk path reference) if transitioning away from Base64-heavy metadata.
- `characterDisplayName` / `playerLevel` if UI needs them explicitly.

## Implementation Phases

### Phase A: Portrait stability
1. Add studio validation + diagnostics.
2. Add portrait refresh hooks for equipment and post-load.
3. Verify no duplicate studio instances.

### Phase B: Save screenshot generation
1. Implement `SaveScreenshotService` capture + Base64 encode.
2. Populate `metadata.screenshotBase64` during save.
3. Ensure save list shows new screenshot without restart.

### Phase C: UI fallback and caching polish
1. Add screenshot -> portrait -> placeholder fallback chain.
2. Add per-slot decode cache and cleanup.
3. Add regressions tests for multi-slot behavior.

## Testing Checklist (Project-Specific)

### Portrait Studio
1. Open squad UI and verify portrait renders from `portraitRT`.
2. Change equipment, reopen/reselect unit, verify portrait updates.
3. Change character creator appearance, enter world, verify portrait matches.
4. Load a save and verify portrait updates to loaded appearance.
5. Confirm no duplicate `PortraitStudio_RuntimeInstance` after scene transitions.

### Save Slot Screenshots
1. Save from gameplay and confirm screenshot is shown in slot list.
2. Restart game and confirm same screenshot persists.
3. Create multiple saves and verify previews do not overwrite each other.
4. Delete a save and verify preview disappears with slot.
5. Validate performance when rapidly opening/closing save menu.

## Risk Notes
- Current save architecture has provider-based runtime restore but still needs robust screenshot capture integration.
- Base64 screenshots can increase save file size; monitor memory and serialization time.
- If save metadata grows further, consider migration to separate thumbnail files.
