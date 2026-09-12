#region

using System;
using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Systems
{
    public enum MemberStance
    {
        Neutral, // Default — follow orders.
        Aggressive, // Engages any nearby enemy without waiting for orders.
        Defensive, // Holds position and only fires when fired upon.
        Passive // Does not engage; avoids combat.
    }

    public enum MemberRolePreference
    {
        Any,
        Assault,
        Support,
        Scout,
        Medic
    }

    public enum SquadUnitState
    {
        Idle,
        Moving,
        Attacking,
        Following,
        Dead
    }

    /// <summary>
    ///     Represents a controllable squad member and references character sub-systems.
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

        [Header("Behaviour")] [SerializeField] private MemberStance stance = MemberStance.Neutral;

        [SerializeField] private MemberRolePreference rolePreference = MemberRolePreference.Any;

        [Header("Runtime State")] [SerializeField]
        private bool autoTrackRuntimeState = true;

        [SerializeField] [Min(0f)] private float attackStatePersistSeconds = 0.75f;
        [SerializeField] private SquadUnitState currentState = SquadUnitState.Idle;

        private SquadCommandType? _lastIssuedCommand;
        private float _attackStateExpiresAt;

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

        public SquadUnitState CurrentState => currentState;

        public event Action<SquadMember, SquadUnitState> RuntimeStateChanged;


        private void Awake()
        {
            RefreshReferences();
            // SquadMember only exists on NPC squadmates; player prefab clones must not keep UnitRole.Player.
            if (unit != null)
                unit.SetRole(UnitRole.SquadMember);
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

        private void Update()
        {
            if (!autoTrackRuntimeState) return;

            RefreshRuntimeState();
        }

        public bool IsAvailableForOrders()
        {
            return unitHealth != null && !unitHealth.IsDead;
        }

        public void SetCommandState(SquadCommandType commandType)
        {
            _lastIssuedCommand = commandType;
            if (commandType == SquadCommandType.Attack)
                _attackStateExpiresAt = Time.time + Mathf.Max(0f, attackStatePersistSeconds);

            RefreshRuntimeState();
        }

        public void ClearCommandState()
        {
            _lastIssuedCommand = null;
            _attackStateExpiresAt = 0f;
            RefreshRuntimeState();
        }

        public void RefreshReferences()
        {
            AutoWire();
            EnsureMemberId();
        }

        public void AssignMemberIdForRestore(string restoredMemberId)
        {
            if (string.IsNullOrWhiteSpace(restoredMemberId)) return;
            if (string.Equals(memberId, restoredMemberId, StringComparison.Ordinal)) return;

            memberId = restoredMemberId;
        }

        private void AutoWire()
        {
            if (unit == null) unit = GetComponent<Unit>();

            if (unitController == null) unitController = GetComponent<UnitController>();

            if (unitStats == null) unitStats = GetComponent<UnitStats>();

            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();

            if (unitCombat == null) unitCombat = GetComponent<UnitCombat>();

            if (followController == null) followController = GetComponent<FollowController>();
        }

        private void EnsureMemberId()
        {
            if (!string.IsNullOrWhiteSpace(memberId)) return;

            if (unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
            {
                memberId = unit.UnitId;
                return;
            }

            memberId = Guid.NewGuid().ToString("N");
        }

        private void RefreshRuntimeState()
        {
            if (unitHealth != null && unitHealth.IsDead)
            {
                SetRuntimeState(SquadUnitState.Dead);
                return;
            }

            if (unitController != null && (unitController.IsMoving || unitController.HasMoveTarget))
            {
                if (_lastIssuedCommand == SquadCommandType.Follow)
                    SetRuntimeState(SquadUnitState.Following);
                else
                    SetRuntimeState(SquadUnitState.Moving);

                return;
            }

            if (_lastIssuedCommand == SquadCommandType.Follow)
            {
                SetRuntimeState(SquadUnitState.Following);
                return;
            }

            if (ShouldRemainInAttackingState())
            {
                SetRuntimeState(SquadUnitState.Attacking);
                return;
            }

            SetRuntimeState(SquadUnitState.Idle);
        }

        private bool ShouldRemainInAttackingState()
        {
            if (_lastIssuedCommand != SquadCommandType.Attack) return false;

            if (Time.time < _attackStateExpiresAt) return true;

            return unitCombat is { MarkedTarget: { IsDead: false } };
        }

        private void SetRuntimeState(SquadUnitState state)
        {
            if (currentState == state) return;

            currentState = state;
            RuntimeStateChanged?.Invoke(this, state);
        }
    }
}