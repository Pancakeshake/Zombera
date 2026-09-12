# Crest Reference → Zombera River / Lake / Ocean Plan

**Status:** Superseded by unified execution plan  
**Active plan:** Cursor plan `river_carve_soft_banks` (soft carve + Crest refs merged)

This file remains as the MCP reference inventory. **Execution order and MicroSplat ownership live in the unified plan.**

## Material ownership (corrected)

- Shore sand / pebbles = **our MicroSplat** palette (`Sand01`, `RiverSand`, `Sand` on `WorldSurfacePalette`) via `WorldSurfacePainter`.
- Do **not** import Crest example terrain layers (`Pebbles_B`, `Sand_01`, etc.).
- Crest refs teach **carve silhouette** + **water construction / ocean display**, not terrain textures.

## Sources (MCP-scanned)

- `Crest-Examples/LakesAndRivers` — primary river + lake + soft inland **carve** look
- `Crest-Examples/PirateCove` (+ Environment) — primary ocean / shore display
- `Crest-Examples/Main` — minimal ocean baseline (depth color, clip, depth cache)

## Owners

`HydrologyProfile`, `WorldWaterProfile`, `HydrologyCarver`, `CrestRiverSplineBuilder`, `CrestLakeWaterBodyPlacementUtility`, `CrestWorldWaterRenderer` / `CrestOceanWaterBackend`.  
No fourth water stack. Loop: `unity-river-hydrology-loop`.

---

## 1. Reference patterns

### LakesAndRivers — inland

| Layer | Steal |
|-------|--------|
| Carve shape | Soft hills, carved channels → basin (geometry) |
| River Crest | Height radius ~20, subdiv 8; Flow; ShapeFFT Blend; `SplinePointDataWaves` fade at ends |
| Lake | Widened corridor into basin; quieter lake FFT |
| OceanRenderer | Foam on, flow on, dyn waves off, clip off |
| Depth | Realtime cache over water region |

### PirateCove — ocean

| Layer | Steal |
|-------|--------|
| Display | Depth turquoise→blue; foam; sand/gravel **read** via MicroSplat Sand |
| Skip | Whirlpools, boats |

### Crest Main — baseline only

Depth-driven shallow color + depth cache. Not river authoring.

---

## 2. Gaps (current)

| Area | Gap |
|------|-----|
| Soft banks R4 | Hard cut / broken close-up — **primary gate** |
| Crest height radius | Was full `shoreBlendWidthMeters` (~80); target channel half + ~8 m pad |
| Waves | Need end fade (`SplinePointDataWaves`) |
| Lake material | `crestLakeMaterial` null |
| Shore paint | Confirm MicroSplat weights after R4 — no Crest layers |

---

## 3. Success

R1≥7, R2≥6, R3≥7, **R4≥8**, R5≥6, R6≥6 (2 consecutive). Large, seed 1, fixed region 1589847769. End ≤ `BuildWaterSurfaces` until R4 clears.

---

## 4. Phase order (unified)

0. Baseline capture + session  
**B (first).** Crest channel radius split → profile carve soft banks (R3/R4)  
**A.** Crest inland parity (wave fade, lake material, spectra) once R4 climbing  
**C.** MicroSplat shore confirm after R4≥8  
**D.** Ocean foam / coastal / depth cache (PirateCove)  
**E.** Network levers only if R1/R2 lag  

---

## 5. Reference paths

```
Assets/03_ThirdParty/Crest/Crest-Examples/LakesAndRivers/Scenes/LakesAndRivers.unity
Assets/03_ThirdParty/Crest/Crest-Examples/PirateCove/Scenes/PirateCove-Day.unity
Assets/03_ThirdParty/Crest/Crest-Examples/Main/Scenes/main.unity
Assets/02_Shared/ScriptableObjects/World/Profiles/HydrologyProfile.asset
Assets/02_Shared/ScriptableObjects/World/Profiles/WorldWaterProfile.asset
Assets/02_Shared/ScriptableObjects/World/Profiles/WorldSurfacePalette.asset
Assets/Plans/river-refs/
```
