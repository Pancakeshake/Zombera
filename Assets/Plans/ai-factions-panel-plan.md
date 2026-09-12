# AI Plan: Factions System + Factions Panel From Current Infrastructure

## 1) Goal
Add a real factions system to your current game and expose it through a Factions panel that fits your existing HUD and squad-management infrastructure.

Target outcome:
- Move from the current hardcoded allegiance buckets to data-driven factions.
- Track faction standing, hostility, alliances, and discovery state.
- Let the player inspect faction status in a dedicated panel.
- Persist faction state through your provider-based save/load flow.

Initial faction groups to support cleanly:
- Player / Survivor colony
- Zombies
- Bandits / Raiders
- Neutral survivor groups
- Friendly settlements / trader groups

## 2) What Exists Already

### Current faction logic
- You already have a lightweight faction primitive in `UnitFaction`.
- `Unit.Faction` is currently derived from `UnitRole`, not stored as independent runtime data.
- Hostility is centralized in `UnitFactionUtility.AreHostile(...)`.
- Combat already respects faction hostility via `UnitCombat` friendly-fire checks.

This means the current system is good as a combat gate, but too thin for:
- reputation
- diplomacy
- multiple survivor factions
- discovered vs undiscovered groups
- faction ownership of settlements, patrols, quests, raids, or trade

### Current UI infrastructure you can reuse
- Squad management already supports feature tabs with dedicated controllers.
- World HUD also supports self-contained panel shells and runtime controller creation.
- You already followed this pattern for Formations, Jobs, and Map.

This makes a Factions panel straightforward to add in one of two ways:
- as another squad-management top tab
- as a World HUD side panel using the same shell/controller pattern

### Current save/load infrastructure you can reuse
- `GameSaveData` already aggregates subsystem data blocks.
- Save providers are modular and ordered by priority.
- Jobs and map already demonstrate the exact extension model you should mirror.

## 3) Current Architecture Constraints To Design Around

### 3.1 `UnitFaction` is currently too small
Current enum:
- Survivor
- Zombie
- Bandit

This is useful for combat but not enough for a faction game layer because it cannot express:
- two survivor groups that dislike each other
- discovered but not yet contacted factions
- player standing with multiple human groups
- faction-specific territories or settlements

### 3.2 `Unit.Faction` is computed from role
Right now, a unit's faction is effectively determined by `UnitRole`.

That means you cannot cleanly support:
- multiple bandit clans
- multiple human settlements
- dynamic allegiance changes
- recruited NPCs changing from one faction to another

### 3.3 Combat depends on faction hostility checks already
This is good news. The plan should preserve `UnitCombat` as the main hostility consumer, and improve the source of truth behind it rather than rewriting combat.

## 4) Recommended High-Level Design

## 4.1 Introduce a real faction domain layer
Create a new factions domain, suggested folder:
- `Assets/01_Game/01_Core/Factions/`

Core types:

- `FactionId`
  - String-backed identifier such as `player.colony`, `zombie.horde`, `bandit.redknives`, `settlement.ironhaven`

- `FactionDefinition`
  - Static data asset / ScriptableObject
  - fields:
    - factionId
    - displayName
    - description
    - icon
    - primaryColor
    - defaultAttitudeProfile
    - isPlayableContact
    - startsDiscovered
    - category (Survivor, Infected, Raider, Settlement, Trader)

- `FactionStandingState`
  - Runtime relation from player faction to another faction
  - fields:
    - factionId
    - standingValue
    - diplomacyState
    - discovered
    - lastKnownRegionId
    - lastInteractionSummary

- `FactionDiplomacyState`
  - Unknown
  - Neutral
  - Friendly
  - Allied
  - Suspicious
  - Hostile
  - AtWar

- `FactionMember`
  - Component for units/NPCs/settlements
  - stores explicit `factionId`
  - becomes the runtime source of truth instead of deriving everything from `UnitRole`

## 4.2 Add a central faction service
Create:
- `FactionManager`

