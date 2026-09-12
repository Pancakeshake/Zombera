# River Hydrology Session

Copy to `Library/StyleMatch/river-session.md` on first run. Update **every iteration** and **every scheduled wake** before exiting.

## Session header (required — parse on every wake)

```yaml
schema: river-hydro-session/v1
status: running          # running | goal_met | blocked | stopped
mode: river              # river | crest-only
phase: A                 # A network | B carve | C crest | D polish
iteration: 0
max_iterations: 60
map_tier: Large
pipeline_start: SolveHydrology
pipeline_end: BuildWaterSurfaces
refs_dir: Assets/Plans/river-refs
target_aerial: river-target-prepaint-aerial.png
target_closeup: river-target-prepaint-closeup-softblend.png
target_corridor: river-target-prepaint-corridor.png
success_goal: "R1>=7, R3>=7, R4>=8, R5>=6, R6>=6 (2 consecutive)"
best_iteration: null
best_r4: null
last_hypothesis: null
last_capture_aerial: null
last_capture_closeup: null
next_wake_action: baseline   # baseline | continue | stop
stop_reason: null            # goal_met | max_iterations | mcp_down | compile_blocked | user_stopped | blocked
mcp_preflight_ok: null
last_wake_at: null
pending_steps_count: 0
seed: 1
fixed_region_seed: 1589847769
river_focus: null          # "x,y,z" after iter 0
lake_focus: null
ocean_shore_focus: null
aerial_look_at: null
```

## River hydro loop state

- Refs: Assets/Plans/river-refs/
- Profiles: HydrologyProfile.asset, WorldWaterProfile.asset
- Pipeline: SolveHydrology → BuildWaterSurfaces (no paint)
- Goal: soft banks R4≥8 + continuity/carve/surface bars
- Iteration: 0 / 60
- Phase: A
- Best iteration: none (R4 —)
- Last change: (none)
- Status: running

| Iter | Phase | Change | R1 | R2 | R3 | R4 | R5 | R6 | Keep? | Errors |
|------|-------|--------|----|----|----|----|----|----|-------|--------|

### Planned steps (living — update every iteration)

```markdown
- [ ] a1 | profile | Lower riverFlowThreshold slightly for more continuous stems | R1 | pending
- [ ] a2 | profile | Raise valleyFlowPreference toward valleys | R1,R2 | pending
- [ ] b1 | profile | Increase carveShoulderWidthMeters for softer banks | R3,R4 | pending
- [ ] b2 | profile | Increase shoreBlendWidthMeters | R4 | pending
- [ ] b3 | profile | Soften riverCarveStrength if walls remain | R3,R4 | pending
- [ ] b4 | profile | Tune min/max river depth for shelf shallowing | R3,R4 | pending
- [ ] c1 | profile | Enable/tune foamStrength + foamShoreWidthMeters | R5,R6 | pending
- [ ] c2 | profile | Calm coastalWaveWeight / wind for readable shores | R5,R6 | pending
- [ ] d1 | profile | Mouth flare multiplier/fraction | R6 | pending
- [ ] d2 | profile | Meander wavelength / bank widths if too straight | R1,R2 | pending
```

## Wake log (append one line per scheduled wake)

| wake_at | action | result |
|---------|--------|--------|
