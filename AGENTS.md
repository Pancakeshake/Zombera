# Zombera — AI Agent Instructions

Shared standards for AI coding agents in this repo (read by both Cline and VS Code Copilot).

## 1. Project Context

Unity project (URP, C#) — zombie survival game with RTS squad mechanics, procedural world generation on **Unity Terrain**, streaming NavMesh, base building, faction systems, and crafting.

**Terrain note:** The active surface is Unity Terrain. MapMagic is not the current terrain system (Legacy/third-party MapMagic code may still exist — do not assume it for new work).

### Key Directories

| Directory | Purpose |
|-----------|---------|
| `Assets/01_Game/` | Core game code — frameworks, systems, world, UI |
| `Assets/Editor/` | Editor-only tools and utilities |
| `Assets/Scripts/` | Legacy/general scripts |
| `Assets/00_Scenes/` | Unity scenes |
| `Assets/Plans/` | AI-generated plan documents for features/refactors |
| `Assets/Tests/` | Test assemblies |
| `Packages/` | UPM packages (local embedded + registry) |
| `docs/` | Architecture docs, design docs, specs |
| `scripts/` | Python/PowerShell helper scripts |

### Exploration Guidance

- **Don't over-explore.** If you're doing more than 3–4 file operations before starting work, ask the user.
- **Search `Assets/01_Game/` first** for gameplay code, then `Assets/Editor/`.
- Use the **SigMap** signature index before broad file searches — `sigmap ask "<question>"` or `sigmap --query "<topic>"` return signatures without reading whole files.
- Check `Packages/manifest.json` for local/embedded packages before editing package code.
- **WorldBuilder:** read `docs/world-builder-pipeline.md` (SoT → task → STOP, ≤3–4 opens). Never invent stage order. Order/prereqs/outputs → `WorldBuildStageRegistry` only.

---

## 2. Code Complexity & File Size (Hard Rules)

### Script Length: Below 500 Lines

- **Every `.cs` file stays below 500 lines.** When approaching the limit, split via:
  - **Partial classes**: `ClassName.Responsibility.cs`
  - **Extract sub-responsibility**: move cohesive logic into a static utility or collaborator class
  - **Extract data structures**: move structs/enums/data classes into their own files
  - Existing pattern: `WorldHUDController.BuildHud.CatalogState.cs`, `ProceduralRoadSystem.TileBuild.cs`, `ModularSingleLevelHouseGeneratorTool.{Assets,Models,Generation,Building}.cs`

### Cognitive Complexity: Below 15 Per Method

- **Every method must have cognitive complexity below 15.**
  - **Guard clauses / early returns** — flip `if (cond) { ...big block... }` into `if (!cond) return;`
  - **Extract helper methods** — if a method does 3+ distinct things, extract each
  - **Replace nested conditionals** — `switch`, dictionaries, or polymorphism over deep `if/else`
  - **Limit loop nesting** — never more than 2 levels without extraction
  - **Avoid flag-driven flow** — separate methods over `if (isPhaseA) { ... } else { ... }`

### When Splitting Files

- Keep the **primary file** (no suffix) for the core class, fields, and Unity lifecycle methods (`Awake`, `Start`, `Update`).
- Name partials by responsibility: `ClassName.Responsibility.cs`; each gets a header comment describing its scope.
- All partials share the same namespace and `partial` class declaration.

---

## 3. Architecture Preservation

### World vs Menu Gating

- Never spawn/initialize world systems in `MainMenu`.
- Gate by `GameManager.Instance.CurrentState`: **allowed** `LoadingWorld`, `Playing`, `Paused`; **blocked** `MainMenu`.
- Check `GameState` before terrain/NavMesh/chunk/zombie work.

### Ownership Table

Prefer changing the **owner** of a responsibility, not adding side effects elsewhere.

| Owner | Responsibility |
|-------|---------------|
| `WorldManager` | World-session lifecycle + chunk streaming tick + simulation tick |
| `PlayerSpawner` | Spawn/bootstrap + world-tile stabilization + initial NavMesh readiness |
| `StreamingNavMeshTileService` | Per-tile NavMesh build/removal (world tile stream events) |
| `CombatEncounterManager` | Encounter rules and attack resolution |

### Hidden Runtime Wiring

- Prefer **scene-visible refs/components** over auto-add at runtime.
- If auto-add is needed, make it **idempotent** and **log once**.

### Tuning

- If a value is adjusted repeatedly, **promote it to a serialized field or config asset** — don't duplicate constants.

---

## 4. Streaming & NavMesh Conventions

### World tile events

- Prefer first-party world tile stream events (`WorldTileStreamSource` / `IWorldTileGameplayEvents`) for tile-scoped updates.
- Do not wire legacy MapMagic `TerrainTile.OnTileApplied` / `OnAllComplete` for new work.

### NavMesh

- Prefer `StreamingNavMeshTileService` for procedural streaming worlds.
- If using `NavMeshSurface`, treat it as authoritative — avoid double-building via `NavMeshBuilder` in the same flow.

### Spawn Safety

- Don't trust raw XZ until **at least one ContentReady world tile exists**.
- Prefer "flatter" spawn selection (slope/normal sampling) over fixed points.

### Performance

- Avoid per-frame `FindObjectsByType` in `Update` — cache or run on ticks/events.

---

## 5. C# Style & Performance

### Type Style

- Prefer `var` when the RHS makes the type obvious.
- Prefer target-typed `new()` when the declared type is explicit.
- Prefer explicit types when it improves readability or the RHS is not obvious.

```csharp
// Good
var hitCount = Physics.OverlapSphereNonAlloc(pos, r, _hits, mask);
List<Unit> allies = new(8);
ZombieController zombie = Instantiate(zombiePrefab);
```

### Control Flow

- Prefer **guard-clauses and early returns** to reduce nesting.
- Keep state machines explicit — no transitions hidden in clever expressions.

### Naming

- **Public API**: `PascalCase`; **private fields**: `_camelCase`
- **Serialized fields**: keep private, still `_camelCase` (`[SerializeField]`)
- No abbreviations unless domain-standard (e.g., `NavMesh`, `UMA`)

### Performance-Critical Code (Update/FixedUpdate/Ticks)

- **No per-frame allocations**: no LINQ, `ToList`, string concatenation, or `new` in tight loops unless unavoidable.
- Prefer **NonAlloc** physics queries (`OverlapSphereNonAlloc`) and reuse buffers.
- **Cache component lookups** (`GetComponent`) outside hot paths.
- Hoist repeated `UnityEngine.Object` null checks out of tight loops.

### Dead Code

- If a field is only assigned, either **use it** or **delete it**.
- If a collection is only updated, either **query it** or **remove it**.
- Prefer **deleting unused private helpers** over suppressing warnings.

---

## 6. Subagent & Efficiency Strategy

### Subagent Delegation

- **Delegate to subagents wherever possible**: file exploration and code reading, signature searches and context gathering, simple localized edits, boilerplate generation.
- Reserve the **main context** for: architecture reasoning and multi-file coordination, complex refactors requiring cross-system understanding, plan creation and review.

### Parallel Operations

- Run up to 5 focused research tasks in parallel when reading many files.
- Batch multiple SEARCH/REPLACE edits to the same file in a single call.

### Context Preservation

- Use **SigMap** before broad file searches; use its blast-radius/impact checks before modifying shared files.
- Read exact line ranges instead of whole files when working from signature anchors.
- After writing new files or symbols, notify the index via MCP, or regenerate with `sigmap --adapter copilot` so signatures stay current.
- Regenerate after major refactors or new systems: `sigmap --adapter copilot` from a terminal (requires a TTY; rewrites the auto-generated block in `.github/copilot-instructions.md` — never hand-edit below its marker).

### Edit Efficiency

- Prefer targeted edits; use full rewrites only for new files or complete overhauls.
- After any edit, use the returned final state as the reference for subsequent edits.

---

## 7. Repository Guardrails

- Work in the **main project root** (`Assets/...`), not the nested sample project under `Zombera/Assets/...`.
- Do not revert unrelated changes.
- Keep naming consistent with existing Zombera systems and folders.
- **Preserve serialized field names** and **public UnityEvent/API** names unless intentionally migrating assets.
- Prefer **small, reviewable diffs** per file or subsystem — don't mix unrelated cleanups.
- Exclude files too large for normal Git commits (bulk archives, vendor dumps); prefer imported project assets plus source links. Use Git LFS only for intentionally versioned large binaries.
- Treat large package/container formats (`.unitypackage`, `.zip`, `.7z`, `.rar`, `.tar`, `.gz`) as non-source artifacts that should stay ignored unless explicitly required.

### Enviro Runtime Behavior

- Enviro 3 weather startup may auto-add a `BoxCollider` and `Rigidbody` to the Enviro manager at runtime — treat as expected third-party behavior unless it causes gameplay/system side effects.
- If side effects occur, prefer **layer and raycast-mask isolation** before modifying Enviro third-party code.

---

## 8. Running C# in Unity

When executing C# in Unity (creating objects, modifying scenes, adding components) via MCP/`unity-mcp-cli`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 1. Your logic here
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // 2. Register changes for Undo/Redo and tracking
        result.RegisterObjectCreation(cube);

        // 3. Log the result
        result.Log("Created {0}", cube);
    }
}
```

### Rules for Success

1. **Class name MUST be `CommandScript`** — any other name causes failure.
2. **Use `internal` accessibility** — `public` causes "Inconsistent Accessibility" errors.
3. **Use the `result` object**:
   - `result.RegisterObjectCreation(obj)` after creating objects
   - `result.RegisterObjectModification(obj)` BEFORE changing properties
   - `result.DestroyObject(obj)` instead of `Object.DestroyImmediate`
   - `result.Log(...)`, `result.LogWarning(...)`, `result.LogError(...)`
4. **Avoid top-level statements** — always wrap in the class structure.

### Verifying Work

Unity work often has **no immediate feedback**. Always verify:
- **Object creation/modification** → take a screenshot
- **Code execution** → check console for errors
- **Multi-step task** → verify after each logical phase

### Unity MCP (IvanMurzak) stdio Tools

- Unity tools are also available as stdio MCP tools (prefix `mcp_gamedev-mcp-s_*`, e.g. `assets-find`, `gameobject-find`, `console-get-logs`). Prefer calling these MCP tools over spawning `unity-mcp-cli` in a terminal when verifying work.
- These tools require the `ai-game-developer` stdio server from `.vscode/mcp.json` to be running (port `24566`). If Unity logs show connection-refused errors against `localhost:24566`, start the MCP server in the IDE rather than changing code.