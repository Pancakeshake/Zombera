# Project Overview
- **Game Title**: Zombera
- **Issue**: Characters (Player and Squad) are invisible in the World scene.
- **Cause**: UMA `DynamicCharacterAvatar` components are not having their `activeRace` set, causing build abortions. Additionally, the `SpawnAppearanceStylingService` returns early if no saved profile is found.

# Implementation Steps
## 1. Update SpawnAppearanceStylingService.cs
- Modify `ApplySelectedAppearanceProfile` to handle empty/null JSON by generating a default profile (HumanMale) instead of returning early.
- This ensures that a new player always has a visible UMA body.

## 2. Repair NpcAppearanceVariantSpawner.cs
- Fix the corrupted syntax (triple braces at end).
- Ensure randomized squad mates correctly trigger their UMA builds.

## 3. Update Player Prefab
- Set the default race to "HumanMale" in the `DynamicCharacterAvatar` component.
- This provides a fallback if runtime assignment is delayed or fails.

## 4. Final Validation
- Start a new game and verify the player and squad members are visible.

# Verification & Testing
1. **Visual Check**: Run the game and verify all characters are rendered.
2. **Log Check**: Verify that "No activeRace set" warnings no longer appear in the console.
