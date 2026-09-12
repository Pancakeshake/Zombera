# Project Overview
- **Issue**: Characters are invisible in the World scene.
- **Diagnosis**:
    - The `Player` unit in the `World` scene has 0 UMA slots, even though it has a Race assigned.
    - UMA characters require a `UMAGenerator` and `UMAContext` in the scene (or persistent) to build.
    - The persistent `[GameManager]` object in `Boot.unity` is missing its `UMA_GLIB` child, which contains these core components.
    - A local `UMA_GLIB` was added to `MainMenu.unity`, allowing the preview to work, but it is destroyed upon transitioning to the `World` scene.
    - `AppearanceProfileService.cs` is calling `avatar.ClearSlots()`, which might be too aggressive if not followed by a full wardrobe re-application.

# Implementation Steps

## 1. Fix Boot Scene Infrastructure
- Open `Assets/Scenes/Boot.unity`.
- Locate the `[[GameManager]]` object.
- Re-instantiate or restore the `UMA_GLIB` child from the `[GameManager]` prefab.
- Ensure the `UMA_GLIB` is active.

## 2. Refine AppearanceProfileService.cs
- Remove the aggressive `avatar.ClearSlots()` call.
- Instead of manual slot management, use the `DynamicCharacterAvatar`'s `WardrobeRecipes` management if possible, or ensure `ChangeRace` has time to populate base slots before wardrobe is applied.
- Actually, the safest way is:
    1. Set Race.
    2. Clear Wardrobe (only wardrobe, not all slots).
    3. Apply new Wardrobe.
    4. Build.

## 3. Clean up MainMenu.unity
- Remove the manual `UMA_GLIB` added to `MainMenu.unity` to prevent duplicate generators (since the one in `Boot` will now persist).

# Verification & Testing
1. **Infrastructure Check**: Run the game from `Boot`. Verify that `UMA_GLIB` exists in the `DontDestroyOnLoad` scene during gameplay.
2. **Character Creation**: Verify that the character preview in `MainMenu` still works.
3. **World Spawn**: Enter the `World` scene. Verify that the `Player` and squad members correctly build their meshes and are visible.
4. **Log Check**: Ensure no "No activeRace set" or "RaceLibrary NOT FOUND" warnings appear.
