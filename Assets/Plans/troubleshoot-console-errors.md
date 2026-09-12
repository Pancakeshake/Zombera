# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with zombies, building, and inventory management.
- Players: Single player.
- Target Platform: PC (StandaloneWindows64).
- Render Pipeline: URP.

# Core Issue: Duplicated Scripts
The project has multiple duplicated script files across several systems (UI, Zombies, Player Spawner), likely resulting from folder reorganizations where files were copied instead of moved. This has caused a massive number of compilation errors (CS0111: Type already defines a member...) because partial classes are being compiled multiple times with the same methods.

Additionally, there is a NullReferenceException in the UMA system (`UMAData.SaveMountedItems`) which is likely a side effect of the broken compilation state or a missing scene reference.

# Key Asset & Context
The following folder pairs contain duplicate scripts:
1. **UI:** `Assets/01_Game/08_UI/[Category]/` and `Assets/01_Game/08_UI/Scripts/[Category]/`
   - Prefabs reference the GUIDs of files **outside** the `Scripts/` folder.
2. **Zombies:** `Assets/01_Game/04_Zombies/` and `Assets/01_Game/04_Zombies/Management/`
   - Prefabs reference the GUIDs of files **inside** the `Management/` folder.
3. **Player Spawner:** `Assets/01_Game/03_Characters/Spawning/` and `Assets/01_Game/03_Characters/Scripts/`
   - Prefabs reference the GUIDs of files **inside** the `Spawning/` folder.

# Implementation Steps

## Phase 1: Cleanup Unused Duplicates
1. **Delete unused HUD/Menu/Squad duplicates:**
   - Remove all files in `Assets/01_Game/08_UI/Scripts/HUD/` that have matching names in `Assets/01_Game/08_UI/HUD/`.
   - Remove all files in `Assets/01_Game/08_UI/Scripts/Menus/` that have matching names in `Assets/01_Game/08_UI/Menus/`.
   - Remove `Assets/01_Game/08_UI/Scripts/SquadManagement/ZomberaSquadManagementUI.cs`.
2. **Delete unused ZombieManager duplicates:**
   - Remove all `ZombieManager.*` files in `Assets/01_Game/04_Zombies/` (retaining the ones in `Management/`).
3. **Delete unused PlayerSpawner duplicates:**
   - Remove all `PlayerSpawner.*` files in `Assets/01_Game/03_Characters/Scripts/` (retaining the ones in `Spawning/`).

## Phase 2: Consolidate Used Scripts into Standard Folder Structure
1. **Move HUD/Menu/Squad Scripts (Preserving GUIDs):**
   - Move scripts from `Assets/01_Game/08_UI/HUD/` to `Assets/01_Game/08_UI/Scripts/HUD/`.
   - Move scripts from `Assets/01_Game/08_UI/Menus/` to `Assets/01_Game/08_UI/Scripts/Menus/`.
   - Move `Assets/01_Game/08_UI/Squad/ZomberaSquadManagementUI.cs` and related partials to `Assets/01_Game/08_UI/Scripts/SquadManagement/`.
2. **Move PlayerSpawner Scripts (Preserving GUIDs):**
   - Move scripts from `Assets/01_Game/03_Characters/Spawning/` to `Assets/01_Game/03_Characters/Scripts/`.
3. **Clean up empty folders:**
   - Remove redundant folders: `Assets/01_Game/08_UI/HUD/`, `Assets/01_Game/08_UI/Menus/`, `Assets/01_Game/08_UI/Squad/`, and `Assets/01_Game/03_Characters/Spawning/`.

## Phase 3: Verify and Fix Secondary Issues
1. **Compilation Check:**
   - Verify that all CS0111 and related compilation errors are resolved.
2. **UMA Troubleshooting:**
   - Investigate the `NullReferenceException` in `UMAData.cs`.
   - Check if `DynamicCharacterAvatar` in the scene has a `UMAGenerator` assigned or if there is a `UMAGenerator` in the scene.
   - If missing, add or assign the `UMAGenerator`.

# Verification & Testing
- **Compilation:** Ensure the Console is clear of compilation errors.
- **Prefab Integrity:** Open `MainMenu.prefab`, `WorldHUDCanvas.prefab`, `[GameManager].prefab`, and `PlayerSpawner.prefab` in the inspector to ensure scripts are still attached.
- **Runtime Test:** Play the game from the `Boot` scene and ensure the UI and spawning systems function correctly.
- **UMA Test:** Ensure characters generate without throwing NREs in the console.
