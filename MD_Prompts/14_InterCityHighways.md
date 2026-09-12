# 14 — Plan Inter-City Highways

| Field | Value |
|-------|-------|
| Registry order | 14 |
| StageId | `PlanInterCityHighways` |
| Section | Sites |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Skipped when settings/city count fail
- Symptom: No highways; later PlanRoads may or may not compensate.
- Wrong path: Assuming Sites always seeds Artifacts.Roads.
- Correct: Check planInterCityHighwaysBeforePads / connectCitiesWithHighways / city count >= 2.
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

### 2026-09-07 — Highways miss pad gizmos (snap + HalfWidth vs plateau)
- Symptom: Inter-city polylines terminate near blue pad gizmos, not on plateau edges; planning can take minutes.
- Wrong path: Wide `TrySnapEndpointToTraversable` relocates ends off site; anchors used full HalfWidth while gizmos use arterial/plateau (~0.55× fallback); HighwayEntry stored original pin while polyline used snapped end; shared pathfinder treated as pad owner.
- Correct: Planner-owned plateau-edge pins (`HighwayEntry` == polyline ends); highway-scoped pin cells; approach-grade fail/retry; corridor cost fields + Improve edge cache; fingerprint `roadTerrainWritePolicyVersion` = 6.
- Evidence: screenshot seed; `[InterCityHighwayPlanner]` / `[PlanInterCityHighways]` timing + off-plateau diags.
- Status: fixed (code)
