using UnityEngine;
using Zombera.Characters;
using Zombera.Inventory;

namespace Zombera.UI
{
    public sealed partial class InventoryPanelController
    {
        private bool TryDropItemStack(ItemDefinition item, int quantity)
        {
            return InventoryPanelActionLayer.TryDrop(
                _currentInventory,
                item,
                quantity,
                TrySpawnDroppedPickup,
                ResolveItemName,
                message => InventoryPanelDiagnostics.LogError(this, message));
        }

        private bool TrySpawnDroppedPickup(ItemDefinition item, int quantity)
        {
            try
            {
                SpawnDroppedPickup(item, quantity);
                return true;
            }
            catch (System.Exception ex)
            {
                InventoryPanelDiagnostics.LogError(this,
                    $"Failed to spawn dropped pickup for {quantity}x {ResolveItemName(item)}: {ex.Message}");
                return false;
            }
        }

        private void SpawnDroppedPickup(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0) return;

            var origin = _currentUnit != null ? _currentUnit.transform.position : Vector3.zero;
            var forward = _currentUnit != null ? _currentUnit.transform.forward : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            var spawnPosition = origin + forward.normalized * 1.25f + Vector3.up * 0.2f;

            var pickupRoot = new GameObject($"Dropped_{item.name}")
            {
                transform =
                {
                    position = spawnPosition
                }
            };

            var trigger = pickupRoot.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.45f;

            var rb = pickupRoot.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            var pickup = pickupRoot.AddComponent<ItemPickup>();
            pickup.Initialize(item, quantity);
        }

        public bool TransferItemToUnit(ItemDefinition item, int quantity, Unit targetUnit)
        {
            if (item == null || quantity <= 0 || targetUnit == null || _currentInventory == null) return false;
            if (targetUnit == _currentUnit) return false;

            var targetInventory = targetUnit.Inventory ?? targetUnit.GetComponent<UnitInventory>();
            if (targetInventory == null) return false;

            var transferred = InventoryPanelActionLayer.TryTransfer(
                _currentInventory,
                targetInventory,
                item,
                quantity,
                ResolveItemName,
                targetUnit.name,
                message => InventoryPanelDiagnostics.LogWarning(this, message),
                message => InventoryPanelDiagnostics.LogError(this, message));

            if (!transferred) return false;

            if (IsItemEquipped(item))
                TryUnequipItem(item);

            RefreshSlots();
            return true;
        }

        public bool TransferDraggingItemToTarget(Unit targetUnit)
        {
            if (!IsDraggingItem || _draggingStack.item == null || targetUnit == null) return false;

            return TransferItemToUnit(_draggingStack.item, _draggingStack.quantity, targetUnit);
        }
    }
}
