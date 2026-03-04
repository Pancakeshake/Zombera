using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;

namespace Zombera.Systems
{
    /// <summary>
    /// Tick-based autonomous behavior for squad members.
    /// </summary>
    public sealed class SquadAI : MonoBehaviour
    {
        [SerializeField] private float aiTickInterval = 0.2f;
        [SerializeField] private Unit unit;
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private UnitController unitController;

        private readonly List<UnitHealth> visibleTargets = new List<UnitHealth>();
        private float tickTimer;

        private void Awake()
        {
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unitCombat == null)
            {
                unitCombat = GetComponent<UnitCombat>();
            }

            if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }

            if (unit != null)
            {
                unit.SetRole(UnitRole.SquadMember);
                unit.SetOptionalAI(this);
            }
        }

        private void Update()
        {
            tickTimer += Time.deltaTime;

            if (tickTimer < aiTickInterval)
            {
                return;
            }

            tickTimer = 0f;
            TickAI();
        }

        private void TickAI()
        {
            if (unit == null || !unit.IsAlive || unitCombat == null)
            {
                return;
            }

            visibleTargets.Clear();

            if (UnitManager.Instance != null)
            {
                List<Unit> nearbyEnemies = UnitManager.Instance.FindNearbyEnemies(unit, 20f);

                for (int i = 0; i < nearbyEnemies.Count; i++)
                {
                    Unit enemy = nearbyEnemies[i];

                    if (enemy != null && enemy.Health != null && !enemy.Health.IsDead)
                    {
                        visibleTargets.Add(enemy.Health);
                    }
                }
            }

            if (visibleTargets.Count > 0)
            {
                unitCombat.ExecuteAttack(visibleTargets);
            }
            else
            {
                unitController?.Stop();
            }

            // TODO: Add threat-aware cover, retreat, and assist behaviors.
        }
    }
}