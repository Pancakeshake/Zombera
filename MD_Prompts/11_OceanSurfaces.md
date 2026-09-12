# 11 — Build Ocean Surfaces

| Field | Value |
|-------|-------|
| Registry order | 11 |
| StageId | `BuildOceanSurfaces` |
| Section | Water |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Soft-skip hides unbound Crest ocean
- Symptom: Run succeeds with no ocean visuals.
- Wrong path: Treating soft-skip as full ocean coverage.
- Correct: Bind IOceanWaterRenderer or explicitly accept skip in test criteria.
- Status: open

### Enum id 43 excluded from byte RunRange
- Wrong path: RunRange 1..41 / 2..39 skips ocean.
- Correct: Registry order.
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
