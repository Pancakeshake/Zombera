# Project Overview 
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world with base building and tactical combat.
- Players: Single player
- Target Platform: PC (StandaloneWindows64)
- Render Pipeline: Universal Render Pipeline (URP)

# Game Mechanics 
## Core Gameplay Loop
The player scavenges for resources, builds bases, and survives against zombie hordes. The game relies on a decoupled event-driven architecture.

# UI
The game uses UGUI for HUD and menus, with a central `HUDManager` coordinating the display.

# Key Asset & Context
- `Assets/Scripts/Zombera.Runtime.asmdef`: The primary assembly definition currently containing broken references.
- `Assets/ThirdParty/Free Wood Door Pack/Script/`: Location of the third-party door scripts.

# Implementation Steps
1. **Fix Zombera.Runtime Assembly**: 
   - Edit `Assets/Scripts/Zombera.Runtime.asmdef`.
   - Remove `"FreeWoodDoorPack"` from the `references` array.
   - Remove `"GUID:f4cb6d2c24715bd4884385645277f011"` (which is missing/invalid) from the `references` array.
   - **Dependency**: None.

2. **Create Third-Party Assembly**:
   - Create a new file `Assets/ThirdParty/Free Wood Door Pack/Script/FreeWoodDoorPack.asmdef`.
   - Content:
     ```json
     {
       "name": "FreeWoodDoorPack",
       "rootNamespace": "DoorScript",
       "references": [],
       "includePlatforms": [],
       "excludePlatforms": [],
       "allowUnsafeCode": false,
       "overrideReferences": false,
       "precompiledReferences": [],
       "autoReferenced": true,
       "defineConstraints": [],
       "versionDefines": [],
       "noEngineReferences": false
     }
     ```
   - **Dependency**: Step 1.

3. **Restore Reference (Cleanly)**:
   - Re-add the `FreeWoodDoorPack` reference to `Zombera.Runtime.asmdef` using the GUID of the newly created assembly.
   - **Dependency**: Step 2.

# Verification & Testing
- **Compilation Check**: Confirm that the project compiles without errors in the Unity Editor.
- **Log Verification**: Ensure that the "duplicate definition" errors for `CoreEventBus` and `SpawnAppearanceStylingService` are cleared.
- **Functionality Check**: Verify that `DoorInteractor` can still find and interact with doors in a test scene (e.g., `ClothingTesting`).
