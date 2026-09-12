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
- The World HUD F2 inventory panel (built by `Tools/1.Quick Dev Tools/Build World HUD`) now uses `InventoryPanelController` to bind selected-unit `UnitInventory.Items` into `InvSlot_*` tiles with search/filter support.

## Bow / arrow / inventory item pipelines (manual)

Editor menus under **`Tools/Zombera/Inventory/...`** (Bow bootstrap, generic creators, Arrow pipeline, etc.) were removed. Create or duplicate `ItemDefinition` / weapon / pickup assets in the Project window and wire references in the Inspector (or restore the old editor scripts from git history if you need one-click scaffolding again).

## Inventory Icon Render Tooling ✅ Repeatable
- Use `Tools/Zombera/Inventory Icons/Open Or Create Icon Render Scene` to open a dedicated icon capture scene and avoid one-off camera setups in gameplay scenes.
- Use `Tools/Zombera/Inventory Icons/Setup Render Rig In Active Scene` to ensure a standardized orthographic camera, transparent background, neutral key/fill light rig, and capture render texture wiring.
- Use `Tools/Zombera/Inventory Icons/Capture Icon For Selected Prefab` for one-off captures and `Tools/Zombera/Inventory Icons/Batch Capture Icons (Selected Folder)` to render/export all prefabs in a selected folder.
- Captures are exported to `Assets/Art/InventoryIcons/Generated` as PNG sprites; the shared render texture asset is `Assets/Art/InventoryIcons/IconCaptureRT.renderTexture`.

## World / art / squad / building editor tooling (manual)

One-shot **Tools/Zombera** commands for **URP material batch-fix** (`Art`), **HDRI skybox**, **fog of war**, **startup squad defaults**, and **Post Apocalyptic → modular prefab import** were removed. Do that work in the Lighting / Materials / scene hierarchy / `PlayerSpawner` Inspector as needed, or restore the deleted scripts from git.

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
