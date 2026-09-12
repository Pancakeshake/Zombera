# Project Overview
- Game Title: Zombera
- Goal: Enable unequipping items and swapping items between squad members.
- Current Status: Inventory system supports adding, removing, and equipping. UI supports drag-and-drop to equip and context menu for equip/drop.

# Game Mechanics
## Unequipping
Players can unequip items in two ways:
1. **Context Menu**: Right-click an "Equipped" item in the inventory grid and select "Unequip".
2. **Direct Interaction**: Right-click an equipment slot to unequip the item.

## Item Swapping/Transfer
Players can move items between squad members:
1. **Drag-and-Drop**: Drag an item from the inventory grid onto a squad member's portrait in the bottom bar.
2. **Context Menu**: (Optional) "Transfer" button to move the item to another member.

# Key Asset & Context
- `Assets/Scripts/UI/HUD/InventoryPanelController.cs`: Refactor context menu to support "Unequip". Add transfer logic.
- `Assets/Scripts/UI/HUD/SquadPortraitDropTarget.cs`: New component to handle item drops on squad portraits.
- `Assets/Scripts/UI/HUD/EquipmentSlotInteraction.cs`: New component to handle right-clicking equipment slots for unequipping.

# Implementation Steps

### 1. Update InventoryPanelController
- Add "Unequip" state to the context menu "Equip" button. If the item is already equipped, the button should say "Unequip".
- Implement `HandleContextUnequipClicked`.
- Implement `TransferItemToUnit(ItemDefinition item, int quantity, Unit targetUnit)` logic. This will remove the item from the current inventory and add it to the target's inventory.

### 2. Implement SquadPortraitDropTarget
- Create a new script `SquadPortraitDropTarget.cs` that implements `IDropHandler`.
- It will find the `InventoryPanelController` and, if an item is being dragged, call `TransferItemToUnit` with the target unit bound to that portrait slot.

### 3. Implement EquipmentSlotInteraction
- Add `IPointerClickHandler` to equipment slots.
- On right-click, call `EquipmentSystem.Unequip(slot)`.

### 4. Refine ItemDefinition/Inventory
- Ensure `UnitInventory.TryAddItem` is used to verify weight limits on the receiver.

# Verification & Testing
- **Unequip**: Right-click an equipped item; verify the visual attachment disappears and the "Equipped" label/frame is removed.
- **Transfer**: Drag a weapon from the Player to a Squad Member; verify it disappears from the Player's inventory and appears in the Member's inventory. Check weight limits.
- **Direct Unequip**: Right-click the "Chest" equipment slot; verify the item is unequipped.
