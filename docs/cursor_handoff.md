# Cursor + Copilot Handoff (Zombera)

Updated: 2026-04-23

## Purpose

Use this file as a shared context baseline for both Cursor and Copilot.

Goals:
- Give fast, accurate project understanding.
- Reduce duplicate exploration prompts.
- Make parallel edits safer when both assistants are used at the same time.

## Project Snapshot

- Engine: Unity (URP).
- Genre: top-down zombie survival squad sandbox.
- Runtime focus: world streaming, unit combat, squad command flow, inventory/loot, zombie pressure, base-building placement loop.
- Important note: some docs are older planning artifacts; runtime code is the source of truth.

## Scene Pipeline

Build order:
1. Boot
2. MainMenu
3. Loading
4. World_MapMagicStream

Primary flow:
- Boot initializes core systems through GameManager.
- MainMenu routes through character creator into StartNewGame.
- Loading handles transition and progress overlay.
- World scene runs world systems, spawning, simulation, and HUD.

## Core Architecture (Authoritative Files)

Top-level orchestration:
- Assets/Core/GameManager.cs
- Assets/Core/EventSystem.cs
- Assets/Core/TimeSystem.cs
- Assets/Core/SaveSystem.cs
- Assets/Core/StartupReadinessValidator.cs

Manager layer:
- Assets/Systems/UnitManager.cs
- Assets/Systems/CombatManager.cs
- Assets/Systems/AIManager.cs
- Assets/Systems/SquadManager.cs
- Assets/Systems/CommandSystem.cs
- Assets/Systems/ZombieManager.cs
- Assets/Systems/SaveManager.cs
- Assets/Systems/BaseManager.cs

Unit model:
- Assets/Characters/Unit.cs
- Assets/Characters/UnitController.cs
- Assets/Characters/UnitCombat.cs
- Assets/Characters/UnitInventory.cs

World model:
- Assets/World/WorldManager.cs
- Assets/World/Simulation/WorldSimulationManager.cs
- Assets/World/WorldEventSystem.cs
- Assets/World/ChunkLoader.cs
- Assets/World/RegionSystem.cs
- Assets/World/TerrainResolver.cs
- Assets/World/MapSpawner.cs

Input/UI flow:
- Assets/Systems/PlayerInputController.cs
- Assets/UI/Menus/MainMenuController.cs
- Assets/UI/Menus/CharacterCreatorController.cs
- Assets/UI/Menus/LoadingSceneController.cs
- Assets/UI/Menus/LoadingScreenOverlay.cs
- Assets/UI/RuntimeUiEventSystemUtility.cs

MapMagic and spawn bootstrap:
- Assets/Characters/PlayerSpawner.cs

## Current Runtime Reality (Important)

- Player/world bootstrap and navmesh handling have been actively hardened around MapMagic runtime behavior.
- Runtime navmesh defaults are now terrain-first, with MeshFilter sources opt-in to avoid dynamic overlay contamination.
- UnitController has stronger navmesh recovery sampling for large vertical offsets.
- MapSpawner avoids creating fallback flat ground when MapMagic terrain exists.

## Known Documentation Drift

- docs/project_context.md and docs/architecture.md contain useful intent but are partially stale compared to implemented code.
- docs/bugs.md currently contains placeholder content and is not a reliable issue ledger.

## Editor Tooling Philosophy

This repo strongly prefers repeatable Editor commands over one-off manual setup.

Reference:
- .github/copilot-instructions.md

Examples:
- Assets/Editor/MapMagicWorldSetupTool.cs
- Assets/Editor/FogOfWarSetupTool.cs
- Assets/Editor/WorldHUDSetupTool.cs
- Assets/Editor/StartupSquadSetupTool.cs

## Parallel Cursor + Copilot Workflow (Recommended)

Use lane-based ownership.

Lane A (World/Runtime):
- GameManager, PlayerSpawner, WorldManager, ZombieManager, navmesh/spawn/runtime simulation files.

Lane B (UI/Tools/Data):
- UI menus, HUD setup tools, item data/tools, docs, editor automation, visual polish.

Avoid both assistants editing the same file in the same time window.

### Conflict Prevention Rules

1. Declare file ownership before prompting either assistant.
2. Keep prompts scoped to a small file set.
3. Save frequently and reload file changes before new prompts.
4. If overlap is unavoidable, serialize changes: one assistant edits first, second assistant rebases on latest file state.

### Git Strategy

Preferred branch naming:
- cursor/<task>
- copilot/<task>

If doing true parallel local work, prefer separate worktrees.

Suggested sequence:
1. Implement in separate branch/worktree.
2. Compile and fix errors.
3. Run focused smoke test for touched systems.
4. Merge one branch at a time.

### Merge Checklist

- Boot -> MainMenu -> Loading -> World flow works.
- Player spawns on valid terrain (no void/origin fallback).
- NavMesh triangulation is coherent (no fragmented strips from overlay meshes).
- Player/squad units can path and do not immediately fall back to transform movement unexpectedly.
- At least one zombie encounter path remains functional.

## Prompt Starters

Cursor starter:
"Read docs/cursor_handoff.md first. Treat runtime code as source of truth if docs conflict. I am working on: <task>. Restrict edits to: <files>."

Copilot starter:
"Use docs/cursor_handoff.md as baseline context. Preserve existing architecture and editor-tooling workflow. I am editing: <files> for <task>."

## Priority Indexing Order For New Sessions

1. Assets/Core/GameManager.cs
2. Assets/Characters/PlayerSpawner.cs
3. Assets/World/WorldManager.cs
4. Assets/Systems/PlayerInputController.cs
5. Assets/Systems/ZombieManager.cs
6. Assets/Systems/SquadManager.cs
7. Assets/Systems/CommandSystem.cs
8. Assets/Characters/UnitController.cs
9. Assets/Systems/SaveManager.cs
10. Assets/Editor/MapMagicWorldSetupTool.cs
11. Assets/Editor/WorldHUDSetupTool.cs
12. .github/copilot-instructions.md

## Guardrails

- Work from the main project root (Assets/...), not nested sample paths.
- Do not revert unrelated local changes.
- Prefer idempotent editor automation for repeat setup tasks.
- Keep naming and folder conventions aligned with existing Zombera systems.
