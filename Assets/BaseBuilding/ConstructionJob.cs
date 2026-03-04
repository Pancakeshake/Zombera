using UnityEngine;
using Zombera.Core;

namespace Zombera.BaseBuilding
{
    /// <summary>
    /// Tracks material delivery and work progress for a blueprint.
    /// </summary>
    public sealed class ConstructionJob : MonoBehaviour
    {
        [SerializeField] private float requiredMaterials = 100f;
        [SerializeField] private float requiredWork = 100f;

        public Blueprint TargetBlueprint { get; private set; }
        public float DeliveredMaterials { get; private set; }
        public float WorkProgress { get; private set; }
        public bool IsCompleted { get; private set; }

        public bool HasAllMaterials => DeliveredMaterials >= requiredMaterials;

        public void Initialize(Blueprint blueprint)
        {
            TargetBlueprint = blueprint;
            DeliveredMaterials = 0f;
            WorkProgress = 0f;
            IsCompleted = false;

            if (TargetBlueprint != null)
            {
                TargetBlueprint.SetState(BuildingState.Blueprint);
            }
        }

        public void DeliverMaterials(float amount)
        {
            if (IsCompleted || amount <= 0f)
            {
                return;
            }

            DeliveredMaterials = Mathf.Min(requiredMaterials, DeliveredMaterials + amount);

            if (TargetBlueprint != null && DeliveredMaterials > 0f)
            {
                TargetBlueprint.SetState(BuildingState.UnderConstruction);
            }
        }

        public void AddWork(float amount)
        {
            if (IsCompleted || amount <= 0f || !HasAllMaterials)
            {
                return;
            }

            WorkProgress = Mathf.Min(requiredWork, WorkProgress + amount);

            if (WorkProgress >= requiredWork)
            {
                CompleteConstruction();
            }
        }

        public void TryAutoDeliverMaterials(BaseStorage storage)
        {
            if (storage == null || HasAllMaterials)
            {
                return;
            }

            // TODO: Consume actual material requirements from storage.
            DeliverMaterials(1f);
        }

        public void CompleteConstruction()
        {
            if (IsCompleted)
            {
                return;
            }

            IsCompleted = true;

            if (TargetBlueprint != null)
            {
                TargetBlueprint.MarkCompleted();

                string buildingId = TargetBlueprint.BuildingData != null
                    ? TargetBlueprint.BuildingData.buildingId
                    : TargetBlueprint.name;

                EventSystem.PublishGlobal(new BuildingCompletedEvent
                {
                    BuildingId = buildingId,
                    Position = TargetBlueprint.transform.position,
                    BuildingObject = TargetBlueprint.gameObject
                });
            }

            // TODO: Register final structure with base systems and save state.
        }
    }
}