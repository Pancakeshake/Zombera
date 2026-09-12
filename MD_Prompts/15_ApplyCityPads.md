# 15 — Apply City Pads

| Field | Value |
|-------|-------|
| Registry order | Sites (after highways) |
| StageId | `ApplyCityPads` |
| Section | Sites |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Invalidate cascade unused from hub
- Symptom: Dependents look Done while artifacts stale after pad changes.
- Wrong path: Declaring Invalidates without calling WorldBuildPipelineRunner.Invalidate.
- Correct: Invalidate refine/stamp/layout/paint after pads, or force re-run.
- Status: open

### 2026-09-05 — Pads off-map + rectangular terrain pits after hub align
- Symptom: City labels/pads float outside the visible mesh; Apply City Pads punches black rectangular holes and leaves a displaced flat terrain plane; green selection bounds grow past the main grid.
- Wrong path: Treating pad heights / falloff as the primary failure when sites and landforms are still in session world XZ.
- Correct: `WorldTerrainGrid` is parented under the city hub stack. `AlignHubTransformToWorldOrigin` / single-city hub recentering dragged tile world positions while landforms and site centers stayed absolute. Re-snap tiles via `ResnapWorldTerrainGridToSession` after any hub move (and defensively at Select Sites / Apply City Pads). Re-run from Allocate or Reset then Sites→Pads after the fix.
- Evidence: Scene view after Apply City Pads; hub `transform` not at session origin when terrains were allocated/aligned.
- Status: fixed

### 2026-09-05 — Early planning-field cores (arterial + 10m)
- Symptom: Late mega-apron ApplyCityPads fought natural landforms/erosion and flattened huge footprints.
- Wrong path: Full adaptive apron after biomes as the only pad write.
- Correct: `ReserveCityPads` after GenerateBaseLandforms stamps city-footprint+`cityPadFlatMarginMeters` cores with falloff blend; Erode freezes cores; ApplyCityPads reasserts cores + highway carve + bake.
- Large quota: 2 metropolis / 3 city / 10–15 settlements; provisional placement relaxes slope/continuity so rugged landforms still hit quota.
- Status: fixed

## Entry template

```markdown
### YYYY-MM-DD — short title
- Symptom:
- Wrong path:
- Correct:
- Evidence (options / seed / log):
- Status: open | fixed
```
