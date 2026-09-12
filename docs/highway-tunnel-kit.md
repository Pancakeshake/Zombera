# Highway Tunnel Kit

Modular highway tunnel meshes for Track B (`MountainTunnel` / `TunnelMeshPlacer`).
Built in Blender via MCP; exported with the same FBX axis settings as the shop-glass kit.

## Paths

| Kind | Path |
|------|------|
| Meshes | `Assets/02_Shared/Meshes/Roads/Tunnel/` |
| Prefabs | `Assets/02_Shared/Prefabs/Props/Road/Tunnel/` |
| Source `.blend` | `Assets/02_Shared/Meshes/Roads/Tunnel/Source/HighwayTunnelKit.blend` |
| Prefab menu | **Tools → World → Roads → Create or Update Tunnel Kit Prefabs** |

## Pieces

| Prefab / FBX | Role |
|--------------|------|
| `SM_Tunnel_Mid_10m` | Tileable horseshoe tube + **floor slab**, **10 m** along +Z; MeshColliders kept for enterable |
| `SM_Tunnel_Portal` | Daylighting mouth + **4 m** collar; flare extends **2 m** toward approach (−Z); visual-only |
| `Tunnel_PortalGate` | Collider-only box **14 × 5.5 × 1 m** (non-enterable mouths only) |

## Enterable contract

- Mid floor: ~**14 m** clear width at bed Y, Interior/asphalt material, MeshCollider required.
- `RoadNetworkSettings.tunnelEnterable` (default **false**): Terrain holes at mouths, no portal gates, mid floors registered for NavMesh.
- Non-enterable: portal gates block mouths; mid colliders may still exist but agents are gated.

## Dimensions (locked)

- Inner clear width: **14.0 m** (`highwayWidth` 12 + 1 m each side)
- Inner clear height: **5.5 m**
- Wall thickness: **0.6 m**
- Mid length: **10.0 m** → placer `ceil(spanLength / 10)`
- Portal collar: **4.0 m** into mountain; outer flare half-width ~**9 m**
- Pivot: roadbed centerline at mouth (portal) / segment start (mid)
- Materials: `M_Tunnel_Exterior` → worn concrete; `M_Tunnel_Interior` → asphalt_02 (darker)

## Placement contract (Track B)

```text
LookRotation(exitXZ - entryXZ) on Y-up
Portal A at entry, forward into mountain
Mids along polyline, abutting 10 m steps (full entry→exit bore)
Portal B = same prefab rotated 180° at exit
Gate at each portal mouth only when tunnelEnterable == false
```

## Mesh notes

- Shell is built as explicit inner/outer surfaces. **Portal has a front mouth rim only**; the rear collar stays open so mid segments can abut without coplanar Z-fighting.
- Mid segments have **no** end rims; mid includes a **floor slab** for enterable traversal.
- Do not stack mid and portal in the same Y range when previewing — abut mid at `y = 4` after the portal collar.

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

Build space in Blender: **X** lateral, **Y** along tunnel, **Z** up. After export, travel is **+Z** in Unity.

## Unity setup after import

1. Let Unity import the new FBXs (or **Assets → Refresh**).
2. Run **Create or Update Tunnel Kit Prefabs** (mid keeps MeshColliders; portal strips them).
3. Assign prefabs on `RoadNetworkSettings`; enable `tunnelEnterable` only after mouth stitch + traverse acceptance.
