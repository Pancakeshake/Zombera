using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using Zombera.Core;
using Zombera.Environment;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Simulation;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.Simulation
{
    [AddComponentMenu("Zombera/World/World State Simulation Bridge")]
    [DisallowMultipleComponent]
    public sealed class WorldStateSimulationBridge : MonoBehaviour
    {
        [Serializable]
        private sealed class StateChangedEvent : UnityEvent<WorldStateChangeSet>
        {
        }

        [Header("State")]
        [SerializeField] private WorldBuilderService worldBuilderService;
        [SerializeField] private WorldStateManager worldStateManager;

        [Header("View Refresh")]
        [Tooltip("Optional materializer or registry with RefreshLoadedBuildingViews(WorldStateChangeSet), RefreshBuildingViews(WorldStateChangeSet), or RefreshLoadedViews().")]
        [SerializeField]
        private MonoBehaviour buildingViewRefreshTarget;

        [SerializeField] private bool autoDiscoverRefreshTarget = true;
        [SerializeField] private bool refreshBoundViewsFallback = true;
        [SerializeField] private StateChangedEvent onStateChanged = new();

        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private WorldStateSimulationService _service;
        private bool _subscribed;

        public WorldStateSimulationService Service
        {
            get
            {
                EnsureService();
                return _service;
            }
        }

        private void OnEnable()
        {
            EnsureService();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void InjectStateManager(WorldStateManager manager)
        {
            if (worldStateManager == manager)
                return;

            Unsubscribe();
            worldStateManager = manager;
            _service = null;
            EnsureService();
        }

        public bool SynchronizeToDayNight() =>
            SynchronizeToDayNight(FindFirstObjectByType<DayNightController>());

        public bool SynchronizeToDayNight(DayNightController dayNight)
        {
            if (!CanSynchronizeForCurrentGameState())
                return true;

            if (dayNight == null)
                return false;

            return AdvanceToHour(ComputeAbsoluteGameHour(dayNight.DayNumber, dayNight.CurrentHour));
        }

        public bool AdvanceHours(int hours)
        {
            EnsureService();
            if (_service == null)
                return LogFailure("advance hours", null);

            return _service.AdvanceHours(hours, out _, out var report) ||
                LogFailure("advance hours", report);
        }

        public bool AdvanceToHour(long targetHour)
        {
            EnsureService();
            if (_service == null)
                return LogFailure("advance to hour", null);

            return _service.AdvanceToHour(targetHour, out _, out var report) ||
                LogFailure("advance to hour", report);
        }

        public bool FireBuildingNow(WorldEntityId buildingId, float intensity01 = 1f)
        {
            EnsureService();
            if (_service == null)
                return LogFailure("fire building", null);

            return _service.ApplyEventNow(
                    WorldEventType.FireBuilding,
                    buildingId,
                    intensity01,
                    out _,
                    out var report) ||
                LogFailure("fire building", report);
        }

        public bool AbandonBuildingNow(WorldEntityId buildingId)
        {
            EnsureService();
            if (_service == null)
                return LogFailure("abandon building", null);

            return _service.ApplyEventNow(
                    WorldEventType.AbandonBuilding,
                    buildingId,
                    1f,
                    out _,
                    out var report) ||
                LogFailure("abandon building", report);
        }

        public static long ComputeAbsoluteGameHour(int dayNumber, float currentHour)
        {
            var dayIndex = Math.Max(0, dayNumber - 1);
            var hour = Mathf.FloorToInt(Mathf.Clamp(currentHour, 0f, 23.9999f));
            return dayIndex * 24L + hour;
        }

        private void EnsureService()
        {
            ResolveStateManagerIfNeeded();
            if (worldStateManager == null)
                return;

            _service ??= new WorldStateSimulationService(worldStateManager);
            Subscribe();
        }

        private void ResolveStateManagerIfNeeded()
        {
            if (worldStateManager != null)
                return;

            if (worldBuilderService == null)
                worldBuilderService = FindFirstObjectByType<WorldBuilderService>();

            worldStateManager = worldBuilderService != null
                ? worldBuilderService.StateManager
                : FindFirstObjectByType<WorldStateManager>();
        }

        private void Subscribe()
        {
            if (_subscribed || worldStateManager == null)
                return;

            worldStateManager.StateChanged += HandleStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || worldStateManager == null)
                return;

            worldStateManager.StateChanged -= HandleStateChanged;
            _subscribed = false;
        }

        private void HandleStateChanged(WorldStateChangeSet changes)
        {
            if (!HasBuildingChange(changes))
                return;

            onStateChanged?.Invoke(changes);
            if (!TryInvokeRefreshTarget(changes) && refreshBoundViewsFallback)
                RefreshBoundViews(changes);
        }

        private bool TryInvokeRefreshTarget(WorldStateChangeSet changes)
        {
            ResolveRefreshTargetIfNeeded();
            if (buildingViewRefreshTarget == null)
                return false;

            return TryInvokeRefreshMethod(buildingViewRefreshTarget, "RefreshLoadedBuildingViews", changes) ||
                TryInvokeRefreshMethod(buildingViewRefreshTarget, "RefreshBuildingViews", changes) ||
                TryInvokeRefreshMethod(buildingViewRefreshTarget, "RefreshLoadedViews", null);
        }

        private void ResolveRefreshTargetIfNeeded()
        {
            if (!autoDiscoverRefreshTarget || buildingViewRefreshTarget != null)
                return;

            var candidates = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (HasRefreshMethod(candidates[i]))
                {
                    buildingViewRefreshTarget = candidates[i];
                    return;
                }
            }
        }

        private static bool TryInvokeRefreshMethod(MonoBehaviour target, string methodName, WorldStateChangeSet changes)
        {
            var args = changes != null ? new object[] { changes } : Array.Empty<object>();
            var parameters = changes != null ? new[] { typeof(WorldStateChangeSet) } : Type.EmptyTypes;
            var method = target.GetType().GetMethod(methodName, InstanceFlags, null, parameters, null);
            if (method == null)
                return false;

            method.Invoke(target, args);
            return true;
        }

        private static bool HasRefreshMethod(MonoBehaviour target)
        {
            if (target == null)
                return false;

            var type = target.GetType();
            return type.GetMethod("RefreshLoadedBuildingViews", InstanceFlags, null, new[] { typeof(WorldStateChangeSet) }, null) != null ||
                type.GetMethod("RefreshBuildingViews", InstanceFlags, null, new[] { typeof(WorldStateChangeSet) }, null) != null ||
                type.GetMethod("RefreshLoadedViews", InstanceFlags, null, Type.EmptyTypes, null) != null;
        }

        private static void RefreshBoundViews(WorldStateChangeSet changes)
        {
            var views = FindObjectsByType<WorldStateEntityView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < views.Length; i++)
            {
                if (ShouldRefreshView(views[i], changes))
                    views[i].SendMessage("RefreshFromWorldState", changes, SendMessageOptions.DontRequireReceiver);
            }
        }

        private static bool ShouldRefreshView(WorldStateEntityView view, WorldStateChangeSet changes)
        {
            if (view == null || view.EntityKind != WorldEntityKind.Building || !view.HasWorldEntityId)
                return false;

            return ContainsId(changes.UpdatedEntityIds, view.WorldEntityId) ||
                ContainsId(changes.AddedEntityIds, view.WorldEntityId);
        }

        private static bool HasBuildingChange(WorldStateChangeSet changes) =>
            changes != null &&
            (HasBuildingId(changes.UpdatedEntityIds) || HasBuildingId(changes.AddedEntityIds));

        private static bool HasBuildingId(System.Collections.Generic.List<WorldEntityId> ids)
        {
            if (ids == null)
                return false;

            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i].kind == WorldEntityKind.Building)
                    return true;
            }

            return false;
        }

        private static bool ContainsId(System.Collections.Generic.List<WorldEntityId> ids, WorldEntityId id)
        {
            if (ids == null)
                return false;

            return ids.BinarySearch(id) >= 0;
        }

        private bool LogFailure(string operation, WorldValidationReport report)
        {
            var detail = report != null && report.Errors.Count > 0
                ? string.Join("; ", report.Errors)
                : "WorldState simulation service is unavailable.";
            Debug.LogWarning($"[WorldStateSimulationBridge] Failed to {operation}: {detail}", this);
            return false;
        }

        private static bool CanSynchronizeForCurrentGameState()
        {
            var gm = GameManagerGateway.Instance;
            if (gm == null)
                return true;

            var state = gm.CurrentState;
            return state is GameState.LoadingWorld or GameState.Playing or GameState.Paused;
        }
    }
}
