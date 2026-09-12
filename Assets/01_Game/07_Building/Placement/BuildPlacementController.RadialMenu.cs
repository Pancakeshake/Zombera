using System;
using System.Reflection;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    public sealed partial class BuildPlacementController
    {
        private bool TryOpenBuildingRadialMenu()
        {
            if (!TryResolveRadialMenu(out var radialMenu)) return false;

            ApplyRadialMenuRuntimeOverrides(radialMenu);

            var isOpen = GetBoolMemberValue(radialMenu, "IsOpen");
            if (isOpen == true)
            {
                InvokeMethod(radialMenu, "CloseMenu");
                return true;
            }

            InvokeMethod(radialMenu, "OpenMenu");
            return true;
        }

        private bool TryResolveRadialMenu(out object radialMenu)
        {
            radialMenu = _cachedRadialMenu;
            if (radialMenu is MonoBehaviour)
                return true;

            _cachedRadialMenuType ??= FindType(
                "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Implementations.BuildingRadialMenuUI");

            if (_cachedRadialMenuType == null)
                return false;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var instanceProp = _cachedRadialMenuType.GetProperty("Instance", flags);
            radialMenu = instanceProp?.GetValue(null);

            if (radialMenu == null)
                return false;

            _cachedRadialMenu = radialMenu;
            return true;
        }

        private void ApplyRadialMenuRuntimeOverrides(object radialMenu)
        {
            if (radialMenu is not MonoBehaviour radialMenuBehaviour) return;

            ApplyRadialMenuVerticalOffset(radialMenuBehaviour);

            if (radialMenuUsesUnscaledTime)
                ConfigureRadialMenuUnscaledTime(radialMenuBehaviour);
        }

        private void ApplyRadialMenuVerticalOffset(MonoBehaviour radialMenuBehaviour)
        {
            var radialRect = radialMenuBehaviour.GetComponent<RectTransform>();
            if (radialRect == null) return;

            if (_cachedRadialMenuRect != radialRect)
            {
                _cachedRadialMenuRect = radialRect;
                _cachedRadialMenuBaseAnchoredPosition = radialRect.anchoredPosition;
                _cachedRadialMenuScreenHeight = -1;
                _cachedRadialMenuUnscaledConfigured = false;
            }

            var screenHeight = Mathf.Max(1, Screen.height);
            if (_cachedRadialMenuScreenHeight == screenHeight) return;

            _cachedRadialMenuScreenHeight = screenHeight;
            var yOffset = screenHeight * Mathf.Max(0f, radialMenuVerticalScreenOffsetPercent);
            radialRect.anchoredPosition = new Vector2(
                _cachedRadialMenuBaseAnchoredPosition.x,
                _cachedRadialMenuBaseAnchoredPosition.y + yOffset);
        }

        private void ConfigureRadialMenuUnscaledTime(MonoBehaviour radialMenuBehaviour)
        {
            if (_cachedRadialMenuUnscaledConfigured) return;

            var animators = radialMenuBehaviour.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
                animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;

            _cachedRadialMenuUnscaledConfigured = true;
        }

        private static Type FindType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null) return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }

        private static bool? GetBoolMemberValue(object instance, string memberName)
        {
            if (instance == null) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null && field.GetValue(instance) is bool fieldValue)
                return fieldValue;

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.GetValue(instance) is bool propertyValue)
                return propertyValue;

            return null;
        }

        private static void InvokeMethod(object instance, string methodName)
        {
            if (instance == null) return;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var method = instance.GetType().GetMethod(methodName, flags);
            method?.Invoke(instance, null);
        }
    }
}
