# 9 — Carve Water Features

| Field | Value |
|-------|-------|
| Registry order | 9 |
| StageId | `CarveWaterFeatures` |
| Section | Planning Field |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Rebuild ocean mask in Carve
- Symptom: Duplicate ocean flood / mismatch with Solve.
- Wrong path: Calling HydrologySolver.BuildOceanMask again.
- Correct: BuildOceanMaskFromPlan from HydrologyPlan.WaterClass.
- Status: fixed (guards exist — do not reintroduce)

## Entry template

```markdown
### YYYY-MM-DD — short title
- Symptom:
- Wrong path:
- Correct:
- Evidence (options / seed / log):
- Status: open | fixed
```
