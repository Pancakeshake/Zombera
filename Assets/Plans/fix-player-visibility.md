# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with UMA character customization.
- Players: Single player with squadmates.
- Target Platform: PC (Windows)
- Render Pipeline: URP

# Game Mechanics
## UMA Character Integration
- The game uses Unity Multipurpose Avatar (UMA) for the player and NPCs.
- Players can customize their character in the Main Menu, and the appearance should persist into the World scene.
- Squadmates are randomized using UMA DNA and Wardrobe.

# UI
- Character Creator UI drives UMA DNA and Wardrobe selection.

# Key Asset & Context
- `Player.prefab`: The main character prefab with `DynamicCharacterAvatar`.
- `AppearanceProfileService.cs`: Manages UMA profile application.
- `SpawnAppearanceStylingService.cs`: Coordinates appearance application during spawn.

# Implementation Steps
## 1. Improve Player Spawn Appearance Initialization
- Update `SpawnAppearanceStylingService.ApplySelectedAppearanceProfile` to force a UMA build even if the profile appears unchanged (to ensure the first-time build triggers).
- Use a randomized "HumanMale" profile as the default fallback for new games instead of an empty profile, ensuring characters have base slots (Hair/Beard) and valid DNA from the start.
- Implement `ForceEnableRenderers` for the player, similar to how squadmates are handled, to ensure visibility and prevent culling issues.

## 2. Enhance Error Reporting in Profile Application
- Update `AppearanceProfileService.TryApplyProfile` to log errors to the Unity Console if the operation report contains errors, making it easier to diagnose missing assets or race/slot mismatches.

# Verification & Testing
- Start the game from `Boot` scene.
- Go to `MainMenu` -> `Create Player` (optional).
- Start the game.
- Verify the Player character is visible and matches the selection (or has a random look if none was selected).
- Verify Squadmates are visible.
