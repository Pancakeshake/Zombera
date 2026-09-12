# 30 — Build Procedural Road Meshes

| Field | Value |
|-------|-------|
| Registry order | 30 |
| StageId | `BuildEasyRoadsMeshes` |
| Section | Roads |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Skip height writes without corridor bake flag
- Symptom: Missing pad/highway heights or unexpected double writes.
- Wrong path: Skipping height writes when HighwayCorridorsBakedPostRefine unset.
- Correct: Fail-closed — keep writes unless flag + artifacts present.
- Status: fixed (guards exist)

### Road cache on during acceptance
- Wrong path: ReuseCachedRoadsOnSameSeed true for vertical slice.
- Correct: Force false for acceptance baselines.
- Status: open

## Entry template

```markdown
### YYYY-MM-DD — short title
- Symptom:
- Wrong path:
- Correct:
- Evidence (options / seed / log):
- Status: open | fixed
```
