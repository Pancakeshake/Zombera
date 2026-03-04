🎯 Minimum Viable Game Loop (MVGL)

The smallest playable loop for your game should be:

Explore
↓
Fight zombies
↓
Loot items
↓
Recruit survivor
↓
Return to base
↓
Upgrade squad
↓
Go back out

That’s it.

Everything else is optional early on.

🧩 MVGL Gameplay Example

A 5–10 minute play session should look like this:

1️⃣ Start with 1 player + 1 survivor

2️⃣ Walk into a small town

3️⃣ Fight 5–10 zombies

4️⃣ Open a loot container

5️⃣ Find ammo or weapon

6️⃣ Rescue another survivor

7️⃣ Return to base storage

8️⃣ Go explore again

If this feels good → your game works.

🧱 Minimum Systems Required

To support the MVGL you only need 7 systems.

1️⃣ Character Controller

Move and shoot.

UnitController
UnitCombat
UnitHealth
2️⃣ Zombie AI

Basic enemy behavior.

Idle
Chase
Attack
3️⃣ Squad System

Just 2–3 units.

follow leader
auto attack

No formations yet.

4️⃣ Loot System

One container type.

Open container
↓
Get item

Items can just be:

pistol
ammo
bandage
food
5️⃣ Inventory

Very simple.

list inventory
weight limit
equip weapon
6️⃣ Recruitment

One recruit method.

rescue survivor
↓
join squad

No settlements yet.

7️⃣ Base Storage

Just one base object.

storage crate

Drop off loot.

🗺 MVGL Map

You only need one tiny map.

Example:

Town
 ├ 5 houses
 ├ 1 police station
 ├ 1 small base

No chunk streaming yet.

🧟 MVGL Enemies

Only 2 zombie types.

slow zombie
runner

That’s enough to test combat.

👥 MVGL Squad

Max squad:

4 units

Later you expand to 30.

🎮 MVGL Playtime

A good prototype should support:

10–15 minutes gameplay

before repeating.

That’s enough to test if the loop works.

⚡ What You Do After MVGL Works

Once the loop is fun, you add systems:

1️⃣ squad commands
2️⃣ formations
3️⃣ world streaming
4️⃣ base building
5️⃣ advanced zombies
6️⃣ world events

The important rule:

Never expand systems before the core loop works.

🧠 Your Final Prototype Goal

Your first playable build should feel like:

Tiny Kenshi + zombie survival

One small map
Few survivors
Few zombies
But the loop works.

✅ At that point you know the game idea works.

If you'd like, I can also show you the 3 biggest design traps that kill games like this, so you can avoid them early.

---

## Gameplay Flow Architecture (Event-Driven)

Use layered flow:

Player Input
↓
Character / Squad Systems
↓
Gameplay Systems
↓
World Systems
↓
Persistence Systems

### Core Principle

Systems communicate primarily through managers and events, not direct references.

### Top-Level Managers

- GameManager
- WorldManager
- SquadManager
- ZombieManager
- LootManager
- BaseManager
- SaveManager

### Shared Gameplay Events

- OnUnitDeath
- OnLootGenerated
- OnZombieSpawn
- OnBuildingComplete
- OnSquadCommandIssued
- OnWorldSimulationTick

### Canonical Flows

Player combat flow:

Player Input
↓
UnitController
↓
CombatSystem
↓
TargetingSystem
↓
DamageSystem
↓
UnitHealth
↓
UnitDeath
↓
LootManager

Zombie flow:

ZombieSpawner
↓
ZombieAI
↓
UtilitySystem
↓
ZombieStateMachine
↓
CombatSystem
↓
DamageSystem

Squad command flow:

Player Input
↓
CommandSystem
↓
SquadManager
↓
FormationController
↓
UnitController

### Performance Defaults

- Squad AI tick: 0.2s
- Zombie AI tick: 0.4s
- World simulation pulse: 10s
- Chunk streaming: active 3x3 (radius 1)
- Zombie lifecycle: pooled spawn/despawn path

### Data / Logic Separation

Data layer: ScriptableObjects

Logic layer: MonoBehaviour systems

Examples:

- WeaponData → WeaponSystem
- ItemData → Inventory systems
- ZombieType → Zombie systems


🧠 The Key Rule for Your Game

Follow this rule throughout development:

Always expand depth before size.

Example progression:

Bad:

10 towns
100 items
20 zombie types

Good:

1 town
4 zombie types
10 items
but deep mechanics

Depth makes games fun.