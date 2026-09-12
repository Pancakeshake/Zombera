using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.Systems;
using Zombera.UI;

namespace Zombera.World
{
    public partial class WorldManager
    {
        private static readonly Dictionary<string, PropertyInfo> ReflectionCache = new();

        private static PropertyInfo GetCachedProperty(Type type, string propertyName, BindingFlags flags)
        {
            var key = type.FullName + "." + propertyName + "." + (int)flags;
            if (ReflectionCache.TryGetValue(key, out var prop)) return prop;

            prop = type.GetProperty(propertyName, flags);
            ReflectionCache[key] = prop;
            return prop;
        }

        private bool ShouldForceWorldSpawnedBuildingsOnly()
        {
            return forceWorldSpawnedBuildingsOnly
                   && useProceduralStreamingWorld
                   && enableStreamedCityBuilder;
        }

        private void TrySuppressEasyBuildAutomaticPersistence()
        {
            if (_easyBuildAutoPersistenceSuppressed) return;
            if (!disableEasyBuildAutoPersistenceInSpawnOnlyMode) return;
            if (!ShouldForceWorldSpawnedBuildingsOnly()) return;

            var managerType = ResolveType(EasyBuildManagerTypeName);
            if (managerType == null) return;

            var managerInstances = ResolveEasyBuildManagerInstances(managerType);
            if (managerInstances.Count == 0) return;

            var appliedAny = false;
            foreach (var managerInstance in managerInstances)
                appliedAny |= ApplySpawnOnlyPersistencePolicy(managerInstance, managerType);

            if (appliedAny) _easyBuildAutoPersistenceSuppressed = true;
        }

        private static List<object> ResolveEasyBuildManagerInstances(Type managerType)
        {
            var instances = new List<object>(4);

            var instanceProperty = GetCachedProperty(managerType, "Instance", BindingFlags.Public | BindingFlags.Static);
            var staticInstance = instanceProperty?.GetValue(null);
            if (staticInstance != null) instances.Add(staticInstance);

            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;
                if (!managerType.IsInstanceOfType(behaviour)) continue;
                if (instances.Contains(behaviour)) continue;
                instances.Add(behaviour);
            }

            return instances;
        }

        private static bool ApplySpawnOnlyPersistencePolicy(object managerInstance, Type managerType)
        {
            if (managerInstance == null || managerType == null) return false;

            var saveSettingsProperty = GetCachedProperty(managerType, "SaveSettings", BindingFlags.Public | BindingFlags.Instance);
            var saveSettings = saveSettingsProperty?.GetValue(managerInstance);
            if (saveSettings == null) return false;

            var settingsType = saveSettings.GetType();

            var enableSavingProperty = GetCachedProperty(settingsType, "EnableSaving", BindingFlags.Public | BindingFlags.Instance);
            enableSavingProperty?.SetValue(saveSettings, false);

            var autoSaveProperty = GetCachedProperty(settingsType, "AutoSave", BindingFlags.Public | BindingFlags.Instance);
            autoSaveProperty?.SetValue(saveSettings, false);

            var saveModeProperty = GetCachedProperty(settingsType, "SaveMode", BindingFlags.Public | BindingFlags.Instance);
            if (saveModeProperty != null && saveModeProperty.PropertyType.IsEnum)
            {
                var manualMode = Enum.Parse(saveModeProperty.PropertyType, "Manual");
                saveModeProperty.SetValue(saveSettings, manualMode);
            }

            saveSettingsProperty.SetValue(managerInstance, saveSettings);
            return true;
        }

