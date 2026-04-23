# Character Creator + Main Menu Restyle Plan

## Goal
Build a full character creation flow and menu restyle that supports:
- Height changes
- Per-body-part sizing
- Hair selection
- Beard selection
- Gender/race switch
- Skin tone selection

This plan is tailored to current project wiring and UMA usage.

## Current Baseline (Verified)
- Main menu start flow is controlled by MainMenuController and already gates start on character creation when enabled.
- Character creator currently saves character name, preset stats/loadout, portrait, and UMA recipe string.
- Player spawner applies the saved UMA recipe string at runtime.
- MainMenu scene already has UMAPreviewAvatar and UMAPreviewCamera configured.
- CharacterCreatorController in scene has sparse explicit references and relies on auto-resolve.

## Design Direction
- Keep existing runtime compatibility by continuing to save and apply full UMA recipe strings.
- Add a structured appearance profile for explicit control values (race, DNA sliders, hair/beard choices, colors).
- Build customization UI as tabbed controls inside the existing creator panel.
- Restyle main menu into a two-zone layout:
  - Left: title + actions
  - Right: live 3D character preview + customization tabs

## Architecture Changes

### 1) Appearance Data Model
Add explicit model classes for appearance values.

Planned files:
- Assets/UI/Menus/CharacterCreation/CharacterAppearanceProfile.cs
- Assets/UI/Menus/CharacterCreation/CharacterDnaEntry.cs
- Assets/UI/Menus/CharacterCreation/CharacterWardrobeSelection.cs

Model fields:
- raceName (HumanMale/HumanFemale)
- bodyValues: list of DNA key/value pairs
- hairRecipeName
- beardRecipeName
- skinColor
- hairColor
- eyeColor (optional)

Compatibility:
- Keep CharacterSelectionState.SelectedUmaRecipe as primary spawn payload.
- Store the explicit appearance profile in CharacterSelectionState as JSON for UI restore and future systems.

### 2) UMA Bridge Service
Create a focused bridge that applies and captures appearance values.

Planned file:
- Assets/UI/Menus/CharacterCreation/UmaAppearanceService.cs

Responsibilities:
- Apply race via DynamicCharacterAvatar.ChangeRace(...)
- Apply wardrobe choices via SetSlot(...) / ClearSlot(...)
- Apply shared colors via SetColor(...)
- Apply DNA values using GetDNA() dictionary and setter values
- Trigger BuildCharacter(...) only when needed
- Capture current avatar state back into CharacterAppearanceProfile

### 3) Customization Catalog
Define what controls are shown and their ranges.

Planned files:
- Assets/UI/Menus/CharacterCreation/CharacterCustomizationCatalog.cs (ScriptableObject)
- Assets/UI/Menus/CharacterCreation/CharacterCustomizationControlDef.cs

Why:
- Avoid hardcoding DNA names in UI logic
- Allow easy tuning per race and control range

Starter control groups:
- Body: height, upperMuscle, lowerMuscle, belly
- Proportions: headSize, armLength, forearmLength, legSeparation, feetSize
- Face (optional phase 2): jawSize, noseSize, earSize

Important:
- First generate a race-specific DNA key report and only expose keys that actually exist for your configured races.

## Main Menu + Creator UI Restyle

### Layout Plan
- Keep MainMenuController and CharacterCreatorController as orchestration points.
- Replace ad-hoc creator panel layout with clear sections:
  - Header: character name + race selector
  - Tabs: Body, Hair/Beard, Skin, Presets
  - Center/right: UMA preview render
  - Footer: Randomize, Reset, Confirm, Back

### Interaction Plan
- Slider drag updates preview with lightweight throttling.
- Dropdown changes (race/hair/beard) apply immediately.
- Confirm stores both:
  - selected UMA recipe string
  - structured appearance profile JSON
- Back closes without committing transient slider changes (unless configured otherwise).

### Visual Plan
- Keep current gritty/survivor palette direction but modernize hierarchy:
  - stronger contrast for selected tab
  - cleaner spacing and typography scale
  - clearer active/inactive button states
  - subtle panel entrance animation and tab transition

## Scene and Script Integration

### Existing scripts to update
- Assets/UI/Menus/CharacterCreatorController.cs
  - Add appearance model lifecycle
  - Bind tab controls
  - Use UmaAppearanceService for applying changes
  - Keep existing name validation and confirm flow

