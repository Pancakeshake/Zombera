# PROJECT CONTEXT

This document describes the current development state of the project.

AI tools should read this file before generating code.

This file works together with:

game_design.md
architecture.md
copilot_prompt.md

These define the overall design and architecture.

This file describes the CURRENT state of the project.

---

# PROJECT STATUS

Development stage:

Early Prototype

Goal:

Build the first playable version of the core gameplay loop.

Core gameplay loop:

Explore
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

The current focus is implementing the systems required for this loop.

---

# CURRENT SYSTEM PRIORITIES

Systems should be built in this order:

1 Player movement
2 Combat system
3 Zombie AI
4 Loot system
5 Squad system
6 Inventory system
7 Base storage
8 Debug tools

Avoid implementing large world systems until these are working.

---

# SYSTEM IMPLEMENTATION STATUS

Movement System
Status: Planned

Combat System
Status: Planned

Zombie AI
Status: Planned

Loot System
Status: Planned

Squad System
Status: Planned

Inventory System
Status: Planned

Base System
Status: Planned

World Generation
Status: Not Started

Chunk Streaming
Status: Not Started

Save System
Status: Not Started

---

# CURRENT PLAYABLE GOAL

The first playable build should allow the player to:

Move around a small map.

Fight a small group of zombies.

Open loot containers.

Recruit one survivor.

Store items in a base storage crate.

This is the minimum viable gameplay loop.

---

# CURRENT WORLD SETUP

The prototype world should contain:

1 small town
5 houses
1 police station
1 safe base

Zombie population:

10 to 20 zombies.

Initial squad size:

1 player
1 recruitable survivor.

---

# CURRENT PLAYER START

Player starts alone.

Starting items:

Pistol
10 ammo
Bandage
Food

Nearby locations:

Small house
Basic loot containers
Few zombies

---

# DEBUG TOOLS REQUIRED

Debug tools should be implemented early to speed up development.

Required debug features:

Spawn zombie
Spawn survivor
Spawn loot
Spawn zombie horde
Toggle god mode
Toggle slow motion

Debug hotkeys:

F1 debug menu
F2 slow motion
F4 spawn zombie
F5 spawn survivor
F6 god mode

---

# AI DESIGN NOTES

Zombie AI should remain simple initially.

Initial states:

Idle
Wander
Chase
Attack

Later versions may include:

Horde coordination
Special zombie abilities
Advanced threat detection

---

# CURRENT UNIT TYPES

Units currently planned:

Player
Survivor
Zombie

All units must use the same Unit architecture.

Unit components:

UnitController
UnitHealth
UnitCombat
UnitInventory
UnitStats

---

# UI STATUS

Currently implemented:

Main menu
Character creator

Planned UI:

HUD
Squad panel
Command panel
Inventory screen
Base storage UI

UI should remain simple in the prototype phase.

---

# DEVELOPMENT RULES

Prioritize playable gameplay over system completeness.

Implement simple versions of systems first.

Avoid implementing advanced features too early.

Avoid premature optimization.

Use debug tools to test systems quickly.

---

# NEXT SYSTEM TO IMPLEMENT

Next system:

Player movement system.

Requirements:

Top-down movement.

Keyboard input.

Basic camera follow.

UnitController component should handle movement.

---

# NOTES FOR AI ASSISTANTS

When generating code:

Follow the architecture defined in architecture.md.

Respect the system order defined above.

Generate modular scripts with clear responsibilities.

Prefer scaffolding and TODO comments when unsure.

Do not introduce large new systems unless requested.
