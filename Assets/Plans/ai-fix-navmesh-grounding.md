# AI Fix Plan: NavMesh Grounding and Vertical Alignment

## Problem Summary
AI agents and/or player-controlled characters are not staying correctly grounded.
Observed symptoms:
- Agents float above the terrain.
- Agents clip into or pass through the ground.
- Height jumps appear after load, spawn, or streamed-tile transitions.

## Goals
1. Ensure every spawned unit starts on valid NavMesh.
2. Keep runtime movement constrained to ground-aligned height.
3. Eliminate transform/agent desync that causes sinking or hovering.
4. Make behavior stable across save/load and streaming world updates.

## Scope
- Runtime spawn pipeline
- NavMesh bake and area settings
- Agent component tuning
- Movement integration (NavMeshAgent vs Animator/root motion)
- Save/load position restore path
- Streaming/tile activation path

## Non-Goals
- Combat behavior tuning
- Pathfinding optimization unrelated to grounding
- Animation polish beyond vertical alignment correctness

## Phase 1: Instrumentation and Repro Harness
### 1.1 Add focused diagnostics
Log these values whenever a unit spawns, restores, or teleports:
- World position
- Sampled NavMesh position and sample distance
- Agent enabled state
- agent.nextPosition
- transform.position
- agent.baseOffset
- Ground raycast hit point and normal

### 1.2 Build deterministic repro cases
Create repeatable scenarios:
- Spawn on flat terrain
- Spawn on slope near max slope angle
- Spawn on seam between streamed tiles
- Load save where player and squad are on uneven terrain

### 1.3 Success criteria
- Each repro yields consistent logs with no random vertical spikes.

## Phase 2: Authoring and Bake Validation
### 2.1 NavMesh bake sanity checks
Validate project-wide NavMesh settings:
- Correct Agent Type selected for all relevant surfaces
- Max slope and step height match character dimensions
- Terrain and walkable colliders included
- Non-walkable and obstacle layers excluded correctly

### 2.2 Surface coverage checks
Inspect for holes/gaps:
- Terrain holes
- Mesh collider discontinuities
- Missing NavMeshSurface on streamed chunks
- Incorrect carve settings on obstacles

### 2.3 Success criteria
- Continuous, expected walkable coverage in all test zones.

## Phase 3: Agent Configuration Standardization
### 3.1 Define a single grounding profile
For each unit class (player, squad, zombies), set and enforce:
- agent.radius
- agent.height
- agent.baseOffset
- agent.stepHeight equivalent through bake + character shape
- agent.autoTraverseOffMeshLink policy

### 3.2 Resolve component conflicts
Avoid dual authority over Y position:
- If NavMeshAgent controls movement, disable root-motion Y writes.
- If using root motion, sync via agent.updatePosition/updateRotation policy and explicit nextPosition sync.
- Ensure CharacterController, Rigidbody, and NavMeshAgent are not all competing.

### 3.3 Success criteria
- No persistent offset drift over 5+ minutes of movement.

## Phase 4: Spawn and Restore Hardening
### 4.1 Spawn-on-NavMesh helper
Add a shared helper for spawn/restore:
1. Use NavMesh.SamplePosition with radius fallback tiers.
2. If found, place unit at sampled position.
3. Temporarily disable agent, set transform, re-enable, then Warp to same position.
4. Stop and clear path after warp.

### 4.2 Ground fallback when sample fails
If NavMesh sample fails:
- Perform downward raycast against ground layers.
- Place at hit point + configurable foot clearance.
- Mark unit as pending NavMesh attach and retry sample for N frames.

### 4.3 Save/load restore ordering
Ensure restore order is:
1. World/tiles ready
2. NavMesh for loaded area ready
3. Units exist
4. Apply deferred transform restore with NavMesh-safe placement

### 4.4 Success criteria
- After load, all units appear grounded and immediately pathable.

## Phase 5: Streaming and Tile Boundary Robustness
### 5.1 Tile-ready gate before movement
For streamed chunks:
- Do not resume AI locomotion until local NavMesh data is available.
- Re-sample/wrap units crossing freshly loaded tile seams.

### 5.2 Seam correction pass
When tile loads complete:
- Run one-time correction for nearby units:
  - Sample NavMesh around current position
  - If vertical delta exceeds threshold, warp to sampled point

### 5.3 Success criteria
- No sinking/floating spikes during tile load-in events.

## Phase 6: Verification Matrix
### 6.1 Functional tests
- New game spawn grounding
- Save/load grounding on flat and steep terrain
- Squad spawn grounding with mixed elevations
- Combat chase over uneven terrain
- Streaming transitions near tile seams

### 6.2 Stress tests
- Rapid save/load cycles (10x)
- Fast travel/teleport loops
- Large squad count with simultaneous movement

### 6.3 Metrics to capture
- Count of NavMesh sample failures
- Count of fallback raycast placements
- Max vertical error: abs(transform.y - sampled.y)
- Time to become pathable after load

### 6.4 Pass/fail thresholds
- Zero units below terrain or visibly floating in test suite.
- Max vertical error <= 0.08m in steady state.
- Pathable within 1 second after deferred restore.

## Suggested Implementation Targets
- Assets/01_Game/03_Characters/Scripts/PlayerSpawner.SpawnPipeline.cs
- Assets/01_Game/09_SaveSystem/Providers/PlayerSaveProvider.cs
- Assets/01_Game/01_Core/Managers/GameManager.WorldSession.cs
- Assets/01_Game/01_Core/Managers/GameManager.cs
- Any shared movement/nav helper used by AI controllers

## Task Breakdown For Unity AI Agent
1. Add diagnostics and repro toggles first.
2. Implement shared NavMesh-safe placement helper.
3. Replace direct transform placement calls in spawn and restore flows.
4. Add tile-ready gating and seam correction.
5. Tune baseOffset/height/radius presets per unit type.
6. Run verification matrix and output a short failure report with log counters.

## Rollout Strategy
- Stage A: Enable diagnostics only.
- Stage B: Enable spawn/restore helper behind feature flag.
- Stage C: Enable seam correction and tile gating.
- Stage D: Remove flag after two clean test passes.

## Risks and Mitigations
- Risk: Over-warping causes visible snapping.
  - Mitigation: Apply correction only when vertical error exceeds threshold.
- Risk: SamplePosition false positives on wrong floor level.
  - Mitigation: Use area mask and max height delta guard.
- Risk: Root motion reintroduces Y drift.
  - Mitigation: Lock Y authority to one system and assert in debug builds.

## Definition of Done
- All verification tests pass.
- No visible floating/sinking across spawn, movement, save/load, and streaming transitions.
- Grounding helper is the only allowed path for runtime placement in spawn/restore code.
- Logs show stable sample success rates with near-zero fallback usage in normal gameplay.
