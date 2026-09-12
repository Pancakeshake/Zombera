#region

using System.Linq;
using UnityEngine;
using Zombera.Core;
using Zombera.Data;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable MemberCanBePrivate.Global

namespace Zombera.BaseBuilding
{
    /// <summary>
    ///     Tracks material delivery and work progress for a blueprint.
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

            if (TargetBlueprint) TargetBlueprint.SetState(BuildingState.Blueprint);
        }

        public void DeliverMaterials(float amount)
        {
            if (IsCompleted || amount <= 0f) return;

            DeliveredMaterials = Mathf.Min(requiredMaterials, DeliveredMaterials + amount);

            if (TargetBlueprint && DeliveredMaterials > 0f)
                TargetBlueprint.SetState(BuildingState.UnderConstruction);
        }

        public void AddWork(float amount)
        {
            if (IsCompleted || amount <= 0f || !HasAllMaterials) return;

            WorkProgress = Mathf.Min(requiredWork, WorkProgress + amount);

            if (WorkProgress >= requiredWork) CompleteConstruction();
        }

        public void TryAutoDeliverMaterials(BaseStorage storage)
        {
            if (storage == null || HasAllMaterials || TargetBlueprint == null) return;

            if (TargetBlueprint.BuildingData == null || TargetBlueprint.BuildingData.requiredMaterials == null)
            {
                // No material requirements defined — deliver immediately.
                DeliverMaterials(requiredMaterials - DeliveredMaterials);
                return;
            }

            var requirements = TargetBlueprint.BuildingData.requiredMaterials;
            var delivered = requirements
                .Where(static req => req.item != null && req.amount > 0)
                .Sum(req => TryTakeMaterialsForRequirement(storage, req));

            if (delivered > 0f) DeliverMaterials(delivered);
        }

        public void CompleteConstruction()
        {
            if (IsCompleted) return;

            IsCompleted = true;
            if (!TargetBlueprint) return;

            TargetBlueprint.MarkCompleted();

            var buildingId = TargetBlueprint.BuildingData != null
                ? TargetBlueprint.BuildingData.buildingId
                : TargetBlueprint.name;

            CoreEventBus.PublishGlobal(new BuildingCompletedEvent
            {
                BuildingId = buildingId,
                Position = TargetBlueprint.transform.position,
                BuildingObject = TargetBlueprint.gameObject
            });
        }

        private int TryTakeMaterialsForRequirement(BaseStorage storage, MaterialRequirementData requirement)
        {
            var available = storage.GetAmount(requirement.item);
            var needed = requirement.amount - Mathf.RoundToInt(DeliveredMaterials); // approximate per-item tracking
            var take = Mathf.Clamp(needed, 0, available);
            if (take <= 0) return 0;
            return storage.RemoveMaterial(requirement.item, take) ? take : 0;
        }
    }
}