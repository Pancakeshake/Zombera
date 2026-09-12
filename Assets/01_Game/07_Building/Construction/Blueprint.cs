#region

using UnityEngine;
using UnityEngine.Serialization;
using Zombera.Data;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison

namespace Zombera.BaseBuilding
{
    /// <summary>
    ///     Represents a placeable building blueprint and tracks construction state transitions.
    /// </summary>
    public sealed class Blueprint : MonoBehaviour
    {
        [SerializeField] private ConstructionJob constructionJob;

        [Header("Ghost Visuals")] [SerializeField]
        private Renderer ghostRenderer;

        [SerializeField] private Color colorBlueprint = new(0.3f, 0.6f, 1f, 0.5f);
        [SerializeField] private Color colorUnderConstruction = new(1f, 0.8f, 0.2f, 0.6f);
        [SerializeField] private Color colorCompleted = new(0.2f, 1f, 0.3f, 0.8f);

        [FormerlySerializedAs("State")] [SerializeField]
        private BuildingState state = BuildingState.Blueprint;

        public BuildingData BuildingData { get; private set; }

        // ReSharper disable once UnusedMember.Global
        public BuildingState State
        {
            get => state;
            private set => state = value;
        }

        // ReSharper disable once UnusedMember.Global
        public void Initialize(BuildingData buildingData)
        {
            BuildingData = buildingData;
            State = BuildingState.Blueprint;

            if (constructionJob == null) constructionJob = gameObject.AddComponent<ConstructionJob>();
            constructionJob.Initialize(this);

            ApplyGhostTint(colorBlueprint);
        }

        public void SetState(BuildingState newState)
        {
            State = newState;

            switch (newState)
            {
                case BuildingState.Blueprint:
                    ApplyGhostTint(colorBlueprint);
                    break;
                case BuildingState.UnderConstruction:
                    ApplyGhostTint(colorUnderConstruction);
                    break;
                case BuildingState.Completed:
                    ApplyGhostTint(colorCompleted);
                    break;
                default:
                    ApplyGhostTint(colorBlueprint);
                    break;
            }
        }

        public void MarkCompleted()
        {
            SetState(BuildingState.Completed);

            // Swap ghost for final structure prefab if one is assigned.
            if (BuildingData == null || BuildingData.completedPrefab == null) return;

            Instantiate(BuildingData.completedPrefab, transform.position, transform.rotation);
            Destroy(gameObject);
        }

        private void ApplyGhostTint(Color color)
        {
            if (ghostRenderer == null) ghostRenderer = GetComponentInChildren<Renderer>();

            if (ghostRenderer == null) return;

            foreach (var mat in ghostRenderer.materials) mat.color = color;
        }
    }

    public enum BuildingState
    {
        Blueprint,
        UnderConstruction,
        Completed
    }
}