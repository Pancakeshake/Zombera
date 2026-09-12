using System;
using UnityEngine;
using Zombera.EasyBuildAdapter;

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Resolution helpers for the EasyBuild bridges. Implemented on top of the typed
    ///     <see cref="EasyBuildFacade" />; the stringly-typed parameters are kept only for
    ///     signature compatibility with existing bridge call sites and are ignored.
    /// </summary>
    internal static class EasyBuildBridgeRuntimeResolverHelper
    {
        internal static bool TrySetControllerMode(
            object controller,
            string modeName,
            string buildingModeEnumTypeName,
            ref Type cachedBuildingModeType)
        {
            if (controller == null) return false;

            return EasyBuildFacade.TrySetControllerMode(modeName);
        }

        internal static object ResolveSelectedPartPlacementSettings(object controller)
        {
            if (controller == null) return null;

            return EasyBuildFacade.GetSelectedPartPlacementSettings();
        }

        internal static string ResolveCurrentControllerModeName(object controller)
        {
            if (controller == null) return null;

            return EasyBuildFacade.GetActiveControllerModeName();
        }

        internal static object ResolveBuildingControllerInstance(
            string buildingControllerTypeName,
            ref Type cachedBuildingControllerType,
            ref object cachedBuildingController)
        {
            if (cachedBuildingController is MonoBehaviour cachedBehaviour && cachedBehaviour != null)
                return cachedBuildingController;

            cachedBuildingController = EasyBuildFacade.ResolveBuildingController();
            return cachedBuildingController;
        }

        internal static object ResolveBuildingManagerInstance(
            string buildingManagerTypeName,
            ref Type cachedBuildingManagerType)
        {
            return EasyBuildFacade.BuildingManagerBehaviour;
        }

        internal static object ResolveBuildingInputInstance(
            string buildingInputTypeName,
            object controller,
            ref Type cachedBuildingInputType,
            ref object cachedBuildingInput)
        {
            if (cachedBuildingInput is MonoBehaviour cachedBehaviour && cachedBehaviour != null)
                return cachedBuildingInput;

            cachedBuildingInput = EasyBuildFacade.ResolveBuildingInput(controller as Component);
            return cachedBuildingInput;
        }

        internal static bool TryResolveRadialMenu(
            string radialMenuTypeName,
            ref Type cachedRadialMenuType,
            ref object cachedRadialMenu,
            out object radialMenu)
        {
            if (cachedRadialMenu is MonoBehaviour cachedBehaviour && cachedBehaviour != null)
            {
                radialMenu = cachedRadialMenu;
                return true;
            }

            radialMenu = EasyBuildFacade.RadialMenuBehaviour;
            if (radialMenu == null) return false;

            cachedRadialMenu = radialMenu;
            return true;
        }

        internal static void ApplyRadialMenuVerticalOffset(
            MonoBehaviour radialMenuBehaviour,
            float radialMenuVerticalScreenOffsetPercent,
            ref RectTransform cachedRadialMenuRect,
            ref Vector2 cachedRadialMenuBaseAnchoredPosition,
            ref int cachedRadialMenuScreenHeight,
            ref bool cachedRadialMenuUnscaledConfigured)
        {
            var radialRect = radialMenuBehaviour.GetComponent<RectTransform>();
            if (radialRect == null) return;

            if (cachedRadialMenuRect != radialRect)
            {
                cachedRadialMenuRect = radialRect;
                cachedRadialMenuBaseAnchoredPosition = radialRect.anchoredPosition;
                cachedRadialMenuScreenHeight = -1;
                cachedRadialMenuUnscaledConfigured = false;
            }

            var screenHeight = Mathf.Max(1, Screen.height);
            if (cachedRadialMenuScreenHeight == screenHeight) return;

            cachedRadialMenuScreenHeight = screenHeight;
            var yOffset = screenHeight * Mathf.Max(0f, radialMenuVerticalScreenOffsetPercent);
            radialRect.anchoredPosition = new Vector2(
                cachedRadialMenuBaseAnchoredPosition.x,
                cachedRadialMenuBaseAnchoredPosition.y + yOffset);
        }

        internal static void ConfigureRadialMenuUnscaledTime(
            MonoBehaviour radialMenuBehaviour,
            ref bool cachedRadialMenuUnscaledConfigured)
        {
            if (cachedRadialMenuUnscaledConfigured) return;

            var animators = radialMenuBehaviour.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
                animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;

            cachedRadialMenuUnscaledConfigured = true;
        }
    }
}
