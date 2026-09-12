# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival game with survivors, squad management, and zombies.
- Target Platform: Standalone Windows 64
- Render Pipeline: Universal Render Pipeline (URP)

# Character Appearance Option Catalog System
The goal is to replace hardcoded lists and static constants in the character creator with a catalog-driven system using ScriptableObjects.

## Key Assets & Context
- `Assets/Scripts/UI/Menus/CharacterCreation/CharacterAppearanceCatalog.cs`: New ScriptableObject to store available races, wardrobe options, and DNA slider definitions.
- `Assets/Scripts/UI/Menus/CharacterCreation/CharacterCreatorCustomizationController.cs`: Updated to load and use the catalog.

## Implementation Steps

### 1. Create Character Appearance Catalog
- **File**: `Assets/Scripts/UI/Menus/CharacterCreation/CharacterAppearanceCatalog.cs`
- **Work**:
    - Define `CharacterRaceOption` (name, display name, preferred gender).
    - Define `CharacterWardrobeOption` (recipe name, display name, race/gender tags).
    - Define `CharacterDnaControlDefinition` (dnaName, display name, min/max/default, isLocked).
    - Create the `CharacterAppearanceCatalog` ScriptableObject class.

### 2. Update CharacterCreatorCustomizationController
- **File**: `Assets/Scripts/UI/Menus/CharacterCreation/CharacterCreatorCustomizationController.cs`
- **Work**:
    - Add `[SerializeField] private CharacterAppearanceCatalog optionCatalog;`.
    - Implement a `TryResolveCatalog()` method to load from Resources if the serialized field is null.
    - Replace `BodyControlDefinitions` static array with dynamic loading from the catalog.
    - Update `RefreshRaceOptions` to populate `_raceOptions` from the catalog.
    - Update `RefreshWardrobeOptions` to populate `_hairOptions` and `_beardOptions` based on the current race/gender from the catalog.
    - Update `HandleBodyControlSliderChanged` and `ConvertBodySliderToDnaValue` to use min/max/locked ranges from the catalog definitions.

### 3. Catalog Configuration (Manual)
- **Work**:
    - Create a new asset instance of `CharacterAppearanceCatalog` in the project.
    - Populate it with "HumanFemale" and "HumanMale" races.
    - Add DNA definitions for "height", "upperMuscle", etc.
    - (Note: I will provide the script but the asset population is a manual step for the user or done via a utility script).

# Verification & Testing
- **UI Consistency**: Verify that the race toggles and tabs still function correctly.
- **Dynamic Sliders**: Add a new DNA definition to the catalog and verify it appears in the "Body" tab.
- **Wardrobe Filtering**: Verify that hair/beard options change when switching races/genders.
- **Save/Load Round-Trip**: Ensure profile JSON still serializes and applies correctly across the catalog-driven UI.
