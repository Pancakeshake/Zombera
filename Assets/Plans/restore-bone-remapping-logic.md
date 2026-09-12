# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with clothing/equipment system.
- Players: Single player.
- Target Platform: PC (Windows).

# Game Mechanics
## Modular Character Bone Remapping Fix
The user needs to restore a specific bone remapping logic that was lost in a checkpoint restore. This logic handles "non-mirrored" rigs by detecting if the character's feet face the same direction and applying a 180-degree rotation proxy to bones that require flipping (like feet/legs) when remapping mirrored clothing assets (boots).

# Key Asset & Context
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: Contains the `RemapSkinnedMesh` method where the logic resides.

# Implementation Steps

## 1. Update RemapSkinnedMesh in EquipmentSystem.cs
Restore the non-mirrored rig detection and the rotation proxy logic as shown in the provided image.
- Add detection for `isNonMirroredRig` based on the forward vectors of "LeftFoot" and "RightFoot" bones.
- Inside the `SkinnedMeshRenderer` loop, reset local rotation and position of the renderer.
- Add the `CorrectionProxy_180` logic to flip bones containing "Left" or "Right" if the rig is non-mirrored.
- **File**: `Assets/Scripts/Inventory/EquipmentSystem.cs`

# Verification & Testing
- **Visual Check**: Equip boots on the character and verify that both boots face the correct direction (forward).
- **Hierarchy Check**: Verify that `CorrectionProxy_180` objects are created as children of the character's bones when needed.
