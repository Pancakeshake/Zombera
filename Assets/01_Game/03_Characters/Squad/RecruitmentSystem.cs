#region

using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Handles survivor recruitment through rescue, settlement hire, random encounter, or prisoner conversion.
    /// </summary>
    public sealed class RecruitmentSystem : MonoBehaviour
    {
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private bool addFollowControllerOnRecruit = true;

        private void Awake()
        {
            squadManager = ResolveSquadManager();
        }

        // ReSharper disable once UnusedMember.Local
        private SquadMember PrepareSquadMember(SurvivorController survivor)
        {
            var recruitObject = survivor.gameObject;
            var recruitUnit = recruitObject.GetComponent<Unit>();

            if (recruitUnit == null) recruitUnit = recruitObject.AddComponent<Unit>();

            if (addFollowControllerOnRecruit && recruitObject.GetComponent<FollowController>() == null)
                recruitObject.AddComponent<FollowController>();

            var squadMember = recruitObject.GetComponent<SquadMember>();

            if (squadMember == null) squadMember = recruitObject.AddComponent<SquadMember>();

            squadMember.RefreshReferences();
            recruitUnit.SetRole(UnitRole.SquadMember);
            return squadMember;
        }

        // ReSharper disable once UnusedMember.Local
        private SquadManager ResolveSquadManager()
        {
            if (squadManager == null) squadManager = SquadManager.Instance;

            if (squadManager == null) squadManager = FindFirstObjectByType<SquadManager>();

            return squadManager;
        }
    }

    public enum RecruitmentMethod
    {
        Rescue,
        HireFromSettlement,
        RandomWanderer,
        PrisonerRecruitment
    }
}