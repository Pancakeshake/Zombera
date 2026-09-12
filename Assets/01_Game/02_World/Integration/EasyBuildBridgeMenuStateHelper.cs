using System;
using UnityEngine;
using Zombera.EasyBuildAdapter;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeMenuStateHelper
    {
        internal static bool TryToggleBuildingRadialMenu(
            object radialMenu,
            Action ensureSelectedPart,
            Action<object> applyRadialMenuRuntimeOverrides,
            Action resolveCursorPlacementBinder,
            Action forceMousePlacementRefresh,
            Action setPlacementMode,
            ref bool manualBuildModeActive)
        {
            ensureSelectedPart();

            applyRadialMenuRuntimeOverrides(radialMenu);
            resolveCursorPlacementBinder();

            var menuComponent = radialMenu as Component;
            if (EasyBuildFacade.IsMenuOpen(menuComponent))
            {
                EasyBuildFacade.TryCloseMenu(menuComponent);
                manualBuildModeActive = false;
                return true;
            }

            EasyBuildFacade.TryOpenMenu(menuComponent);
            manualBuildModeActive = true;
            setPlacementMode();
            forceMousePlacementRefresh();
            return true;
        }

        internal static void EnsureRadialMenuClosed(object radialMenu)
        {
            var menuComponent = radialMenu as Component;
            if (EasyBuildFacade.IsMenuOpen(menuComponent))
                EasyBuildFacade.TryCloseMenu(menuComponent);
        }

        internal static void ForceCloseBuildUi(
            Action ensureRadialMenuClosed,
            Action clearPlacementPreview,
            ref bool manualBuildModeActive,
            ref int currentHudSelectionIndex)
        {
            ensureRadialMenuClosed();
            clearPlacementPreview?.Invoke();
            manualBuildModeActive = false;
            currentHudSelectionIndex = -1;
        }
    }
}
