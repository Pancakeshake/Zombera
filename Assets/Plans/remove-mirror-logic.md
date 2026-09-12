# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with clothing/equipment system.
- Players: Single player.

# Game Mechanics
## Removal of Mirrored Rig Correction
The automated mirroring logic (using `CorrectionProxy_180`) is causing visual distortion on the right side of the character. We will remove this logic and revert to direct bone remapping.

# Implementation Steps

## 1. Update RemapSkinnedMesh in EquipmentSystem.cs
- Remove the `isNonMirroredRig` detection logic.
- Remove the `CorrectionProxy_180` creation and assignment logic.
- Ensure `targetBone` is assigned directly to `characterBones.Find(b => b.name == boneName)`.
- **File**: `Assets/Scripts/Inventory/EquipmentSystem.cs`

## 2. Scene Cleanup
- Delete all existing `CorrectionProxy_180` GameObjects from the character hierarchy to restore the rig's original rotation state.
- Trigger a full equipment rebuild to verify the fix.

# Verification & Testing
- **Visual Check**: Verify the character's right side (and left side) is no longer distorted.
- **Hierarchy Check**: Verify no `CorrectionProxy_180` objects remain under the `Player` transform.
