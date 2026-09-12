# 21 — Place Wilderness Nature

| Field | Value |
|-------|-------|
| Registry order | after Roads / before City |
| StageId | `PlaceWildernessNature` |
| Section | Wilderness |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Soft-skip when nature placer unbound
- Symptom: No wilderness vegetation; run still Succeeded.
- Correct: Bind WorldNaturePlacer or accept skip in criteria.
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
