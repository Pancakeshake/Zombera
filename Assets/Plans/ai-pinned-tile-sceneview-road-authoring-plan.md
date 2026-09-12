# AI Plan: Pinned Tile Scene View Road Authoring + Later Bake

## 1) Goal
Enable a workflow where you can edit pinned MapMagic terrain/splines in Scene view, see roads regenerate immediately in-editor, and then run a controlled bake/commit step for production runtime data.

## 2) Target Workflow
1. Pin a MapMagic tile.
2. Enter Road Authoring Mode (Editor-only).
3. Edit terrain and spline inputs fghngfgfin Scene view.
4. Roads regenerate for pinned tile only (fast preview loop).
5. Approve result.
6. Run Bake/Commit command:
   - rebuild gameplay graph/derived data
   - bake masks
   - return to runtime tile-stream mode

## 3) Architecture Decision
Keep two explicit modes:
1. Authoring Preview Mode (Editor only, pinned tile focused).
2. Runtime Streaming Mode (Play/runtime, tile-event driven).

Do not mix these modes automatically. Use explicit toggle and explicit commit action.

## 4) Scope of Changes

## 4.1 Runtime component changes
File: Assets/01_Game/02_World/Roads/ProceduralRoadSystem.cs
1. Add serialized mode fields:
   - `enableEditorPinnedTileAuthoringMode`
   - `autoRegenPinnedTileOnSceneChanges`
   - `authoringRegenDebounceSeconds`
2. Add editor-safe public methods:
   - `RegeneratePinnedTilePreviewNow()`
   - `ClearPinnedTilePreviewRoads()`
3. Ensure preview regeneration is tile-scoped and does not mutate unrelated streamed tiles.

## 4.2 Editor tooling commands
File: Assets/Editor/WorldRoadGenerationSetupTool.cs
Add menu commands under one group:
1. `Tools/1.Quick Dev Tools/Roads/Authoring/Enable Pinned Tile Road Authoring`
2. `Tools/1.Quick Dev Tools/Roads/Authoring/Regenerate Pinned Tile Roads Now`
3. `Tools/1.Quick Dev Tools/Roads/Authoring/Clear Pinned Tile Road Preview`
4. `Tools/1.Quick Dev Tools/Roads/Authoring/Bake And Commit Road Data`
5. `Tools/1.Quick Dev Tools/Roads/Authoring/Disable Pinned Tile Road Authoring`

## 4.3 Bake/commit orchestration
Reuse existing road tooling commands in sequence:
1. Build graph from authoring/runtime source.
2. Bake derived road data.
3. Bake road masks.
4. Save assets and mark scene dirty.
5. Disable authoring mode toggle and restore runtime mode defaults.

Primary existing tooling to call:
- Assets/Editor/RoadGameplayTooling.Commands.cs
- Assets/Editor/RoadGameplayTooling.MaskBake.cs

## 5) Mode Behavior Specification

## Authoring Preview Mode
1. Works only in Editor.
2. Operates only on pinned tile context.
3. Regenerates roads from current MapMagic spline output.
4. Applies terrain stamp preview for that tile.
5. Keeps preview roads grouped under a deterministic container for easy clear/rebuild.

## Runtime Streaming Mode
1. Uses tile apply events via MapMagicTileStreamBridge.
2. Uses existing per-tile generation path.
3. No editor-only hooks active.

## 6) Data Safety and Guardrails
1. Never overwrite full-world road runtime state from pinned tile preview only.
2. Never bake masks implicitly during every Scene edit.
3. Require explicit Bake/Commit command.
4. Add validation before commit:
   - MapMagic target bound
   - pinned tile exists
   - spline source exists
   - RoadGameplayService + graph assets resolve

## 7) Preview Ownership Rules
1. Preview roads created in authoring mode must be tagged/grouped as preview-owned.
2. Clear command removes only preview-owned roads.
3. Commit command should rebuild from source graph/splines, not from preview scene objects.

## 8) Performance Plan
1. Debounce Scene-change regeneration (default 0.2-0.4s).
2. Cap regen frequency to avoid editor spikes while dragging spline points.
3. Keep pinned tile only; do not regenerate neighboring tiles during authoring.
4. Log regen duration and road count for tuning.

## 9) UX Plan
1. Inspector status block on ProceduralRoadSystem:
   - current mode
   - pinned tile id
   - last regen time
   - last preview road count
2. Optional gizmos for source spline vs generated centerline alignment.
3. Dialog confirmations for clear/commit commands.

## 10) Implementation Phases

## Phase A: Controls and plumbing
1. Add authoring-mode flags and preview APIs to ProceduralRoadSystem.
2. Add menu commands in WorldRoadGenerationSetupTool.

## Phase B: Pinned-tile preview regen
1. Implement pinned tile resolve path.
2. Implement clear-and-regenerate preview cycle.
3. Add debounce and diagnostics.

## Phase C: Bake/commit command
1. Wire existing RoadGameplayTooling build/bake/mask steps.
2. Add preflight validation + post-bake summary dialog.

## Phase D: Hardening
1. Ownership-safe clear logic.
2. Mode rollback on failure.
3. Team-facing setup/report command.

## 11) Validation Checklist
1. Editing pinned tile spline updates only pinned tile preview roads.
2. Clearing preview removes only preview-owned objects.
3. Bake/commit updates graph, derived data, and masks successfully.
4. Runtime play mode still generates from tile-stream events correctly.
5. No duplicate road generation owners active simultaneously.

## 12) Recommended Default Settings
1. `enableEditorPinnedTileAuthoringMode = false` by default.
2. `autoRegenPinnedTileOnSceneChanges = true` when mode enabled.
3. `authoringRegenDebounceSeconds = 0.25`.

## 13) Best-Practice Summary
Yes, use pinned-tile Scene view authoring for fast iteration, but keep it as an explicit editor preview mode and always finalize through an explicit bake/commit step before runtime/world deployment.