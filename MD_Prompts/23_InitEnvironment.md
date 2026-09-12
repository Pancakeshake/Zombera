# 23 — Initialize Environment State

| Field | Value |
|-------|-------|
| Registry order | 23 |
| StageId | `InitializeEnvironmentState` |
| Section | Environment |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### 2026-09-04 — Paint gate blocked early Environment
- Symptom: Hub Environment ▶ / Run Section pulled Water→Sites→… or failed without paint.
- Wrong path: Hard-prereq on `PaintNaturalSurfaces` while UI showed Environment after Planning Field.
- Correct: Prereq is ConfigureSkyAndWeather only; registry Environment block sits after Planning Field.
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
