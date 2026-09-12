# Project Overview
- **Game Title**: Zombera
- **Goal**: Implement the equipping of 3D weapon models into the player's hands.
- **Current System**: Uses `EquipmentSystem.cs` to manage slots, `ItemDefinition.cs` for data, and `WeaponSystem.cs` for combat logic.

# Game Mechanics
- **Equipment System**: Items are bound to `EquipmentSlot` (LeftHand, RightHand, etc.).
- **Socketing**: Models are instantiated and parented to specific bones or socket transforms found on the character's hierarchy.
- **Combat Integration**: Equipping a visual also updates the `WeaponSystem` with combat stats (damage, fire rate).

# Key Assets & Context
- **Scripts**: 
    - `Assets/Scripts/Inventory/ItemDefinition.cs` (Data container for the prefab)
    - `Assets/Scripts/Inventory/EquipmentSystem.cs` (The logic that spawns the prefab)
    - `Assets/Scripts/Combat/WeaponSystem.cs` (Handles the animator and combat state)
- **Data**: `ItemDefinition` ScriptableObjects for each weapon.

# Implementation Steps

## 1. Configure the ItemDefinition Asset
For every weapon model you want to equip:
1. Locate or create the `ItemDefinition` asset for the weapon.
2. **Visuals Section**:
    - Assign your 3D weapon model to `equippedVisualPrefab`.
    - Set `equippedVisualLocalPosition`, `EulerAngles`, and `Scale` (Start with `Vector3.one` for scale).
3. **Equipment Section**:
    - Set `enforceSpecificEquipSlot` to `true`.
    - Set `forcedEquipSlot` to `RightHand` (or `LeftHand`).
4. **Combat Section**:
    - Assign the corresponding `WeaponData` to `equippedWeaponData`.

## 2. Verify Character Sockets
Ensure your player prefab is ready to receive the model:
- **Humanoid characters**: Ensure the `Animator` is set to Humanoid. `EquipmentSystem` will automatically find the hand bones.
- **Generic characters**: Create a child transform under the hand bone and name it exactly `Socket_RightHand` or `Socket_LeftHand`.

## 3. Triggering the Equipment (Code)
To equip the weapon via script (e.g., in a player controller or interaction script):
```csharp
using Zombera.Inventory;

public void EquipWeaponToPlayer(GameObject player, ItemDefinition weaponItem)
{
    EquipmentSystem equipment = player.GetComponent<EquipmentSystem>();
    if (equipment != null)
    {
        // This will:
        // 1. Remove any old weapon in the hand
        // 2. Instantiate the new prefab at the hand socket
        // 3. Apply the offsets defined in the ItemDefinition
        // 4. Update the WeaponSystem for combat
        equipment.Equip(EquipmentSlot.RightHand, weaponItem);
    }
}
```

# Verification & Testing
1. **Visual Check**: Play the game and trigger the equip logic. The weapon should appear in the player's hand.
2. **Alignment Check**: If the weapon is floating or rotated incorrectly, adjust the offsets in the `ItemDefinition` asset **while in Play Mode** to find the perfect values, then copy them back to the asset.
3. **Animation Check**: Ensure the `HasWeapon` bool in the Animator is turning on, and the `Equip` trigger is fired.
4. **Combat Check**: Verify that attacking now uses the damage/fire rate from the `WeaponData` associated with the item.
