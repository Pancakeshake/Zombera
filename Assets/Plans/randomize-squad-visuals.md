# Project Overview
- **Game Title**: Zombera
- **High-Level Concept**: Survival RPG with UMA character customization and squad management.
- **Goal**: Ensure the Player unit visuals accurately reflect the creation choices and that squad mates have randomized UMA appearances.

# Game Mechanics
## Character Spawning
- **Player**: Visuals should be applied immediately upon spawn in the world, matching the choices made in the Main Menu Character Creator.
- **Squad Mates**: Controllable squad NPCs should have randomized UMA appearances (Gender, DNA, Hair, Beard, Colors) to provide visual variety.

# UI
- No new UI. This task focuses on wiring the backend spawning logic to the UMA system.

# Key Asset & Context
- `Assets/Scripts/UI/Menus/CharacterCreation/AppearanceProfileService.cs`: Central service for profile generation and application.
- `Assets/Scripts/Characters/NpcAppearanceVariantSpawner.cs`: Component responsible for NPC visual randomization.
- `Assets/Resources/CharacterAppearanceCatalog.asset`: The data source for valid UMA options.

# Implementation Steps
## 1. Enhance AppearanceProfileService
- Modify `GenerateRandomProfile` to accept a `CharacterAppearanceCatalog`.
- Implement randomization for:
    - **Race**: Randomly choose between "HumanMale" and "HumanFemale".
    - **DNA**: Iterate through the catalog's DNA definitions and assign random values.
    - **Wardrobe**: Pick random Hair and Beard recipes appropriate for the chosen race/gender.
    - **Colors**: Keep existing color randomization but ensure it fits the UMA color slots.

## 2. Update NpcAppearanceVariantSpawner
- Ensure it correctly passes (or the service resolves) the `CharacterAppearanceCatalog`.
- The spawner already calls `TryApplyProfile`, which I've already updated to use UMA. This should now correctly rebuild the NPC as a UMA character.

## 3. Verify Player Spawning
- `PlayerSpawner.cs` already calls `StartApplySelectedAppearanceNextFrame`.
- Ensure the `playerPrefab` (and any squad member prefabs) have the `DynamicCharacterAvatar` component.

# Verification & Testing
1. **Player Test**: Create a character with specific hair/color in Main Menu, start the game, and verify the character looks the same in the world.
2. **Squad Test**: Start a game with the "Startup Squad" enabled. Verify that all 8 squad members have different, randomized UMA appearances.
3. **Log Check**: Verify `[NpcAppearanceVariantSpawner]` logs show successful application.
