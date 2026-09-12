# 19 — Paint Natural Surfaces

| Field | Value |
|-------|-------|
| Registry order | 19 |
| StageId | `PaintNaturalSurfaces` |
| Section | Surfaces |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Living log of **issues, things that do not work, and wrong paths** for this stage. Append-only; mark `Status: fixed` instead of deleting.

## Issues / wrong paths

### Full paint left on during iteration
- Symptom: ~30s+ paint; slow hub loops.
- Wrong path: Quality/Balanced for Fast Build iteration.
- Correct: Hub Fast Build → `SurfacePaintQuality.Fast`; acceptance / coastal verify → `Quality`. Fast Roads must not OR-force Fast paint.
- Status: fixed (2026-09-09 SurfacePaintQualityMode)

### 2026-09-09 — Quality soften dominated stage (~241s / 71%)
- Symptom: `paintTiming` Quality tiles=144 sum≈340s with soften≈241s, paintLoop≈92s.
- Wrong path: Full-res float[,,] 2-pass soften only; assume paintLoop-first.
- Correct: Planar cache-friendly blur + `Parallel.For` + active-layer reuse across passes; Quality alphamap path uses downsampled soften (half then quarter) + parallel paint rows. Keep soft floors r≥3 / 0.92 / 2. Macro paint avoids per-texel lambda alloc.
- Evidence: after half-res, sum≈175s (soften≈76s, paintLoop≈91s); quarter-res barely helped soften (≈69s) — upsample dominated. Next: Quality cell@256 soft-before-upscale + parallel upscale/downsample/stride-fill.
- Status: fixed (2026-09-09 Quality soften speed; follow-on cell@256)

### 2026-09-09 — Parallel tile CPU paint (Balanced/Quality)
- Symptom: Per-tile serial paint left CPU cores idle after cell@256 (~31s sum still mostly paintLoop+soften+setAlphamap).
- Wrong path: Nested `Parallel.For` rows while also parallelizing tiles (thrash); SetAlphamaps off main thread; shared scratch buffers across workers.
- Correct: Main-thread job capture + prebind; batch `Parallel.For` tiles with `TileWorkerMode` (serial rows + TLS soften/upscale scratch); main-thread `SetAlphamaps`; reuse batch alphamap buffers (cap ~2–6). Fingerprint `surfacePaintAlgorithmVersion=6`.
- Status: open (measure wall `paintTiming` after hub Quality rebuild)

### 2026-09-09 — Macro zones skipped ApplyPeakSnow
- Symptom: Quality/Balanced with useMacroBiomeRegions painted soft edges but missing snow→rock→grass ladder.
- Wrong path: `ApplyElevationOverlays` early-return after thin `ApplyMacroZoneElevationOverlay`.
- Correct: Macro paints base colors only; always run full `ApplyPeakSnow` ladder.
- Status: fixed

### 2026-09-09 — Quay vs open-beach soft acceptance
- Symptom: Coastal verify screenshots show hard rock shoreline after soft paint.
- Wrong path: Treat quay strip as SoftRadius failure.
- Correct: `CityPadSeawardSurfaceStamp` after paint is intentional hard overwrite; crop open beach away from pads for soft-edge checks.
- Status: open (documented acceptance split)

### 2026-09-07 — Lot compose inside Paint Natural Surfaces
- Symptom: Paint stage wall time inflated by urban lot stamps; City `GenerateLotTerrain` skipped via `LotTerrainComposedDuringPaint`; hub up-to-Paint showed urban stamps early.
- Wrong path: `TryComposeLotTerrain` / `GenerateDistrictLotTerrain` inside Surfaces stage; registry outputs included `lotTerrain`.
- Correct: Paint is natural-only; `GenerateLotTerrain` (City, after PlaceBuildings) owns urban alphamap stamps; hub Reset→Paint = natural alphamap only.
- Evidence: plan review + `WorldBuildStageRegistry` ownership; fixed in PaintNaturalSurfacesStage / registry outputs `["surfaces"]`.
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
