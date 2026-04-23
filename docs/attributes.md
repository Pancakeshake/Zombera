# Zombera Attributes System

## Overview

All attributes live on `UnitStats.cs` as `int` fields (1–100 range, `MinSkillLevel`/`MaxSkillLevel`).  
Accessed via `GetSkillValue(UnitSkillType)` and set via `SetSkill(UnitSkillType, int)`.  
The `UnitSkillType` enum defines all valid types.

---

## Attributes

### Strength ✅ Fully Implemented
- **Effects:** Scales all outgoing damage (1×–3×). Adds max HP per level (0.2%/level). HP stacks multiplicatively with Toughness and Constitution.
- **XP Sources:** Heavy carry walking, combat hits (unarmed bonus), weight training (T key hotkey).
- **Key methods:** `ApplyStrengthDamageScaling()`, `AddStrengthExperience()`, `SetStrengthBaseHealth()`.

---

### Toughness ✅ Fully Implemented
- **Effects:** Reduces incoming damage 0–50% (level 1–100). Small max HP bonus (stacks with Strength × Constitution).
- **XP Sources:** `RecordDamageTaken(float)` — called automatically in `UnitHealth.TakeDamage()`.
- **Key methods:** `GetToughnessDamageReduction()`, `ApplyToughnessDamageReduction()`.
- **Wired:** `UnitHealth.TakeDamage()` applies reduction before dealing damage and then awards XP.

### Constitution ✅ Fully Implemented
- **Effects:** Large max HP scaling (0.5%/level). HP stacks multiplicatively with Strength × Toughness.
- **XP Sources:** `RecordMealConsumed()`, `RecordVitaminConsumed()`, `RecordDamageTaken()` (small award).
- **Still needs call sites:** Food/consumable system doesn't exist yet — call `RecordMealConsumed()` from it when built.

---

### Shooting ✅ Fully Wired
- **Effects (implemented):** Damage multiplier 1×–1.5× applied to all ranged hits. Effective range scales 1×–1.5× (level 1–100). Shots fired beyond effective range have a miss-chance roll that Shooting skill reduces (0% miss at/within range, up to 70% miss heavily out-of-range for a level-1 shooter vs 40% for level-100). Shooting contributes 30% of the accuracy formula in encounter combat.
- **XP Sources:** `RecordRangedHit()` called in `WeaponSystem.FireProjectileAt()`.
- **Key methods:** `ApplyShootingDamageScaling()`, `GetShootingEffectiveRangeMultiplier()`, `GetShootingHitChanceBonus()`.

### Melee ✅ Damage Wired
- **Effects (implemented):** Damage multiplier 1×–1.75× applied to all melee hits. Used in hit accuracy formula (60% Melee + 40% Strength).
- **Effects (TODO):** Attack speed, knockback chance — not wired yet.
- **XP Sources:** `RecordMeleeHit()` — called automatically in `WeaponSystem`, `UnitCombat`, `CombatEncounterManager`.
- **Key methods:** `ApplyMeleeDamageScaling()`.

### Morale ❌ Removed
- **Effects:** Removed this session. Accuracy/evasion/speed modifiers and MoraleChanged event all deleted.

### Medical ✅ Progression + Heal Wired
- **Effects (implemented):** Heal-amount multiplier 1×–1.5× (`GetMedicalHealMultiplier()`). Award Medical XP proportional to heal applied.
- **XP Sources:** `RecordHealApplied(float)` — called inside `UnitHealth.Heal(float, UnitStats)` overload.
- **Key methods:** `GetMedicalHealMultiplier()`, `RecordHealApplied(float)`, `AddMedicalExperience(float)`, `MedicalLeveledUp` event.

### Engineering ✅ Progression + Build XP Wired
- **Effects (implemented):** XP awarded on each wall placement via `BuildPlacementController`. Full progression (100 levels, level-up event).
- **XP Sources:** `RecordBuildPiecePlaced()` — called in `BuildPlacementController.TryPlaceSelectedWall()`. Assign `placerStats` in Inspector.
- **Key methods:** `RecordBuildPiecePlaced()`, `AddEngineeringExperience(float)`, `EngineeringLeveledUp` event.

