# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with zombie combat, base building, and skill progression.
- Players: Single player
- Target Platform: Standalone Windows
- Render Pipeline: URP

# Game Mechanics
## Core Gameplay Loop
The player scavenges for resources, builds a base, and defends against zombie hordes. Character progression is handled via a skill system that awards XP based on actions (combat, healing, etc.).

# Key Asset & Context
- `InventoryManager.cs`: Handles item storage and weight.
- `AITickManager.cs`: Distributes AI processing to avoid CPU spikes.
- `DamageSystem.cs`: Static utility for damage and XP.
- `EquipmentSystem.cs`: Manages character visuals and gear.
- `SaveSystem.cs`: Handles persistence.

# Implementation Steps

## 1. Fix Inventory Quantity Deduction
- **File**: `Assets/Scripts/Inventory/InventoryManager.cs`
- **Logic**: Modify `TryRemoveItem` to subtract from the total pool of a specific item across all stacks.
- **Code Change**:
    ```csharp
    private bool TryRemoveItem(ItemDefinition itemDefinition, int quantity) {
        int remainingToRemove = quantity;
        for (int i = items.Count - 1; i >= 0; i--) {
            if (items[i].item == itemDefinition) {
                int toTake = Mathf.Min(items[i].quantity, remainingToRemove);
                items[i].quantity -= toTake;
                remainingToRemove -= toTake;
                if (items[i].quantity <= 0) items.RemoveAt(i);
                if (remainingToRemove <= 0) break;
            }
        }
        RecalculateWeight();
        return remainingToRemove <= 0;
    }
    ```

## 2. Fix AI Tick Skipping
- **File**: `Assets/Scripts/AI/AITickManager.cs`
- **Logic**: Adjust the persistent index when a zombie is removed.
- **Code Change**:
    ```csharp
    public void Unregister(ZombieController zombie) {
        int index = _zombies.IndexOf(zombie);
        if (index != -1) {
            _zombies.RemoveAt(index);
            if (index <= _currentIndex && _currentIndex > 0) _currentIndex--;
        }
    }
    ```

## 3. Award Ranged Combat XP
- **File**: `Assets/Scripts/Combat/DamageSystem.cs`
- **Logic**: Add a case for `Ranged` damage in the XP distribution switch.
- **Code Change**:
    ```csharp
    case DamageType.Ranged:
        sourceStats.RecordRangedHit(); // Ensure this method exists in UnitStats
        break;
    ```

## 4. Editor Visual Stability
- **File**: `Assets/Scripts/Inventory/EquipmentSystem.cs`
- **Logic**: Guard `BuildCharacter` calls against uninitialized UMA states.
- **Code Change**:
    ```csharp
    if (avatar != null && avatar.umaData != null) {
        avatar.BuildCharacter();
    }
    ```

# Verification & Testing
1. **Inventory**: Add 5 unstackable items of type X. Call `RemoveItem(X, 3)`. Verify 2 items remain.
2. **AI**: Spawn 10 zombies. Kill zombie #2. Verify zombie #3 still receives a tick on the next frame.
3. **XP**: Kill a zombie with a pistol. Check the player's `UnitStats` for increased Ranged XP.
4. **Editor**: Open a character prefab and change a gear slot. Ensure no red errors appear in the console.