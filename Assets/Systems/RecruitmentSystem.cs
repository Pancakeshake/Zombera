using UnityEngine;
using Zombera.Characters;

namespace Zombera.Systems
{
    /// <summary>
    /// Handles survivor recruitment through rescue, settlement hire, random encounter, or prisoner conversion.
    /// </summary>
    public sealed class RecruitmentSystem : MonoBehaviour
    {
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private bool addFollowControllerOnRecruit = true;

        private void Awake()
        {
            if (squadManager == null)
            {
                squadManager = SquadManager.Instance;
            }

            if (squadManager == null)
            {
                squadManager = FindFirstObjectByType<SquadManager>();
            }
        }

        public bool TryRecruit(SurvivorAI survivor, RecruitmentMethod method)
        {
            if (survivor == null || survivor.IsRecruited)
            {
                return false;
            }

            bool accepted = survivor.EvaluateRecruitment(method);

            if (!accepted)
            {
                return false;
            }

            SquadMember squadMember = PrepareSquadMember(survivor);

            if (squadMember == null)
            {
                return false;
            }

            survivor.MarkRecruited();
            ResolveSquadManager()?.RegisterMember(squadMember);

            return true;
        }

        public void TriggerRecruitmentDialogue(SurvivorAI survivor, DialogueEvent dialogueEvent)
        {
            if (survivor == null || dialogueEvent == null)
            {
                return;
            }

            Core.EventSystem.PublishGlobal(new Core.DialogueRequestedEvent
            {
                DialogueData = dialogueEvent,
                Survivor = survivor
            });
        }

        private SquadMember PrepareSquadMember(SurvivorAI survivor)
        {
            GameObject recruitObject = survivor.gameObject;
            Unit recruitUnit = recruitObject.GetComponent<Unit>();

            if (recruitUnit == null)
            {
                recruitUnit = recruitObject.AddComponent<Unit>();
            }

            if (addFollowControllerOnRecruit && recruitObject.GetComponent<FollowController>() == null)
            {
                recruitObject.AddComponent<FollowController>();
            }

            SquadMember squadMember = recruitObject.GetComponent<SquadMember>();

            if (squadMember == null)
            {
                squadMember = recruitObject.AddComponent<SquadMember>();
            }

            squadMember.RefreshReferences();
            recruitUnit.SetRole(UnitRole.SquadMember);
            return squadMember;
        }

        private SquadManager ResolveSquadManager()
        {
            if (squadManager == null)
            {
                squadManager = SquadManager.Instance;
            }

            if (squadManager == null)
            {
                squadManager = FindFirstObjectByType<SquadManager>();
            }

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