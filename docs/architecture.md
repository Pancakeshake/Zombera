# PROJECT ARCHITECTURE

This document describes the technical architecture for the game.

The project uses Unity with a modular C# architecture.

Design goals:

- modular systems
- small scripts
- reusable components
- AI-friendly structure
- scalable sandbox architecture

---

# CORE ARCHITECTURE LAYERS

The game follows a layered architecture.

Input Layer
↓
Unit Layer
↓
Gameplay Systems
↓
AI Systems
↓
World Systems
↓
Persistence Systems

Each layer communicates through managers or events.

---

# CORE MANAGERS

Core managers coordinate systems.

GameManager
UnitManager
CombatManager
AIManager
WorldManager
DebugManager
SaveManager

Managers should not contain gameplay logic.
They only coordinate systems.

---

# UNIT ARCHITECTURE

All characters in the game share the same Unit architecture.

Unit prefab structure:

Unit
 ├ UnitController
 ├ UnitHealth
 ├ UnitCombat
 ├ UnitInventory
 ├ UnitStats
 └ AIComponent (optional)

Unit types include:

Player
Survivor
Zombie
Future factions

All units must use the same combat and health systems.

---

# COMPONENT DESIGN PRINCIPLES

Each system must be implemented as small components.

Example:

UnitController
Handles movement and navigation.

UnitHealth
Handles damage and death.

UnitCombat
Handles attacking and weapon usage.

UnitInventory
Handles item storage and equipment.

UnitStats
Stores attributes such as strength or shooting skill.

---

# AI ARCHITECTURE

AI uses a hybrid system:

Utility AI
+
State Machine

AI structure:

AIComponent
 ├ Sensors
 ├ UtilityEvaluator
 └ StateMachine

Sensors detect world information.

Utility system scores possible actions.

State machine executes behaviors.

Example states:

Idle
Wander
Chase
Attack
Follow

AI must use tick updates rather than frame updates.

---

# COMBAT ARCHITECTURE

All combat logic flows through the CombatManager.

Combat systems:

CombatManager
TargetingSystem
DamageSystem
WeaponSystem
ProjectileSystem

Combat flow example:

UnitCombat
↓
CombatManager
↓
TargetingSystem
↓
DamageSystem
↓
UnitHealth

---

# WORLD ARCHITECTURE

The world is managed by WorldManager.

World systems:

WorldManager
MapSpawner
ZombieSpawner
LootSpawner
RegionSystem
WorldEventSystem

The world eventually uses chunk streaming.

Chunk system:

ChunkLoader
ChunkGenerator
ChunkCache

Prototype versions may use static maps.

---

# SQUAD ARCHITECTURE

Squads are managed by SquadManager.

Squad systems:

SquadManager
SquadMember
FollowController
FormationController
CommandSystem

Squad commands:

Move
Attack
Hold
Follow
Defend

---

# INVENTORY ARCHITECTURE

Inventory systems:

InventoryManager
ItemDefinition
EquipmentSystem
LootTable
LootContainer

Item data should be stored using ScriptableObjects.

Inventory types:

Player inventory
Squad inventory
Base storage

---

# BASE BUILDING ARCHITECTURE

Base building uses blueprint construction.

Systems:

BuildManager
Blueprint
ConstructionJob
WorkerAI
BaseStorage

Construction flow:

Place blueprint
↓
Assign worker
↓
Bring materials
↓
Construction progress
↓
Building complete

---

# SAVE SYSTEM ARCHITECTURE

Save system stores world state.

SaveManager collects data from:

Player
Squad
WorldChunks
Bases
LootContainers
Zombies

Only data models should be saved.
Do not serialize scene objects directly.

---

# DEBUG SYSTEM ARCHITECTURE

Debug tools are centralized in DebugManager.

Debug systems:

DebugMenu
SpawnDebugTools
AISimulationTools
PerformanceMonitor
AIVisualizer

Debug hotkeys:

F1 debug menu
F2 slow motion
F3 toggle AI debug
F4 spawn zombie
F5 spawn survivor
F6 god mode

---

# UI ARCHITECTURE

UI is divided into two main groups.

Menus:

MainMenu
CharacterCreator
Settings

HUD:

SquadPanel
CommandPanel
Minimap
PlayerStatus
Hotbar
AlertPanel

Each UI panel should be implemented as a prefab.

UI systems should be coordinated through HUDManager.

---

# FOLDER STRUCTURE

Assets

Core
World
Characters
AI
Combat
Inventory
BaseBuilding
Systems
UI
Debug
Prefabs
ScriptableObjects
Scenes
Art

Systems should be grouped by functionality.

Avoid large generic script folders.

---

# SCRIPT DESIGN RULES

Scripts should follow these rules:

Keep scripts under 300 lines where possible.

Use descriptive names.

Separate data and behavior.

Use ScriptableObjects for configuration.

Prefer composition over inheritance.

---

# PERFORMANCE GUIDELINES

Target performance:

30 squad units
200+ zombies

AI must use tick updates rather than per-frame logic.

Recommended tick intervals:

Squad AI: 0.2 seconds
Zombie AI: 0.4 seconds
World simulation: 10 seconds

---

# DEVELOPMENT WORKFLOW

Development should follow this order:

Movement
Combat
Zombie AI
Loot
Squad system
World expansion
Base building
Save system

Avoid building large systems before core gameplay works.
