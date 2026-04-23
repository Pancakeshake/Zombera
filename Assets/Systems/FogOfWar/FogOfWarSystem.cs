using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Systems
{
    /// <summary>
    /// Evaluates all active fog targets against active vision sources and applies visibility state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FogOfWarSystem : MonoBehaviour
    {
        [Header("Update")]
        [SerializeField, Min(0.02f)] private float updateIntervalSeconds = 0.08f;
        [SerializeField] private bool hideTargetsWhenNoSources = true;

        [Header("Runtime Bootstrap")]
        [SerializeField] private bool autoBootstrapUnitsAtRuntime = true;
        [SerializeField, Min(0.2f)] private float bootstrapRefreshSeconds = 1f;
        [SerializeField] private bool includeSurvivorRoleAsVisionSource = true;
        [SerializeField] private bool autoBootstrapPlayerVisionOverlay = true;

        [Header("Line Of Sight")]
        [SerializeField] private bool requireLineOfSight = false;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        private readonly List<Unit> bootstrapUnitsBuffer = new List<Unit>(64);
        private float nextRefreshTime;
        private float nextBootstrapTime;

        private void OnEnable()
        {
            if (!FogOfWarRuntimeConfig.FeatureEnabled)
            {
                if (Application.isPlaying)
                {
                    RemoveFogComponentsInOwnerSceneAtRuntime();
                    Destroy(this);
                }
                else
                {
                    enabled = false;
                }

                return;
            }

            nextRefreshTime = Time.time;
            nextBootstrapTime = Time.time;
        }

        private void Update()
        {
            TryAutoBootstrapUnits();

            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + updateIntervalSeconds;
            RefreshFogState();
        }

        private void RemoveFogComponentsInOwnerSceneAtRuntime()
        {
            var ownerScene = gameObject.scene;

            FogOfWarVisionOverlay[] overlays = FindObjectsByType<FogOfWarVisionOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < overlays.Length; i++)
            {
                FogOfWarVisionOverlay overlay = overlays[i];
                if (overlay == null || overlay.gameObject.scene != ownerScene)
                {
                    continue;
                }

                Destroy(overlay);
            }

            FogOfWarTarget[] targets = FindObjectsByType<FogOfWarTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < targets.Length; i++)
            {
                FogOfWarTarget target = targets[i];
                if (target == null || target.gameObject.scene != ownerScene)
                {
                    continue;
                }

                Destroy(target);
            }

            FogOfWarVisionSource[] sources = FindObjectsByType<FogOfWarVisionSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                FogOfWarVisionSource source = sources[i];
                if (source == null || source.gameObject.scene != ownerScene)
                {
                    continue;
                }

                Destroy(source);
            }

            FogOfWarSystem[] systems = FindObjectsByType<FogOfWarSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < systems.Length; i++)
            {
                FogOfWarSystem system = systems[i];
                if (system == null || system == this || system.gameObject.scene != ownerScene)
                {
                    continue;
                }

                Destroy(system);
            }
        }

        public void RefreshFogState()
        {
            IReadOnlyList<FogOfWarVisionSource> sources = FogOfWarVisionSource.ActiveSources;
            IReadOnlyList<FogOfWarTarget> targets = FogOfWarTarget.ActiveTargets;

            if (targets == null || targets.Count == 0)
            {
                return;
            }

            bool hasOperationalSource = false;
            if (sources != null)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    FogOfWarVisionSource source = sources[i];
                    if (source != null && source.IsOperational)
                    {
                        hasOperationalSource = true;
                        break;
                    }
                }
            }

            float now = Time.time;

            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                FogOfWarTarget target = targets[targetIndex];
                if (target == null || !target.enabled)
                {
                    continue;
                }

                bool visible = false;

                if (hasOperationalSource)
                {
                    for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                    {
                        FogOfWarVisionSource source = sources[sourceIndex];
                        if (source == null || !source.IsOperational || source.OwnsTarget(target))
                        {
                            continue;
                        }

                        if (source.CanSee(target, requireLineOfSight, lineOfSightMask))
                        {
                            visible = true;
                            break;
                        }
                    }
                }
                else
                {
                    visible = !hideTargetsWhenNoSources;
                }

                target.ApplyVisionState(visible, now);
            }
        }

        private void TryAutoBootstrapUnits()
        {
            if (!autoBootstrapUnitsAtRuntime)
            {
                return;
            }

            if (Time.time < nextBootstrapTime)
            {
                return;
            }

            nextBootstrapTime = Time.time + Mathf.Max(0.2f, bootstrapRefreshSeconds);
            Unit overlayOwnerUnit = autoBootstrapPlayerVisionOverlay ? ResolveOverlayOwnerUnit() : null;

            bootstrapUnitsBuffer.Clear();
            UnitManager manager = UnitManager.Instance;

            if (manager != null)
            {
                manager.GetAllActiveUnits(bootstrapUnitsBuffer);
            }
            else
            {
                Unit[] discoveredUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (discoveredUnits != null)
                {
                    for (int i = 0; i < discoveredUnits.Length; i++)
                    {
                        Unit discoveredUnit = discoveredUnits[i];
                        if (discoveredUnit != null)
                        {
                            bootstrapUnitsBuffer.Add(discoveredUnit);
                        }
                    }
                }
            }

            for (int i = 0; i < bootstrapUnitsBuffer.Count; i++)
            {
                Unit unit = bootstrapUnitsBuffer[i];
                if (unit == null || !unit.gameObject.activeInHierarchy)
                {
                    continue;
                }

                FogOfWarVisionSource source = unit.GetComponent<FogOfWarVisionSource>();

                if (ShouldBootstrapVisionSource(unit) && source == null)
                {
                    source = unit.gameObject.AddComponent<FogOfWarVisionSource>();
                }

                if (ShouldBootstrapTarget(unit) && unit.GetComponent<FogOfWarTarget>() == null)
                {
                    unit.gameObject.AddComponent<FogOfWarTarget>();
                }

                if (autoBootstrapPlayerVisionOverlay)
                {
                    bool shouldOwnOverlay = source != null && unit == overlayOwnerUnit;
                    EnsureOverlayOwnership(unit, shouldOwnOverlay);
                }
            }
        }

        private static void EnsureOverlayOwnership(Unit unit, bool shouldOwnOverlay)
        {
            if (unit == null)
            {
                return;
            }

            FogOfWarVisionOverlay[] overlays = unit.GetComponents<FogOfWarVisionOverlay>();
            int overlayCount = overlays != null ? overlays.Length : 0;

            if (shouldOwnOverlay)
            {
                if (overlayCount == 0)
                {
                    unit.gameObject.AddComponent<FogOfWarVisionOverlay>();
                    return;
                }

                for (int i = 1; i < overlayCount; i++)
                {
                    DestroyOverlayComponent(overlays[i]);
                }

                return;
            }

            for (int i = 0; i < overlayCount; i++)
            {
                DestroyOverlayComponent(overlays[i]);
            }
        }

        private static void DestroyOverlayComponent(FogOfWarVisionOverlay overlay)
        {
            if (overlay == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(overlay);
            }
            else
            {
                DestroyImmediate(overlay);
            }
        }

        private static Unit ResolveOverlayOwnerUnit()
        {
            PlayerInputController[] inputControllers = FindObjectsByType<PlayerInputController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < inputControllers.Length; i++)
            {
                PlayerInputController inputController = inputControllers[i];
                if (inputController == null || !inputController.enabled || !inputController.isActiveAndEnabled)
                {
                    continue;
                }

                Unit inputUnit = inputController.GetComponent<Unit>();
                if (inputUnit == null)
                {
                    inputUnit = inputController.GetComponentInParent<Unit>();
                }

                if (inputUnit != null && inputUnit.IsAlive)
                {
                    return inputUnit;
                }
            }

            UnitManager manager = UnitManager.Instance;
            if (manager != null)
            {
                Unit managedPlayer = manager.FindFirstUnitByRole(UnitRole.Player);
                if (managedPlayer != null)
                {
                    return managedPlayer;
                }
            }

            Unit[] units = FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (unit != null && unit.Role == UnitRole.Player && unit.IsAlive)
                {
                    return unit;
                }
            }

            return null;
        }

        private bool ShouldBootstrapVisionSource(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.Role == UnitRole.Player || unit.Role == UnitRole.SquadMember)
            {
                return true;
            }

            return includeSurvivorRoleAsVisionSource && unit.Role == UnitRole.Survivor;
        }

        private static bool ShouldBootstrapTarget(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            return UnitFactionUtility.AreHostile(UnitFaction.Survivor, unit.Faction);
        }
    }
}
