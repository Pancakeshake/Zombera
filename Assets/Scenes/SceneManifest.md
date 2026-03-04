# Scene Structure

This scaffold reserves the following scenes:

- Boot
- MainMenu
- World

## Responsibilities

- Boot: loads and initializes core systems.
- MainMenu: handles front-end flow and save slot selection.
- World: loads chunk-streamed world simulation.

## TODO

- Create Unity scene assets named `Boot`, `MainMenu`, and `World` in this folder.
- Configure build settings scene order: Boot -> MainMenu -> World.
- Follow [docs/playtest_scene_setup_checklist.md](../../docs/playtest_scene_setup_checklist.md) for the full wiring checklist.
