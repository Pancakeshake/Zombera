#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;
using Zombera.Systems.Digging;
using Zombera.UI;

#endregion

namespace Zombera.Systems
{
    public sealed partial class PlayerInputController
    {

        private void CancelDigIfMoving()
        {
            if (diggingSystem != null && diggingSystem.IsDigging && unitController != null && unitController.IsMoving)
                diggingSystem.CancelDig();
        }


        private void HandleInteractInput()
        {
            if (!WasActionPressedThisFrame(interactAction, interactKey)) return;

            // Door takes priority when nearby; fall through to container if no door found.
            if (doorInteractor != null && doorInteractor.Interact()) return;
            if (TryInteractNearestItemPickup()) return;
            containerInteractor?.Interact();
        }


        private bool TryInteractNearestItemPickup()
        {
            return EnsureItemPickupInteractorBound() && _typedItemPickupInteractor.Interact();
        }


        private bool EnsureItemPickupInteractorBound()
        {
            if (_typedItemPickupInteractor != null) return true;

            // Honor an inspector-assigned interactor first, then fall back to a local lookup.
            _typedItemPickupInteractor = itemPickupInteractor as ItemPickupInteractor;
            if (_typedItemPickupInteractor == null)
                _typedItemPickupInteractor = GetComponent<ItemPickupInteractor>();

            return _typedItemPickupInteractor != null;
        }


        private bool HandleDigInput(bool hasNearbyEnemies = false, bool pointerOverUi = false)
        {
            if (!rightClickDigEnabled || diggingSystem == null) return false;

            if (!TryReadSecondaryMouseButtonState(out var rightPressed, out var rightReleased, out _)) return false;

            var consumedRightClick = false;

            if (pointerOverUi)
            {
                if (rightReleased) diggingSystem.CancelDig();

                return false;
            }

            if (rightPressed)
            {
                // Combat takes priority over digging when enemies are nearby.
                var hasCombatTarget = hasNearbyEnemies || (requireNoCombatTarget && TryGetUnitHealthUnderCursor(out _));

                if (!hasCombatTarget)
                {
                    consumedRightClick = true;
                    ClearBowRangedCombatState(true);
                    if (TryGetGroundPoint(out var digPoint)) _ = diggingSystem.StartDig(digPoint);
                }
            }

            if (rightReleased) diggingSystem.CancelDig();

            return consumedRightClick;
        }
    }
}
