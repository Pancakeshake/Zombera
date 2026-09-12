# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG in a zombie-infested world.
- Players: Single player.
- Target Platform: PC (Windows).
- Render Pipeline: URP.
- UI System: uGUI.

# Game Mechanics
## Core Gameplay Loop
The player scavenges, builds, and survives against zombies.
## Controls and Input Methods
Standard survival RPG controls.

# UI
Standard HUD and menus.

# Key Asset & Context
- `WeaponSystem` uses Fog of War vision sources to determine max bow range.
- `FogOfWarSystem` and related components handle unit visibility and overlays.

# Implementation Steps
1. **Modify WeaponSystem Core**:
    - File: `Assets/01_Game/05_Combat/Weapons_System/WeaponSystem.cs`
    - Action: Remove the `_ownerVisionSource` field.
2. **Modify WeaponSystem Owner Context**:
    - File: `Assets/01_Game/05_Combat/Weapons_System/WeaponSystem.OwnerContext.cs`
    - Action: Remove the logic that resolves the `_ownerVisionSource` component.
3. **Modify WeaponSystem Bow Subsystem**:
    - File: `Assets/01_Game/05_Combat/Weapons_System/WeaponSystem.ProjectilesBow.cs`
    - Action: Simplify `ResolveMaximumRangeMeters` to return the `configuredMaxRange` directly, removing the dependency on `FogOfWarVisionSource`.
4. **Delete Vision System Files**:
    - Delete `Assets/01_Game/01_Core/Vision/FogOfWarVisionSource.cs`
    - Delete `Assets/01_Game/01_Core/Vision/FogOfWarVisionOverlay.cs`
    - Delete `Assets/01_Game/01_Core/Vision/FogOfWarTarget.cs`
    - Delete `Assets/01_Game/01_Core/Vision/FogOfWarSystem.cs`
    - Delete `Assets/01_Game/01_Core/Vision/FogOfWarRuntimeConfig.cs`
5. **Update Documentation**:
    - File: `Project_Overview.md`
    - Action: Remove references to `FogOfWarSystem`.

# Verification & Testing
- **Compilation Check**: Ensure the project compiles without errors after removing the system and its references.
- **Weapon Test**: Verify that the `WeaponSystem` still initializes correctly and bows still function (returning `configuredMaxRange`).
- **Log Check**: Ensure no "Missing Component" errors appear in the console during runtime due to removed scripts.
