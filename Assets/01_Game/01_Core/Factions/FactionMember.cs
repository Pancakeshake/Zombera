using UnityEngine;
using Zombera.Characters;

namespace Zombera.Factions
{
    [DisallowMultipleComponent]
    public sealed class FactionMember : MonoBehaviour
    {
        [SerializeField] private string factionId = string.Empty;
        [SerializeField] private string lastKnownRegionId = string.Empty;

        private Unit _unit;

        public string FactionId => factionId;
        public string LastKnownRegionId => lastKnownRegionId;

        private void Awake()
        {
            _unit = GetComponent<Unit>();
            EnsureSeededFromRole(_unit != null ? _unit.Role : UnitRole.Survivor);
        }

        private void OnEnable()
        {
            if (FactionManager.HasInstance)
                FactionManager.Instance.RegisterMember(this);
        }

        private void OnDisable()
        {
            if (FactionManager.HasInstance)
                FactionManager.Instance.UnregisterMember(this);
        }

        public void EnsureSeededFromRole(UnitRole role)
        {
            if (!string.IsNullOrWhiteSpace(factionId)) return;
            factionId = UnitFactionUtility.DefaultFactionIdFromRole(role);
        }

        public void SetFactionId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (string.Equals(factionId, id, System.StringComparison.Ordinal)) return;

            factionId = id;
            if (FactionManager.HasInstance)
                FactionManager.Instance.NotifyMemberFactionChanged(this);
        }

        public void SetLastKnownRegion(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId)) return;
            lastKnownRegionId = regionId;

            if (FactionManager.HasInstance)
                FactionManager.Instance.UpdateLastKnownRegion(factionId, regionId);
        }
    }
}
