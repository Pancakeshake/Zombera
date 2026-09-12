#if UNITY_EDITOR
#region

using System.Linq;
using UnityEditor;
using UnityEngine;
using Zombera.Systems;

#endregion

namespace Zombera.EditorTools
{
    public static class RtsControlWiringValidator
    {
        [MenuItem("Tools/World/RTS/Validate Control Wiring", priority = -500)]
        public static void ValidateControlWiring()
        {
            var squadManagers = Object.FindObjectsByType<SquadManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var commandSystems = Object.FindObjectsByType<CommandSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var selectionManagers = Object.FindObjectsByType<SelectionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var commandManagers = Object.FindObjectsByType<CommandManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var issues = 0;

            if (squadManagers.Length == 0)
            {
                Debug.LogWarning("[RTS Wiring] No SquadManager found in open scenes.");
                issues++;
            }

            if (commandSystems.Length == 0)
            {
                Debug.LogWarning("[RTS Wiring] No CommandSystem found in open scenes.");
                issues++;
            }

            if (selectionManagers.Length == 0)
            {
                Debug.LogWarning("[RTS Wiring] No SelectionManager found in open scenes.");
                issues++;
            }

            if (commandManagers.Length == 0)
            {
                Debug.LogWarning("[RTS Wiring] No CommandManager found in open scenes.");
                issues++;
            }

            foreach (var selectionManager in selectionManagers)
            {
                if (selectionManager == null) continue;

                var hasSquadManager = selectionManager.GetType().GetField("squadManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(selectionManager) != null;
                if (hasSquadManager) continue;

                Debug.LogWarning($"[RTS Wiring] SelectionManager '{selectionManager.name}' has no SquadManager reference (runtime fallback is used).", selectionManager);
                issues++;
            }

            foreach (var commandManager in commandManagers)
            {
                if (commandManager == null) continue;

                var hasSquadManager = commandManager.GetType().GetField("squadManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(commandManager) != null;
                if (hasSquadManager) continue;

                Debug.LogWarning($"[RTS Wiring] CommandManager '{commandManager.name}' has no SquadManager reference (runtime fallback is used).", commandManager);
                issues++;
            }

            if (issues == 0)
                Debug.Log("[RTS Wiring] Validation complete: no blocking wiring issues found.");
            else
                Debug.LogWarning($"[RTS Wiring] Validation complete: found {issues} potential wiring issue(s).");
        }

        [MenuItem("Tools/World/RTS/Auto-Wire Coordinator", priority = -500)]
        public static void AutoWireCoordinator()
        {
            var coordinators = Object.FindObjectsByType<PlayerControlModeCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var coordinator = coordinators.FirstOrDefault();
            if (coordinator == null)
            {
                var go = new GameObject("PlayerControlModeCoordinator");
                coordinator = go.AddComponent<PlayerControlModeCoordinator>();
                Undo.RegisterCreatedObjectUndo(go, "Create PlayerControlModeCoordinator");
                Debug.Log("[RTS Wiring] Created PlayerControlModeCoordinator in the active scene.", go);
            }

            var selectionManagers = Object.FindObjectsByType<SelectionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var commandManagers = Object.FindObjectsByType<CommandManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var selectionManager in selectionManagers)
                TryAssignPrivateField(selectionManager, "controlModeCoordinator", coordinator);

            foreach (var commandManager in commandManagers)
                TryAssignPrivateField(commandManager, "controlModeCoordinator", coordinator);

            EditorUtility.SetDirty(coordinator);
            Debug.Log("[RTS Wiring] Auto-wire complete for PlayerControlModeCoordinator references.", coordinator);
        }

        private static void TryAssignPrivateField(Object target, string fieldName, Object value)
        {
            if (target == null) return;

            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) return;

            field.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
