using System;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Systems
{
    public enum MemberStance
    {
        Neutral,    // Default — follow orders.
        Aggressive, // Engages any nearby enemy without waiting for orders.
        Defensive,  // Holds position and only fires when fired upon.
        Passive,    // Does not engage; avoids combat.
    }

    public enum MemberRolePreference
    {
        Any,
        Assault,
        Support,
        Scout,
        Medic,
    }

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

        [Header("Behaviour")]
        [SerializeField] private MemberStance stance = MemberStance.Neutral;
        [SerializeField] private MemberRolePreference rolePreference = MemberRolePreference.Any;
        [SerializeField] private bool autonomousPatrol;
        [SerializeField] private bool autonomousScavenge;

        public string MemberId => memberId;
        public Unit Unit => unit;
        public UnitController UnitController => unitController;
        public UnitStats UnitStats => unitStats;
        public UnitHealth UnitHealth => unitHealth;
        public UnitCombat UnitCombat => unitCombat;
        public FollowController FollowController => followController;

        public MemberStance Stance
        {
            get => stance;
            set => stance = value;
        }

        public MemberRolePreference RolePreference
        {
            get => rolePreference;
            set => rolePreference = value;
        }

        /// <summary>When true the member will patrol nearby waypoints while idle rather than standing still.</summary>
        public bool AutonomousPatrol
        {
            get => autonomousPatrol;
            set => autonomousPatrol = value;
        }

        /// <summary>When true the member will search nearby containers for loot while idle.</summary>
        public bool AutonomousScavenge
        {
            get => autonomousScavenge;
            set => autonomousScavenge = value;
        }

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

        /// <summary>Returns true if this member will auto-engage the given target based on current stance.</summary>
        public bool WillAutoEngage()
        {
            return stance == MemberStance.Aggressive;
        }

        /// <summary>Returns true if the member will fire back when attacked.</summary>
        public bool WillReturnFire()
        {
            return stance != MemberStance.Passive;
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
    }
}