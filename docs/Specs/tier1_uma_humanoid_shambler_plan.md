# Tier 1 UMA Humanoid Shambler Plan (Remaining Manual Work)

This file now lists only what still needs to be done in Unity/editor after code-side implementation.

## Already implemented in code
1. Created baseline assets and folders:
   - Assets/Prefabs/Zombies/Zombie_UMA_Tier1.prefab
   - Assets/Animations/Zombies/Zombie_Tier1.controller
   - Assets/ScriptableObjects/ZombieTypes/ZT_Tier1_Shambler.asset
2. Added ZombieType stat application at spawn in AI ZombieSpawner.
3. Added zombie UMA randomization runtime hooks:
   - ZombieUmaVisualProfile ScriptableObject class
   - ZombieUmaAppearance component
4. Added zombie animation event bridge:
   - ZombieAnimationController component
   - Combat and health event trigger support
5. Prewired Boot ZombieManager ambientZombieType to ZT_Tier1_Shambler.
6. Implemented base Tier 1 animator scaffold in assets:
   - Added parameters: Speed, AttackTrigger, HitTrigger, DieTrigger, IsDead
   - Added states: Idle, Locomotion, Attack, Hit, Die
   - Added trigger-driven AnyState transitions for attack/hit/die
   - Wired zombie prefab UMA animationController to Zombie_Tier1.controller

## Remaining work you need to do

## 1) Finalize the zombie prefab in Unity
1. Open Assets/Prefabs/Zombies/Zombie_UMA_Tier1.prefab.
2. Remove/disable player-only components (especially PlayerInputController).
3. Ensure these components exist and are configured:
   - Unit (Role = Zombie)
   - UnitController
   - UnitHealth
   - UnitCombat
   - UnitStats
   - NavMeshAgent
   - ZombieAI
   - ZombieStateMachine
   - ZombieAnimationController
   - ZombieUmaAppearance
   - DynamicCharacterAvatar

## 2) Optional animator polish in Unity
1. Open Assets/Animations/Zombies/Zombie_Tier1.controller.
2. Review the current scaffolded states/transitions and tune timings.
3. Replace placeholder Attack/Hit/Die motions with your preferred clips.
4. Optionally add IsDead bool-based transition rules in addition to DieTrigger.

## 3) Create visual profile assets for randomization
1. Create 1 to 3 ZombieUmaVisualProfile assets in Assets/ScriptableObjects/ZombieVisualProfiles.
2. For each profile, fill small pools only:
   - raceNames (HumanMaleDCS/HumanFemaleDCS)
   - recipeAssets and/or recipeStrings
   - uniformScaleRange with tight range
3. Assign these profiles to ZombieUmaAppearance profilePool (or fallbackProfile).

## 4) Wire scene references
1. World scene:
   - Add/confirm AI ZombieSpawner exists.
   - Assign zombiePrefab = Zombie_UMA_Tier1.prefab.
2. WorldEventSystem:
   - Assign eventZombieType = ZT_Tier1_Shambler.
3. World.Spawning.ZombieSpawner:
   - Assign defaultZombieType = ZT_Tier1_Shambler.
4. Debug spawn tools (if used in your scene/prefab):
   - Assign debugZombieType = ZT_Tier1_Shambler.

## 5) Validate in play mode
1. Press F4 and confirm spawned zombies are UMA humanoids.
2. Confirm walk locomotion uses Speed-driven animation.
3. Confirm attack/hit/death triggers play during combat.
4. Confirm ambient/horde spawns use ZT_Tier1_Shambler.
5. Confirm no warnings about missing zombie prefab fallback in normal flow.

## Done criteria
1. Zombies spawn as UMA humanoid prefabs, not primitive runtime prototypes.
2. One shared controller handles Tier 1 shambler animation set.
3. Zombies randomize from a small controlled appearance pool.
4. Combat loop shows correct attack/hit/death responses.
