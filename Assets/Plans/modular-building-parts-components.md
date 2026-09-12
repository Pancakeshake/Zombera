# Modular Building Parts — Component Plan

## Overview

The 12 prefabs in `Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/` are the kit pieces the `ModularSingleLevelHouseGeneratorTool` assembles into complete buildings. Right now they have meshes and colliders but none of the gameplay components (health, NavMesh, identity) needed for them to function as real in-game structures.

This plan covers what each prefab needs and which existing scripts to attach.

---

## Audit: Current Prefab State

| Prefab | Geometry | Collider | Needs Rebuild? |
|--------|----------|----------|----------------|
| `Wall.prefab` | ❌ (broken variant) | ❌ | **Yes** |
| `Window.prefab` | ❌ (broken variant) | ❌ | **Yes** |
| `Doorway.prefab` | ❌ (broken variant) | ❌ | **Yes** |
| `Front_Stairs.prefab` | ❌ (broken variant) | ❌ | **Yes** |
| `Floor.prefab` | ✅ | ✅ | No — add components only |
| `Foundation.prefab` | ✅ | ✅ | No — add components only |
| `Triangle_Floor.prefab` | ✅ | ✅ | No — add components only |
| `Triangle_Foundation.prefab` | ✅ | ✅ | No — add components only |
| `Half_Wall.prefab` | ✅ | ✅ | No — add components only |
| `Stairs.prefab` | ✅ | ✅ | No — add components only |
| `Light.prefab` | ✅ | ✅ | No — add components only |
| `Torch.prefab` | ✅ | ✅ | No — add components only |

The 4 broken prefabs are `PrefabInstance` YAML referencing an external GUID (`c025898837d27004486a54cf85275231`) — likely a missing base prefab or corrupted import. They must be rebuilt from scratch.

---

## Required Scripts (All Exist)

| Script | Namespace | Purpose |
|--------|-----------|---------|
| `BuildPiece` | `Zombera.BuildingSystem` | Category, WallType, snap points, health ref |
| `StructureHealth` | `Zombera.BuildingSystem` | Damage/destroy for structural pieces |
| `DoorHealth` | `Zombera.BuildingSystem` | Door-specific health, NavMesh carve disable on break |
| `DoorBlocker` | `Zombera.BuildingSystem` | Toggle NavMeshObstacle carve on door open/close |
| `DoorController` | `Zombera.BuildingSystem` | Door swing animation |
| `RuntimePlacedStructureFixer` | `Zombera.BuildingSystem` | Post-placement: collider, NavMeshObstacle, terrain flatten |

---

## Grid Specs (Generator Alignment)

- **Cell size**: 3m × 3m
- **Wall height**: 3m
- **Foundation top Y**: 0.1m
- **Floor surface Y**: 0.1m (above foundation)
- **All pieces must** pivot at bottom-center, origin at (0,0,0), X = width, Z = depth

---

## Per-Prefab Component Plan

### 1. Floor (`Floor.prefab`) — Walkable Surface

**Already has**: MeshFilter, MeshRenderer, BoxCollider (3×0.1×3)

**Add**:
- `BuildPiece`
  - `Category` = Floor
- `StructureHealth`
  - `Max Health` = 200
  - `Destroy GameObject On Death` = true

**NavMesh**: Ensure GameObject is marked Static (or tagged for runtime NavMesh). The flat top surface at Y=0 needs to be walkable.

---

### 2. Foundation (`Foundation.prefab`) — Ground Base

**Already has**: MeshFilter, MeshRenderer, BoxCollider (3×0.15×3)

**Add**:
- `BuildPiece`
  - `Category` = Floor (or Utility)
- `StructureHealth`
  - `Max Health` = 300

---

### 3. Triangle Floor (`Triangle_Floor.prefab`)

**Already has**: MeshFilter, MeshRenderer, MeshCollider

**Add**:
- `BuildPiece`
  - `Category` = Floor
- `StructureHealth`
  - `Max Health` = 200

---

### 4. Triangle Foundation (`Triangle_Foundation.prefab`)

**Already has**: MeshFilter, MeshRenderer, MeshCollider

**Add**:
- `BuildPiece`
  - `Category` = Floor
- `StructureHealth`
  - `Max Health` = 300

---

### 5. Wall (`Wall.prefab`) — REBUILD NEEDED

**Must build from scratch** (current file is a broken PrefabInstance).

**Hierarchy**:
```
Wall (root)
├── Mesh (child) — MeshFilter + MeshRenderer, 3m wide × 3m tall × 0.3m thick
```

**Root components**:
- `BoxCollider` — size (3, 3, 0.3), center (0, 1.5, 0)
- `BuildPiece`
  - `Category` = Wall
  - `Wall Type` = Full
- `StructureHealth`
  - `Max Health` = 150

**NavMesh**: `NavMeshObstacle` with Carve enabled (the `RuntimePlacedStructureFixer` auto-adds this if `ensureNavMeshObstacle` is true, but having it baked in is safer).

---

### 6. Window (`Window.prefab`) — REBUILD NEEDED

**Hierarchy**:
```
Window (root)
├── Mesh (child) — MeshFilter + MeshRenderer, wall with window cutout, 3×3×0.3
```

**Root components**:
- `BoxCollider` — size (3, 3, 0.3), center (0, 1.5, 0)
- `BuildPiece`
  - `Category` = Wall
  - `Wall Type` = Window
