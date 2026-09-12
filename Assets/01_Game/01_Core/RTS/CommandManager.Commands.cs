using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.Characters;
using Zombera.Factions;

namespace Zombera.Systems
{
    public sealed partial class CommandManager
    {
        private bool IssueMoveCommand(Vector3 groundPoint)
        {
            if (requireSelectionForOrders)
            {
                if (squadManager.TryIssueOrderToSelection(SquadCommandType.Move, groundPoint)) return true;

                squadManager.IssueOrder(SquadCommandType.Move, groundPoint);
                return true;
            }

            squadManager.IssueOrder(SquadCommandType.Move, groundPoint);
            return true;
        }

        private bool IssueAttackCommand(UnitHealth targetHealth)
        {
            if (targetHealth == null || targetHealth.IsDead) return false;

            var focusPoint = targetHealth.transform.position;

            if (requireSelectionForOrders)
            {
                if (squadManager.TryIssueAttackOrderToSelection(targetHealth, focusPoint)) return true;

                squadManager.IssueAttackOrder(targetHealth, focusPoint);
                return true;
            }

            squadManager.IssueAttackOrder(targetHealth, focusPoint);
            return true;
        }

        private bool TryGetGroundPoint(Vector2 pointerScreenPosition, out Vector3 groundPoint)
        {
            groundPoint = default;
            if (!TryResolveWorldCamera(out var commandCamera)) return false;

            return RtsPointerQueryUtility.TryGetGroundPoint(
                commandCamera,
                pointerScreenPosition,
                groundMask,
                rayDistance,
                logMovementGroundingDiagnostics,
                out groundPoint);
        }

        private bool TryGetTargetUnderPointer(Vector2 pointerScreenPosition, out UnitHealth targetHealth)
        {
            targetHealth = null;
            if (!TryResolveWorldCamera(out var commandCamera)) return false;

            if (!RtsPointerQueryUtility.TryGetNearestUnitHealthUnderPointer(
                    commandCamera,
                    pointerScreenPosition,
                    targetableMask,
                    queryTriggerInteraction,
                    rayDistance,
                    _targetHits,
                    out var nearestTarget))
                return false;

            var sourceUnit = ResolveSelectionUnit();

            if (!IsValidTarget(nearestTarget, sourceUnit)) return false;

            targetHealth = nearestTarget;
            return true;
        }

        private bool IsValidTarget(UnitHealth candidateHealth, Unit sourceUnit)
        {
            if (candidateHealth == null || candidateHealth.IsDead) return false;

            var targetUnit = candidateHealth.GetComponentInParent<Unit>();
            if (targetUnit == null) return false;

            if (IsSelectedSquadUnit(targetUnit)) return false;

            if (!requireHostileTargetsForAttack) return true;

            if (sourceUnit == null) return true;
            return FactionManager.AreUnitsHostile(sourceUnit, targetUnit);
        }

        private bool IsSelectedSquadUnit(Unit targetUnit)
        {
            if (targetUnit == null || squadManager == null) return false;

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var selectedMember = selectedMembers[i];
                if (selectedMember?.Unit == targetUnit) return true;
            }

            return false;
        }

        private Unit ResolveSelectionUnit()
        {
            if (squadManager == null) return null;

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var member = selectedMembers[i];
                if (member?.Unit != null) return member.Unit;
            }

            return null;
        }

        private bool TryResolveWorldCamera(out Camera commandCamera)
        {
            commandCamera = worldCamera != null ? worldCamera : Zombera.Core.CameraRegistry.Main;
            return commandCamera != null;
        }

        private static bool IsPointerOverUi(Vector2 pointerScreenPosition)
        {
            var uiEventSystem = EventSystem.current;
            if (uiEventSystem == null || !uiEventSystem.isActiveAndEnabled) return false;

            if (Mouse.current != null && uiEventSystem.IsPointerOverGameObject()) return true;

            if (s_uiPointerEventData == null || s_uiPointerEventDataOwner != uiEventSystem)
            {
                s_uiPointerEventDataOwner = uiEventSystem;
                s_uiPointerEventData = new PointerEventData(uiEventSystem);
            }

            s_uiPointerEventData.Reset();
            s_uiPointerEventData.position = pointerScreenPosition;

            UiRaycastResults.Clear();
            uiEventSystem.RaycastAll(s_uiPointerEventData, UiRaycastResults);
            return UiRaycastResults.Count > 0;
        }
    }
}
