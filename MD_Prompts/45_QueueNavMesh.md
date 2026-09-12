# 45 — Queue NavMesh

| Field | Value |
|-------|-------|
| Registry order | 45 |
| StageId | `QueueNavMesh` |
| Section | Finalize |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### NavMesh timeout / Play Mode NotCovered
- Symptom: Stage fails after 120s wait; hub Tests still mark Play Mode NavMesh NotCovered.
- Wrong path: Assuming QueueNavMesh bakes itself.
- Correct: Ensure StreamingNavMeshTileService reports NavigationReady; cover Play Mode in tests.
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
