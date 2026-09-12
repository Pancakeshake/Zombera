# Modular highway bridge mesh kit (Blender MCP)

**Status: IMPLEMENTED (assets)** — FBX + Blender source + docs + editor prefab tool shipped. Run Unity menu **Tools → World → Roads → Create or Update Bridge Kit Prefabs** after import. Does **not** implement water pathfinding corridors or `BridgeMeshPlacer`.

**Default timing:** ship as its **own asset PR**, parallel with water-corridor work; placer consumes these prefabs later.

Related: [plan-first one paint](../../../.cursor/plans/plan-first_one_paint_1c2061b8.plan.md) workstreams C–D; tunnel pattern [modular-highway-tunnel-mesh-kit.md](modular-highway-tunnel-mesh-kit.md).

---

## Piece set

Paths:

- Meshes: `Assets/02_Shared/Meshes/Roads/Bridge/`
- Prefabs: `Assets/02_Shared/Prefabs/Props/Road/Bridge/`
- Docs: `docs/highway-bridge-kit.md`

| Piece | File | Role |
|-------|------|------|
| Mid | `SM_Bridge_Mid_10m.fbx` | Deck + rails, 10 m along Z |
| Abutment | `SM_Bridge_Abutment.fbx` | Bank seat + apron + wings |
| Pier | `SM_Bridge_Pier.fbx` | Intermediate support under deck |
| Gate | Unity-only | `Bridge_PortalGate.prefab` |

## Hard dimensions

Derived from `RoadNetworkSettings.highwayWidth` = **12**:

- Clear width **14.0 m**, deck thickness **0.55 m**, rail **1.15 m**
- Mid **10 m**; abutment seat **4 m**; non-enterable v1 via gate (no MeshCollider on visuals)

## Acceptance

- No internal z-fighting (rails outside deck; abutment boolean-unioned)
- FBX Y-up, pivot at roadbed centerline
- Prefabs + docs describe placer contract
- `.blend` includes `HighwayBridgePreview40m` for visual QA
