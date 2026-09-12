# 2 — Ensure World Builder Stack

| Field | Value |
|-------|-------|
| Registry order | 2 |
| StageId | `EnsureWorldBuilderStack` |
| Section | Setup |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Enum id outside naive RunRange
- Symptom: Ensure stack skipped when running byte-range pipelines (id=44).
- Wrong path: RunRange by (byte)WorldBuildStageId 1..41.
- Correct: Registry list order / prereq pull.
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
