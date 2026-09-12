#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.BaseBuilding;
using Zombera.Core;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Coordinates base building jobs and tracks completed structures.
    /// </summary>
    public sealed class BaseManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private BuildManager buildManager;

        private readonly List<string> _completedBuildingIds = new();
        public IReadOnlyList<string> CompletedBuildingIds => _completedBuildingIds;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;

            IsInitialized = true;
            CoreEventBus.Instance?.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;

            IsInitialized = false;
            CoreEventBus.Instance?.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            _completedBuildingIds.Clear();
        }

        public BaseStorage GetBaseStorage()
        {
            return buildManager != null ? buildManager.BaseStorage : null;
        }

        public Vector3 GetSnappedBuildPosition(Vector3 worldPosition)
        {
            return buildManager == null ? worldPosition : buildManager.GetSnappedPosition(worldPosition);
        }

        public float GetSnappedBuildYaw(float yawDegrees)
        {
            return buildManager == null ? yawDegrees : buildManager.GetSnappedYaw(yawDegrees);
        }

        private void OnBuildingCompleted(BuildingCompletedEvent gameEvent)
        {
            if (string.IsNullOrWhiteSpace(gameEvent.BuildingId)) return;

            if (!_completedBuildingIds.Contains(gameEvent.BuildingId)) _completedBuildingIds.Add(gameEvent.BuildingId);
        }
    }
}