# Project Overview
- Game Title: Zombera
- High-Level Concept: Zombie survival simulation blending RTS-style squad management with base-building and third-person combat.
- Players: Single player, tactical squad control.
- Inspiration / Reference Games: RTS-style management, survival sims.
- Tone / Art Direction: Gritty, production-ready survival aesthetic.
- Target Platform: PC (StandaloneWindows64).
- Screen Orientation / Resolution: Landscape (1920x1080).
- Render Pipeline: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
Survival through management: command units to gather resources, loot containers, and craft items to defend against zombies and build bases.
## Controls and Input Methods
- Keyboard (F1-F5) for tab switching.
- Mouse for UI interaction (CRAFT button, quantity selection, category filtering).
- New Input System used for gameplay.

# UI
## Layout and Design
The crafting UI is integrated into the Squad Management screen (Tab F3).
- **Left Sidebar**: Category filters (Tools, Weapons, Supplies, etc.) and search bar.
- **Center Panel**: Scrollable list of recipes. Each entry shows:
    - Icon
    - Name and Description
    - Crafting time (e.g., 10s)
    - Output quantity (e.g., x4)
- **Right Panel (Details)**:
    - Selected item name, rarity, and large icon.
    - Item description and "USES" list.
    - **Required Materials**: Grid of slots showing required items (icon and current/required count).
    - **Quantity Control**: Plus/Minus buttons and "MAX" button.
    - **Craft Button**: Primary action button showing total craft time.

## UI Implementation Strategy
- Use **uGUI** as per project convention.
- **Prefab-based approach**: Create a dedicated Crafting UI prefab to ensure visual fidelity with the provided design.
- **Tab Reorganization**: Move the existing `SkillsTabController` to a separate tab (e.g., Missions or a new dedicated Skills tab) and use the F3 'Crafting' tab exclusively for the new Crafting system.
- Responsive layout using `LayoutGroup`, `ContentSizeFitter`, and proper `RectTransform` anchoring.

# Key Asset & Context
## New Data Assets
- `CraftingRecipe`: ScriptableObject defining ingredients, outputs, time, and requirements.
- `CraftingIngredient`: Serializable class for item/amount/tag requirements.
- `CraftingOutput`: Serializable class for output item/amount.
- `CraftingStationType`: Enum for workbench requirements.

## Core Services
- `CraftingService`: Singleton/Service for validation, instant crafting, and queue management.
- `CraftingTabController`: Manages the UI lifecycle, recipe population, and user input.

## Integration Points
- `InventoryManager`: Global material checks.
- `UnitInventory`: Local unit inventory for outputs and personal ingredients.
- `UnitStats`: For skill-based quality/speed/unlock gates.
- `SaveManager`: Persisting the crafting queue via `CraftingSaveProvider`.

# Implementation Steps
## Phase 1: Data Model & Registry
1. **Implement `CraftingRecipe` and related data types**: Create the ScriptableObject structure under `Assets/01_Game/06_Inventory/Crafting/`.
    - **Assigned role**: developer
    - **Dependencies**: None
2. **Implement `CraftingRecipeRegistry`**: Create a central registry to load and query all recipes.
    - **Assigned role**: developer
    - **Dependencies**: Step 1

## Phase 2: Core Logic & Service
1. **Implement `CraftingService`**: Handle the logic for `CanCraft`, `CraftInstant`, and the `Queue` system.
    - **Assigned role**: developer
    - **Dependencies**: Step 2
2. **Implement `CraftingInventoryTransaction`**: Ensure atomic add/remove operations to prevent resource loss on failure.
    - **Assigned role**: developer
    - **Dependencies**: Step 3

## Phase 3: UI Implementation
1. **Refactor `ZomberaSquadManagementUI`**: Update the "Crafting" tab logic to use the new `CraftingTabController` instead of the placeholder `SkillsTabController` logic.
    - **Assigned role**: developer
    - **Dependencies**: Step 4
2. **Implement `CraftingTabController`**: Build the UI hierarchy (programmatically or via prefab) to match the provided design.
    - **Assigned role**: developer
    - **Dependencies**: Step 5
3. **Wire UI Events**: Connect buttons (Craft, Quantity, Search) to `CraftingService`.
    - **Assigned role**: developer
    - **Dependencies**: Step 6

## Phase 4: Integration & Persistence
1. **Implement `CraftingStation`**: Add world-object station component for requirements.
    - **Assigned role**: developer
    - **Dependencies**: None
2. **Implement `CraftingSaveProvider`**: Register with `SaveManager` to persist the crafting queue.
    - **Assigned role**: developer
    - **Dependencies**: Step 4
3. **Skill & Trait Integration**: Connect `UnitStats` to influence crafting speed and quality.
    - **Assigned role**: developer
    - **Dependencies**: Step 4

# Verification & Testing
## Unit Tests
- `Test_CraftingValidation`: Verify `CanCraft` returns false if ingredients are missing.
- `Test_AtomicTransaction`: Verify ingredients are not consumed if output inventory is full.
- `Test_QueuePersistence`: Verify queue entries survive Save/Load cycle.

## Manual Checks
- Verify UI updates in real-time when ingredients are added/removed from inventory.
- Verify search and category filters correctly hide/show recipes.
- Verify "MAX" button calculates the correct quantity based on available ingredients and weight limits.
