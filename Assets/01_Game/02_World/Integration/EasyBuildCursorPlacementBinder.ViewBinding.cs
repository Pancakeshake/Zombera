#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildCursorPlacementBinder
    {
        /// <summary>
        ///     Wires gameplay camera to every Easy Build view and switches to Orbital ray placement.
        ///     Must run before <c>BuildingController.SetMode(Placement)</c> — FirstPerson view ships with an
        ///     unassigned <c>m_raycastCamera</c> on player prefabs and will throw during preview spawn otherwise.
        /// </summary>
        public void EnsurePlacementViewReady()
        {
            if (!useCursorRayPlacement) return;

            ResolveBuildingController();
            if (buildingController == null) return;

            var cam = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (cam == null) return;

            WireAllViewRaycastCameras(buildingController, cam);
            ApplyOrbitalViewAndCamera();
        }

        private void ApplyOrbitalViewAndCamera()
        {
            if (!useCursorRayPlacement) return;

            ResolveBuildingController();
            if (buildingController == null) return;

            var cam = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (cam == null) return;

            _cachedOrbitalViewType ??= FindType(OrbitalViewTypeName);
            var orbitalType = _cachedOrbitalViewType;
            if (orbitalType == null || !typeof(Component).IsAssignableFrom(orbitalType)) return;

            var orbital = buildingController.GetComponent(orbitalType);
            if (orbital == null) orbital = buildingController.gameObject.AddComponent(orbitalType);

            SetMemberValue(orbital, "RaycastCamera", cam);
            SetMemberValue(orbital, "OriginTransform", cam.transform);

            EnsureOrbitalInViewsList(buildingController, orbital);

            _cachedBuildingViewEnumType ??= FindType(BuildingViewTypeEnumName);
            var viewEnumType = _cachedBuildingViewEnumType;
            if (viewEnumType == null) return;

            var orbitalEnumValue = Enum.Parse(viewEnumType, "Orbital");
            InvokeMethod(buildingController, "SetView", orbitalEnumValue);
        }

        private void WireAllViewRaycastCameras(MonoBehaviour controller, Camera cam)
        {
            if (controller == null || cam == null) return;

            var views = GetMemberValue(controller, "Views") as Array;
            if (views != null)
            {
                for (var i = 0; i < views.Length; i++)
                {
                    if (views.GetValue(i) is Component view)
                        WireSingleBuildingView(view, cam);
                }
            }

            _cachedBuildingViewType ??= FindType(BuildingViewTypeName);
            var viewType = _cachedBuildingViewType;
            if (viewType == null || !typeof(Component).IsAssignableFrom(viewType)) return;

            var viewComponents = controller.GetComponents(viewType);
            for (var i = 0; i < viewComponents.Length; i++)
            {
                if (viewComponents[i] is Component view)
                    WireSingleBuildingView(view, cam);
            }
        }

        private static void WireSingleBuildingView(Component view, Camera cam)
        {
            if (view == null || cam == null) return;

            SetMemberValue(view, "RaycastCamera", cam);
            SetMemberValue(view, "m_raycastCamera", cam);
            SetMemberValue(view, "OriginTransform", cam.transform);
            SetMemberValue(view, "m_originTransform", cam.transform);
        }

        private void ResolveBuildingController()
        {
            if (buildingController != null) return;

            _cachedBuildingControllerType ??= FindType(BuildingControllerTypeName);
            var controllerType = _cachedBuildingControllerType;
            if (controllerType == null || !typeof(Component).IsAssignableFrom(controllerType)) return;

            buildingController = GetComponent(controllerType) as MonoBehaviour;
            if (buildingController != null) return;

            // Some setups keep BuildingController on a different scene object.
            buildingController = GetStaticMemberValue(controllerType, "Instance") as MonoBehaviour;
        }

        private static void EnsureOrbitalInViewsList(Component controller, Component orbital)
        {
            if (controller == null || orbital == null) return;

            if (!TryResolveOrbitalViewListContext(controller, out var viewType, out var orbitalEnumValue))
                return;

            var existing = GetMemberValue(controller, "Views") as Array;
            if (existing == null)
            {
                SetSingleViewEntry(controller, viewType, orbital);
                return;
            }

            var output = BuildViewArrayWithOrbital(existing, orbital, orbitalEnumValue, viewType);
            SetMemberValue(controller, "Views", output);
        }

        private static bool TryResolveOrbitalViewListContext(Component controller, out Type viewType,
            out object orbitalEnumValue)
        {
            viewType = null;
            orbitalEnumValue = null;

            var binder = controller.GetComponent<EasyBuildCursorPlacementBinder>();
            if (binder == null) return false;

            binder._cachedBuildingViewType ??= FindType(BuildingViewTypeName);
            binder._cachedBuildingViewEnumType ??= FindType(BuildingViewTypeEnumName);

            viewType = binder._cachedBuildingViewType;
            var enumType = binder._cachedBuildingViewEnumType;
            if (viewType == null || enumType == null) return false;

            orbitalEnumValue = Enum.Parse(enumType, "Orbital");
            return true;
        }

        private static void SetSingleViewEntry(Component controller, Type viewType, Component orbital)
        {
            var single = Array.CreateInstance(viewType, 1);
            single.SetValue(orbital, 0);
            SetMemberValue(controller, "Views", single);
        }

        private static Array BuildViewArrayWithOrbital(Array existing, Component orbital, object orbitalEnumValue,
            Type viewType)
        {
            var views = CollectViewsWithOrbitalReplacement(existing, orbital, orbitalEnumValue, out var alreadyHaveOrbital);
            if (!alreadyHaveOrbital)
                views.Add(orbital);

            var output = Array.CreateInstance(viewType, views.Count);
            for (var i = 0; i < views.Count; i++)
                output.SetValue(views[i], i);

            return output;
        }

        private static List<Component> CollectViewsWithOrbitalReplacement(Array existing, Component orbital,
            object orbitalEnumValue, out bool alreadyHaveOrbital)
        {
            alreadyHaveOrbital = false;
            var views = new List<Component>();

            for (var i = 0; i < existing.Length; i++)
            {
                var view = existing.GetValue(i) as Component;
                if (view == null) continue;

                if (IsOrbitalView(view, orbitalEnumValue))
                {
                    if (!alreadyHaveOrbital)
                    {
                        views.Add(orbital);
                        alreadyHaveOrbital = true;
                    }

                    continue;
                }

                views.Add(view);
            }

            return views;
        }

        private static bool IsOrbitalView(Component view, object orbitalEnumValue)
        {
            if (view == null) return false;

            var viewType = GetMemberValue(view, "ViewType");
            if (viewType != null && orbitalEnumValue != null && viewType.Equals(orbitalEnumValue))
                return true;

            return view.GetType().Name.IndexOf("orbital", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
