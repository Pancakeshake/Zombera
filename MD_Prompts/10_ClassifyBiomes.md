# 10 — Classify Biomes And Buildability

| Field | Value |
|-------|-------|
| Registry order | 10 |
| StageId | `ClassifyBiomesAndBuildability` |
| Section | Planning Field |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Silent programmatic palette defaults
- Symptom: Classification changes when Plains/Ocean/Badlands missing from palette.
- Wrong path: Ignoring ApplyProgrammaticDefaults side effects.
- Correct: Ensure palette assets complete before acceptance runs.
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
