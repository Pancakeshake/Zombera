#region

using System;
using UnityEngine;
using Zombera.Factions;
using Zombera.Systems;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Root composition for a universal unit entity.
    ///     Aggregates shared systems used by player, squad, survivor, and zombie archetypes.
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
        private FactionMember _factionMember;

        public UnitFaction Faction
        {
            get
            {
                var factionId = FactionId;
                return UnitFactionUtility.CoarseFactionFromFactionId(factionId);
            }
        }

        public string FactionId
        {
            get
            {
                var member = FactionMembership;
                if (member != null && !string.IsNullOrWhiteSpace(member.FactionId))
                    return member.FactionId;

                return UnitFactionUtility.DefaultFactionIdFromRole(role);
            }
        }

        public FactionMember FactionMembership
        {
            get
            {
                if (_factionMember == null) _factionMember = GetComponent<FactionMember>();
                return _factionMember;
            }
        }

        public bool IsAlive => health == null || !health.IsDead;

        private void Awake()
        {
            AutoWire();
            EnsureUnitId();
            ApplyRoleToController();
            EnsureFactionMember();
        }

        private void Reset()
        {
            AutoWire();
            EnsureUnitId();
            ApplyRoleToController();
        }

        private void OnEnable()
        {
            if (UnitManager.HasInstance)
                UnitManager.Instance.RegisterUnit(this);
        }

        private void OnDisable()
        {
            if (UnitManager.HasInstance)
                UnitManager.Instance.UnregisterUnit(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoWire();
            EnsureUnitId();
            ApplyRoleToController();
        }
#endif

        public string UnitId => unitId;
        public UnitRole Role => role;
        public UnitController Controller => controller;
        public UnitHealth Health => health;
        public UnitCombat Combat => combat;

        public UnitInventory Inventory
        {
            get
            {
                if (inventory == null) inventory = GetComponent<UnitInventory>();

                return inventory;
            }
        }

        public UnitStats Stats => stats;
        public MonoBehaviour OptionalAI => optionalAI;

        public void SetRole(UnitRole unitRole)
        {
            role = unitRole;
            ApplyRoleToController();
        }

        public void SetOptionalAI(MonoBehaviour aiComponent)
        {
            optionalAI = aiComponent;
        }

        public void AssignUnitIdForRestore(string restoredUnitId)
        {
            if (string.IsNullOrWhiteSpace(restoredUnitId)) return;
            if (string.Equals(unitId, restoredUnitId, StringComparison.Ordinal)) return;

            unitId = restoredUnitId;
        }

        /// <summary>
        ///     Assigns a new runtime id. Used when prefab clones share a baked id.
        /// </summary>
        public void RegenerateUnitId()
        {
            unitId = Guid.NewGuid().ToString("N");
        }

        private void AutoWire()
        {
            if (controller == null) controller = GetComponent<UnitController>();

            if (health == null) health = GetComponent<UnitHealth>();

            if (combat == null) combat = GetComponent<UnitCombat>();

            if (inventory == null) inventory = GetComponent<UnitInventory>();

            if (stats == null) stats = GetComponent<UnitStats>();
        }

        private void EnsureUnitId()
        {
            if (!string.IsNullOrWhiteSpace(unitId)) return;

            unitId = Guid.NewGuid().ToString("N");
        }

        private void ApplyRoleToController()
        {
            if (controller != null) controller.SetRole(role);
        }

        private void EnsureFactionMember()
        {
            var member = FactionMembership;
            if (member == null) member = gameObject.AddComponent<FactionMember>();
            member.EnsureSeededFromRole(role);
        }
    }
}