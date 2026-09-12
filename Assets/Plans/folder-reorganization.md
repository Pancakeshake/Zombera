# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world with base building and tactical combat.
- Players: Single player
- Inspiration / Reference Games: Project Zomboid, State of Decay
- Tone / Art Direction: Gritty, realistic survival
- Target Platform: PC (Windows)
- Render Pipeline: URP

# Folder Reorganization Plan
The goal is to reorganize `Assets/01_Game` so that every system has functional subfolders containing its scripts, improving discoverability and maintainability.

## Key Asset & Context
- **00_Framework**: Core event bus, testing tools, and debugging utilities.
- **01_Core**: High-level managers, input handling, and core systems (Vision, Environment, RTS).
- **02_World**: Terrain generation, weather systems, and spawning logic.
- **03_Characters**: Player/Squad controllers, AI, and management.
- **04_Zombies**: Zombie lifecycle and spawning management.
- **06_Inventory**: Inventory, equipment, items, and looting systems.
- **07_Building**: Base management, construction, and prop interactions.
- **08_UI**: Runtime UI management and specific squad UI modules.
- **09_SaveSystem**: Persistence logic.

## Implementation Steps

### 1. Reorganize 00_Framework
- Create `Assets/01_Game/00_Framework/Events/` and move `CoreEventBus.cs`.
- Create `Assets/01_Game/00_Framework/Testing/` (already exists, but consolidate any stray testing scripts).
- Create `Assets/01_Game/00_Framework/Debugging/` and move `Debugging/`, `Debug/`, and `PerfTrace.cs`.
- Create `Assets/01_Game/00_Framework/Tools/` and move `Editor/`, `BuildingPropSeeder.cs`.
- Create `Assets/01_Game/00_Framework/Animation/` and move existing animation scripts.

### 2. Reorganize 01_Core
- Create `Assets/01_Game/01_Core/Managers/` and move `GameManager.cs`, `UnitManager.cs`, `AIManager.cs`, `CursorManager.cs`, `TimeSystem.cs`, `IGameSystem.cs`, `StartupReadinessValidator.cs`.
- Create `Assets/01_Game/01_Core/Input/` and move `PlayerInputController.*`, `PlayerBowController.cs`.
- Create `Assets/01_Game/01_Core/Vision/` and move `FogOfWar/` contents.
- Create `Assets/01_Game/01_Core/Environment/` and move `Environment/` (Day/Night scripts).
- Create `Assets/01_Game/01_Core/RTS/` and move `RTS/` (Selection scripts).
- Create `Assets/01_Game/01_Core/Interaction/` and move `Digging/`, `CommandSystem.cs`, `DialogueEvent.cs`.
- Create `Assets/01_Game/01_Core/Data/` and move `CameraRegistry.cs`, `CharacterPortraitCatalog.cs`, `RuntimeAiRegistry.cs`, `CharacterSelectionState.cs`.

### 3. Reorganize 02_World
- Create `Assets/01_Game/02_World/Atmosphere/` and move `Environment/EnviroWeatherCycleController.cs`, `EnviroRainFixUtility.cs`.
- Create `Assets/01_Game/02_World/Streaming/` and move `Scripts/ChunkCache.cs`, `ChunkLoader.cs`, `ChunkGenerator.cs`.
- Create `Assets/01_Game/02_World/Terrain/` and move `Scripts/MapMagicMicroSplatTileSync.cs`, `MapMagicDiagnostic.cs`.
- Create `Assets/01_Game/02_World/Spawning/` and move `Scripts/LootSpawner.cs`, `Scripts/City/TownSpawner.cs`.
- Remove empty `Scripts/` and `Environment/` folders.

### 4. Reorganize 03_Characters
- Create `Assets/01_Game/03_Characters/Movement/` and move `SurvivorController.cs`, `SquadController.cs`, `FollowController.cs`, `FormationController.cs`.
- Create `Assets/01_Game/03_Characters/AI/` and move `SurvivorAI.cs`, `SquadAI.cs`.
- Create `Assets/01_Game/03_Characters/Squad/` and move `SquadManager.cs`, `SquadMember.cs`.

### 5. Reorganize 04_Zombies
- Create `Assets/01_Game/04_Zombies/Core/` and move `ZombieManager.cs` and all partials.

### 6. Reorganize 06_Inventory
- Create `Assets/01_Game/06_Inventory/Management/` and move `InventoryManager.cs`, `LootManager.cs`.
- Create `Assets/01_Game/06_Inventory/Equipment/` and move `EquipmentSystem.cs` and partials.
- Create `Assets/01_Game/06_Inventory/Items/` and move `ItemDefinition.cs`, `ItemPickup.cs`, `ItemPickupInteractor.cs`, `ContainerInteractor.cs`.
- Create `Assets/01_Game/06_Inventory/Looting/` and move `LootTable.cs`, `LootContainer.cs`, `UnitLootDropper.cs`.
- Remove empty `Scripts/` folder.

### 7. Reorganize 07_Building
- Create `Assets/01_Game/07_Building/Management/` and move `BaseManager.cs`.
- Create `Assets/01_Game/07_Building/Construction/` and move `BuildManager.cs`, `Blueprint.cs`, `BaseStorage.cs`, `ConstructionJob.cs`, `WorkerAI.cs`.
- Create `Assets/01_Game/07_Building/Props/` and move `DoorInteractor.cs`, `DoorController.cs`, `DoorHealth.cs`, `DoorBlocker.cs`.
- Create `Assets/01_Game/07_Building/Placement/` and move `BuildPlacementController.cs`, `BuildGhostPreview.cs`, `BuildPiece.cs`, `BuildPrefabSpriteLibrary.cs`.
- Create `Assets/01_Game/07_Building/Integrations/` and move all `EasyBuildBridge*` scripts.
- Remove empty `Scripts/` folder.

### 8. Reorganize 08_UI
- Create `Assets/01_Game/08_UI/Core/` and move `ZomberaCanvasLayer.cs`, `RuntimeUiEventSystemUtility.cs`, `MouseClickPulseOverlay.cs`.
- Create `Assets/01_Game/08_UI/Squad/` and move `Scripts/SquadManagement/` contents.
- Remove empty `Scripts/` folder.

### 9. Reorganize 09_SaveSystem
- Create `Assets/01_Game/09_SaveSystem/Core/` and move `SaveManager.cs`.

## Verification & Testing
- Ensure the project compiles without errors.
- Verify that ScriptableObject references and Prefab script references are preserved by Unity (meta files should ideally be moved alongside scripts).
- Run the `Boot` scene and ensure systems initialize correctly via `GameManager`.
- Check `CoreEventBus` functionality in a test scene.
