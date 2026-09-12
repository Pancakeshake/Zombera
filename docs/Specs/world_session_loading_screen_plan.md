# WORLD SESSION LOADING SCREEN IMPLEMENTATION PLAN

System Name:
Intermediate World Session Loading Flow (Black Screen)

System Type:
Scene Flow + UI + Runtime Bootstrapping

---

# GOAL

Implement an intermediate loading step between Main Menu and World so:

- the player never sees gameplay user data before world load completes,
- the loading UI stays in front (full black screen for now),
- world session start is deterministic,
- cross-scene references are removed from menu preview wiring.

---

# CURRENT FINDINGS (FROM LOGS + SCAN)

1. `GameManager.StartNewGame()` can call `BeginWorldSession()` immediately when active scene is already `World`.
2. `StartupReadinessValidator` currently reports 6 missing required world components during session start:
   - `WorldManager`
   - `RegionSystem`
   - `ChunkLoader`
   - `ChunkGenerator`
   - `LootSpawner`
   - `WorldEventSystem`
3. Main menu creator uses `UMAPreviewAvatar`, and Unity reports a cross-scene reference warning to `UMA_GLIB` in `World`.
4. `ZomberaSquadManagementUI` currently allows visibility in `GameState.LoadingWorld`, which is incompatible with "hide user info until load complete".
5. No existing loading scene/controller currently exists.

Conclusion:
A dedicated loading scene and explicit load handshake are needed.

---

# SCOPE (V1)

In scope:

- Add a new `Loading` scene with a black full-screen overlay.
- Route New Game / Load Game through Loading scene first.
- Async load `World` scene from loading scene.
- Keep gameplay info UI hidden until world load + session init complete.
- Remove/avoid cross-scene UMA references in menu preview setup.

Out of scope (V1):

- Branded loading art
- Progress bars or tips
- Fancy transitions/fades

---

# TARGET FLOW

## New Game

1. Main Menu click `Start`.
2. `GameManager` sets state to `LoadingWorld`.
3. `GameManager` loads `Loading` scene (single mode).
4. `LoadingSceneController` displays black canvas immediately.
5. `LoadingSceneController` triggers world load via `LoadSceneAsync("World")`.
6. After async reaches ready-to-activate, scene activates.
7. `GameManager` runs `BeginWorldSession()` only after `World` is active.
8. `GameManager` sets state to `Playing`.
9. Gameplay UI and user data become visible.

## Load Game

Same as above, except saved slot is applied before final transition to `Playing`.

---

# SCENE CHANGES

## 1) New scene

Add `Assets/Scenes/Loading.unity` with:

- Root `LoadingUIRoot`
- Canvas (Screen Space Overlay, high sorting order)
- Full-screen `Image` with pure black color
- Optional centered text `Loading...` (can be omitted for strict black)
- `LoadingSceneController` component

## 2) Build Settings order

Recommended order:

1. `Boot`
2. `MainMenu`
3. `Loading`
4. `World`

---

# CODE CHANGES (PLANNED)

## A) `Assets/Core/GameManager.cs`

Add loading-scene support:

- Serialized fields:
  - `bool useIntermediateLoadingScene = true`
  - `string loadingSceneName = "Loading"`
- Start session methods (`StartNewGame`, `LoadGame`) should:
  - set pending session request,
  - load `Loading` scene first (when enabled),
  - avoid direct `BeginWorldSession()` while not in active world scene.
- Add internal handoff method callable by loading scene, example:
  - `RequestWorldLoadFromLoadingScene()`
- Ensure `BeginWorldSession()` only executes after world activation.

## B) New script: `Assets/UI/Menus/LoadingSceneController.cs`

Responsibilities:

- Render/own black screen root.
- On `Start`, call into `GameManager` to begin async world load.
- Keep black overlay active until `GameManager` confirms session ready.
- Disable input interaction while loading.

## C) UI gating updates

### `Assets/UI/SquadManagement/ZomberaSquadManagementUI.cs`

- Update `IsGameplayUiAllowed()` so `LoadingWorld` does NOT permit visibility.
- Allow only `Playing` and `Paused`.

### `Assets/Core/GameManager.cs`

- Keep `SyncGameplayUiVisibility` behavior strict:
  - hidden for `Booting`, `MainMenu`, `LoadingWorld`.
  - visible only for `Playing`/`Paused`.

## D) Readiness validation timing

`RunReadinessValidation()` should run:

- on boot init (core checks),
- after world load activation (world checks),
- not at points where `World` is not yet loaded.

If needed, split validator invocation into:

- core/menu pass
- world-runtime pass

so errors are accurate to scene context.

---

# UMA CROSS-SCENE REFERENCE FIX PLAN

Problem:
`UMAPreviewAvatar` in `MainMenu` is holding/attempting a reference to `UMA_GLIB` object in `World`, which Unity refuses to save.

Fix approach:

1. Ensure all `CharacterCreatorController` and `UMAPreviewAvatar` object references are scene-local to `MainMenu`.
2. Do not assign scene object references from `World` in MainMenu inspector.
3. If a runtime UMA dependency is needed:
   - resolve it at runtime from same loaded scene, or
   - reference project assets/prefabs, not scene instances.
4. Re-save `MainMenu` scene and verify warning no longer appears.

---

# ACCEPTANCE CRITERIA

1. Clicking Start from Main Menu transitions to black loading screen first.
2. No survivor/player identity UI appears during loading.
3. World activates only after loading handoff completes.
4. On completion, game enters `Playing` with expected HUD visibility.
5. No Unity warning about cross-scene `UMAPreviewAvatar` -> `UMA_GLIB` references.
6. No immediate bounce back to Main Menu during normal new-game flow.

---

# TEST CHECKLIST

1. Boot -> MainMenu -> Start -> observe black loading screen.
2. During loading, verify no squad/world HUD data is visible.
3. Arrive in World and verify movement/input works.
4. Confirm `GameState` transitions: `MainMenu -> LoadingWorld -> Playing`.
5. Trigger Start repeatedly in separate runs; ensure no cross-scene warning appears.
6. Validate from both:
   - opening game at Boot
   - opening editor play mode from MainMenu scene

---

# IMPLEMENTATION ORDER

1. Create `Loading.unity` + black UI root.
2. Add `LoadingSceneController` script.
3. Update `GameManager` session start + loading handoff.
4. Tighten gameplay UI visibility gating.
5. Fix UMA scene reference wiring and re-save MainMenu.
6. Run startup/playtest checklist.

This order minimizes regressions and gives a visible loading barrier early.