---

### Agility ✅ Speed, Dodge & XP Wired
- **Effects (implemented):** Move speed multiplier 1×–1.4× baked into `UnitController.RefreshAppliedSpeed()`. Dodge chance 0–25% rolled after hit resolution in `CombatEncounterManager` (`GetAgilityDodgeChance()`). XP awarded while sprinting.
- **XP Sources:** `RecordSprintDistance(float)` — called each frame in `UnitController.TickStamina()` while sprinting.
- **Key methods:** `GetAgilityMoveSpeedMultiplier()`, `GetAgilityDodgeChance()`.

### Endurance ✅ Stamina Pool & Regen Wired
- **Effects (implemented):** `MaxStamina = baseMaxStamina × GetEnduranceStaminaMultiplier()` (1×–2×). `StaminaRegenPerSecondIdle` and `StaminaRegenPerSecondWalk` properties now multiply raw rate by `GetEnduranceRegenMultiplier()` (1×–1.75×). XP awarded during exertion.
- **XP Sources:** `RecordExertionTime(float)` — called each frame while sprinting in `UnitController.TickStamina()`.
- **Key methods:** `GetEnduranceStaminaMultiplier()`, `GetEnduranceRegenMultiplier()`.

### Scavenging ✅ XP Wired, Loot Bonus Active
- **Effects (implemented):** XP awarded on container search. `GetScavengingLootMultiplier()` applies to `LootContainer.OpenContainer()` roll count (1×–1.5×).
- **XP Sources:** `RecordContainerSearched()` — called in `ContainerInteractor.Interact()`.
- **Key methods:** `GetScavengingLootMultiplier()`.

### Stealth ✅ XP, Detection & Evasion Wired
- **Effects (implemented):** XP accumulates per-frame when the unit evades `EnemySensor` detection. `GetStealthDetectionRadiusMultiplier()` now reduces effective detection radius in `EnemySensor.Sense()`. Units beyond their stealth-adjusted range are removed from the enemy buffer and awarded stealth XP.
- **Key methods:** `GetStealthDetectionRadiusMultiplier()`, `RecordUndetectedTime(float)`.

---

## Stamina System ✅ Implemented
- `MaxStamina = baseMaxStamina × GetEnduranceStaminaMultiplier()`
- Sprint (hold Left Shift while moving): drains `staminaDrainPerSecondSprint` (default 15/s)
- Regen: `staminaRegenDelaySeconds` after last sprint, then `StaminaRegenPerSecondIdle` or `StaminaRegenPerSecondWalk` — both scaled by `GetEnduranceRegenMultiplier()`
- Sprint auto-stops when stamina hits 0
- `StaminaChanged` event fires on every drain/regen tick, bound to HUD via `PlayerStatusController.BindUnit()`
- HUD health and stamina bars live-update via `UnitHealth.Damaged/Healed` and `UnitStats.StaminaChanged` events
- `PlayerSpawner` calls `HUDManager.BindPlayerUnit(Unit)` after spawn to connect the pipeline

## Loot Container Interaction ✅ Implemented
- **E key** to loot nearest container within `interactRadius = 2.5m`
- `ContainerInteractor` component auto-added to player by `PlayerSpawner`
- Awards Scavenging XP on search, fires `ContainerLootedEvent`
- Loot transfers into `UnitInventory` (the component the squad management UI reads)

## Inventory UI ✅ Wired to Live Data
- Press **I** or **F2** to open the inventory tab in the squad management screen
- `ZomberaSquadManagementUI.OpenInventoryTab()` always calls `PopulateInitialData()` on open, pulling the latest `UnitInventory.Items` from the selected survivor
- `InventoryTabController.SetSlots()` renders a 6-column scrollable grid of all carried items
- The World HUD F2 inventory panel (built by `Tools/Zombera/World UI/Build World HUD`) now uses `InventoryPanelController` to bind selected-unit `UnitInventory.Items` into `InvSlot_*` tiles with search/filter support.

