# AI DEVELOPMENT GUIDE

This file provides instructions for AI tools assisting development of this Unity project.

AI should read this file together with:

game_design.md
architecture.md

These files define the mechanics and architecture of the game.

---

# PROJECT SUMMARY

This project is a top-down sandbox survival squad game.

Core gameplay includes:

- exploring a zombie apocalypse world
- fighting zombies
- looting resources
- recruiting survivors
- managing a squad
- building bases

The design is inspired by sandbox survival games with squad mechanics.

---

# DEVELOPMENT GOALS

The primary goal is to create a scalable sandbox architecture.

Key requirements:

- modular systems
- reusable components
- small scripts
- AI-friendly architecture
- performance capable of supporting large numbers of units

Target scale:

30 squad members
200+ zombies

---

# CODING STYLE

All code must follow these conventions.

Language: C#
Engine: Unity

Use the following practices:

Prefer composition over inheritance.

Use small modular scripts.

Avoid scripts larger than ~300 lines.

Use descriptive names.

Avoid monolithic classes.

Separate data and behavior.

---

# COMPONENT DESIGN

Gameplay systems should be implemented using Unity components.

Example component structure:

Unit
 ├ UnitController
 ├ UnitHealth
 ├ UnitCombat
 ├ UnitInventory
 ├ UnitStats

Each component has a single responsibility.

---

# SYSTEM DESIGN

Systems should follow manager-based architecture.

Managers coordinate systems but do not implement gameplay logic.

Examples:

GameManager
UnitManager
CombatManager
AIManager
WorldManager
DebugManager
SaveManager

Managers should communicate through events where possible.

---

# AI SYSTEMS

AI must use a hybrid architecture:

Utility AI
+
State Machine

Structure:

AIComponent
 ├ Sensors
 ├ UtilityEvaluator
 └ StateMachine

Avoid large behavior trees.

AI must support tick-based updates.

Example intervals:

Squad AI: 0.2 seconds
Zombie AI: 0.4 seconds

Avoid heavy per-frame updates.

---

# COMBAT SYSTEM

Combat must be centralized.

All damage flows through the CombatManager.

Structure:

CombatManager
TargetingSystem
DamageSystem
WeaponSystem
ProjectileSystem

Example flow:

UnitCombat → CombatManager → DamageSystem → UnitHealth

Avoid units damaging each other directly.

---

# WORLD SYSTEM

World systems must support procedural generation and chunk streaming.

Core systems:

WorldManager
ChunkLoader
ChunkGenerator
ZombieSpawner
LootSpawner
WorldEventSystem

Prototype versions may use a static map before chunk streaming is implemented.

---

# INVENTORY SYSTEM

Inventory must support:

player inventory
squad inventory
base storage

Items should use ScriptableObjects.

Example:

ItemDefinition
WeaponData
LootTableData

Inventory should use a list-based system with weight limits.

---

# SQUAD SYSTEM

Squad management must support up to 30 units.

Systems:

SquadManager
SquadMember
FollowController
FormationController
CommandSystem

Commands include:

Move
Attack
Hold
Follow
Defend

---

# BASE BUILDING SYSTEM

Base construction uses blueprint building.

Systems:

BuildManager
Blueprint
ConstructionJob
WorkerAI
BaseStorage

Construction flow:

Place blueprint
Assign worker
Deliver materials
Build progress
Complete structure

---

# DEBUG SYSTEM

Debugging tools are required for rapid development.

Systems include:

DebugManager
DebugMenu
SpawnDebugTools
AIVisualizer
PerformanceMonitor

Debug features must include:

Spawn zombie
Spawn survivor
Spawn loot
Spawn horde
Toggle god mode
Toggle slow motion

---

# UI SYSTEM

UI should be modular and panel-based.

Main menus:

MainMenu
CharacterCreator
Settings

Gameplay HUD panels:

SquadPanel
CommandPanel
Minimap
PlayerStatus
Hotbar
AlertPanel

Each panel should be implemented as a prefab.

UI coordination should be handled by HUDManager.

---

# PERFORMANCE REQUIREMENTS

The game must support:

30 squad units
200+ zombies

To achieve this:

AI must use tick updates.

Avoid heavy logic in Update().

Use object pooling for frequently spawned objects.

---

# DEVELOPMENT APPROACH

Development should prioritize playable prototypes.

System order:

1 Movement
2 Combat
3 Zombie AI
4 Loot
5 Squad system
6 World expansion
7 Base building
8 Save system

Avoid implementing complex systems before core gameplay works.

---

# AI ASSISTANT BEHAVIOR

When generating code, AI tools should:

Respect existing architecture.

Follow naming conventions defined in architecture.md.

Create modular scripts.

Add comments explaining responsibilities.

Avoid implementing placeholder systems incorrectly.

When unsure, generate scaffolding with TODO sections rather than guessing implementation.
