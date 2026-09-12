# 1 — Reset Generated World

| Field | Value |
|-------|-------|
| Registry order | 1 |
| StageId | `ResetGeneratedWorld` |
| Section | Setup |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Full-scene FindObjectsByType sweeps
- Symptom: Reset spikes editor time / GC.
- Wrong path: Expanding unrestricted Transform sweeps for legacy HydrologySurfaces / orphan WorldTerrainGrid roots.
- Correct: Narrow to known roots/catalog; keep legacy sweeps minimal.
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
