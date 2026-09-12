This Technical Overview Documentation provides an in-depth look into the **Zombera** Unity project, a survival/strategy zombie game. The project utilizes a sophisticated management layer, a utility-based AI system, and a modular architecture built on the Universal Render Pipeline (URP).

---

## 1. Project Description
**Zombera** is a zombie survival simulation that blends RTS-style squad management with base-building and third-person combat. Players navigate a procedurally generated or streamed world, manage a squad of units, and survive against varying classes of zombies.
- **Core Pillars**: Survival, Tactical Squad Control, Base Construction, and Dynamic World Simulation.
- **Target Experience**: A high-fidelity, performance-oriented simulation where players balance resource management, exploration, and combat.

## 2. Gameplay Flow / User Loop
1.  **Boot & Initialization**: The game starts in the `Boot` scene where `GameManager` initializes core services (Time, Events, Save System).
2.  **Meta/Menu**: The `MainMenu` scene allows for character creation, loading saves, and global settings.
3.  **World Session**: Upon starting, a loading sequence (`Loading` scene) prepares the `World` scene. This involves terrain generation (MapMagic), road network setup, and building placement.
4.  **Simulation Loop**:
    *   **Management**: Players command units using an RTS-style selection system.
    *   **Survival**: Resource gathering, looting containers, and managing unit stats (health, stamina).
    *   **Combat**: Defending against zombie waves using melee and ranged weapons.
    *   **Building**: Placing structures and defenses using a grid-based or free-form placement system.
5.  **Persistence**: The state is captured via the `SaveManager` and can be restored, including unit positions, inventories, and constructed bases.

## 3. Architecture
The project follows a **Manager-Service pattern** orchestrated by a central bootstrap.
- **Central Authority**: `GameManager` (Singleton) manages high-level lifecycle, scene transitions, and world loading stages.
- **Communication**: A `CoreEventBus` facilitates decoupled communication between systems using a Publish/Subscribe model with support for immediate or queued (deterministic) dispatch.
- **System Registration**: Core systems (like `UnitManager` and `LootManager`) register themselves with the `GameManager` during initialization.
- **Assembly Definition**: Logic is partitioned via `Zombera.Runtime.asmdef` and `Zombera.Editor.asmdef` to manage compilation boundaries.

`Location: Assets/01_Game/00_Framework, Assets/01_Game/01_Core`

## 4. Game Systems & Domain Concepts

### Unit & Squad System
- `Unit`: The base class for all entities (Players, Allies, Zombies).
- `UnitManager`: A spatial-grid-optimized registry for querying units by role, faction, or proximity.
- `SquadManager`: Handles grouping of player-controlled units and collective behaviors.
- `UnitRole/UnitFaction`: Defines hostility and behavior logic (e.g., Player vs. Zombie).
`Location: Assets/01_Game/03_Characters, Assets/01_Game/01_Core/Systems`

### Utility AI System
- `UnitBrain`: An abstract base for AI logic, using a Utility-based decision system.
- `UtilityEvaluator`: Scores different `UnitDecision` options based on sensor data.
- `Sensors`: (`EnemySensor`, `NoiseSensor`, `AllySensor`) Collect world context.
- `Actions`: Reusable logic modules like `MoveAction`, `AttackAction`, and `FollowAction`.
- `States`: High-level FSM states (`ChaseState`, `AttackState`, `IdleState`) that execute decision outcomes.
`Location: Assets/01_Game/00_Framework/AI, Assets/01_Game/03_Characters/AI`

### Inventory & Equipment System
- `ItemDefinition`: ScriptableObject-based data defining item properties, icons, and prefabs.
- `EquipmentSystem`: Manages visual attachments (UMA/Mesh), stats, and resistances for units.
- `LootManager`: Orchestrates spawning items in containers or as drops from units.
`Location: Assets/01_Game/06_Inventory`

### Building System
- `BaseManager`: Tracks player-built structures and territories.
- `BuildPlacementController`: Handles the UI-to-World logic for placing new structures.
- `EasyBuild`: A modular system for rapid construction using radial menus and catalog-based selection.
`Location: Assets/01_Game/07_Building`

## 5. Scene Overview
- **Boot**: The technical entry point. No gameplay; initializes `GameManager`.
- **MainMenu**: UI-heavy scene for character customization and session setup.
- **Loading**: An intermediate scene used to mask world generation and asset prewarming.
- **World**: The primary gameplay scene. Often uses `MapMagic` for terrain streaming and dynamic object placement.
- **_Recovery**: A utility folder containing scene backups and auto-saves.

## 6. UI System
The project primarily uses a combination of **UGUI** for complex, dynamic HUD elements and potentially **UITK** for newer menu structures.
- `WorldHUDController`: The primary HUD hub, managing tabs for Squad, Inventory, Map, and Crafting.
- `ZomberaCanvasLayer`: Manages sorting and layering of UI elements.
- `SquadPortraitStrip`: A specialized UGUI component for RTS-style unit selection and status tracking.
- `CharacterCreatorUIFactory`: Programmatically builds customization interfaces.
- **Binding**: Systems often use event-driven updates via `CoreEventBus` to refresh UI without tight coupling.

`Location: Assets/01_Game/08_UI`

## 7. Asset & Data Model
- **ScriptableObjects**: Heavily used for data definitions (`ItemDefinition`, `LootTable`, `CharacterAppearanceProfile`).
- **UMA (Unity Multipurpose Avatar)**: Used for dynamic character and zombie generation, allowing for varied clothing and physical features.
- **Save Data**: A JSON or binary-based system handled by `SaveManager`, tracking `ItemSaveRegistry` and unit states.
- **Asset Organization**:
    - `01_Game`: All source code and core logic.
    - `Art`: Textures, materials, and UI sprites.
    - `ThirdParty`: External plugins (MapMagic, Microsplat, Enviro, EasyRoads3D).

## 8. Notes, Caveats & Gotchas
- **World Loading Sequence**: The `GameManager` enforces a strict loading pipeline: Terrain -> NavMesh -> Roads -> Buildings -> Units. Changing the initialization order of these systems can break dependencies.
- **Spatial Grid**: `UnitManager` uses a manual spatial grid (CellSize: 10f) for performance. High-speed units or large radii queries should be tested against this cell size.
- **UMA Integration**: Character visuals are asynchronous. `GameManager` includes polling logic to wait for UMA avatars to finish building before activating gameplay.
- **Event Bus Queueing**: If `useQueuedMode` is enabled on the `CoreEventBus`, events are deferred to `Update()`. This is useful for networking/replay but can introduce a one-frame latency.