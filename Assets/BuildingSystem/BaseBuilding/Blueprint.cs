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

        [Header("Ghost Visuals")]
        [SerializeField] private Renderer ghostRenderer;
        [SerializeField] private Color colorBlueprint = new Color(0.3f, 0.6f, 1f, 0.5f);
        [SerializeField] private Color colorUnderConstruction = new Color(1f, 0.8f, 0.2f, 0.6f);
        [SerializeField] private Color colorCompleted = new Color(0.2f, 1f, 0.3f, 0.8f);

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

            ApplyGhostTint(colorBlueprint);
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
            }
        }

        public void MarkCompleted()
        {
            SetState(BuildingState.Completed);

            // Swap ghost for final structure prefab if one is assigned.
            if (BuildingData != null && BuildingData.completedPrefab != null)
            {
                Instantiate(BuildingData.completedPrefab, transform.position, transform.rotation);
                Destroy(gameObject);
            }
        }

        private void ApplyGhostTint(Color color)
        {
            if (ghostRenderer == null)
            {
                ghostRenderer = GetComponentInChildren<Renderer>();
            }

            if (ghostRenderer == null) return;

            foreach (Material mat in ghostRenderer.materials)
            {
                mat.color = color;
            }
        }
    }

    public enum BuildingState
    {
        Blueprint,
        UnderConstruction,
        Completed
    }
}