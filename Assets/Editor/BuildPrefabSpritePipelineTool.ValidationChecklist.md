# Build Prefab Sprite Pipeline Validation Checklist

Run this checklist in Unity Editor after refactor changes.

## Menu Commands

- [ ] Run `Tools/World/Building Icons/Open Or Create Icon Render Scene` once.
- [ ] Run `Tools/World/Building Icons/Setup Render Rig In Active Scene` once.
- [ ] Run `Tools/World/Building Icons/Capture Icon For Selected Prefab` once.
- [ ] Run `Tools/World/Building Icons/Batch Capture Icons (Selected Folder)` once.
- [ ] Run `Tools/World/Building Icons/Batch Capture Icons (Configured Folders)` once.

## Single Capture Validation

- [ ] Capture one selected prefab and verify generated PNG is under `Assets/Art/BuildingIcons/Generated`.
- [ ] Verify importer settings on the generated PNG:
- [ ] `Texture Type = Sprite`
- [ ] `Sprite Mode = Single`
- [ ] `Alpha Is Transparency = true`
- [ ] `Mip Maps = disabled`
- [ ] `Read/Write = disabled`
- [ ] `NPOT Scale = None`

## Batch Validation

- [ ] Run both batch modes and verify summary counts for processed/captured/failed/duplicates.
- [ ] Verify duplicate part-reference collisions include both prefab paths in logs.
- [ ] Verify generated sprite files and `Assets/Data/Building/BuildPrefabSpriteLibrary.asset` are updated.

## Runtime Lookup Validation

- [ ] Enter Play Mode and verify `BuildPrefabSpriteLibrary.TryGetSprite` still resolves expected icons in runtime HUD/radial flow.

## Static Analysis / Compile

- [ ] Re-run editor compile/analyzer checks and confirm prior flagged items are cleared:
- [ ] complexity hotspot in batch orchestration
- [ ] complexity hotspot in sprite library merge/write
- [ ] always-true branch in texture importer section
- [ ] unread `RenderRig.Root` field
