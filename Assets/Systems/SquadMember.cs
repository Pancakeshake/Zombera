using System;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Systems
{
    /// <summary>
    /// Represents a controllable squad member and references character sub-systems.
    /// </summary>
    public sealed class SquadMember : MonoBehaviour
    {
        [SerializeField] private string memberId;
        [SerializeField] private Unit unit;
        [SerializeField] private UnitController unitController;
        [SerializeField] private UnitStats unitStats;
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private FollowController followController;

        public string MemberId => memberId;
        public Unit Unit => unit;
        public UnitController UnitController => unitController;
        public UnitStats UnitStats => unitStats;
        public UnitHealth UnitHealth => unitHealth;
        public UnitCombat UnitCombat => unitCombat;
        public FollowController FollowController => followController;

        private void Awake()
        {
            RefreshReferences();
            unit?.SetRole(UnitRole.SquadMember);
        }

        private void OnEnable()
        {
            RefreshReferences();
            SquadManager.Instance?.RegisterMember(this);
        }

        private void OnDisable()
        {
            SquadManager.Instance?.UnregisterMember(this);
        }

        public bool IsAvailableForOrders()
        {
            return unitHealth != null && !unitHealth.IsDead;
        }

        public void RefreshReferences()
        {
            AutoWire();
            EnsureMemberId();
        }

        private void AutoWire()
        {
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }

            if (unitStats == null)
            {
                unitStats = GetComponent<UnitStats>();
            }

            if (unitHealth == null)
            {
                unitHealth = GetComponent<UnitHealth>();
            }

            if (unitCombat == null)
            {
                unitCombat = GetComponent<UnitCombat>();
            }

            if (followController == null)
            {
                followController = GetComponent<FollowController>();
            }
        }

        private void EnsureMemberId()
        {
            if (!string.IsNullOrWhiteSpace(memberId))
            {
                return;
            }

            if (unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
            {
                memberId = unit.UnitId;
                return;
            }

            memberId = Guid.NewGuid().ToString("N");
        }

        // TODO: Add per-member stance, role preference, and autonomous behavior toggles.
    }
}