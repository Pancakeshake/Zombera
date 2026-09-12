# Project Overview
- Game Title: Zombera
- High-Level Concept: A survival/strategy zombie game with RTS-style squad management, base building, and combat.
- Players: Single player (with squad control).
- Inspiration / Reference Games: RTS survival games.
- Tone / Art Direction: High-fidelity, survival-oriented.
- Target Platform: Standalone Windows.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
Resource gathering, squad management, base construction, and survival against zombie waves.
## Controls and Input Methods
- RTS-style selection and orders.
- New Input System for player controls.
- Tab-based HUD for management (Squad, Inventory, Crafting, etc.).
- Build mode activated via 'B' or HUD tiles.

# UI
The project uses UGUI for HUD and management panels. The World HUD has a top bar with tabs for different management screens.

# Key Asset & Context
- **WorldHUDController**: Manages the tabbed panels in the HUD.
- **CraftingTabController**: Manages the crafting UI, recipes, and item production.
- **BuildPlacementController**: Manages legacy build mode and ghost previews.
- **EasyBuildRadialMenuInputBridge**: Manages the newer EasyBuild radial menu and construction system.

# Implementation Steps
## Task 1: Link Crafting Menu
1. **Modify [WorldHUDController.cs]**:
   - Add `[SerializeField] private GameObject craftingTabPrefab;` to hold the `CraftingTab` prefab.
   - Add `private CraftingTabController _craftingTab;` to store the live reference.
   - Assigned role: developer
   - Dependencies: None

2. **Modify [WorldHUDController.Lifecycle.cs]**:
   - Implement `EnsureCraftingTabController()`:
     - Check if `craftingPanel` has a `CraftingTabController`.
     - If not, and `craftingTabPrefab` is assigned, instantiate it under `craftingPanel`.
     - Find and destroy any placeholder text objects (e.g., "Crafting system coming soon.") in the `craftingPanel`.
   - Call `EnsureCraftingTabController()` in `Start()`.
   - Assigned role: developer
   - Dependencies: Step 1

3. **Modify [WorldHUDController.TabLayout.cs]**:
   - In `ApplyTab(TabId tab)`, add logic for `TabId.Crafting`:
     - Resolve the active unit from `portraitStrip.SelectedUnit`.
     - If `_craftingTab` is valid, call `_craftingTab.SetContext(unit, unit?.Inventory)` and `_craftingTab.SetContextSurvivor(unit?.name)`.
   - Assigned role: developer
   - Dependencies: Step 2

## Task 2: Fix Build Menu Deactivation Bug
4. **Modify [WorldHUDController.BuildHud.Commands.cs]**:
   - In `TryActivateBottomBuildItem(int itemIndex)` for the cancel case (`itemIndex == 9`):
     - Update the logic to ensure that if `_legacyBuildPlacementController` exists, its `ExitBuildMode()` or `TryActivateHudItem(9)` is called even if `_easyBuildRadialMenuInputBridge.TryCancelBuildUi()` handles the request. This ensures the legacy build ghost is hidden.
   - Assigned role: developer
   - Dependencies: None

# Verification & Testing
1. **Crafting Linkage**:
   - Enter Play Mode.
   - Press `F3` or click the CRAFTING tab.
   - Verify that the crafting UI appears (replacing the "coming soon" text).
   - Verify that recipes are populated and it reflects the selected unit's inventory.
2. **Build Menu Cancellation**:
   - Enter Play Mode.
   - Open the Build Menu (press `B` or select a building tile).
   - Verify the building ghost is visible.
   - Press `Esc`.
   - Verify that the Build Menu closes AND the building ghost is removed.
   - Repeat with the `B` key to ensure it still works as expected.
