# AI Plan: Quick Formations Buttons Beside Squad Tabs

## 1) Goal
Add a quick-formation control strip in squads mode, positioned next to the existing squad page buttons (1, 2, 3, 4), with one-click presets:
- Default
- Spread
- Wedge
- Circle
- Spear Wall

Desired outcome:
- fast formation switching without opening the full Formations tab
- visual active-state feedback for current formation
- safe coexistence with existing bottom-bar behaviors (Squads/Builds toggle, build strip, collapse/expand)

## 2) Where To Integrate

Primary UI anchor is the same container that currently holds numeric squad tabs:
- [Assets/01_Game/08_UI/Scripts/HUD/SquadPortraitStrip.Tabs.cs](Assets/01_Game/08_UI/Scripts/HUD/SquadPortraitStrip.Tabs.cs)
  - creates and configures squad tab buttons
  - handles runtime button generation when missing

Bottom bar visibility and mode gating lives in:
- [Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.TabLayout.cs](Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.TabLayout.cs)
  - toggles bottom strip visibility
  - hides numeric tab buttons in build-extras mode

Runtime formation application path already exists:
- [Assets/01_Game/03_Characters/Controller/FormationController.cs](Assets/01_Game/03_Characters/Controller/FormationController.cs)
- [Assets/01_Game/01_Core/Interaction/CommandSystem.cs](Assets/01_Game/01_Core/Interaction/CommandSystem.cs)
- [Assets/01_Game/08_UI/Scripts/SquadManagement/FormationsTabController.cs](Assets/01_Game/08_UI/Scripts/SquadManagement/FormationsTabController.cs)

## 3) UX Specification

## 3.1 Placement
- Add quick-formation buttons to the same horizontal row as squad tabs.
- Position order:
  - Squads toggle
  - 1 2 3 4
  - separator
  - Default Spread Wedge Circle Spear Wall

## 3.2 Visibility Rules
- Visible only when bottom bar is in squads mode.
- Hidden when build item strip mode is active.
- Hidden when menu panels are open (same behavior as existing bottom row).
- Respect collapsed bottom-bar state (hide quick strip while collapsed).

## 3.3 Interaction Rules
- Click preset => update active formation immediately.
- If there is a current selection, optionally regroup selected members once after switching.
- If nothing is selected, set formation profile only (applies to next move command).
- Active preset button uses highlighted style.

## 3.4 Labels
- Use short labels to fit row width:
  - DEF
  - SPR
  - WDG
  - CIR
  - SPW
- Optional tooltip/full label on hover (editor/runtime-safe fallback: no tooltip if not available).

## 4) Runtime Wiring Plan

## 4.1 Add shared quick-preset mapping
Create a small shared mapping to avoid duplicating label/type pairs between full Formations tab and quick strip.

Suggested file:
- [Assets/01_Game/08_UI/Scripts/SquadManagement/FormationPresetCatalog.cs](Assets/01_Game/08_UI/Scripts/SquadManagement/FormationPresetCatalog.cs)

Contents:
- array of preset entries: `FormationType`, long title, short title
- helper to resolve active index by formation type

## 4.2 Extend SquadPortraitStrip tabs row
In [Assets/01_Game/08_UI/Scripts/HUD/SquadPortraitStrip.Tabs.cs](Assets/01_Game/08_UI/Scripts/HUD/SquadPortraitStrip.Tabs.cs):
- create `QuickFormationStrip` root under the same tabs parent used for squad tab buttons
- create one button per required preset
- cache button references for active visual updates
- subscribe click handlers to call a small apply method

Add methods:
- `EnsureQuickFormationButtons()`
- `ConfigureQuickFormationButtons()`
- `ApplyQuickFormation(FormationType type)`
- `RefreshQuickFormationVisuals()`

## 4.3 Resolve runtime dependencies in strip
In `SquadPortraitStrip`:
- resolve `CommandSystem` and `FormationController` lazily (same pattern used elsewhere)
- if `CommandSystem.Formation` exists, use it as source of truth

Apply behavior:
- set formation via `FormationController.SetFormation(type)`
- if selected squad members exist, call `CommandSystem.RegroupMembers(selected)` once

## 4.4 Bottom-bar mode gating
In [Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.TabLayout.cs](Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.TabLayout.cs):
- include quick-formation root in existing show/hide orchestration for bottom-row siblings
- hide in build-extras mode and in collapsed state

Important:
- do not let quick buttons interfere with numeric tab detection logic in `ApplyLegacyTopTabButtonsState`
- quick button labels are non-numeric, so current numeric filtering remains safe

## 4.5 Full Formations tab consistency
In [Assets/01_Game/08_UI/Scripts/SquadManagement/FormationsTabController.cs](Assets/01_Game/08_UI/Scripts/SquadManagement/FormationsTabController.cs):
- switch to shared preset catalog (if added)
- keep same formation types and wording so quick strip and panel stay aligned

## 5) Save/State Behavior
- No new save schema is required if active formation already persists through existing formation save data.
- Quick strip is only another way to set `FormationController.ActiveFormation`.
- On startup or tab refresh, quick strip reads current active formation and highlights matching button.

## 6) Risk And Mitigation

Risk: row overcrowding at lower resolutions.
Mitigation: short labels, fixed button width, hide quick strip when width is insufficient (optional fallback threshold).

Risk: excessive regroup spam from repeated clicking.
Mitigation: add short debounce (0.1-0.2s) before regroup call, or skip regroup if same formation already active.

Risk: mismatch between quick strip and full Formations tab.
Mitigation: use one shared preset catalog and one source of truth (`FormationController`).

## 7) Implementation Phases

### Phase 1: Core quick strip
- build quick buttons beside squad tabs
- wire clicks to `SetFormation`
- active highlight feedback

### Phase 2: Command integration polish
- regroup selected members on change
- add debounce/same-value guard

### Phase 3: Responsive/layout polish
- compact labels, spacing tuning
- optional overflow rule for low-width displays

## 8) Acceptance Criteria
- In squads mode, quick formation buttons appear beside 1/2/3/4.
- Clicking each quick button updates `FormationController.ActiveFormation` correctly.
- Active button highlight always matches current formation.
- Buttons are hidden in build mode and when bottom bar is collapsed.
- No regressions in squad tab switching, build strip, or existing hotkeys.

## 9) Fast First Slice
Implement first:
1. Add quick buttons in `SquadPortraitStrip` tab row.
2. Wire to `FormationController.SetFormation`.
3. Highlight active formation.
4. Hide strip when build mode is active.

This delivers immediate value with minimal risk before deeper layout polish.