# Copilot Instructions - Zombera

This file stores persistent implementation workflow rules for AI-assisted coding in this repository.

## Primary Workflow Rule

Use repeatable, tool-driven changes instead of one-off manual setup whenever practical.

- Prefer creating Unity Editor menu commands under `Tools/Zombera/...` for setup actions.
- Keep setup commands idempotent so they can be run multiple times safely.
- After code edits, check compile status and fix errors before finalizing.

## BuildSystem Baseline Standards

These rules define the current base-building implementation style.

### Modular Wall Geometry

- Base wall dimensions: `2m x 2.5m x 0.2m`.
- Starter wall pieces:
  - `Wall_Full_01`
  - `Wall_Window_01`
  - `Wall_Door_01`
  - `Wall_Damaged_01`
- Keep all wall variants at matching outer dimensions.

### Pivot and Prefab Layout

- Use bottom-center pivot behavior for modular walls.
- Keep a root wall object and a child visual object where needed.
- Preserve consistent collider bounds across variants.

### Shared Runtime Components

- Use `BuildPiece` for wall metadata and type.
- Use `StructureHealth` for destructible behavior.
- Keep wall scripts small and responsibility-focused.

### Snapping Rules

- Snap wall placement position to `2m` grid.
- Snap yaw rotation to `90` degree increments.
- Expose snap helper APIs for reuse by ghost and placement systems.

### Placement UX

- Use ghost placement preview before instantiation.
- Ghost coloring:
  - green = valid
  - red = blocked
- Default build interaction flow:
  - toggle build mode
  - choose wall type
  - preview snapped placement
  - left click place
  - right click or escape cancel

### Material Variation (No New Mesh Required)

- Reuse the same wall meshes.
- Drive variation through material skins.
- Support skin selection during placement.

## Repository-Specific Guardrails

- Work in the main project root (`Assets/...`), not the nested sample project under `Zombera/Assets/...`.
- Do not revert unrelated changes.
- Keep naming consistent with existing Zombera systems and folders.
- Exclude files too large for normal Git commits in general (especially bulk archives and vendor package dumps); prefer imported project assets plus source links, and use Git LFS only for intentionally versioned large binaries.
- Treat large package/container formats (`.unitypackage`, `.zip`, `.7z`, `.rar`, `.tar`, `.gz`) as non-source distribution artifacts that should stay ignored unless explicitly required.

## Character Creator UI Tooling Baseline

- For MainMenu character creator restyle work, prefer Phase 3 editor commands under `Tools/Zombera/Character Creation/Phase 3/...` instead of manual scene setup.
- Use `Apply Mockup Baseline (Selected Creator)` before manual visual nudging.
- Use `Create Mockup Overlay (Selected Sprite)` and `Remove Mockup Overlay` to align to reference art in-scene.
- Use `Export Current Tuned Values Preset (Selected Creator)` to capture final tuned values into a reusable preset asset.
- Use `Enable Scene Inspect Mode (Selected Creator)` when tuning UI in Scene view so overlay canvases do not disappear when zooming close.
- Use `Disable Scene Inspect Mode (Restore Overlay)` after tuning to return to production overlay render mode.

## Zombie Animation Tooling Baseline

- For zombie animation graph updates, use `Tools/Zombera/Animation/Rebuild Zombie Default Controller` instead of hand-editing controller assets.
- After rebuilding, wire instances with `Tools/Zombera/Animation/Wire All Zombie Prefabs` to keep prefab clip bindings consistent.
- Use `Tools/Zombera/Animation/Wire Selected Zombie Components` for targeted prefab or scene-object fixes.
- Keep a dedicated zombie `CombatIdle` state in the animator tree and drive it via the `IsInCombat` bool parameter.
- Keep zombie reaction and death clip wiring explicit in `ZombieAnimationController` override fields so variant playback does not depend on fragile clip-name matching.
- Keep zombie combat idle wiring explicit with both `baseCombatIdleClip` and `combatIdleOverrideClips` using `Combat_Idle_Zombie` and `Combat_Idle_Zombie2` rather than relying on folder-name inference.
- Ensure both combat-idle FBX assets use Humanoid import settings so runtime override swapping behaves consistently across zombie prefabs.

