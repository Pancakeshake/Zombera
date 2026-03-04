# GAME DESIGN DOCUMENT

## PROJECT OVERVIEW

Project Name: TBD

Genre:
Top-down sandbox survival squad game.

Inspiration:
Kenshi
Project Zomboid

Core fantasy:
Lead a squad of survivors in a zombie apocalypse.
Explore, recruit survivors, loot gear, build bases, and survive increasingly dangerous zombie threats.

---

# CORE GAME LOOP

Explore world
↓
Fight zombies
↓
Loot items
↓
Recruit survivors
↓
Return to base
↓
Upgrade squad
↓
Repeat

The game should always support this loop.

---

# PLAYER EXPERIENCE

Start alone in the world.

Early gameplay:
- explore small town
- fight zombies
- find loot
- recruit survivors

Mid game:
- manage squad
- explore larger areas
- defend base

Late game:
- large squad
- massive zombie hordes
- multiple bases

---

# GAME WORLD

World type:
Procedural hybrid world.

Regions:
- Wilderness
- Towns
- Cities
- Military zones

World features:
- houses
- hospitals
- police stations
- farms
- military bases

World uses chunk streaming.

Chunk size recommendation:
32x32 tiles.

Active chunk grid:
3x3 around player.

---

# PLAYER SYSTEM

Player is a Unit.

Player can:
- move
- shoot
- melee
- use items
- command squad

Player can switch control between squad members.

---

# SQUAD SYSTEM

Max squad size: 30 units.

Each squad member is a Unit.

Roles:
- shooter
- melee
- medic
- engineer
- leader

Squad commands:
Move
Attack
Hold
Follow
Defend

Exploration movement styles:
- blob follow
- loose formation
- ordered march

Combat formations:
- line
- wedge
- defensive circle

---

# ZOMBIE SYSTEM

Zombie types (initial):

Slow zombie
Runner
Tank
Screamer
Spitter

Zombie AI uses:

Utility AI
+
State machine

States:
Idle
Wander
Chase
Attack
Investigate noise

Zombie hordes can spawn dynamically.

---

# UNIT ARCHITECTURE

All characters use the same base Unit system.

Unit components:

UnitController
UnitHealth
UnitCombat
UnitInventory
UnitStats

Optional components:

PlayerInput
SquadAI
ZombieAI
SurvivorAI

---

# COMBAT SYSTEM

Combat types:

Guns
Melee
Throwables

Weapons:

Pistol
Rifle
Shotgun
Melee weapons
Grenades

Combat architecture:

TargetingSystem
DamageSystem
WeaponSystem
ProjectileSystem

---

# INVENTORY SYSTEM

Inventory uses:

List-based system
Weight limit
Equipment slots

Items include:

Weapons
Ammo
Food
Medical supplies
Materials

Inventory types:

Player inventory
Squad inventory
Base storage

---

# LOOT SYSTEM

Loot containers spawn items using loot tables.

Container examples:

Fridge
Cabinet
Locker
Gun crate
Medical cabinet

Loot is generated when container opens.

---

# RECRUITMENT SYSTEM

Survivors can join squad through:

Rescue
Hiring in settlements
Random wanderers
Captured prisoners

Each survivor has:

Stats
Traits
Morale

---

# BASE BUILDING

Uses blueprint construction system.

Construction flow:

Place blueprint
↓
Assign workers
↓
Bring materials
↓
Build progress
↓
Complete structure

Base features:

Storage
Crafting
Medical
Defense

---

# WORLD EVENTS

Dynamic world events:

Zombie hordes
Survivor encounters
Supply drops
Raids

World simulation tick:
10 seconds.

---

# SAVE SYSTEM

Save data includes:

Player
Squad
World chunks
Zombies
Loot containers
Bases

Save system stores data models rather than scene objects.

---

# UI STRUCTURE

Main UI components:

HUD
Squad panel
Command panel
Minimap
Inventory
Base UI

Menus:

Main menu
Character creator
Settings

---

# DEBUG SYSTEM

Debug tools include:

Debug menu
Spawn tools
AI state viewer
Pathfinding visualization
Performance monitor
Stress tests

Debug hotkeys:

F1 debug menu
F2 slow motion
F3 toggle AI debug
F4 spawn zombie
F5 spawn survivor
F6 god mode

---

# PERFORMANCE TARGETS

Target:

30 squad units
200+ zombies

AI must use tick system instead of frame updates.

Recommended AI tick rates:

Squad AI: 0.2 seconds
Zombie AI: 0.4 seconds

---

# DEVELOPMENT PRINCIPLES

Keep systems modular.

Small scripts preferred.

Avoid monolithic managers.

Use ScriptableObjects for game data.

Design systems so AI tools can easily generate and refactor code.

---

# FUTURE FEATURES

Vehicles
Co-op multiplayer
NPC factions
Trading
Story missions

These are post-MVP features.
