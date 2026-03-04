using UnityEngine;
using Zombera.Data;

namespace Zombera.BaseBuilding
{
    /// <summary>
    /// Represents a placeable building blueprint and tracks construction state transitions.
    /// </summary>
    public sealed class Blueprint : MonoBehaviour
    {
        [SerializeField] private ConstructionJob constructionJob;

        public BuildingData BuildingData { get; private set; }
        public BuildingState State { get; private set; } = BuildingState.Blueprint;

        public void Initialize(BuildingData buildingData)
        {
            BuildingData = buildingData;
            State = BuildingState.Blueprint;

            if (constructionJob != null)
            {
                constructionJob.Initialize(this);
            }

            // TODO: Initialize ghost visuals and placement validation markers.
        }

        public ConstructionJob GetOrCreateConstructionJob()
        {
            if (constructionJob == null)
            {
                constructionJob = gameObject.AddComponent<ConstructionJob>();
                constructionJob.Initialize(this);
            }

            return constructionJob;
        }

        public void SetState(BuildingState newState)
        {
            State = newState;

            // TODO: Update visual representation per state.
        }

        public void MarkCompleted()
        {
            SetState(BuildingState.Completed);

            // TODO: Replace blueprint prefab with final building instance.
        }
    }

    public enum BuildingState
    {
        Blueprint,
        UnderConstruction,
        Completed
    }
}