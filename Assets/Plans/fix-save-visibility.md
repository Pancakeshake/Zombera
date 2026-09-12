# Project Overview
- **Game Title**: Zombera
- **Issue**: Save files are created on disk but do not appear in the "Load Game" or "Saves" menus.
- **Root Cause**: The `SaveSystem` relies on a manual `index.json` file to track available saves. If this index is out of sync, corrupted, or not updated correctly (e.g., due to background task timing or missing synchronization), saves on disk are ignored.

# Proposed Solution
Refactor `SaveSystem` to scan the persistent data folder for `*.sav` files instead of relying purely on an index file. This ensures that any save file present on disk is always visible to the user.

# Key Assets & Context
- **Script**: `Assets/01_Game/01_Core/Systems/SaveSystem.cs`
- **Save Path**: `Application.persistentDataPath/Saves/`
- **File Extension**: `.sav`

# Implementation Steps

## 1. Refactor `SaveSystem.cs`
- **Modify `GetAvailableSlotIds()`**: Implement a disk scan to find all `.sav` files in the save folder. Merge these with any in-memory slots in `_saveSlots`.
- **Modify `LoadMetadataIndex()`**: Repurpose this method to serve as a synchronization point that ensures `_saveSlots` is populated with keys found on disk.
- **Improve `GetAllSlotMetadata()`**: Ensure it can handle slots found on disk that might not be in the cache yet.

## 2. Robust File Writing
- **Modify `SaveGameData()`**: Add more logging around the background task to catch potential failures and ensure the `_saveSlots` dictionary is updated immediately on the main thread.

# Verification & Testing
1. **Manual Verification**:
   - Start the game, create a new save.
   - Verify "Save successful" appears.
   - Go to the Load Menu and verify the new save appears immediately.
   - Close the game, restart, and verify the save still appears in the Main Menu.
2. **Edge Cases**:
   - Manually delete `index.json` and verify saves still appear.
   - Manually copy a `.sav` file and verify it appears as a new slot.
   - Save while the disk is full or read-only (simulated via permissions) and check for error logs.
