# 33 — Generate Lot Terrain

| Field | Value |
|-------|-------|
| Registry order | 33 |
| StageId | `GenerateLotTerrain` |
| Section | City |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Double lot alphamap after paint compose
- Wrong path: Always regenerating when LotTerrainComposedDuringPaint set.
- Correct: Early-out when paint already composed stamps.
- Status: fixed (guards exist)

## Entry template

```markdown
### YYYY-MM-DD — short title
- Symptom:
- Wrong path:
- Correct:
- Evidence (options / seed / log):
- Status: open | fixed
```
