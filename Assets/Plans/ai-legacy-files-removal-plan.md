# AI Plan: Legacy File Cleanup and Removal

## 1) Objective
Remove legacy and stale files safely, reduce maintenance overhead, and avoid breaking scene/prefab/script compatibility.

This plan is based on a project scan of:
- deprecation markers (`[Obsolete]`, `legacy`, `deprecated`, fallback wrappers)
- backup/stray files
- duplicate nested Unity project folders
- runtime references to legacy components

## 2) Scan Findings (Evidence-Based)

## 2.1 High-confidence stale file
- [Assets/01_Game/02_World/Roads/ProceduralRoadSystem.cs.bak](Assets/01_Game/02_World/Roads/ProceduralRoadSystem.cs.bak)

Why it is a candidate:
- `.bak` source backup in project tree
- not part of intended Unity script set

Action:
- remove immediately (or move to external archive outside repo)

## 2.2 Deprecated controller likely removable (after one final scene sweep)
- [Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs](Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs)

Evidence:
- explicitly marked `[System.Obsolete("Use SaveGameMenuController...")]`
- scan found no code references
- GUID search found no prefab/scene/asset references in scanned Unity YAML assets

Action:
- remove in Phase 1 if final full GUID reference check is still zero

## 2.3 Obsolete wrappers: mixed state

### Likely removable wrappers
- [Assets/01_Game/03_Characters/AI/SurvivorAI.cs](Assets/01_Game/03_Characters/AI/SurvivorAI.cs)
- [Assets/01_Game/03_Characters/AI/SquadAI.cs](Assets/01_Game/03_Characters/AI/SquadAI.cs)

Evidence:
- both marked obsolete wrapper types
- no prefab/scene/asset GUID hits found in scan

Action:
- remove in Phase 2 after final project-wide GUID confirmation

### Migrate-first wrapper (not immediate delete)
- [Assets/01_Game/00_Framework/AI/ZombieAI.cs](Assets/01_Game/00_Framework/AI/ZombieAI.cs)

Evidence:
- obsolete wrapper class
- still referenced by multiple zombie prefabs (GUID hits found)

Action:
- do not delete yet
- migrate prefabs from `ZombieAI` to `ZombieController` first

## 2.4 Legacy system that is still active (not removable yet)
- [Assets/01_Game/07_Building/Placement/BuildPlacementController.cs](Assets/01_Game/07_Building/Placement/BuildPlacementController.cs)
- [Assets/01_Game/01_Core/Input/PlayerInputController.cs](Assets/01_Game/01_Core/Input/PlayerInputController.cs)
- [Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.BuildHud.Dependencies.cs](Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.BuildHud.Dependencies.cs)

Evidence:
- despite "Legacy Build Mode" labeling, runtime HUD/player input still depends on this path

Action:
- treat as migrate-first technical debt, not deletion target

## 2.5 Repository clutter candidates (outside active Unity asset flow)
- duplicate nested project folders:
  - [My project](My project)
  - [Zombera](Zombera)
- root ad-hoc issue parsing artifacts/scripts:
  - [extract_issues.js](extract_issues.js)
  - [issues_nz.js](issues_nz.js)
  - [list_rz.js](list_rz.js)
  - [list_rz2.js](list_rz2.js)
  - [parse_rider.js](parse_rider.js)
  - [show_rz.js](show_rz.js)
  - [issues_nz.txt](issues_nz.txt)
  - [rz_err.txt](rz_err.txt)
  - [rz_err_content.txt](rz_err_content.txt)
  - [rz_issues.txt](rz_issues.txt)
  - [rider_issues.json](rider_issues.json)

Action:
- move to `Archive/legacy-cleanup/` or delete if confirmed unused by your current workflow

Note:
- keep current SARIF/derived artifacts workflow if you still rely on it (`scripts/sort_rider_sarif_for_action.py` etc.)

## 3) Removal Strategy (Phased)

### Phase 0: Safety snapshot
1. Create a cleanup branch.
2. Export a list of candidate files and current GUID references.
3. Record baseline compile status.

### Phase 1: Safe immediate deletes
1. Remove [Assets/01_Game/02_World/Roads/ProceduralRoadSystem.cs.bak](Assets/01_Game/02_World/Roads/ProceduralRoadSystem.cs.bak).
2. Remove [Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs](Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs) if final GUID check remains zero.
3. Run Unity compile + scene open smoke test.

### Phase 2: Remove unused obsolete wrappers
1. Re-run GUID reference check for:
   - [Assets/01_Game/03_Characters/AI/SurvivorAI.cs](Assets/01_Game/03_Characters/AI/SurvivorAI.cs)
   - [Assets/01_Game/03_Characters/AI/SquadAI.cs](Assets/01_Game/03_Characters/AI/SquadAI.cs)
2. If still unreferenced, delete both.
3. Compile + playmode smoke.

### Phase 3: Migrate then remove ZombieAI
1. Build an editor migration tool to replace `ZombieAI` components with `ZombieController` on prefabs/scenes.
2. Batch-apply migration to all prefabs referencing `ZombieAI`.
3. Re-scan GUID usage to confirm zero references.
4. Delete [Assets/01_Game/00_Framework/AI/ZombieAI.cs](Assets/01_Game/00_Framework/AI/ZombieAI.cs).

### Phase 4: Repository clutter cleanup
1. Move duplicate project folders ([My project](My project), [Zombera](Zombera)) outside repo or to archive if they are historical copies.
2. Consolidate root one-off scripts/log dumps into `Archive/legacy-cleanup/` or delete.
3. Update `.gitignore` to prevent regeneration of transient root dumps.

## 4) Verification Checklist
After each phase:
1. Unity compile clean (no missing script errors).
2. Open key scenes and validate:
   - world boot
   - pause menu + save/load menu flow
   - zombie spawn/AI behavior
   - build HUD and build placement flows
3. Check console for missing MonoBehaviour script warnings.
4. Re-run GUID scans for removed scripts to ensure zero residual references.

## 5) Guardrails
- Do not remove any `Assets/ThirdParty/**` files as part of this pass unless they are explicit backups you created.
- Do not remove legacy-labeled files still referenced by prefabs/scenes.
- Prefer migration tools for component-type swaps instead of manual prefab edits.

## 6) Recommended First Cleanup PR
Scope:
1. Delete [Assets/01_Game/02_World/Roads/ProceduralRoadSystem.cs.bak](Assets/01_Game/02_World/Roads/ProceduralRoadSystem.cs.bak).
2. Delete [Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs](Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs) after one final GUID check.
3. (Optional) delete [Assets/01_Game/03_Characters/AI/SurvivorAI.cs](Assets/01_Game/03_Characters/AI/SurvivorAI.cs) and [Assets/01_Game/03_Characters/AI/SquadAI.cs](Assets/01_Game/03_Characters/AI/SquadAI.cs) if final reference check is clean.

This gives you immediate cleanup value with low break risk.