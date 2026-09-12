#region

using System;
using System.Collections.Generic;
using System.Reflection;
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

        public void SetWorldCamera(Camera worldCam)
        {
            worldCamera = worldCam;
            if (digProgressWorldUI != null) digProgressWorldUI.SetWorldCamera(worldCam);
            if (bowShotProgressWorldUI != null) bowShotProgressWorldUI.SetWorldCamera(worldCam);

            if (easyBuildCursorPlacementBinder != null) easyBuildCursorPlacementBinder.SetGameplayCamera(worldCam);
        }


        public void InjectSystems(CombatManager manager, CombatSystem system, SquadManager squad)
        {
            if (manager != null) combatManager = manager;
            if (system != null) combatSystem = system;
            if (squad != null) squadManager = squad;

            _ = ResolveEncounterManager();
        }


        private void ResolveCoreReferences()
        {
            if (playerUnit == null) playerUnit = GetComponent<Unit>();
            if (unitController == null) unitController = GetComponent<UnitController>();
            if (unitCombat == null) unitCombat = GetComponent<UnitCombat>();
            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();
            if (playerAnimationController == null)
                playerAnimationController = GetComponent<PlayerAnimationController>();
            if (playerAnimationController == null)
                playerAnimationController = gameObject.AddComponent<PlayerAnimationController>();
            if (worldCamera == null) worldCamera = Zombera.Core.CameraRegistry.Main;

            if (selectionBoxUi == null)
                selectionBoxUi = FindFirstObjectByType<SelectionBoxUI>();

            if (controlModeCoordinator == null)
                controlModeCoordinator = PlayerControlModeCoordinator.Instance != null
                    ? PlayerControlModeCoordinator.Instance
                    : FindFirstObjectByType<PlayerControlModeCoordinator>();

            _ = ResolveEncounterManager();
        }


        private void ResolveInteractionReferences()
        {
            if (containerInteractor == null) containerInteractor = GetComponent<ContainerInteractor>();

            if (itemPickupInteractor == null) EnsureItemPickupInteractorBound();

            if (easyBuildCursorPlacementBinder == null) easyBuildCursorPlacementBinder = GetComponent<EasyBuildCursorPlacementBinder>();
            if (easyBuildCursorPlacementBinder == null && autoAttachEasyBuildCursorPlacementBinder)
                easyBuildCursorPlacementBinder = gameObject.AddComponent<EasyBuildCursorPlacementBinder>();

            // EasyBuild is the only supported build style; ensure cursor-driven placement binder exists.
            if (easyBuildCursorPlacementBinder == null)
                easyBuildCursorPlacementBinder = gameObject.AddComponent<EasyBuildCursorPlacementBinder>();

            if (easyBuildRadialMenuInputBridge == null)
                easyBuildRadialMenuInputBridge = GetComponent<EasyBuildRadialMenuInputBridge>();
            if (easyBuildRadialMenuInputBridge == null && autoAttachEasyBuildRadialMenuInputBridge)
                easyBuildRadialMenuInputBridge = gameObject.AddComponent<EasyBuildRadialMenuInputBridge>();

            // Keep B-key build radial menu flow available even in scenes/prefabs missing explicit bridge wiring.
            if (easyBuildRadialMenuInputBridge == null)
                easyBuildRadialMenuInputBridge = gameObject.AddComponent<EasyBuildRadialMenuInputBridge>();

            if (easyBuildRadialMenuInputBridge != null && !easyBuildRadialMenuInputBridge.enabled)
                easyBuildRadialMenuInputBridge.enabled = true;

            if (legacyBuildPlacementController == null)
                legacyBuildPlacementController = GetComponent<BuildPlacementController>();
            if (legacyBuildPlacementController == null && autoAttachBuildPlacementControllerForHudBuilds)
                legacyBuildPlacementController = gameObject.AddComponent<BuildPlacementController>();
        }


        private void EnsureRuntimeSupportComponents()
        {
            EnsureCursorManager();
            EnsureDiggingSupportComponents();

            if (combatFootwork == null) combatFootwork = GetComponent<PlayerCombatFootwork>();
            if (combatFootwork == null) combatFootwork = gameObject.AddComponent<PlayerCombatFootwork>();
        }


        private void EnsureCursorManager()
        {
            if (cursorManager == null) cursorManager = GetComponent<CursorManager>();

            if (cursorManager == null) cursorManager = CursorManager.RuntimeInstance;

            if (cursorManager == null)
                cursorManager = FindFirstObjectByType<CursorManager>(FindObjectsInactive.Include);

            if (cursorManager == null) cursorManager = gameObject.AddComponent<CursorManager>();

            EnsureCursorSupportComponents();
        }


        private void EnsureCursorSupportComponents()
        {
            if (GetComponent<CursorBuildModeBridge>() == null)
                gameObject.AddComponent<CursorBuildModeBridge>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GetComponent<CursorDebugOverlay>() == null)
                gameObject.AddComponent<CursorDebugOverlay>();
#endif
        }


        private void EnsureDiggingSupportComponents()
        {
            if (diggingSystem == null) diggingSystem = GetComponent<DiggingSystem>();
            if (diggingSystem == null) diggingSystem = gameObject.AddComponent<DiggingSystem>();

            if (digProgressWorldUI == null) digProgressWorldUI = GetComponent<DigProgressWorldUI>();
            if (digProgressWorldUI == null) digProgressWorldUI = gameObject.AddComponent<DigProgressWorldUI>();

            if (bowShotProgressWorldUI == null) bowShotProgressWorldUI = GetComponent<BowShotProgressWorldUI>();
            if (bowShotProgressWorldUI == null)
                bowShotProgressWorldUI = gameObject.AddComponent<BowShotProgressWorldUI>();
        }


        private void ConfigureWorldUiBindings()
        {
            if (digProgressWorldUI != null)
            {
                digProgressWorldUI.SetTarget(transform);
                digProgressWorldUI.SetDiggingSystem(diggingSystem);
                digProgressWorldUI.SetWorldCamera(worldCamera);
            }

            if (bowShotProgressWorldUI != null)
            {
                bowShotProgressWorldUI.SetTarget(transform);
                bowShotProgressWorldUI.SetBowController(this);
                bowShotProgressWorldUI.SetWorldCamera(worldCamera);
            }
}
    }
}
