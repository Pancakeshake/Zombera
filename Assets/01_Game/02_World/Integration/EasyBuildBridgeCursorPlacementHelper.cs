using UnityEngine;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeCursorPlacementHelper
    {
        internal static void ResolveCursorPlacementBinder(
            Component owner,
            ref EasyBuildCursorPlacementBinder cachedCursorPlacementBinder)
        {
            if (cachedCursorPlacementBinder != null) return;

            cachedCursorPlacementBinder = owner.GetComponent<EasyBuildCursorPlacementBinder>();
            if (cachedCursorPlacementBinder != null) return;

            cachedCursorPlacementBinder = owner.GetComponentInParent<EasyBuildCursorPlacementBinder>();
        }

        internal static void ForceMousePlacementRefreshIfMenuOpen(
            object radialMenu,
            EasyBuildCursorPlacementBinder cachedCursorPlacementBinder)
        {
            var isOpen = EasyBuildBridgeReflectionHelper.GetBoolMemberValue(radialMenu, "IsOpen");
            if (isOpen == true)
                cachedCursorPlacementBinder?.ForceMousePlacementRefresh();
        }
    }
}
