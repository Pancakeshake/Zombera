# First Playtest Scene Setup Checklist

Use this to get a working Boot -> MainMenu -> World flow for the current prototype.

## 1) Create real Unity scenes

- In Unity, create and save three `.unity` scenes in `Assets/Scenes`:
  - `Boot.unity`
  - `MainMenu.unity`
  - `World.unity`
- Keep/ignore the existing placeholder `.scene` files; Unity should use `.unity` scenes.

## 2) Build settings order

- Open Build Settings and add scenes in this order:
  1. `Boot`
  2. `MainMenu`
  3. `World`
- Make `Boot` scene index 0.

## 3) Boot scene (persistent systems)

Create a `CoreRoot` object and add/configure:

- `GameManager`
  - `Initialize On Start` = true
  - `Auto Start Session For Testing` = false (menu flow)
  - `Load World Scene On Session Start` = true
  - `World Scene Name` = `World`
- Core systems:
  - `Zombera.Core.EventSystem`
  - `TimeSystem`
  - `SaveSystem`
- Top-level managers:
  - `UnitManager`
  - `CombatManager`
  - `AIManager`
  - `SquadManager`
  - `ZombieManager`
  - `LootManager`
  - `BaseManager`
  - `SaveManager`
- Optional but recommended in Boot:
  - `DebugManager`
  - `StartupReadinessValidator`

Recommended validator settings:
- `Run Validation On Awake` = false (GameManager triggers it)
- `Run Only Once` = false (allows boot + world pass)
- Keep default scene names (`Boot`, `MainMenu`, `World`)

Note: most references can be left empty initially; current manager code auto-resolves scene references at runtime.

## 4) MainMenu scene

- Create UI root/canvas and place your menu buttons.
- Add `MainMenuController`:
  - `World Scene Name` = `World`
  - Wire button references (`Start`, `Settings`, `Quit`).
- Add/create optional:
  - `CharacterCreatorController`
  - `SettingsMenuController`

`CharacterCreatorController` expected UI:
- `TMP_InputField` for character name (`3-16` chars, trimmed, non-empty).
- `TMP_Dropdown` for appearance presets.
- `Confirm`, `Back/Close`, and optional `Random Name` button.
- Preview labels for:
  - Stats (`HP`, `Damage`, `Speed`, `Stamina`, `Carry`)
  - Starting loadout
  - Flavor/tooltip text
  - Validation message
- Optional portrait `Image` for selected preset.

`MainMenuController` flow:
- `Start` opens `CharacterCreatorController` first (if assigned).
- `Confirm` in character creator saves selection to runtime + profile, then calls `GameManager.StartNewGame()`.
- `Back` closes character creator and returns to menu.
- If no character creator panel is assigned, `Start` goes straight to `StartNewGame()`.

## 5) World scene (minimum gameplay)

### Player
- Add one player GameObject with:
  - `Unit`
  - `UnitController`
  - `UnitHealth`
  - `UnitCombat`
  - `UnitInventory`
  - `UnitStats`
  - `PlayerInputController`

### World systems
- Add one world root with:
  - `WorldManager`
  - `ChunkLoader`
  - `ChunkGenerator`
  - `ChunkCache`
  - `RegionSystem`
  - `MapSpawner`
  - `LootSpawner`
  - `WorldEventSystem`
  - `WorldSimulationManager`
  - `RegionManager`
  - `Zombera.World.Simulation.HordeManager`
  - `Zombera.World.Simulation.SurvivorManager`
  - `Zombera.World.Spawning.ZombieSpawner`
  - `Zombera.World.Spawning.SurvivorSpawner`

### Prototype content
- 10-20 zombies in world or spawnable via debug.
- At least 1 recruitable survivor (`SurvivorAI` + `Unit` stack).
- At least 1 loot container prefab assigned to `LootSpawner`.
- At least 1 base storage crate.

## 6) Debug tools (recommended)

- Add `DebugKeybinds` and `SpawnDebugTools` in scene (or persistent debug root).
- Verify hotkeys:
  - `F1` menu
  - `F2` slow motion
  - `F4` spawn zombie
  - `F5` spawn survivor
  - `F6` god mode

## 7) Smoke test pass criteria

- Launch from `Boot` scene.
- Main menu appears.
- Start Game loads `World` and enters `Playing` state.
- Player can move.
- Player can attack at least one zombie.
- Loot container can be opened.
- One survivor can be recruited (becomes squad member).
- Base storage can accept at least one item.

## 8) Fast troubleshooting

- Start button does nothing:
  - Ensure Boot scene has `GameManager` and `Initialize On Start = true`.
- World loads but no simulation:
  - Ensure `WorldManager` exists and can resolve player unit.
- No dynamic spawns:
  - Check `ZombieManager` / `LootSpawner` / `WorldEventSystem` references or runtime auto-discovery.
- Squad commands not affecting recruits:
  - Confirm recruited survivor has `SquadMember` and `FollowController` after recruitment.
