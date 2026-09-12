# 12 — Build Water Surfaces

| Field | Value |
|-------|-------|
| Registry order | 12 |
| StageId | `BuildWaterSurfaces` |
| Section | Water |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Crossings empty at first Water pass
- Symptom: Bridge/crossing cutouts missing on Crest inland water after single Run All.
- Wrong path: Assuming Artifacts.Crossings populated here.
- Correct: Re-run after ResolveWaterCrossings, or add post-crossing water sync.
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