## Inventory Bow Tooling Baseline

- For first-pass Bow authoring, use `Tools/Zombera/Inventory/Bow/Tools/Bootstrap Bow Gameplay Pipeline (Icon + Equip + Pickup)` to create/link Bow item, Bow weapon data, and Bow pickup prefab in one repeatable step.
- Keep `Tools/Zombera/Inventory/Bow/Tools/Bootstrap Item Icon Pipeline (Bow)` as a compatibility alias that routes to the full Bow gameplay bootstrap.
- Use `Tools/Zombera/Inventory/Bow/Tools/Assign Selected Sprite To Bow Item` to update only the Bow inventory icon from a selected Sprite.
- Use `Tools/Zombera/Inventory/Bow/Tools/Assign Selected Prefab To Bow Equipped Visual` to set the in-hand visual asset (`ItemDefinition.equippedVisualPrefab`), supporting both Prefab and FBX model selections.
- Use `Tools/Zombera/Inventory/Bow/Tools/Assign Selected Prefab To Bow World Pickup Visual` to set the world pickup visual asset (`ItemDefinition.worldPickupPrefab`), supporting both Prefab and FBX model selections.
- Use `Tools/Zombera/Inventory/Bow/Tools/Wire Bow Systems On Selected Unit` on a selected unit/prefab to ensure `WeaponSystem`, `EquipmentSystem`, `ItemPickupInteractor`, and `Socket_RightHand` are wired consistently.
- Keep Bow visual fallback deterministic: when Bow visual slots are empty, bootstrap/wiring should prefer `Assets/ThirdParty/Free medieval weapons/Models/Wooden Bow.fbx` and fall back to `Assets/ThirdParty/Free medieval weapons/Prefabs/Wooden Bow.prefab` so a 3D bow model is always present.
- Keep bow projectile visuals explicit on `WeaponSystem`: wiring should assign `Assets/ThirdParty/Free medieval weapons/Prefabs/Arrow.prefab` to the bow-arrow visual slot and prefer that visual at runtime.
- Keep inventory icon wiring on `ItemDefinition.inventoryIcon` so Squad Management inventory slots can render per-item images consistently.
- Keep combat linkage explicit through `ItemDefinition.equippedWeaponData` so equipping Bow in hand slots updates `WeaponSystem` deterministically.

## Inventory Generic Creator Tooling Baseline

- For reusable item authoring, use `Tools/Zombera/Inventory/Creators/Tools/...` to generate baseline templates for Generic, Weapon (+ WeaponData), Ammo, Medical, Food, Vitamin, and Material items.
- Treat template creators as starter assets: rename/tune generated `ItemDefinition` and `WeaponData` values in Inspector after creation.
- Keep weapon template linkage explicit through `ItemDefinition.equippedWeaponData` so generated weapon items are combat-ready by default.

## Inventory Arrow Tooling Baseline

- Use `Tools/Zombera/Inventory/Arrow/Tools/Bootstrap Arrow Item Pipeline (Item + Pickup)` to ensure `Item_Arrow` and `Pickup_Arrow` exist and are wired.
- Use `Tools/Zombera/Inventory/Arrow/Tools/Create Arrow Item Template` for additional arrow-style ammo variants.
- Use `Tools/Zombera/Inventory/Arrow/Tools/Assign Selected Sprite To Arrow Item` and `Tools/Zombera/Inventory/Arrow/Tools/Assign Selected Prefab To Arrow World Pickup Visual` for quick visual overrides.

## Inventory Icon Render Tooling Baseline

- For inventory icon generation, use `Tools/Zombera/Inventory Icons/Open Or Create Icon Render Scene` to work from a dedicated icon-render scene rather than ad-hoc scene camera setups.
- Use `Tools/Zombera/Inventory Icons/Setup Render Rig In Active Scene` to ensure the orthographic icon camera, neutral key/fill lights, transparent background, and capture render texture are wired consistently.
- Use `Tools/Zombera/Inventory Icons/Capture Icon For Selected Prefab` for single-item captures and `Tools/Zombera/Inventory Icons/Batch Capture Icons (Selected Folder)` for repeatable prefab-folder batch export.
- Keep icon capture output under `Assets/Art/InventoryIcons/Generated` and capture texture asset at `Assets/Art/InventoryIcons/IconCaptureRT.renderTexture` so downstream item-authoring tools can assume stable paths.