Responsibilities:
- register faction definitions
- maintain runtime standings
- answer hostility/friendliness queries
- broadcast standing changes
- resolve a unit's faction from `FactionMember`
- provide panel-friendly summaries

Key API shape:
- `GetFaction(string factionId)`
- `GetStanding(string sourceFactionId, string targetFactionId)`
- `SetStanding(...)`
- `IsHostile(string sourceFactionId, string targetFactionId)`
- `MarkDiscovered(string factionId)`
- `CaptureState()` / `RestoreState()`

## 5) Migration Strategy From Current Faction Logic

Do not remove `UnitFaction` immediately.

Use a staged migration:

### Phase A
- Keep `UnitFaction` as a coarse combat bucket.
- Add `FactionMember` with explicit `factionId`.
- Add `FactionManager.IsHostile(Unit a, Unit b)` using explicit faction ids first.

### Phase B
- Update `UnitCombat` and selection targeting to query `FactionManager`.
- Retain `UnitFactionUtility.FromRole(...)` only as fallback/default seeding.

### Phase C
- Gradually reduce direct dependence on `UnitRole -> faction` assumptions.

This is the safest approach because your combat system already works and should not be destabilized.

## 6) Panel Plan

## 6.1 Recommended panel placement
Best fit for your current project:
- add a `Factions` top tab to the same squad-management UI that now contains Squad, Inventory, Crafting, Skills, Formations, Jobs, Map, and Missions

Why this fits:
- the infrastructure already exists
- you already have controller-per-tab architecture
- Jobs and Formations prove the extension pattern
- it keeps management-facing systems together

Alternative:
- a World HUD side panel if you want Factions to feel more like intel/diplomacy than squad administration

Recommendation:
- start as a squad-management top tab first

## 6.2 Factions panel layout

Left column:
- discovered faction list
- faction icon/color
- standing badge
- last known region / activity

Center panel:
- selected faction overview
- description/lore summary
- diplomacy state
- standing bar or value
- recent incidents
- known leader or settlement name

Right panel:
- actions / intel
- set marker on map to last known location
- view settlements / patrol sightings
- open trade status
- open conflict history

Footer / summary strip:
- total discovered factions
- hostile factions
- allied factions
- unknown contacts

## 6.3 First-pass panel data fields
For each faction row show:
- display name
- category
- discovered yes/no
- standing text
- hostility state
- last seen region
- threat/trade value

## 7) Runtime Systems To Add

## 7.1 Faction discovery
Track when a faction becomes known to the player.

Discovery triggers:
- encountering faction members in world
- entering faction-owned region or settlement
- receiving radio/mission contact
- trade interaction
- combat encounter

This supports a meaningful panel where unknown factions stay hidden until revealed.

## 7.2 Standing / reputation changes
Add simple standing deltas for early implementation:
- attack or kill faction member: negative
- steal/loot owned assets: negative
- defend faction members: positive
- complete mission/trade/help event: positive

Keep this event-driven.

Suggested event hook layer:
- combat kill events
- trade completion events
- quest/mission completion events
- theft/trespass alerts

## 7.3 Territory / ownership metadata
You already have world/region systems. Use them lightly at first.

Add optional ownership references to:
- settlements
- camps
- guard posts
- trader caravans
- patrol groups

This lets the panel surface:
- “last seen in region X”
- “controls settlement Y”
- “patrols nearby”

## 8) Save / Load Plan

Add save schema:
- `FactionSystemSaveData`
  - discovered factions
  - standing values
  - diplomacy states
  - last known region / last interaction notes

Add provider:
- `FactionSaveProvider : ISaveProvider`

Recommended load order:
- after world seed/session restoration
- after player/squad identity restore
- before AI diplomacy/world encounter simulation resumes

This mirrors your existing map/jobs/provider architecture and keeps it consistent.

## 9) Concrete File-Level Plan

## 9.1 New runtime files
Suggested new files:

