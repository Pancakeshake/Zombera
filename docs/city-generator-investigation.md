# City Generator — Investigation Backlog

> Auto-generated from `RoadNetworkSettings` audit on 2026-07-28.
> Items are roughly ordered by impact.

---

## 1. Unify city size between RoadNetworkSettings and CityMathRoadLayout

**Problem:** Two independent knobs control city radius:
- `RoadNetworkSettings.cityGridRadius` (280m) — used by `WorldMapRoadNetworkGenerator` for spoke start points
- `CityMathRoadLayout.cityHalfWidthMinMeters` / `cityHalfWidthMaxMeters` (200m–300m) — used by `CityMathRoadLayoutGenerator` for street grid bounds

**Risk:** If `CityMathRoadLayout` grows past `cityGridRadius`, world-map spokes start inside the city grid. No guardrail exists.

**Suggested fix:** `WorldMapRoadNetworkGenerator` should read city half-width from the active `CityMathRoadLayout` instead of a separate `cityGridRadius`. Or add an `OnValidate` clamp.

---

## 2. `cityExitRoadClass` default is Highway — should be Arterial

**Problem:** World-map spoke roads leaving the city default to `RoadClass.Highway`. These are short connectors (280m–1800m), not true highways. Misclassification affects width, pathfinding anchor stride, and material selection.

**Suggested fix:** Change default to `RoadClass.Arterial`.

---

## 3. `terrainRefinementStrength` at 1.0 + `filterSteepRoadPointsAfterRefinement` = true can fight

**Problem:** Refinement smooths slope violations, then the filter removes points that still exceed the limit. At strength 1.0, nearly every candidate point gets smoothed, so the filter has little work left — but they run sequentially regardless.

**Suggested fix:** If `terrainRefinementStrength >= 0.95f` and `filterSteepRoadPointsAfterRefinement` is true, consider skipping the filter pass (or log a warning that it's redundant).

---

## 4. `fallbackToStraightPathWhenPathfindingFails` defaults to `false`

**Problem:** When terrain A* fails (e.g. on rough terrain), roads are silently dropped instead of falling back to straight-line connections. This means some city exits just disappear.

**Suggested fix:** Default to `true`, or surface a visible warning when roads are dropped.

---

## 5. `useMapMagicSplineOutput` and `fallbackToDeterministicWhenMissing` are legacy toggles

**Problem:** The tooltip on `useMapMagicSplineOutput` says "Legacy toggle kept for older assets." These fields may be candidates for removal once MapMagic spline integration is fully migrated.

**Action:** Audit all MapMagic spline consumers. If the legacy path is no longer used in production, remove both fields.

---

## 6. Dead fields removed (2026-07-28)

These were found with zero game-logic reads and removed:

| Field | Was in section |
|-------|---------------|
| `junctionMaterial` | Materials |
| `roadShoulderWidthMeters` | Placement Quality |
| `townAreaMaskName` | Road Layout Source |


---

## 7. `varyInnerStreetTopology` removed (2026-07-28)

The entire topology-shifting feature was removed — ~600 lines across 10 files. Roads now always use the uniform grid path. The `CityJunctionKind`, `CityStreetJunctionEntry`, and `LastJunctionManifest` stubs remain for downstream null-safe consumers.

If varied topology is ever re-added, it should be rebuilt from scratch with per-column randomized offsets and dead-end support.

---

## 8. Sidewalk control split (cleaned up 2026-07-28)

`RoadNetworkSettings.sidewalkWidthMeters` and `sidewalkMaterial` were removed. Sidewalk mesh generation is now exclusively via EasyRoads native sidewalks on the road prefab. `spawnSidewalkMeshes` remains as the gate toggle.

---

## 9. `CityMathRoadLayout.segmentOffsetRandomisation` / `segmentOffsetMeters` removed (2026-07-28)

Removed along with `varyInnerStreetTopology`. These controlled column shift probability and distance. If re-added, consider per-column randomized offset magnitude (jitter).

---

## 10. `RoadNetworkSettings` headings alphabetized (2026-07-28)

Both the ScriptableObject field order and the custom editor sections are now alphabetical for faster navigation.
