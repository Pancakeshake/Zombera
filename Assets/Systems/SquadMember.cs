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
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            unit?.SetRole(UnitRole.SquadMember);
        }

        public bool IsAvailableForOrders()
        {
            return unitHealth != null && !unitHealth.IsDead;
        }

        // TODO: Add per-member stance, role preference, and autonomous behavior toggles.
    }
}