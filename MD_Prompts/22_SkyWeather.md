# 22 — Configure Sky And Weather

| Field | Value |
|-------|-------|
| Registry order | 22 |
| StageId | `ConfigureSkyAndWeather` |
| Section | Environment |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Blind registry reorder to match UI foldout
- Symptom: Breaking InitializeEnvironmentState paint prereq.
- Wrong path: Moving Environment before Water/Paint in registry to match hub UI.
- Correct: Environment now legitimately follows Planning Field; paint prereq removed; ocean re-pushes weather.
- Status: fixed (2026-09-04)

## Entry template

```markdown
### YYYY-MM-DD — short title
- Symptom:
- Wrong path:
- Correct:
- Evidence (options / seed / log):
- Status: open | fixed
```
