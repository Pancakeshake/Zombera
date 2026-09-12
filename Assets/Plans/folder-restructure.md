# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with squad management, base building, and zombie combat.
- Players: Single player.
- Render Pipeline: URP.
- Target Platform: PC (Windows).

# Folder Restyle Strategy: Feature-Based Organization
The goal is to transition from a "Type-Based" structure (Scripts, Prefabs, Materials in separate roots) to a "Feature-Based" structure where everything needed for a specific game system (Zombies, Weapons, Building) lives in one directory.

## Core Rules
1. **The Feature Rule**: If an asset is used *only* by one system, it lives in that feature's folder (e.g., `01_Game/04_Zombies/Models`).
2. **The Shared Rule**: If an asset is used by *multiple* systems, it lives in `Assets/Shared/` (e.g., `Shared/Materials/Grass`).
3. **The Assembly Rule**: The `Zombera.Runtime.asmdef` must move to the root of the new game logic folder to maintain code visibility.
4. **Third-Party Safety**: Do NOT move `Assets/UMA/` or other critical third-party dependencies that rely on specific pathing or internal registries.

# Key Asset & Context
- **Primary Root**: `Assets/01_Game/`
- **Secondary Roots**: `Assets/Shared/`, `Assets/ThirdParty/`, `Assets/00_Scenes/`
- **Scripts Sources**: `Assets/Scripts/Core`, `Assets/Scripts/Systems`, `Assets/Scripts/Characters`, `Assets/Scripts/Inventory`, etc.

# Implementation Steps

## Phase 1: Directory Preparation
1. Create the following subdirectories in `Assets/01_Game/` if they do not exist:
   - `00_Framework/` (Interfaces, Abstract Classes, EventBus)
   - `01_Core/` (GameManager, Input, State Machine)
   - `02_World/` (Terrain, Generation, Roads, Environment)
   - `03_Characters/` (Player, Survivors, Clothing, Stats)
   - `04_Zombies/` (Horde Logic, AI, Zombie Models)
   - `05_Combat/` (Damage System, Projectiles, Weapons Logic)
   - `06_Inventory/` (Items, Loot, Crafting)
   - `07_Building/` (Base Management, Construction Logic, Prefabs)
   - `08_UI/` (HUD, Menus, UI Assets)
   - `09_SaveSystem/` (Serialization logic)
   - `10_Audio/` (Music, SFX)

## Phase 2: Assembly & Core Migration
1. Move `Assets/Scripts/Zombera.Runtime.asmdef` to `Assets/01_Game/Zombera.Runtime.asmdef`.
2. Move contents of `Assets/Scripts/Core/` to `Assets/01_Game/01_Core/`.
3. Move `Assets/Scripts/Systems/CoreEventBus.cs` (if found) to `Assets/01_Game/00_Framework/`.

## Phase 3: Feature Distribution (Scripts & Assets)
1. **Characters & Players**:
   - Move `Assets/Scripts/Characters/` to `Assets/01_Game/03_Characters/`.
   - Move `Assets/01_Game/Player/` to `Assets/01_Game/03_Characters/Player/`.
   - Move `Assets/01_Game/Clothing/` to `Assets/01_Game/03_Characters/Clothing/`.
2. **Zombies**:
   - Move `Assets/01_Game/Zombies/` to `Assets/01_Game/04_Zombies/`.
   - Move `Assets/Scripts/Systems/ZombieManager.cs` to `Assets/01_Game/04_Zombies/`.
3. **Combat & Weapons**:
   - Move `Assets/Scripts/Combat/` to `Assets/01_Game/05_Combat/`.
   - Move `Assets/01_Game/Weapons/` to `Assets/01_Game/05_Combat/Weapons/`.
   - Move `Assets/Scripts/Systems/CombatManager.cs` to `Assets/01_Game/05_Combat/`.
4. **World & Environment**:
   - Move `Assets/01_Game/World Generation/` to `Assets/01_Game/02_World/Generation/`.
   - Move `Assets/Terrain/` and `Assets/Scripts/World/` to `Assets/01_Game/02_World/`.
5. **Inventory & Loot**:
   - Move `Assets/Scripts/Inventory/` to `Assets/01_Game/06_Inventory/`.
   - Move `Assets/Scripts/Systems/LootManager.cs` to `Assets/01_Game/06_Inventory/`.
6. **Building**:
   - Move `Assets/Scripts/BuildingSystem/` to `Assets/01_Game/07_Building/`.
   - Move `Assets/Scripts/Systems/BaseManager.cs` to `Assets/01_Game/07_Building/`.

## Phase 4: UI & Systems
1. Move `Assets/Scripts/UI/` to `Assets/01_Game/08_UI/`.
2. Move `Assets/Scripts/Systems/SaveManager.cs` to `Assets/01_Game/09_SaveSystem/`.
3. Move `Assets/Scripts/Systems/CursorManager.cs` and `AIManager.cs` to `Assets/01_Game/01_Core/`.

## Phase 5: Cleanup
1. Delete empty folders: `Assets/Scripts/`, `Assets/Terrain/`.
2. **NOTE**: Do NOT move `Assets/UMA/`. It must remain at the root to avoid breaking internal UMA registries.

# Verification & Testing
1. **Compilation Check**: Open the Unity Console and ensure there are no red errors.
2. **Reference Audit**: Select a few `WeaponData` ScriptableObjects and verify they still have their Prefab references.
3. **Play Mode Test**: Enter the `Boot` scene and start the game.
   - Verify that the `GameManager` initializes correctly.
   - Verify that the player can move and open the inventory.
   - Verify that zombies still spawn.