- Assets/Core/CharacterSelectionState.cs
  - Add SelectedAppearanceProfileJson
  - Add SetAppearanceProfileJson(...) and retrieval helper
  - Keep current API backward compatible

- Assets/UI/Menus/MainMenuController.cs
  - Minimal changes expected
  - Keep character creation gating behavior

- Assets/Characters/PlayerSpawner.cs
  - Keep recipe load flow
  - Optional: if profile JSON exists, re-apply profile deltas after recipe load for deterministic overrides

### Scene updates
- Assets/Scenes/MainMenu.unity
  - Add tab controls (sliders/dropdowns/color picker UI)
  - Wire creator references directly where possible
  - Keep current UMAPreviewAvatar and UMAPreviewCamera

## Tool-Driven Workflow (Idempotent)
Create editor tools so setup is repeatable.

Planned file:
- Assets/Editor/CharacterCreationTools.cs

Implemented Phase 3 tooling file:
- Assets/Editor/CharacterCreationPhase3Tools.cs

Menu commands:
- Tools/Zombera/Character Creation/Step 1 - Generate Customization Catalog
- Tools/Zombera/Character Creation/Step 2 - Build Creator UI Skeleton
- Tools/Zombera/Character Creation/Step 3 - Wire MainMenu Scene References
- Tools/Zombera/Character Creation/Step 4 - Validate Setup

Current Phase 3 styling workflow commands:
- Tools/Zombera/Character Creation/Phase 3/Apply Mockup Baseline (Selected Creator)
- Tools/Zombera/Character Creation/Phase 3/Create Mockup Overlay (Selected Sprite)
- Tools/Zombera/Character Creation/Phase 3/Remove Mockup Overlay
- Tools/Zombera/Character Creation/Phase 3/Export Current Tuned Values Preset (Selected Creator)
- Tools/Zombera/Character Creation/Phase 3/Enable Scene Inspect Mode (Selected Creator)
- Tools/Zombera/Character Creation/Phase 3/Disable Scene Inspect Mode (Restore Overlay)

Validation should check:
- Required scene objects/components exist
- Creator references are assigned
- Preview avatar is found
- Catalog has at least one race and one DNA control

## Exact Implementation Order

### Phase 0: Discovery and Safety
1. Add DNA/wardrobe audit utility for active races.
2. Generate and inspect available DNA keys and wardrobe recipes.
3. Finalize starter control list from actual keys.

### Phase 1: Data + Persistence
1. Add CharacterAppearanceProfile model files.
2. Extend CharacterSelectionState with profile JSON storage.
3. Add migration-safe defaults.

### Phase 2: UMA Application Layer
1. Implement UmaAppearanceService.
2. Add unit-like runtime checks (null-safe and missing-key-safe).
3. Verify race switch + DNA + color + wardrobe round-trip.

### Phase 3: Creator Controls
1. Add tabs and controls to CharacterCreatorPanel.
2. Bind controls in CharacterCreatorController.
3. Hook randomize/reset behavior.
4. Save recipe + profile JSON on confirm.

### Phase 4: Main Menu Restyle
1. Rework panel layout and visual hierarchy.
2. Keep existing button object names to avoid flow regressions.
3. Add small transition animations.

### Phase 5: Integration Verification
1. Enter menu, create character, confirm, start game.
2. Verify spawned player matches appearance.
3. Re-open creator and verify profile restoration.

## Test Checklist
- Compile clean after each phase.
- No null reference exceptions in menu flow.
- Race switch preserves valid wardrobe and colors.
- Beard options are disabled/filtered when no compatible recipe exists.
- Slider values persist across panel reopen.
- Spawned runtime avatar matches confirmed preview.

## Risks and Mitigations
- DNA key mismatch across races:
  - Mitigation: catalog generated from live race DNA keys.
- Wardrobe slot recipe incompatibility:
  - Mitigation: filter recipe lists by active race and slot compatibility.
- Over-rebuild performance while dragging sliders:
  - Mitigation: debounce/throttle BuildCharacter calls.
- Scene reference fragility:
  - Mitigation: explicit wiring + validation menu command.

## Suggested Milestone Commits
1. feat(character): add appearance profile model + state persistence
2. feat(character): add UMA appearance service and race/dna/color/wardrobe apply
3. feat(ui): add creator tabs and customization controls
4. feat(ui): restyle main menu and creator layout
5. chore(editor): add Character Creation setup/validation tools
6. test(character): add flow validation checklist and docs updates
