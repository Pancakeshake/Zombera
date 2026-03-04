using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;

namespace Zombera.Systems
{
    /// <summary>
    /// Routes player input into unit movement/combat and squad-level command dispatch.
    /// </summary>
    public sealed class PlayerInputController : MonoBehaviour
    {
        [Header("Player Unit")]
        [SerializeField] private Unit playerUnit;
        [SerializeField] private UnitController unitController;
        [SerializeField] private UnitCombat unitCombat;

        [Header("Gameplay Systems")]
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private CombatSystem combatSystem;
        [SerializeField] private SquadManager squadManager;

        [Header("Input Settings")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float attackScanRadius = 20f;
        [SerializeField] private bool issueSquadMoveOnRightClick = true;

        private readonly List<UnitHealth> visibleTargets = new List<UnitHealth>();

        private void Awake()
        {
            if (playerUnit == null)
            {
                playerUnit = GetComponent<Unit>();
            }

            if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }

            if (unitCombat == null)
            {
                unitCombat = GetComponent<UnitCombat>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (playerUnit == null || unitController == null || unitCombat == null || !playerUnit.IsAlive)
            {
                return;
            }

            HandleMovementInput();
            HandleCombatInput();
            HandleSquadCommandInput();
        }

        private void HandleMovementInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector2 movementInput = new Vector2(horizontal, vertical);
            unitController.SetMoveInput(Vector2.ClampMagnitude(movementInput, 1f));

            if (Input.GetMouseButtonDown(1) && TryGetGroundPoint(out Vector3 groundPoint))
            {
                unitController.MoveTo(groundPoint);

                if (issueSquadMoveOnRightClick && squadManager != null)
                {
                    squadManager.IssueOrder(SquadCommandType.Move, groundPoint);
                }
            }
        }

        private void HandleCombatInput()
        {
            if (Input.GetMouseButtonDown(0) && TryGetUnitHealthUnderCursor(out UnitHealth markedTarget))
            {
                unitCombat.SetMarkedTarget(markedTarget);
            }

            if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0))
            {
                visibleTargets.Clear();

                if (UnitManager.Instance != null)
                {
                    List<Unit> nearbyEnemies = UnitManager.Instance.FindNearbyEnemies(playerUnit, attackScanRadius);

                    for (int i = 0; i < nearbyEnemies.Count; i++)
                    {
                        Unit enemy = nearbyEnemies[i];

                        if (enemy != null && enemy.Health != null && !enemy.Health.IsDead)
                        {
                            visibleTargets.Add(enemy.Health);
                        }
                    }
                }

                if (combatSystem != null)
                {
                    combatSystem.TryExecuteAttack(unitCombat, visibleTargets);
                }
                else if (combatManager != null)
                {
                    combatManager.RequestAttack(unitCombat, visibleTargets);
                }
                else
                {
                    unitCombat.ExecuteAttack(visibleTargets);
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (combatSystem != null)
                {
                    combatSystem.Reload(unitCombat);
                }
                else if (combatManager != null)
                {
                    combatManager.RequestReload(unitCombat);
                }
                else
                {
                    unitCombat.Reload();
                }
            }
        }

        private void HandleSquadCommandInput()
        {
            if (squadManager == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) && TryGetGroundPoint(out Vector3 movePoint))
            {
                squadManager.IssueOrder(SquadCommandType.Move, movePoint);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) && TryGetGroundPoint(out Vector3 attackPoint))
            {
                squadManager.IssueOrder(SquadCommandType.Attack, attackPoint);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                squadManager.IssueOrder(SquadCommandType.HoldPosition);
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                squadManager.IssueOrder(SquadCommandType.Follow);
            }

            if (Input.GetKeyDown(KeyCode.Alpha5) && TryGetGroundPoint(out Vector3 defendPoint))
            {
                squadManager.IssueOrder(SquadCommandType.Defend, defendPoint);
            }
        }

        private bool TryGetGroundPoint(out Vector3 worldPoint)
        {
            worldPoint = default;

            if (worldCamera == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundMask, QueryTriggerInteraction.Ignore))
            {
                worldPoint = hit.point;
                return true;
            }

            Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

            if (fallbackPlane.Raycast(ray, out float distance))
            {
                worldPoint = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        private bool TryGetUnitHealthUnderCursor(out UnitHealth unitHealth)
        {
            unitHealth = null;

            if (worldCamera == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            unitHealth = hit.collider.GetComponentInParent<UnitHealth>();
            return unitHealth != null;
        }
    }
}