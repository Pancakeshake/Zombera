# River Hydrology Loop — Reference

## Rubric (score 1–10 each iteration)

Open **before capture**, **target**, and **new capture** side by side. Write one evidence sentence per dimension.

| ID | Dimension | 10 = target | Fail signal |
|----|-----------|-------------|-------------|
| **R1** | Continuity | Clear main stem(s) → outlet; few broken ribbons | Disconnected cyan blobs |
| **R2** | Width hierarchy | Thin headwaters → wider trunk | Uniform neon strips |
| **R3** | Bed carve | Channel sits in terrain; banks slope into bed | Floating plane / cliff walls into water |
| **R4** | Soft bank blend | Graduated shore; wet shelf; no knife edge | Hard cyan cut, pixelated intersection |
| **R5** | Crest surface | Depth color, foam where appropriate, readable flow | Flat opaque plastic sheet |
| **R6** | Ocean / mouth | Natural join; flare; shore foam | Square flood edge only |

**Primary gate:** R4. Score R4 from **close-up** only. Aerial cannot pass R4 alone.

### Scoring tips for hard cut-in

On close-up, look at the water–terrain junction:

| Score | What you see |
|-------|----------------|
| 1–3 | Knife edge / checkerboard hard clip / neon rim |
| 4–5 | Carve exists but wall-like banks |
| 6–7 | Shoulder visible; still abrupt material edge |
| 8–9 | Soft shelf + shallow color; foam optional |
| 10 | Matches `river-target-prepaint-closeup-softblend.png` intent |

## Gap → lever map

### HydrologyProfile (`…/Profiles/HydrologyProfile.asset`)

| Gap | Fields to try (one at a time) |
|-----|-------------------------------|
| Too few / broken rivers | `riverFlowThreshold` ↓, `minimumRiverLengthMeters`, `maxRiverSystems`, `maxVisibleRiverBranches` |
| No primary trunk | `primaryRiverMinimumLengthMeters`, `primaryOutletSeparationMeters`, `valleyFlowPreference` ↑ |
| Uniform width | `minRiverWidthMeters` / `maxRiverWidthMeters`, `riverWidthByAccumulation` |
| Shallow / deep wrong | `minRiverDepthMeters` / `maxRiverDepthMeters`, `riverDepthByAccumulation` |
| **Hard cut / steep banks** | `carveShoulderWidthMeters` ↑, `shoreBlendWidthMeters` ↑, `riverCarveStrength` (soften), depth ↓ slightly |
| Mouth abrupt | `riverMouthFlareMultiplier`, `riverMouthFlareFraction` |
| Too straight | `meanderMaximumBankWidths`, `meanderWavelengthMeters`, `meanderMinimumValleySlopeDegrees` |
| Inland too high | `maxRiverLandElevationAboveSeaMeters` |
| Bed above flood | `minRiverBedDepthBelowSea` |
| Lakes wrong | `maxVisibleLakes`, `lakeMinimumDepthMeters`, area/volume caps |

### WorldWaterProfile (`…/Profiles/WorldWaterProfile.asset`)

| Gap | Fields |
|-----|--------|
| No shore foam | `enableFoam`, `foamStrength`, `foamShoreWidthMeters` |
| Harsh ocean edge | `foamShoreWidthMeters` ↑, coastal wave weight ↓ |
| Flat plastic look | materials (`crestOceanMaterial`, `crestLakeMaterial`) — only after R3/R4 ok |
| Elevated lakes wrong | `crestLakeSeaLevelToleranceMeters` |
| Outer sea too wild | `outerWaveWeight`, `outerWindSpeedKph` |
| Coast too choppy | `coastalWaveWeight`, `coastalWindSpeedKph`, `coastalCalmPaddingMeters` |

### Code only when profiles cannot express it

Owners: `HydrologyCarver`, `CrestRiverSplineBuilder`, `CrestWorldWaterRenderer`, `CrestOceanWaterBackend*`.  
Prefer profile/serialized fields; if adding constants, promote to profile.

## Capture script (temp cameras)

Prefer `Zombera.Editor.StyleMatch.RiverHydrologyCaptureRunner.Capture(iter, focusOverrides)` —

writes `iter-NNN-{aerial,river-closeup,lake-closeup,ocean-shore}.png` + meta, enforces ≥200KB,
and fails on missing river/ocean.

Do **not** rely on `SceneView.LookAt` before `camera.Render()` — use dedicated Camera GameObjects.
Persist focus positions from iter 0 into `river-session.md`.

## Pipeline stage notes

Registry order (water slice):

1. `SolveHydrology`
2. `CarveWaterFeatures` (may interact with pad restore later — this loop ends before paint)
3. `BuildOceanSurfaces` (`FullMapOnly`)
4. `BuildWaterSurfaces` (`FullMapOnly`)

Crossing cutouts may still be deferred in `BuildWaterSurfaces` — do not block the loop on bridges.

## Revert rules

| Outcome | Action |
|---------|--------|
| R4 up, others flat/up | Keep |
| R4 down | Revert last profile change |
| R1–R3 down hard | Revert; Phase A/B pivot |
| New console errors | Revert; fix or different lever |
| Same R4 for 3 iters | Mark step blocked; try different field or small code in carver |

## Planned steps starter (copy into session)

```markdown
- [ ] a1 | profile | Lower riverFlowThreshold slightly for more continuous stems | R1 | pending
- [ ] a2 | profile | Raise valleyFlowPreference toward valleys | R1,R2 | pending
- [ ] b1 | profile | Increase carveShoulderWidthMeters for softer banks | R3,R4 | pending
- [ ] b2 | profile | Increase shoreBlendWidthMeters | R4 | pending
- [ ] b3 | profile | Soften riverCarveStrength if walls remain | R3,R4 | pending
- [ ] b4 | profile | Tune min/max river depth for shelf shallowing | R3,R4 | pending
- [ ] c1 | profile | Enable/tune foamStrength + foamShoreWidthMeters | R5,R6 | pending
- [ ] c2 | profile | Calm coastalWaveWeight / wind for readable shores | R5,R6 | pending
- [ ] d1 | profile | Mouth flare multiplier/fraction | R6 | pending
- [ ] d2 | profile | Meander wavelength / bank widths if too straight | R1,R2 | pending
```

## Relation to style-match loop

| | Style match | River hydro |
|-|-------------|-------------|
| Goal | Full world vs `reference.png` | Water vs `river-refs` targets |
| End stage | ≤ `BindWeatherConsumers` | `BuildWaterSurfaces` |
| Paint / nature | Often yes | **No** |
| Rubric | B,T,S,V,W,A,L | R1–R6 |
| Capture | Single StyleMatch camera | Aerial + bank close-up |