## World HUD Inventory Binding Baseline

- Keep `InventoryPanelController` on the World HUD `InventoryPanel` so the F2 inventory grid is bound to live `UnitInventory` data.
- Keep this wiring explicit in `Tools/Zombera/World UI/Build World HUD`: assign `InventoryEditTargetController`, search input, filter button/label, and `SlotGrid` to `InventoryPanelController`.
- Preserve runtime compatibility for older scenes by ensuring `WorldHUDController` auto-adds `InventoryPanelController` when missing.

## Third-Party Weapon URP Tooling Baseline

- For purple/magenta material fixes in `Assets/ThirdParty/Free medieval weapons`, use `Tools/Zombera/Art/Fix Free Medieval Weapons (URP Lit)` instead of manually editing each material.
- Keep this command idempotent: it should remap compatible legacy Standard material properties (`_MainTex`, `_Color`, normal/metallic/occlusion/emission) onto URP Lit and safely re-run without duplicating work.
- Preserve prefab validation in the same workflow so missing/null/invalid material shader assignments are surfaced immediately after conversion.

## World HDRI Tooling Baseline

- For world skybox HDRI wiring, use `Tools/Zombera/World/Environment/Apply HDRI Skybox To World` instead of manual Lighting window setup.
- Keep this command idempotent: it should ensure/create a world HDRI skybox material, assign `Assets/HDR/overcast_soil_puresky_2k.hdr`, apply `RenderSettings.skybox`, and refresh environment lighting.
- Keep `DayNightController` skybox wiring explicit in the same command so controller references stay in sync with RenderSettings.

## Fog Of War Tooling Baseline

- For world perception fog setup, use `Tools/Zombera/World/Fog Of War/Setup Fog Of War In Active Scene` instead of manually adding scene components.
- Keep setup idempotent: it should ensure one `FogOfWarSystem` in the active scene, add `FogOfWarVisionSource` to Player/Squad units, add `FogOfWarVisionOverlay` to Player units, and add `FogOfWarTarget` to hostile units.
- Keep perception mapping linear and explicit on `FogOfWarVisionSource`: level 1 = 25m / 180 degrees, level 100 = 100m / 270 degrees.
- Keep visual readability explicit with `FogOfWarVisionOverlay`: render a dark radial world overlay outside the active vision radius so range limits are obvious during play.
- Use `Tools/Zombera/World/Fog Of War/Remove Fog Of War Components In Active Scene` for clean rollback and repeated iteration while tuning scenes.

## Startup Squad Tooling Baseline

- For world-start 20-character squad test setup, use `Tools/Zombera/Squad/Apply 20-Character Startup Test Squad Defaults` instead of manual `PlayerSpawner` field edits.
- Keep the command idempotent: rerunning should re-assert startup squad fields (`spawnStartupSquadOnWorldStart`, roster size, ring radius, and skill-tier array) without duplicating scene objects.
- Use `Tools/Zombera/Squad/Validate Startup Test Squad Wiring` before playtests to confirm prefab assignments and startup roster/tier coverage.
- Keep startup test tiers explicit in roster order (player first): `1, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100`.
- Keep startup squad members commandable by ensuring runtime wiring includes `UnitRole.SquadMember`, `SquadMember`, `FollowController`, and enabled `UnitController` nav agents.

## Post Apocalyptic Modular Import Tooling Baseline

- For importing the Post Apocalyptic pack into modular build prefabs, use `Tools/Zombera/Building/Import Post Apocalyptic Prefabs To Modular Folder`.
- Keep this command idempotent: reruns should update previously generated prefabs in `Assets/BuildingSystem/Prefab_Modular` instead of creating duplicate assets.
- Keep generated prefab output deterministic and flattened under `Assets/BuildingSystem/Prefab_Modular` using stable naming.
- Keep generated prefabs aligned with modular build usage by ensuring imported outputs include `BuildPiece`, `StructureHealth`, and a root collider suitable for placement and damage interactions.

## Update Policy

When a build workflow is improved, update this file and the matching docs entry so future AI work follows the same standard.