- `StructureHealth`
  - `Max Health` = 100 (weaker than solid wall)

---

### 7. Doorway (`Doorway.prefab`) — REBUILD NEEDED

**Most complex prefab**. Needs door mechanics (swing, blocker, breakable door).

**Hierarchy**:
```
Doorway (root) — has BuildPiece + StructureHealth
├── WallMesh (child) — MeshFilter + MeshRenderer, wall with door cutout
├── DoorBlockerNode (child) — NON-ROTATING, holds NavMeshObstacle + DoorHealth
│   └── (no mesh, just the obstacle collider zone)
└── DoorPanel (child) — ROTATES, holds the actual door mesh + DoorController + DoorBlocker
    └── DoorMesh (grandchild) — MeshFilter + MeshRenderer
```

**Root components**:
- `BoxCollider` — size (3, 3, 0.3), center (0, 1.5, 0)
- `BuildPiece`
  - `Category` = Wall
  - `Wall Type` = Door
- `StructureHealth`
  - `Max Health` = 150 (wall body is solid)

**DoorBlockerNode components**:
- `NavMeshObstacle` — Shape = Box, Size = (1, 2.2, 0.5), Center = (0, 1.1, 0), Carve = true
- `DoorHealth`
  - `Max Health` = 80
  - `Obstacle` = reference to the NavMeshObstacle above
  - `Custom Door Leaf` = reference to DoorPanel's DoorController

**DoorPanel components**:
- `DoorController`
  - `Open Angle` = 90
  - `Swing Duration` = 0.25
  - `Starts Open` = false
- `DoorBlocker`
  - `Obstacle` = reference to DoorBlockerNode's NavMeshObstacle
  - `Custom Door Leaf` = reference to self's DoorController

**DoorMesh**:
- MeshFilter + MeshRenderer (the visual door plank, ~0.9m wide × 2.1m tall)
- Pivot at hinge edge (left or right side, local X=±0.45)

---

### 8. Half Wall (`Half_Wall.prefab`)

**Already has**: MeshFilter, MeshRenderer, BoxCollider

**Add**:
- `BuildPiece`
  - `Category` = Wall
  - `Wall Type` = Full
- `StructureHealth`
  - `Max Health` = 100 (half height = less material)

**Note**: Collider should be ~1.5m tall (half of the full 3m wall).

---

### 9. Stairs (`Stairs.prefab`)

**Already has**: MeshFilter, MeshRenderer, MeshCollider

**Add**:
- `BuildPiece`
  - `Category` = Utility
- `StructureHealth`
  - `Max Health` = 120

**NavMesh**: Needs to be walkable. Stair mesh should be on a layer that contributes to NavMesh baking.

---

### 10. Front Stairs (`Front_Stairs.prefab`) — REBUILD NEEDED

**Similar to Stairs but for exterior** (no roof coverage). Generator uses `ExteriorStairsPath` for this.

**Rebuild with same components as Stairs** but probably wider/shallower steps (4.5m wide, 1.5m deep).

- `BuildPiece`
  - `Category` = Utility
- `StructureHealth`
  - `Max Health` = 100

---

### 11. Light (`Light.prefab`)

**Already has**: MeshFilter, MeshRenderer, BoxCollider

**Add**:
- `Light` (Unity component)
  - Type = Point
  - Range = 8
  - Intensity = 3
  - Color = warm white (~3000K)
- `BuildPiece`
  - `Category` = Utility
- `StructureHealth`
  - `Max Health` = 50

---

### 12. Torch (`Torch.prefab`)

**Already has**: MeshFilter, MeshRenderer, BoxCollider

**Add**:
- `Light` (Unity component)
  - Type = Point
  - Range = 6
  - Intensity = 2
  - Color = orange/fire (~1800K)
  - Shadows = off (performance)
- `BuildPiece`
  - `Category` = Utility
- `StructureHealth`
  - `Max Health` = 40

---

## Generator Compatibility

The generator expects these exact filenames in the kit folder:
| Generator Reference | Expected File |
|---------------------|---------------|
| `FoundationPath` | `Building_Foundation.prefab` |
| `FloorPath` | `Building_Floor.prefab` |
| `WallPath` | `Building_Wall.prefab` |
| `DoorwayPath` | `Building_Doorway.prefab` |
| `WindowPath` | `Building_Window.prefab` |
| `StairPath` | `Building_Stair.prefab` |
| `RoofPath` | `Building_Roof.prefab` (falls back to Floor) |
| `ExteriorStairsPath` | `Building_Stair.prefab` (or custom) |

> **Note**: Current filenames don't have `Building_` prefix — e.g., `Wall.prefab` not `Building_Wall.prefab`. The generator code constructs paths as `{KitFolder}/Building_Wall.prefab`. Either rename the prefabs or update `Assets.cs` line ~88-93.

---

## Execution Order

1. **Fix filenames** — rename prefabs to match generator expectations (or update `PopulatePrefabPaths` in `Assets.cs`)
2. **Rebuild 4 broken prefabs** — Wall, Window, Doorway, Front_Stairs
3. **Add components to 8 existing prefabs** — Floor, Foundation, Triangle_*, Half_Wall, Stairs, Light, Torch
4. **Test generation** — run the generator with Studio template, verify the output prefab has all components
5. **Rebuild building catalog** — run Tools → World → Buildings → Rebuild Building Catalog
