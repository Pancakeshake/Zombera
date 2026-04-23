using System;
using UnityEngine;
using Zombera.Systems;

namespace Zombera.Characters
{
    /// <summary>
    /// Root composition for a universal unit entity.
    /// Aggregates shared systems used by player, squad, survivor, and zombie archetypes.
    /// </summary>
    [RequireComponent(typeof(UnitController))]
    [RequireComponent(typeof(UnitHealth))]
    [RequireComponent(typeof(UnitCombat))]
    [RequireComponent(typeof(UnitStats))]
    [DisallowMultipleComponent]
    public sealed class Unit : MonoBehaviour, IUnit
    {
        [SerializeField] private string unitId;
        [SerializeField] private UnitRole role = UnitRole.Player;
        [SerializeField] private UnitController controller;
        [SerializeField] private UnitHealth health;
        [SerializeField] private UnitCombat combat;
        [SerializeField] private UnitInventory inventory;
        [SerializeField] private UnitStats stats;
        [SerializeField] private MonoBehaviour optionalAI;

        public string UnitId => unitId;
        public UnitRole Role => role;
        public UnitFaction Faction => UnitFactionUtility.FromRole(role);
        public UnitController Controller => controller;
        public UnitHealth Health => health;
        public UnitCombat Combat => combat;
        public UnitInventory Inventory
        {
            get
            {
                if (inventory == null)
                {
                    inventory = GetComponent<UnitInventory>();
                }

                return inventory;
            }
        }
        public UnitStats Stats => stats;
        public MonoBehaviour OptionalAI => optionalAI;
        public bool IsAlive => health == null || !health.IsDead;

        private void Reset()
        {
            AutoWire();
            EnsureUnitId();
            ApplyRoleToController();
        }

        private void Awake()
        {
            AutoWire();
            EnsureUnitId();
            ApplyRoleToController();
        }

        private void OnEnable()
        {
            UnitManager.Instance?.RegisterUnit(this);
        }

        private void OnDisable()
        {
            UnitManager.Instance?.UnregisterUnit(this);
        }

        public void SetRole(UnitRole unitRole)
        {
            role = unitRole;
            ApplyRoleToController();
        }

        public void SetOptionalAI(MonoBehaviour aiComponent)
        {
            optionalAI = aiComponent;
        }

        private void AutoWire()
        {
            if (controller == null)
            {
                controller = GetComponent<UnitController>();
            }

            if (health == null)
            {
                health = GetComponent<UnitHealth>();
            }

            if (combat == null)
            {
                combat = GetComponent<UnitCombat>();
            }

            if (inventory == null)
            {
                inventory = GetComponent<UnitInventory>();
            }

            if (stats == null)
            {
                stats = GetComponent<UnitStats>();
            }
        }

        private void EnsureUnitId()
        {
            if (!string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            unitId = Guid.NewGuid().ToString("N");
        }

        private void ApplyRoleToController()
        {
            if (controller != null)
            {
                controller.SetRole(role);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoWire();
            EnsureUnitId();
            ApplyRoleToController();
        }
#endif
    }
}