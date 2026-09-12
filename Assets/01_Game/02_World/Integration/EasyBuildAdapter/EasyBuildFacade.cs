#region

using System;
using MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers;
using MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers.Inputs;
using MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers.States;
using MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Managers;
using MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.Implementations.Behaviors.Implementations;
using MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.Implementations.Conditions.Implementations.Collapse;
using MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Implementations;
using UnityEngine;

#endregion

namespace Zombera.EasyBuildAdapter
{
    /// <summary>
    ///     Typed facade over the Easy Build System runtime. Replaces stringly-typed reflection
    ///     bridges with direct, compile-checked access to EasyBuild's public API. All EasyBuild
    ///     type references should live in this adapter assembly so game assemblies stay decoupled.
    /// </summary>
    public static class EasyBuildFacade
    {
        /// <summary>True when an EasyBuild BuildingManager exists in the loaded scenes.</summary>
        public static bool HasBuildingManager => BuildingManager.Instance != null;

        /// <summary>
        ///     Destroys all placed parts and deletes the EasyBuild runtime save.
        ///     Returns false when no BuildingManager is active.
        /// </summary>
        public static bool TryClearAllPlacedParts(bool includePreplaced = false)
        {
            var manager = BuildingManager.Instance;
            if (manager == null) return false;

            manager.DestroyAllPlacedParts(includePreplaced);
            manager.SaveSystem?.DeleteSave();
            return true;
        }

        // ── Controller / input / menu resolution ────────────────────────────────

        /// <summary>
        ///     The active EasyBuild BuildingController. Falls back to a scene-wide lookup
        ///     (inactive included) when the singleton has not registered yet.
        /// </summary>
        public static MonoBehaviour ResolveBuildingController()
        {
            var controller = BuildingController.Instance;
            if (controller != null) return controller;

            return UnityEngine.Object.FindFirstObjectByType<BuildingController>(FindObjectsInactive.Include);
        }

        /// <summary>The active EasyBuild BuildingManager, or null when none exists.</summary>
        public static MonoBehaviour BuildingManagerBehaviour => BuildingManager.Instance;

        /// <summary>The active EasyBuild radial menu UI, or null when none exists.</summary>
        public static MonoBehaviour RadialMenuBehaviour => BuildingRadialMenuUI.Instance;

        /// <summary>
        ///     Resolves the BuildingInput component, preferring one on the controller's GameObject,
        ///     then falling back to a scene-wide lookup (inactive objects included).
        /// </summary>
        public static MonoBehaviour ResolveBuildingInput(Component controller)
        {
            if (controller != null)
            {
                var fromController = controller.GetComponent<BuildingInput>();
                if (fromController != null) return fromController;
            }

            return UnityEngine.Object.FindFirstObjectByType<BuildingInput>(FindObjectsInactive.Include);
        }

        // ── Radial menu state ────────────────────────────────────────────────────

        /// <summary>True when the given component is an open EasyBuild menu.</summary>
        public static bool IsMenuOpen(Component menu)
        {
            return menu is MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Abstracts.BuildingMenuUI
            {
                IsOpen: true
            };
        }

        /// <summary>Opens the given EasyBuild menu. Returns false when the component is not a menu.</summary>
        public static bool TryOpenMenu(Component menu)
        {
            if (menu is not MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Abstracts.BuildingMenuUI menuUi)
                return false;

            menuUi.OpenMenu();
            return true;
        }

        /// <summary>Closes the given EasyBuild menu. Returns false when the component is not a menu.</summary>
        public static bool TryCloseMenu(Component menu)
        {
            if (menu is not MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Abstracts.BuildingMenuUI menuUi)
                return false;

            menuUi.CloseMenu();
            return true;
        }

        // ── Build mode access ────────────────────────────────────────────────────

        /// <summary>Sets the controller's build mode by name (None, Placement, Destruction, Adjustment).</summary>
        public static bool TrySetControllerMode(string modeName)
        {
            var controller = BuildingController.Instance;
            if (controller == null) return false;
            if (!Enum.TryParse<BuildingMode>(modeName, true, out var mode)) return false;

            controller.SetMode(mode);
            return true;
        }

        /// <summary>The controller's active build mode name, or null when no controller exists.</summary>
        public static string GetActiveControllerModeName()
        {
            var controller = BuildingController.Instance;
            return controller == null ? null : controller.ActiveMode.ToString();
        }

        /// <summary>Placement settings of the controller's currently selected part, or null.</summary>
        public static object GetSelectedPartPlacementSettings()
        {
            var selectedPart = BuildingController.Instance != null ? BuildingController.Instance.SelectedPart : null;
            if (selectedPart == null) return null;

            var placementSystem = selectedPart.PlacementSystem;
            return placementSystem == null ? null : placementSystem.Settings;
        }

        // ── Spawned-building post-processing ─────────────────────────────────────

        /// <summary>
        ///     Toggles EasyBuild collapse conditions and debris behaviors under the given root.
        ///     Used to stop world-spawned buildings from running stability checks and debris audio.
        /// </summary>
        public static void SetCollapseBehaviorsEnabled(GameObject root, bool enabled)
        {
            if (root == null) return;

            var collapseConditions = root.GetComponentsInChildren<BuildingCollapseCondition>(true);
            for (var i = 0; i < collapseConditions.Length; i++)
                collapseConditions[i].enabled = enabled;

            var debrisBehaviors = root.GetComponentsInChildren<BuildingDebrisBehavior>(true);
            for (var i = 0; i < debrisBehaviors.Length; i++)
                debrisBehaviors[i].enabled = enabled;
        }
    }
}
