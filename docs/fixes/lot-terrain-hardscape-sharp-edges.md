# Lot Terrain hardscape sharp edges (MicroSplat)

**Date:** 2026-08-24  
**Result:** Driveway / building-pad / door-path stamps render with hard, texel-sharp edges.

## Symptom

City Lot Terrain paint looked correct in alphamaps (binary sharp stamps) but slabs, driveways, and footpaths still showed soft, blurry edges in-game.

Raising alphamap resolution and overlay meshes did **not** fix it.

## Root cause

MicroSplat was softening edges in the shader:

1. **Height blending** (default) — layer weights are remixed by texture height maps (`_Contrast`), so even binary control stamps get soft transitions.
2. **Linear control sampling** — control maps are sampled with `shared_linear_clamp_sampler`, which ignores `FilterMode.Point` on the texture and bilinear-blurs every texel boundary.
3. **Control UV noise** (`_CONTROLNOISEUV`) — warps control UVs and further softens splat boundaries.

Paint-side sharp stamps (`LotSubZone.Sharp`, `StampPixelAbsolute`, `blendMeters: 0`) were already correct.

## Fix that worked

### Keywords (`MicroSplat_keywords.asset`)

- Enable `_DISABLEHEIGHTBLENDING`
- Enable `_NORMALIZEWEIGHTS` (normalized linear blend ≈ Unity-style weights)
- Disable / remove `_CONTROLNOISEUV`

### Material (`MicroSplat.mat`)

- `_Contrast` raised (e.g. `0.92`) — less critical once height blend is off
- `_NoiseUVParams.y = 0` — kills control-noise strength if the keyword is still present

### Package fragment (persists across regenerations)

In `Packages/com.jbooth.microsplat.core/.../microsplat_terrain_body.txt`, sample control maps with **point** clamp:

```hlsl
SAMPLE_TEXTURE2D(_CustomControl0, shared_point_clamp_sampler, controlUV);
// same for _CustomControl1..7 and _Control0..7
```

Then regenerate the project shaders (MicroSplat compile on `MicroSplat.mat` / *Regenerate all Shaders*).

Verified regenerated `MicroSplat.shader`: `_DISABLEHEIGHTBLENDING` on, `_CONTROLNOISEUV` off, control samples use `shared_point_clamp_sampler`.

## Do not pursue for this bug

- Higher alphamap resolution alone
- Hardscape overlay meshes

Those fight symptoms; the blur was shader blend/sampling.

## Related code

- `Assets/01_Game/02_World/City/Scripts/CityLotTerrainPainter.cs` — sharp stamps + MicroSplat control rebuild
- `Assets/01_Game/02_World/City/Scripts/LotSubZone.cs` — `Sharp` flag
- `Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat.mat` + `MicroSplat_keywords.asset`

## Related: building slab size vs house

**Symptom:** Concrete BuildingPads looked larger than houses, or houses sat oddly on them.

**Cause:** When lot paint could not match a placed building (`lotRect.Contains(footprint.center)`), it fell back to a **geometric pad** that fills most of the lot (lot minus front/side/back reserves). Corner lots without an anchor filled the entire interior after yards.

**Fix (2026-08-24):**
- Match buildings by **maximum footprint overlap** with the lot (not center-in-rect).
- Re-measure live renderer bounds when collecting anchors.
- **Only stamp BuildingPad** when a placed-building footprint is available — never the oversized geometric/corner fill.

Re-run **Buildings** then **Lot Terrain** to refresh paint.

