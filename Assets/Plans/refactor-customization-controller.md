# Refactor Plan: CharacterCreatorCustomizationController.cs

## Phase 1: Safe Split (No behavior change)
- [ ] Convert `CharacterCreatorCustomizationController` to `partial`.
- [ ] Move methods to `CharacterCreatorCustomizationController.Lifecycle.cs`.
- [ ] Move methods to `CharacterCreatorCustomizationController.UiScaffold.cs`.
- [ ] Move methods to `CharacterCreatorCustomizationController.Tabs.cs`.
- [ ] Move methods to `CharacterCreatorCustomizationController.Events.cs`.
- [ ] Move methods to `CharacterCreatorCustomizationController.Profile.cs`.
- [ ] Move methods to `CharacterCreatorCustomizationController.Camera.cs`.
- [ ] Move methods to `CharacterCreatorCustomizationController.Editor.cs`.
- [ ] Clean up host file (keep fields and public wrappers).

## Phase 2: Extract Pure Logic
- [ ] Move camera math to `CharacterCreatorCameraHelper.cs`.
- [ ] Move option cycling/randomization math to a pure helper.
- [ ] Ensure UI factory calls remain centralized.

## Phase 3: De-risk Cleanup
- [ ] Remove dead code (e.g., unused `DnaControlDefinition`).
- [ ] Normalize null/scene guards.
- [ ] Final validation.
