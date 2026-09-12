# Project Overview 
- Game Title: Zombera
- High-Level Concept: Survival RPG with procedural world generation.

# Game Mechanics 
- Procedural terrain generation using MapMagic 2.
- Overview mode for world generation testing.

# Key Asset & Context
- `Assets/Scripts/World/WorldManager.cs`: Configures MapMagic streaming.
- `Assets/Scenes/Testing/WorldGenTest.unity`: Overview scene.

# Implementation Steps
1. **Expand MapMagic Range**: In `WorldManager.ApplyMapMagicProceduralSessionStreaming`, set `mainRange = 3` and `tiles.generateRange = 3`.
2. **Force Center Tile**: In the same method, if `randomMapMagicStartCoordRadius` is 0, set `tiles.genAroundCoordinates = true` and add `Coord(0, 0)` to `tiles.genCoordinates`.
3. **Center Camera**: Set `Main Camera` to `(500, 1500, 500)` and `farClipPlane` to `10000`.

# Verification & Testing
- Confirm 49 tiles and no hole in the center.
