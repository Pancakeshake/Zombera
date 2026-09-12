#region

using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class RuntimePlacedStructureFixer
    {
        // ── EasyBuild Polling ─────────────────────────────────────────

        private void TryProcessLastPlacedPart()
        {
            if (!TryGetLastPlacedPart(out var part, out var placedFrame)) return;
            if (placedFrame <= _lastObservedPlacedFrame) return;

            _lastObservedPlacedFrame = placedFrame;

            if (part == null || part.gameObject == null) return;
            ProcessPlacedStructure(part.gameObject, true);
        }

        private void TryProcessUntrackedPlacedParts()
        {
            if (!TryGetPlacedParts(out var placedParts)) return;

            foreach (var part in placedParts)
            {
                if (part is not Component component || component.gameObject == null) continue;
                ProcessPlacedStructure(component.gameObject, true);
            }
        }

        private void TrySeedExistingPlacedParts()
        {
            if (_seededExistingPlacedParts) return;
            if (!TryGetPlacedParts(out var placedParts)) return;

            foreach (var part in placedParts)
            {
                if (part is not Component component || component.gameObject == null) continue;
                _processedRootIds.Add(component.gameObject.GetInstanceID());
            }

            if (TryGetLastPlacedPart(out _, out var placedFrame))
                _lastObservedPlacedFrame = placedFrame;

            _lastFallbackScanObservedPlacedFrame = _lastObservedPlacedFrame;

            _seededExistingPlacedParts = true;
        }

        // ── EasyBuild Reflection Bridge ───────────────────────────────

        private bool TryGetLastPlacedPart(out Component part, out int placedFrame)
        {
            part = null;
            placedFrame = -1;

            PrimeEasyBuildReflectionCache();
            if (_lastPlacedPartProperty == null || _lastPlacedFrameProperty == null) return false;

            if (_lastPlacedFrameProperty.GetValue(null) is not int frameValue) return false;
            placedFrame = frameValue;

            part = _lastPlacedPartProperty.GetValue(null) as Component;
            return true;
        }

        private bool TryGetPlacedParts(out IEnumerable placedParts)
        {
            placedParts = null;

            PrimeEasyBuildReflectionCache();
            if (_buildingManagerInstanceProperty == null || _getPartsByStateMethod == null || _placedStateValue == null)
                return false;

            var managerInstance = _buildingManagerInstanceProperty.GetValue(null);
            if (managerInstance == null) return false;

            _getPartsByStateInvokeArgs[0] = _placedStateValue;
            placedParts = _getPartsByStateMethod.Invoke(managerInstance, _getPartsByStateInvokeArgs) as IEnumerable;
            return placedParts != null;
        }

        private void PrimeEasyBuildReflectionCache()
        {
            _buildingPartType ??= FindType(BuildingPartTypeName);
            _buildingPartStateType ??= FindType(BuildingPartStateTypeName);
            _buildingManagerType ??= FindType(BuildingManagerTypeName);

            if (_buildingPartType != null)
            {
                const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                _lastPlacedPartProperty ??= _buildingPartType.GetProperty("LastPlacedPart", staticFlags);
                _lastPlacedFrameProperty ??= _buildingPartType.GetProperty("LastPlacedFrame", staticFlags);
            }

            if (_buildingManagerType != null)
            {
                const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                _buildingManagerInstanceProperty ??= _buildingManagerType.GetProperty("Instance", staticFlags);
                _getPartsByStateMethod ??= _buildingManagerType.GetMethod("GetPartsByState", instanceFlags);
            }

            if (_buildingPartStateType != null && _placedStateValue == null)
            {
                try
                {
                    _placedStateValue = Enum.Parse(_buildingPartStateType, "Placed");
                    _getPartsByStateInvokeArgs[0] = _placedStateValue;
                }
                catch
                {
                    _placedStateValue = null;
                    _getPartsByStateInvokeArgs[0] = null;
                }
            }
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
    }
}
