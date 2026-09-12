# Highway Bridge Kit

Modular highway bridge meshes for water-crossing placement (`WaterCrossing` / `BridgeMeshPlacer`).
Built in Blender via MCP; exported with the same FBX axis settings as the tunnel kit.

## Paths

| Kind | Path |
|------|------|
| Meshes | `Assets/02_Shared/Meshes/Roads/Bridge/` |
| Prefabs | `Assets/02_Shared/Prefabs/Props/Road/Bridge/` |
| Source `.blend` | `Assets/02_Shared/Meshes/Roads/Bridge/Source/HighwayBridgeKit.blend` |
| Prefab menu | **Tools → World → Roads → Create or Update Bridge Kit Prefabs** |

## Pieces

| Prefab / FBX | Role |
|--------------|------|
| `SM_Bridge_Mid_10m` | Tileable deck + side rails, **10 m** along +Z after Unity import |
| `SM_Bridge_Abutment` | Bank seat + approach apron + wing walls; seat **4 m** into span |
| `SM_Bridge_Pier` | Cap under deck + shaft down (for spans needing intermediate supports) |
| `Bridge_PortalGate` | Collider-only box **14 × 5.5 × 1 m**, pivot at bed centerline |

Deck is visual-only in v1 — strip MeshColliders on visual prefabs; non-enterable uses portal gates (same contract as tunnel kit).

## Dimensions (locked)

- Inner clear width: **14.0 m** (`highwayWidth` 12 + 1 m each side)
- Deck thickness: **0.55 m** (top at roadbed Y=0)
- Rail height: **1.15 m**, thickness **0.28 m** (outside clear width — no deck overlap)
- Mid length: **10.0 m** → placer `ceil(spanLength / 10)`
- Abutment seat: **4.0 m** into span; approach apron **2.0 m** toward bank (−Z in Unity)
- Pier shaft depth: **8.0 m** under cap (placer may scale later)
- Pivot: roadbed centerline at mouth (abutment) / segment start (mid) / pier center
- Materials: `M_Bridge_Exterior` → worn concrete; `M_Bridge_Deck` → asphalt; `M_Bridge_Rail` → concrete/metal rail

## Placement contract (placer track)

```text
LookRotation(exitXZ - entryXZ) on Y-up
DeckWorldY from bank heights + clearance
Abutment A at entry, forward into span
Mids along polyline, abutting 10 m steps (optional 1 cm gap)
Piers under mid centers when span needs them
Abutment B = same prefab rotated 180° (or scale X = −1) at exit
Gate at each abutment mouth (non-enterable until NavMesh links exist)
```

## Mesh notes

- Rails sit **outside** the clear deck (shared edge only) to avoid z-fighting.
- Abutment is boolean-unioned so foundation / wings / deck do not stack coplanar faces.
- Preview collection `HighwayBridgePreview40m` in the `.blend` is editor-only (4× mid ≈ 40 m span).

## Blender → FBX export flags

```python
bpy.ops.export_scene.fbx(
    filepath=path,
    use_selection=True,
    object_types={'MESH'},
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',
    axis_up='Y',
    bake_space_transform=True,
)
```

Build space in Blender: **X** lateral, **Y** along span, **Z** up. After export, travel is **+Z** in Unity.

## Unity setup after import

1. Let Unity import the new FBXs (or **Assets → Refresh**).
2. Run **Create or Update Bridge Kit Prefabs**.
3. Assign resulting prefabs on bridge placer settings when that PR lands.

## Out of scope

Runtime crossing scan, pathfinding corridors, `BridgeMeshPlacer`, NavMesh links — see plan-first surfaces / water crossings plan.
