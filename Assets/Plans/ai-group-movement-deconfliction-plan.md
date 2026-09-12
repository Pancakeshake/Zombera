# AI Plan: Group Movement Deconfliction (Selected Squad)

## 1) Goal
Fix the default selected-group movement so units no longer fight for the same destination, pile into each other, or stack vertically/inside each other when issuing move and attack-move style commands.

Target behavior:
- selected units spread into stable slots
- arrivals do not collapse into one point
- path crossing and jitter are reduced
- follow-up commands preserve formation intent
- behavior remains responsive with large selections

## 2) Current Observed Risk Points

### 2.1 Single-point fallback still exists
In move commands, if formation controller is missing, all members are sent to the same destination.

Impact:
- immediate clumping
- collision/avoidance thrash
- severe stacking around click point

### 2.2 Tight stopping distance for crowd movement
Unit controller defaults to very small stopping distance.

Impact:
- all units push for near-identical final coordinates
- local avoidance churn near destination

### 2.3 No explicit crowd deconfliction pass for slot validity
Current formation slots are geometric but not validated against local occupancy and tight navmesh funnels.

Impact:
- slots can collapse in constrained terrain
- units can route through the same narrow approach and stack

### 2.4 Unstable member-to-slot mapping under roster/selection order changes
Slot assignment is index-based without persistent local ordering for each issued command.

Impact:
- units can swap targets repeatedly
- path crossing increases

### 2.5 Follow and regroup can reintroduce overlap
Follow controller and regroup logic can repeatedly pull members toward similar offsets without group-level occupancy checks.

Impact:
- bunching during catch-up
- oscillation near leader and destination

## 3) Design Strategy

## 3.1 Always use deconflicted slot assignment
Never route selected group move to one point.

Rule:
- if formation controller exists, use it
- if missing, use internal radial slot fallback (not single point)

## 3.2 Add destination deconfliction layer
After slot generation, run a deconfliction pass:
- navmesh sample each slot
- enforce minimum slot separation (agent radius-based)
- push conflicting slots outward along ring/spiral steps
- reject slots that cannot be resolved within bounds and reassign fallback

## 3.3 Use adaptive spacing by group size and agent size
Compute spacing from:
- selected unit count
- average NavMeshAgent radius
- environment constraint factor

Effect:
- larger groups naturally spread more
- avoids fixed small spacing that collapses under pressure

## 3.4 Increase arrival tolerance for squad move context
Use command-context stopping profile for selected group movement:
- movement command: larger stopping distance
- precise single-unit move: keep tighter value

Effect:
- less pushing for exact same endpoint
- smoother settle behavior

## 3.5 Stable member-slot assignment per command
For each issued command:
- sort members by deterministic key (unit id)
- assign nearest available slot with stable tie-break
- lock assignment for that command instance

Effect:
- less crossing
- less slot swapping jitter

## 3.6 Command-level anti-reissue guard
Avoid reissuing near-identical group move commands every frame when click-hold or hybrid input produces repeat targets.

Effect:
- prevents destination churn
- keeps agent avoidance from resetting continuously

## 4) Concrete Runtime Changes

## 4.1 CommandSystem deconfliction
Primary integration file:
- [Assets/01_Game/01_Core/Interaction/CommandSystem.cs](Assets/01_Game/01_Core/Interaction/CommandSystem.cs)

Changes:
- replace single-point fallback with radial fallback slots
- add SlotDeconfliction step before MoveTo
- add stable member ordering and nearest-slot assignment
- expose crowd tuning fields:
  - minSeparationMultiplier
  - maxDeconflictionIterations
  - deconflictionStepMeters
  - adaptiveSpacingEnabled

## 4.2 FormationController upgrades
Primary integration file:
- [Assets/01_Game/03_Characters/Controller/FormationController.cs](Assets/01_Game/03_Characters/Controller/FormationController.cs)

Changes:
- add helper for adaptive spacing by group size
- add optional obstacle-aware variant hook
- preserve existing formation modes but increase default movement spacing for large groups

## 4.3 UnitController movement profile separation
Primary integration files:
- [Assets/01_Game/03_Characters/Controller/UnitController.cs](Assets/01_Game/03_Characters/Controller/UnitController.cs)
- [Assets/01_Game/03_Characters/Controller/UnitController.AgentBinding.cs](Assets/01_Game/03_Characters/Controller/UnitController.AgentBinding.cs)
- [Assets/01_Game/03_Characters/Controller/UnitController.NavAgentTick.cs](Assets/01_Game/03_Characters/Controller/UnitController.NavAgentTick.cs)

Changes:
- add group-command stopping distance override path
- set per-command arrival tolerance based on command context
- optional temporary reduction of acceleration/auto-brake aggression for crowd settle

## 4.4 FollowController anti-bunching
Primary integration file:
- [Assets/01_Game/03_Characters/Controller/FollowController.cs](Assets/01_Game/03_Characters/Controller/FollowController.cs)

Changes:
- widen lateral offsets when nearby followers overlap
- add local neighbor repulsion term before MoveTo
- jitter-resistant slot memory (short-lived per member)

## 4.5 Selection input command guard
Primary integration file:
- [Assets/01_Game/01_Core/RTS/SelectionManager.cs](Assets/01_Game/01_Core/RTS/SelectionManager.cs)

Changes:
- suppress duplicate move command issue when target point delta is below threshold and command was recent
- keep responsiveness for intentional rapid repositioning

## 5) Recommended Tuning Defaults

Initial values (starting point):
- group spacing baseline: 2.4m
- adaptive spacing gain: +0.08m per extra selected unit above 4
- minimum inter-slot separation: max(1.2m, 2.4 * averageAgentRadius)
- group move stopping distance: 0.6m to 0.9m
- duplicate command suppression window: 0.15s to 0.25s
- duplicate point threshold: 0.5m

## 6) Rollout Phases

### Phase 1: Hard stop on same-point movement
- remove single-point group fallback
- add radial fallback + stable ordering
- add duplicate command guard

### Phase 2: Slot deconfliction and adaptive spacing
- add navmesh slot validation pass
- add separation push-out iterations
- tune spacing against 10, 20, 40 selected units

### Phase 3: Arrival and settle behavior
- group-context stopping distance
- anti-jitter settle improvements
- follow-mode anti-bunching

### Phase 4: Stress tuning
- run max-squad tests in quick single-tile stress scene
- tune for minimal stack/jitter at target unit cap

## 7) Verification Plan

Test matrices:
- open terrain: 5, 10, 20, 40 units
- constrained lane / doorway
- destination near walls and props
- repeated rapid right-click repositioning
- move then immediate attack target then regroup

Acceptance checks:
- no more “all units converge to exact same destination point”
- visible reduction in overlap and vertical stacking
- fewer stalled agents at destination
- lower path crossing during first 2 seconds after command
- control responsiveness remains intact

## 8) Instrumentation To Add

Debug outputs (toggleable):
- command id and selected count
- generated slot count vs resolved slot count
- min pairwise slot separation
- units reassigned due to conflicts
- arrival settle time histogram

Useful for quickly identifying when a specific map area or command path reintroduces clumping.

## 9) Best First Slice

Implement this first for immediate impact:
1. in command system, remove single-point fallback for groups
2. add deterministic member ordering + slot assignment
3. add simple min-distance deconfliction pass
4. add duplicate move-command suppression in selection manager

This should eliminate the most obvious same-point fighting quickly before deeper crowd polish.