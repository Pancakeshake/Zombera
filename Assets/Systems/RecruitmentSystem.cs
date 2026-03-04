using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    /// Handles survivor recruitment through rescue, settlement hire, random encounter, or prisoner conversion.
    /// </summary>
    public sealed class RecruitmentSystem : MonoBehaviour
    {
        [SerializeField] private SquadManager squadManager;

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

            survivor.MarkRecruited();

            // TODO: Convert survivor to squad member prefab/component pipeline.
            // TODO: Register recruit in squad manager with persistent ID.
            _ = squadManager;

            return true;
        }

        public void TriggerRecruitmentDialogue(SurvivorAI survivor, DialogueEvent dialogueEvent)
        {
            // TODO: Push dialogue event into UI/dialogue pipeline.
            _ = survivor;
            _ = dialogueEvent;
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