## Bow Gameplay Setup Tooling ✅ Repeatable
- Use `Tools/Zombera/Inventory/Bow/Tools/Bootstrap Bow Gameplay Pipeline (Icon + Equip + Pickup)` to create/link `Item_Bow`, `Weapon_Bow`, and `Pickup_Bow` baseline assets.
- `Tools/Zombera/Inventory/Bow/Tools/Bootstrap Item Icon Pipeline (Bow)` is retained as a compatibility alias to the full Bow gameplay bootstrap.
- Use `Tools/Zombera/Inventory/Bow/Tools/Assign Selected Sprite To Bow Item` to update only `ItemDefinition.inventoryIcon`.
- Use `Tools/Zombera/Inventory/Bow/Tools/Assign Selected Prefab To Bow Equipped Visual` to set `ItemDefinition.equippedVisualPrefab` (Prefab or FBX model).
- Use `Tools/Zombera/Inventory/Bow/Tools/Assign Selected Prefab To Bow World Pickup Visual` to set `ItemDefinition.worldPickupPrefab` (Prefab or FBX model).
- Use `Tools/Zombera/Inventory/Bow/Tools/Wire Bow Systems On Selected Unit` to ensure selected unit wiring (`WeaponSystem`, `EquipmentSystem`, `ItemPickupInteractor`, `Socket_RightHand`) before playtesting.
- If Bow visual slots are empty, bootstrap and wiring now prefer `Assets/ThirdParty/Free medieval weapons/Models/Wooden Bow.fbx` and fall back to `Assets/ThirdParty/Free medieval weapons/Prefabs/Wooden Bow.prefab` so the Bow has a usable 3D model without manual assignment.
- `Wire Bow Systems On Selected Unit` also assigns `Assets/ThirdParty/Free medieval weapons/Prefabs/Arrow.prefab` as the bow projectile visual on `WeaponSystem` so fired arrows render consistently.

## Inventory Generic Creator Tooling ✅ Repeatable
- Use `Tools/Zombera/Inventory/Creators/Tools/...` to generate baseline item templates for Generic, Weapon (+ `WeaponData`), Ammo, Medical, Food, Vitamin, and Material categories.
- Use these creators as first-pass asset scaffolding, then rename item IDs/display names and fine-tune values in Inspector.

## Arrow Setup Tooling ✅ Repeatable
- Use `Tools/Zombera/Inventory/Arrow/Tools/Bootstrap Arrow Item Pipeline (Item + Pickup)` to ensure `Item_Arrow` and `Pickup_Arrow` are created and wired.
- Use `Tools/Zombera/Inventory/Arrow/Tools/Create Arrow Item Template` for additional arrow-ammo variants.
- Use `Tools/Zombera/Inventory/Arrow/Tools/Assign Selected Sprite To Arrow Item` and `Tools/Zombera/Inventory/Arrow/Tools/Assign Selected Prefab To Arrow World Pickup Visual` for visual assignment.

## Inventory Icon Render Tooling ✅ Repeatable
- Use `Tools/Zombera/Inventory Icons/Open Or Create Icon Render Scene` to open a dedicated icon capture scene and avoid one-off camera setups in gameplay scenes.
- Use `Tools/Zombera/Inventory Icons/Setup Render Rig In Active Scene` to ensure a standardized orthographic camera, transparent background, neutral key/fill light rig, and capture render texture wiring.
- Use `Tools/Zombera/Inventory Icons/Capture Icon For Selected Prefab` for one-off captures and `Tools/Zombera/Inventory Icons/Batch Capture Icons (Selected Folder)` to render/export all prefabs in a selected folder.
- Captures are exported to `Assets/Art/InventoryIcons/Generated` as PNG sprites; the shared render texture asset is `Assets/Art/InventoryIcons/IconCaptureRT.renderTexture`.

