using UnityEngine;
using Zombera.Characters;
using Zombera.Inventory;

namespace Zombera.UI
{
    internal static class InventoryPanelDiagnostics
    {
        public static void LogWarning(Object context, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            Debug.LogWarning($"[InventoryPanelController] {message}", context);
        }

        public static void LogError(Object context, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            Debug.LogError($"[InventoryPanelController] {message}", context);
        }

        public static void LogAutoProvisionedEquipment(Unit unit, EquipmentSystem equipmentSystem)
        {
            if (unit == null || equipmentSystem == null) return;

            Debug.Log(
                $"[InventoryPanelController] Auto-provisioned EquipmentSystem on unit '{unit.name}' to keep HUD equip flows functional.",
                equipmentSystem);
        }

        public static void LogEquipMissingSystem(Object context)
        {
            Debug.LogWarning("[InventoryPanelController] Equip failed: no EquipmentSystem resolved for active unit.", context);
        }

        public static void LogRefreshSummary(Object context, bool enabled, Unit currentUnit, int visibleCount, int slotCount,
            string filter)
        {
            if (!enabled) return;

            var unitName = currentUnit != null ? currentUnit.name : "<none>";
            Debug.Log(
                $"[InventoryPanelController] Refreshed inventory UI for '{unitName}' (visible={visibleCount}, slots={slotCount}, filter={filter}).",
                context);
        }
    }
}