        private void EnforceWorldSpawnedBuildingMode()
        {
            if (!ShouldForceWorldSpawnedBuildingsOnly()) return;
            if (Time.unscaledTime < _nextBuildInputSuppressionAt) return;

            var activeScanInterval = Mathf.Max(0.25f, buildInputSuppressionPollSeconds);
            var idleScanInterval = Mathf.Max(activeScanInterval, buildInputSuppressionIdlePollSeconds);
            _nextBuildInputSuppressionAt = Time.unscaledTime +
                                           (_foundSuppressibleBuilderInputLastScan
                                               ? activeScanInterval
                                               : idleScanInterval);

            var foundSuppressibleInput = false;
            var activeInputControllers = PlayerInputController.ActiveInstances;

            if (activeInputControllers.Count > 0)
            {
                for (var i = 0; i < activeInputControllers.Count; i++)
                    foundSuppressibleInput |= SuppressBuildInputOnController(activeInputControllers[i]);
            }
            else
            {
                // Fallback for frames before controllers have registered in their static active list.
                var discoveredInputControllers = FindObjectsByType<PlayerInputController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

                for (var i = 0; i < discoveredInputControllers.Length; i++)
                    foundSuppressibleInput |= SuppressBuildInputOnController(discoveredInputControllers[i]);
            }

            var suppressedEasyBuildRuntimeBehaviours = 0;
            if (suppressEasyBuildRuntimeBehavioursInSpawnOnlyMode)
            {
                var budget = Mathf.Clamp(maxEasyBuildRuntimeDisablePerScan, 8, 8192);
                foundSuppressibleInput |= SuppressEasyBuildRuntimeBehaviours(
                    budget,
                    out suppressedEasyBuildRuntimeBehaviours);

                if (logEasyBuildRuntimeSuppression && suppressedEasyBuildRuntimeBehaviours > 0)
                    Debug.Log(
                        "[WorldManager] Disabled " + suppressedEasyBuildRuntimeBehaviours +
                        " active EasyBuild runtime behaviours while spawn-only mode is active.",
                        this);
            }

            _foundSuppressibleBuilderInputLastScan = foundSuppressibleInput;
        }

        private static bool SuppressBuildInputOnController(PlayerInputController inputController)
        {
            if (inputController == null || !inputController.isActiveAndEnabled) return false;

            var suppressedAny = false;

            var radialBridge = inputController.GetComponent<EasyBuildRadialMenuInputBridge>();
            if (radialBridge != null && radialBridge.enabled)
            {
                suppressedAny = true;

                if (radialBridge.IsBuildUiActive)
                    radialBridge.TryCancelBuildUi();

                radialBridge.enabled = false;
            }

            var cursorBinder = inputController.GetComponent<EasyBuildCursorPlacementBinder>();
            if (cursorBinder != null && cursorBinder.enabled)
            {
                suppressedAny = true;
                cursorBinder.enabled = false;
            }

            var legacyBuilder = inputController.GetComponent<BuildPlacementController>();
            if (legacyBuilder != null && legacyBuilder.enabled)
            {
                suppressedAny = true;

                if (legacyBuilder.IsBuildModeActive)
                    legacyBuilder.ExitBuildMode();

                legacyBuilder.enabled = false;
            }

            return suppressedAny;
        }

        private bool SuppressEasyBuildRuntimeBehaviours(int disableBudget, out int disabledCount)
        {
            disabledCount = 0;
            var foundEnabledBehaviour = false;

            RefreshEasyBuildRuntimeBehaviourCacheIfNeeded();

            for (var i = _easyBuildRuntimeBehaviourBuffer.Count - 1; i >= 0; i--)
            {
                var behaviour = _easyBuildRuntimeBehaviourBuffer[i];

                if (behaviour == null)
                {
                    _easyBuildRuntimeBehaviourBuffer.RemoveAt(i);
                    continue;
                }

                if (!behaviour.enabled)
                {
                    _easyBuildRuntimeBehaviourBuffer.RemoveAt(i);
                    continue;
                }

                if (!behaviour.gameObject.activeInHierarchy) continue;
                if (!IsEasyBuildRuntimeBehaviour(behaviour)) continue;

                foundEnabledBehaviour = true;
                behaviour.enabled = false;
                disabledCount++;

                if (disabledCount >= disableBudget)
                    break;
            }

            return foundEnabledBehaviour;
        }

        private void RefreshEasyBuildRuntimeBehaviourCacheIfNeeded()
        {
            var now = Time.unscaledTime;

            if (!_forceEasyBuildRuntimeRescan && now < _nextEasyBuildRuntimeRescanAt)
                return;

            _forceEasyBuildRuntimeRescan = false;
            _nextEasyBuildRuntimeRescanAt = now + Mathf.Max(0.25f, easyBuildRuntimeRescanSeconds);
            _easyBuildRuntimeBehaviourBuffer.Clear();

            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null) continue;
                if (!IsEasyBuildRuntimeBehaviour(behaviour)) continue;
                _easyBuildRuntimeBehaviourBuffer.Add(behaviour);
            }
        }

        private static bool IsEasyBuildRuntimeBehaviour(MonoBehaviour behaviour)
        {
            var fullName = behaviour.GetType().FullName;
            return !string.IsNullOrEmpty(fullName)
                   && fullName.StartsWith(EasyBuildRuntimeNamespacePrefix, StringComparison.Ordinal);
        }

        private static Type ResolveType(string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName)) return null;

            var direct = Type.GetType(fullTypeName);
            if (direct != null) return direct;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var candidate = assembly.GetType(fullTypeName);
                if (candidate != null) return candidate;
            }

            return null;
        }
    }
}
