# Zombie Starter Stats Plan

Goal: use the same script stack as Zombie Type1, vary only ZombieType values first, then wire type assets into existing spawners.

## Current system constraints

- Runtime stats are applied from ZombieType in Assets/Scripts/AI/ZombieSpawner.cs (ConfigureSpawnedZombie).
- Current wiring points use a single ZombieType reference each:
  - Assets/Scripts/Systems/ZombieManager.cs: ambientZombieType
  - Assets/Scripts/World/WorldEventSystem.cs: eventZombieType
  - Assets/Scripts/World/Spawning/ZombieSpawner.cs: defaultZombieType
  - Assets/Scripts/Characters/PlayerSpawner.cs: validationZombieType
  - Assets/Scripts/Debug/DebugTools/SpawnDebugTools.cs: debugZombieType

Because these are single references, this starter pass should define all archetypes first, then choose one default per system. Multi-type random selection can be added as a follow-up.

## Starter archetypes from Assets/Models

Top-level folders detected:
- Civilian Zombie
- Construction Zombie
- Farmer
- Firefighter
- Hazmat
- Hospital patient
- Mechanic
- Military
- Nurse
- Swat_zombie

## Recommended first-pass ZombieType values

Notes:
- EffectiveTick is approximately defaultAiTickInterval / aggressionMultiplier.
- Lower EffectiveTick means faster AI reactions.
- Move speed range should stay inside ZombieController clamp defaults (1.2 to 2.0).

| TypeId | DisplayName | Model Folder | BaseHealth | MoveSpeed | AttackDamage | DefaultAiTick | Aggression | Perception | HordeAffinity | EffectiveTick |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| zombie.civilian.shambler | Civilian Shambler | Civilian Zombie | 50 | 1.60 | 7 | 0.42 | 0.90 | 14 | 0.55 | 0.467 |
| zombie.construction.brute | Construction Brute | Construction Zombie | 90 | 1.35 | 11 | 0.45 | 0.85 | 12 | 0.40 | 0.529 |
| zombie.farmer.reaper | Farmer Reaper | Farmer | 65 | 1.70 | 9 | 0.37 | 1.05 | 15 | 0.60 | 0.352 |
| zombie.firefighter.rusher | Firefighter Rusher | Firefighter | 80 | 1.75 | 10 | 0.32 | 1.20 | 17 | 0.75 | 0.267 |
| zombie.hazmat.tank | Hazmat Tank | Hazmat | 95 | 1.40 | 9 | 0.36 | 1.10 | 13 | 0.50 | 0.327 |
| zombie.patient.sprinter | Patient Sprinter | Hospital patient | 45 | 1.85 | 6 | 0.33 | 1.15 | 16 | 0.70 | 0.287 |
| zombie.mechanic.brawler | Mechanic Brawler | Mechanic | 70 | 1.65 | 10 | 0.36 | 1.10 | 15 | 0.60 | 0.327 |
| zombie.military.hunter | Military Hunter | Military | 100 | 1.90 | 12 | 0.28 | 1.40 | 20 | 0.90 | 0.200 |
| zombie.nurse.stalker | Nurse Stalker | Nurse | 55 | 1.75 | 7 | 0.34 | 1.25 | 18 | 0.85 | 0.272 |
| zombie.swat.juggernaut | SWAT Juggernaut | Swat_zombie | 130 | 1.55 | 14 | 0.30 | 1.35 | 19 | 0.95 | 0.222 |

## Prefab strategy (recommended)

Use this as the initial content pipeline:

1. Duplicate Assets/Prefabs/Zombies/Zombie Type1.prefab into one prefab per archetype under a new folder:
   - Assets/Prefabs/Zombies/Variants/Zombie_Civilian.prefab
   - Assets/Prefabs/Zombies/Variants/Zombie_Construction.prefab
   - ...

2. Keep all gameplay scripts and core components unchanged from Zombie Type1:
   - Unit
   - UnitController
   - UnitHealth
   - UnitStats
   - UnitCombat
   - ZombieController
   - ZombieStateMachine
   - ZombieAnimationController
   - NavMeshAgent

3. Swap only the visual mesh/model child for each variant using the corresponding FBX from Assets/Models.

4. Keep colliders, capsule center/height, and NavMeshAgent dimensions initially identical to Zombie Type1. Tune after first playtest.

## Wiring plan (phase 1)

After creating ZombieType assets and prefab variants:

1. Set a safe default ambient type:
   - ZombieManager.ambientZombieType = zombie.civilian.shambler

2. Set event horde type for pressure spikes:
   - WorldEventSystem.eventZombieType = zombie.military.hunter (or zombie.swat.juggernaut for harder starts)

3. Set region materialization default:
   - World/Spawning/ZombieSpawner.defaultZombieType = zombie.civilian.shambler

4. Set startup validation spawn type:
   - PlayerSpawner.validationZombieType = zombie.civilian.shambler

5. Set debug spawn type:
   - SpawnDebugTools.debugZombieType = whichever archetype you are tuning that session.

## Recommended follow-up (phase 2)

To use all 10 types dynamically without manual swaps:

1. Add weighted ZombieType lists to ZombieManager and WorldEventSystem (instead of single references).
2. Add optional ZombieTypeId -> Prefab map to AI/ZombieSpawner so visuals can vary by type.
3. Add region-based filtering (for example, harsher biomes bias Military/SWAT/Hazmat).

This keeps your script stack standardized while enabling visual and stat diversity.