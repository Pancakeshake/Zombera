using System.Collections.Generic;
using UnityEngine;
using Zombera.BaseBuilding;
using Zombera.Core;
using Zombera.Data;

namespace Zombera.Systems
{
    /// <summary>
    /// Coordinates base building jobs and tracks completed structures.
    /// </summary>
    public sealed class BaseManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private BuildManager buildManager;

        private readonly List<string> completedBuildingIds = new List<string>();

        public bool IsInitialized { get; private set; }
        public IReadOnlyList<string> CompletedBuildingIds => completedBuildingIds;

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;
            EventSystem.Instance?.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            EventSystem.Instance?.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            completedBuildingIds.Clear();
        }

        public Blueprint PlaceBlueprint(BuildingData buildingData, Vector3 worldPosition)
        {
            if (buildManager == null)
            {
                return null;
            }

            return buildManager.PlaceBlueprint(buildingData, worldPosition);
        }

        private void OnBuildingCompleted(BuildingCompletedEvent gameEvent)
        {
            if (string.IsNullOrWhiteSpace(gameEvent.BuildingId))
            {
                return;
            }

            if (!completedBuildingIds.Contains(gameEvent.BuildingId))
            {
                completedBuildingIds.Add(gameEvent.BuildingId);
            }
        }
    }
}