# Arterial ring corner curves (city prefab hub)

Reference for the square arterial ring corners in the city prefab road layout. If corners look too wide, faceted, or discontinuous at the junction snap points, use this doc before changing corner geometry.

## Symptoms of a broken corner

- Corner mesh is **visually wider** than the straight arterial segments.
- Corner is a **faceted polygon** instead of a smooth quarter turn.
- Visible **seam or gap** where the corner meets the straight edge chains (not continuous between the two outermost T-junction knots).
- EasyRoads warnings such as **"only 1 spline point was extracted"** or **"distances between markers"** on corner roads (often from too many closely spaced markers with `StraightXZ`).

## Root cause (old broken approach)

Do **not** revert to this pattern:

1. **Separate quarter-arc roads** emitted by `AddArterialCornerArc` / `AddSquareArterialCornerArcs`, butt-jointed to edge chains at trim points (`trimX0 = x0 + cornerRadius`, etc.).
2. **`curvedMarkers = false`** on corner polylines, which forces `ERMarkerControlType.StraightXZ` on every arc marker in `ProceduralRoadSystem.TileBuild.cs` → `CreateRoadFromPoints`.
3. **Dense arc sampling** (e.g. 8 segments on a ~28 m radius quarter arc ≈ 5.5 m spacing). When marker spacing is below road width (~6 m), EasyRoads miter joins overlap and the mesh **fans out wider** than straights.
4. Edge chains ran **trim point to trim point** (`trimX0..trimX1`), while corners were separate pieces with **no junction connector** at the trim join (city prefab builds skip `SnapPointsToExistingMarkers`), producing a visible seam.

## Correct design (integrated corner curves)

All layout logic lives in **`CityMathRoadLayoutGenerator.cs`**, method **`AddSquareArterialRing`** (`useCornerArcs` branch).

### 1. Shorten edge chains to outermost junction knots

Straight arterial chains no longer span `trimX0..trimX1`. They run only between the **first and last street knots** on each edge:

- Horizontal edges: `colFirst` .. `colLast` from `trimmedCols`
- Vertical edges: `rowFirst` .. `rowLast` from `trimmedRows`
- Interior knots between those boundaries stay on the chain (`chainCols` / `chainRows`)
- If an edge has no knots, fallback is the edge midpoint: `(trimX0 + trimX1) * 0.5f` or `(trimZ0 + trimZ1) * 0.5f`

The stretch from each boundary knot **around the rounded corner** to the adjacent edge’s boundary knot is **not** part of the straight chain.

### 2. One continuous curved road per corner

`AddSquareArterialIntegratedCornerCurves` emits **four** corner roads (SW, SE, NE, NW). Each is built by `AddArterialIntegratedCornerCurve` as a **single polyline** from junction knot on edge A → corner arc → junction knot on edge B.

**Point order per corner road:**

1. Boundary knot on edge A (`leadInKnot`)
2. Optional collinear midpoint toward arc tangent (if lead distance ≥ 8 m)
3. Arc tangent point A (`arcStart`)
4. Sparse sampled arc points
5. Arc tangent point B (`arcEnd`)
6. Optional collinear midpoint toward exit knot
7. Boundary knot on edge B (`leadOutKnot`)

**Critical flags and sampling:**

- `curvedMarkers = true` on the `RoadPolyline` so EasyRoads keeps spline control (smooth constant-width curve).
- Arc segment count: `clamp(round(arcLength / (width * 1.5)), 2, 6)` — keeps marker spacing **above** road width.
- Collinear lead-in/out points (`AppendStraightLeadMarkers`) keep the spline tangent aligned with the edge axis so T-junction topology at the knot stays correct (street + chain segment + corner road = 3 roads).

### 3. Junction topology at corners

At each outermost ring knot:

- Local street (perpendicular)
- Straight arterial chain segment (along the edge, between interior knots)
- Integrated corner road (from this knot around the bend to the knot on the adjacent edge)

Corner-adjacent junctions should meet at the **same knot coordinates** as before; only the geometry *between* outermost knots changed from “trim-point arc + overlapping chain” to “one curved road”.

## Pipeline: `curvedMarkers` must stay wired

The flag is set only in the layout generator but must propagate unchanged:

| File | Role |
|------|------|
| `RoadTypes.cs` | `RoadPolyline.curvedMarkers` |
| `CityMathRoadLayoutGenerator.cs` | Set `true` on integrated corner roads |
| `ProceduralRoadSystem.CityPrefabBuild.cs` | Copies to `PendingRoadPath` |
| `ProceduralRoadSystem.Geometry.cs` | Preserved when splitting paths at intersections |
| `ProceduralRoadSystem.TileBuild.cs` | `CreateRoadFromPoints(..., curvedMarkers)` — when `false`, all markers get `StraightXZ` |

If corners widen or facet again, first check that **`curvedMarkers` is still `true`** on corner polylines and still passed through to `CreateRoadFromPoints`.

## Related constants and settings

- **`CornerJunctionClearanceMeters`** (18 m): streets near corners dead-end before the ring; ring knot lists use `trim ± clearance` filtering.
- **`ResolveArterialCornerRadius`**: corner arc radius from layout (`arterialCornerRadiusMeters` or derived from `streetSpacingMeters`).
- **`ResolveUnifiedCityRoadWidthMeters`**: city hub uses **Local** road width from `RoadNetworkSettings` for streets, arterials, and highway exits (currently unified width, e.g. 6 m).
- **`useCornerArcs`**: enabled when `cornerRadius >= 2` and trimmed ring span ≥ 4 m on both axes; otherwise full edge chains without rounded corners.

## Verification

1. Open the city prefab hub scene with `CityPrefabRoadNetworkBuilder`.
2. **Generate City Road Network** (context menu).
3. Confirm:
   - Corner width matches straight arterials.
   - Each corner is one smooth curve between the two outermost junction snap points.
   - No new “angle too sharp” errors **at corner-adjacent** junctions.

## Out of scope (known unrelated warnings)

- **“Angle with the crossing is too sharp”** at T-junctions away from corners.
- **2/3-linked junction** warnings elsewhere on the grid.

These are not caused by corner curve geometry and are not fixed by changing integrated corner roads.

## If corners break again — checklist

1. **`AddSquareArterialIntegratedCornerCurves` still called** from `useCornerArcs` branch (not `AddSquareArterialCornerArcs`).
2. **`curvedMarkers = true`** on corner `RoadPolyline` objects.
3. **Edge chains end at `colFirst`/`colLast`/`rowFirst`/`rowLast`**, not `trimX0`/`trimX1`.
4. **Arc sampling** still sparse (`2..6` segments from width-based formula), not fixed high count with `StraightXZ`.
5. **`CreateRoadFromPoints`** still respects `curvedMarkers` (does not force `StraightXZ` when true).
6. **Road width** consistent: corner roads use same `width` as chains via `ResolveUnifiedCityRoadWidthMeters`.

## Key source locations

- Layout / corner emission: `Assets/01_Game/02_World/Roads/CityMathRoadLayoutGenerator.cs`
  - `AddSquareArterialRing` (~line 461, `useCornerArcs`)
  - `AddSquareArterialIntegratedCornerCurves`
  - `AddArterialIntegratedCornerCurve`
  - `AppendStraightLeadMarkers`, `AppendDistinctPointXZ`
- EasyRoads build: `Assets/01_Game/02_World/Roads/ProceduralRoadSystem.TileBuild.cs` → `CreateRoadFromPoints`
- Regenerate hub: `CityPrefabRoadNetworkBuilder.GenerateCityRoadNetwork`