- `Assets/01_Game/01_Core/Factions/FactionDefinition.cs`
- `Assets/01_Game/01_Core/Factions/FactionDefinitionRegistry.cs`
- `Assets/01_Game/01_Core/Factions/FactionManager.cs`
- `Assets/01_Game/01_Core/Factions/FactionMember.cs`
- `Assets/01_Game/01_Core/Factions/FactionStandingState.cs`
- `Assets/01_Game/01_Core/Factions/FactionDiplomacyState.cs`
- `Assets/01_Game/01_Core/Factions/FactionRuntimeEvents.cs`

## 9.2 Combat integration changes
Update these consumers to use faction ids/manager rather than only coarse enums:

- `Assets/01_Game/03_Characters/Core/UnitCombat.cs`
- targeting/selection systems that currently rely on hostility checks

## 9.3 Unit data changes
Extend unit composition with explicit faction ownership:

- `Assets/01_Game/03_Characters/Core/Unit.cs`

Recommended approach:
- keep current `UnitFaction` property for compatibility
- add explicit faction id/component-backed resolution path

## 9.4 UI changes
Mirror the Jobs/Formations implementation pattern:

- add `FactionsTabController`
- add `factionsTabButton`
- add `factionsTabRoot`
- add `OpenFactionsTab()`
- wire active-state visuals and button callbacks

Primary files likely touched:
- `Assets/01_Game/08_UI/Scripts/SquadManagement/ZomberaSquadManagementUI.cs`
- `Assets/01_Game/08_UI/Scripts/SquadManagement/ZomberaSquadManagementUI.UIBuild.cs`
- `Assets/01_Game/08_UI/Scripts/SquadManagement/ZomberaSquadManagementUI.InteractionsTabs.cs`
- `Assets/01_Game/08_UI/Scripts/SquadManagement/FactionsTabController.cs`

If you want World HUD parity too, mirror the newer pattern used by:
- `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.FormationsJobs.partial.cs`

## 9.5 Save changes
Add schema block and provider:

- `Assets/01_Game/01_Core/Systems/SaveSystem.cs`
- `Assets/01_Game/09_SaveSystem/Core/FactionSystemSaveData.cs`
- `Assets/01_Game/09_SaveSystem/Providers/FactionSaveProvider.cs`

## 10) Recommended Implementation Phases

### Phase 1: Runtime foundation
- Add faction definitions and `FactionManager`
- Add `FactionMember`
- Seed existing units with fallback faction ids from current roles
- Keep combat behavior unchanged externally

### Phase 2: Combat and relationship integration
- Route hostility queries through `FactionManager`
- Add standing values and diplomacy states
- Add simple discovery and standing-change events

### Phase 3: Factions panel
- Add Factions top tab
- Build faction list + details panel
- Bind it to live faction summaries from `FactionManager`

### Phase 4: Persistence
- Add `FactionSystemSaveData`
- Add `FactionSaveProvider`
- Restore discovered factions and standings on load

### Phase 5: World integration
- Attach factions to camps, settlements, patrols, missions, and encounters
- Add last-known-location and territory summaries

## 11) Recommended First Vertical Slice
Build this first:

1. `FactionManager` + `FactionMember`
2. Fallback seeding from existing `UnitRole`
3. `FactionsTabController` showing discovered factions and hostility state
4. `FactionSaveProvider` for discovered factions + standings

That slice gives you:
- real runtime faction ownership
- a visible panel quickly
- safe incremental migration away from hardcoded hostility

## 12) Acceptance Criteria
- Units can belong to explicit named factions, not just coarse role-derived buckets.
- Hostility checks still work through combat and targeting.
- The player can open a Factions panel and inspect discovered groups.
- Standing/diplomacy state can change at runtime.
- Faction discovery and standing persist across save/load.
- Existing survivor/zombie/bandit gameplay continues to work during migration.

## 13) Biggest Risk To Avoid
Do not tie the new system directly to `UnitRole` forever.

`UnitRole` should continue to answer questions like:
- player
- squad member
- zombie
- bandit

But faction identity should answer:
- which survivor settlement this unit belongs to
- whether they are friendly, neutral, allied, or hostile to the player
- which territory/settlement they represent

That separation is the key architectural step that makes a real factions system possible in your current game.