## Free Medieval Weapons URP Fix Tooling ✅ Repeatable
- Use `Tools/Zombera/Art/Fix Free Medieval Weapons (URP Lit)` to batch-convert the `Assets/ThirdParty/Free medieval weapons` materials from incompatible shaders to URP Lit.
- The workflow also validates prefab renderer material links after conversion so null/invalid shader assignments are surfaced in one pass.
- Re-run the tool whenever that third-party pack is reimported or updated and materials revert to purple/magenta.

## World HDRI Setup Tooling ✅ Repeatable
- Use `Tools/Zombera/World/Environment/Apply HDRI Skybox To World` to wire `Assets/HDR/overcast_soil_puresky_2k.hdr` into the World scene skybox setup in one pass.
- The command ensures/creates `Assets/Art/Skybox_HDRI_World.mat`, applies it to `RenderSettings.skybox`, refreshes GI environment lighting, and wires `DayNightController.skyboxMaterial` references.
- Re-run this after HDRI swaps or world lighting resets to re-assert consistent skybox wiring.

## Fog Of War Setup Tooling ✅ Repeatable
- Use `Tools/Zombera/World/Fog Of War/Setup Fog Of War In Active Scene` to wire the runtime fog stack in the active scene with one command.
- The setup pass ensures one `FogOfWarSystem`, adds `FogOfWarVisionSource` to Player/Squad units, adds `FogOfWarVisionOverlay` to Player units, and adds `FogOfWarTarget` to hostile units (Zombie/Bandit/Enemy factions).
- Perception scaling is linear on `FogOfWarVisionSource`: level 1 = 25m range / 180 degree cone, level 100 = 100m range / 270 degree cone.
- `FogOfWarVisionOverlay` provides a dark world-space ring outside view range so the player can clearly read practical sight limits while moving.
- Use `Tools/Zombera/World/Fog Of War/Remove Fog Of War Components In Active Scene` to remove all fog components in-scene for rollback or clean retuning.

## Startup 20-Character Squad Tooling ✅ Repeatable
- Use `Tools/Zombera/Squad/Apply 20-Character Startup Test Squad Defaults` to configure `PlayerSpawner` for player-plus-squad startup testing in one pass.
- Startup roster is configured as 20 total characters (player + 19 squad NPCs) with all-stat tiers in roster order: `1, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100`.
- Use `Tools/Zombera/Squad/Validate Startup Test Squad Wiring` to verify prefab assignments, roster size, and skill-tier coverage before playtesting.
- Runtime startup squad spawning ensures members are commandable by enforcing `UnitRole.SquadMember`, `SquadMember`, `FollowController`, and `UnitController` nav-agent activation.

## Post Apocalyptic Modular Import Tooling ✅ Repeatable
- Use `Tools/Zombera/Building/Import Post Apocalyptic Prefabs To Modular Folder` to batch-convert prefabs from `Assets/ThirdParty/Post_Apocalyptic_Asset_Pack/Prefabs` into modular build prefabs under `Assets/BuildingSystem/Prefab_Modular`.
- Output naming is deterministic and flattened into the destination folder using a `PA_` prefix so reruns update existing generated assets instead of creating duplicates.
- Generated outputs wrap source visuals and add build-system components (`BuildPiece`, `StructureHealth`) plus a root collider (single-mesh `MeshCollider` when possible, otherwise a bounds-based `BoxCollider`).
- Source child colliders are stripped during conversion so modular placement and damage interactions use the generated root collider path.

---

## Outstanding TODOs (Prioritised)

| Priority | Task |
|---|---|
| ✅ Done | Attack speed, knockback chance for Melee/Strength |
| ✅ Done | Desertion/flee AI for high-stress encounters |
| ✅ Done | Engineering: build-speed multiplier when timed placement is added |

---

## Implementation Pattern (reference)

All XP attributes follow the same pattern from Strength:
- Inspector fields: `xxxXpBaseRequirement`, `xxxXpRequirementGrowthPerLevel`
- Runtime field: `[SerializeField] private float xxxExperience`
- Method: `AddXxxExperience(float)` uses shared `AddExperience(ref float xp, ref int level, ...)`
- Event: `public event Action<int> XxxLeveledUp`
- Getter: `GetXxxBonusYyy()` using `SkillT(xxx)` lerp
