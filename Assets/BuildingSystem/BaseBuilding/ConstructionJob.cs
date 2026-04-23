using UnityEngine;
using Zombera.Core;
using Zombera.Data;

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
            if (storage == null || HasAllMaterials || TargetBlueprint == null)
            {
                return;
            }

            if (TargetBlueprint.BuildingData == null || TargetBlueprint.BuildingData.requiredMaterials == null)
            {
                // No material requirements defined — deliver immediately.
                DeliverMaterials(requiredMaterials - DeliveredMaterials);
                return;
            }

            float delivered = 0f;
            foreach (MaterialRequirementData req in TargetBlueprint.BuildingData.requiredMaterials)
            {
                if (req.item == null || req.amount <= 0) continue;
                int available = storage.GetAmount(req.item);
                int needed = req.amount - Mathf.RoundToInt(DeliveredMaterials); // approximate per-item tracking
                int take = Mathf.Clamp(needed, 0, available);
                if (take <= 0) continue;
                if (storage.RemoveMaterial(req.item, take))
                {
                    delivered += take;
                }
            }

            if (delivered > 0f)
            {
                DeliverMaterials(delivered);
            }
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
        }
    }
}