# 29 — Stamp Infrastructure Terrain

| Field | Value |
|-------|-------|
| Registry order | 29 |
| StageId | `StampInfrastructureTerrain` |
| Section | Roads |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Stale after pads without re-run
- Wrong path: Skipping stamp after ApplyCityPads (Invalidates target).
- Correct: Re-run after pads.
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
