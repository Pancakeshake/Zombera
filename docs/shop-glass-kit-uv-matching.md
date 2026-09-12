# Shop Glass Kit — Matching the Existing Walls

How the shop glass storefront kit pieces were built so their texture mapping,
materials, and placement convention match the existing modular walls (`Wall.prefab`).

## 1) Piece Set

Four prefabs in `Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/`:

| Piece | Purpose |
|---|---|
| `ShopGlass_Full` | Full-width window, no end caps (run middle pieces) |
| `ShopGlass_CapLeft` | 0.3 m end pillar on the left |
| `ShopGlass_CapRight` | 0.3 m end pillar on the right |
| `ShopGlass_CapBoth` | Pillars on both ends (single-segment runs) |

Meshes: `Building_Modular_Parts/Meshes/ShopGlass_*.fbx`.

## 2) Dimensions & Structure (match `Wall.prefab` exactly)

- Outer bounds: **3.0 m wide × 3.0 m tall × 0.1 m thick**; pivot **bottom-center**
  (`mesh bounds center (0, 1.5, 0)`), same as `SM_Wall`.
- Glass opening **1.5 m tall**, from **0.3 m to 1.8 m** (0.3 m sill, 1.2 m header —
  the header band is where signage mounts).
- Glass pane **0.015 m** thick, front face recessed **~4.5 cm** behind the wall
  face, with a stepped reveal ring (0.08 m × 0.04 m) — the "step down" from the
  wall thickness.
- Prefab structure cloned from `Wall.prefab`: root with `BuildingPart`,
  `BuildPiece`, `StructureHealth`, collision condition components; `SM_*` child
  with `MeshFilter` + `MeshRenderer` + `MeshCollider`.

## 3) Material Slots (verified via per-submesh triangle normals)

`Wall.prefab` convention:

| Slot | Material | Face |
|---|---|---|
| 0 | `2.Material_Orange` (`T_Grid_Orange_01`, 2×2-cell grid) | **+Z (inner)** |
| 1 | `1.Material` (`T_Grid_Grey_01`, 1-cell grid) | **−Z (outer)** + sides + top/bottom |

Shop glass pieces (3 slots):

| Slot | Material | Face |
|---|---|---|
| 0 | `1.Material` (grey) | outer (street) face + reveals + edges |
| 1 | `2.Material_Orange` (orange) | inner face |
| 2 | `M_Glass_Clear.001` | glass pane |

The skin system (`BuildingSkinReskinTool.TryResolveShopGlassMaterials`) preserves
slot 2 and skins slots 0/1 (exterior/interior) when a `BuildingSkinTable` is used.

## 4) UV Mapping — the part that took the most iterations

### Wall face measurement (per-face, via triangle normals — the whole-mesh range is misleading)

| Wall face | UV range | Over 3 m |
|---|---|---|
| −Z outer (grey) | `U[0..3]`, `V[0..3]` | **1 tile per metre** |
| +Z inner (orange) | `U[-3..0]`, `V[0..3]` | 1 tile per metre |
| thin sides | `U[-0.1..0.1]` | — |
| top/bottom | `U[0..3]` / `U[-3..0]` | 1 tile per metre |

> Trap: the mesh's combined `U[-3..3]` is the SUM of the inner (`-3..0`) and
> outer (`0..3`) faces. Reading the aggregate range gave "2 tiles/m" — wrong.

### Matching recipe (Blender, applied per polygon by dominant normal)

- Front/back faces (±Y in Blender): `U = x * 1 + 0.5`, `V = z * 1`
- Thin side faces (±X): `U = y * 1`, `V = z * 1`
- Top/bottom faces (±Z): `U = x * 1`, `V = y * 1`

Constants:
- **1 tile per metre** on U and V (matches the wall's outer face exactly).
- **+0.5 half-tile U offset** on front/back so the texture does not start on a
  fresh grid line at the piece edge (requested tweak; seamless between adjacent
  pieces because all pieces share the same mapping phase).

Grey grid = 1 cell per tile, orange grid = 2×2 cells per tile — that difference
made a uniform density look wrong on one material until the wall's per-face
mapping was measured.

## 5) Blender → Unity FBX Export (required settings)

The pieces must import with **identity root rotation**, Y-up mesh, pivot at
origin — exactly like `Wall.fbx`:

```python
bpy.ops.export_scene.fbx(
    filepath=path,
    use_selection=True,
    object_types={'MESH'},
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',
    axis_up='Y',
    bake_space_transform=True,   # <- this is the critical one
)
```

Gotchas learned:

- Default Blender export gets Unity's −90° Blender axis fix on the **model root**;
  prefabs bypass that rotation (they assign the mesh directly), so pieces render
  lying flat. `ModelImporter.bakeAxisConversion = true` does **not** rewrite the
  mesh — only `bake_space_transform=True` at export produces a Y-up mesh.
- Build geometry in Blender with height along **Blender Z**; the export bake
  converts it to Unity Y-up.
- Ensure each object sits at origin before export (display offsets after export
  are fine); object origin must be bottom-center.

## 6) Verification Method

A REST `script-execute` measurement (per-face triangle-normal buckets) is the
reliable check:

```csharp
// for each triangle: bucket by dominant normal (+Z/-Z/+X/-X/+Y/-Y), then
// print min/max UV per bucket per submesh
```

Expected after export: outer face `U[-1..2]`, `V[0..3]`; density 1 tile/m;
bounds of every piece `(3, 3, 0.1)` centered `(0, 1.5, 0)`.

## 7) Generator Integration (summary)

- `CityPrefabResidentialLotBuilder` / `ModularSingleLevelHouseGeneratorTool`
  places the pieces on the ground-floor door side of **Commercial** buildings
  only (`ResolveShopGlassSegments` in `...Tool.Building.cs`).
- Run composition: 1-wide → `CapBoth`; 2-wide → `CapLeft + CapRight`;
  3+ wide → `CapLeft + Fulls + CapRight`.
- Commercial roofs are flat/parapet only, resolved from
  `Assets/02_Shared/ScriptableObjects/Buildings/RoofTypes/Flat/`.

## 8) Tuning Points

- `U_PER_METER`, `V_PER_METER`, `U_OFFSET` in the Blender build script.
- Sill/glass heights: `SILL = 0.30`, `GLASS_H = 1.50` (glass 0.3–1.8 m).
- Pillar width: `PILLAR = 0.30`.
