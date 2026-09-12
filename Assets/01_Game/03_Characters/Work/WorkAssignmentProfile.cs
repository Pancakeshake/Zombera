using System;

namespace Zombera.Characters.Work
{
    [Serializable]
    public sealed class WorkAssignmentProfile
    {
        public string memberId = string.Empty;
        public int lootingPriority;
        public int miningPriority;
        public int buildingPriority;
        public int guardingPriority;
        public int cookingPriority;
        public int craftingPriority;

        public WorkPriorityLevel GetPriority(WorkJobType jobType)
        {
            return (WorkPriorityLevel)GetRaw(jobType);
        }

        public void SetPriority(WorkJobType jobType, WorkPriorityLevel level)
        {
            SetRaw(jobType, (int)level);
        }

        public WorkPriorityLevel CyclePriority(WorkJobType jobType)
        {
            var current = GetPriority(jobType);
            var next = current switch
            {
                WorkPriorityLevel.Off => WorkPriorityLevel.Priority9,
                WorkPriorityLevel.Priority9 => WorkPriorityLevel.Priority8,
                WorkPriorityLevel.Priority8 => WorkPriorityLevel.Priority7,
                WorkPriorityLevel.Priority7 => WorkPriorityLevel.Priority6,
                WorkPriorityLevel.Priority6 => WorkPriorityLevel.Priority5,
                WorkPriorityLevel.Priority5 => WorkPriorityLevel.Priority4,
                WorkPriorityLevel.Priority4 => WorkPriorityLevel.Priority3,
                WorkPriorityLevel.Priority3 => WorkPriorityLevel.Priority2,
                WorkPriorityLevel.Priority2 => WorkPriorityLevel.Priority1,
                _ => WorkPriorityLevel.Off
            };

            SetPriority(jobType, next);
            return next;
        }

        public static WorkAssignmentProfile CreateDefault(string memberId)
        {
            var profile = new WorkAssignmentProfile { memberId = memberId };
            profile.SetPriority(WorkJobType.Building, WorkPriorityLevel.Priority3);
            return profile;
        }

        public void ApplyPreset(WorkPriorityPreset preset)
        {
            SetAll(WorkPriorityLevel.Off);

            switch (preset)
            {
                case WorkPriorityPreset.Builder:
                    SetPriority(WorkJobType.Building, WorkPriorityLevel.Priority1);
                    SetPriority(WorkJobType.Mining, WorkPriorityLevel.Priority2);
                    SetPriority(WorkJobType.Crafting, WorkPriorityLevel.Priority4);
                    break;
                case WorkPriorityPreset.Crafter:
                    SetPriority(WorkJobType.Crafting, WorkPriorityLevel.Priority1);
                    SetPriority(WorkJobType.Cooking, WorkPriorityLevel.Priority2);
                    SetPriority(WorkJobType.Building, WorkPriorityLevel.Priority4);
                    break;
                case WorkPriorityPreset.Scavenger:
                    SetPriority(WorkJobType.Looting, WorkPriorityLevel.Priority1);
                    SetPriority(WorkJobType.Guarding, WorkPriorityLevel.Priority3);
                    break;
                case WorkPriorityPreset.Guard:
                    SetPriority(WorkJobType.Guarding, WorkPriorityLevel.Priority1);
                    SetPriority(WorkJobType.Looting, WorkPriorityLevel.Priority4);
                    break;
                case WorkPriorityPreset.Balanced:
                    SetPriority(WorkJobType.Looting, WorkPriorityLevel.Priority3);
                    SetPriority(WorkJobType.Building, WorkPriorityLevel.Priority3);
                    SetPriority(WorkJobType.Guarding, WorkPriorityLevel.Priority3);
                    SetPriority(WorkJobType.Crafting, WorkPriorityLevel.Priority4);
                    SetPriority(WorkJobType.Cooking, WorkPriorityLevel.Priority4);
                    break;
            }
        }

        private void SetAll(WorkPriorityLevel level)
        {
            for (var i = 0; i < 6; i++)
                SetPriority((WorkJobType)i, level);
        }

        private int GetRaw(WorkJobType jobType)
        {
            return jobType switch
            {
                WorkJobType.Looting => lootingPriority,
                WorkJobType.Mining => miningPriority,
                WorkJobType.Building => buildingPriority,
                WorkJobType.Guarding => guardingPriority,
                WorkJobType.Cooking => cookingPriority,
                WorkJobType.Crafting => craftingPriority,
                _ => 0
            };
        }

        private void SetRaw(WorkJobType jobType, int value)
        {
            switch (jobType)
            {
                case WorkJobType.Looting: lootingPriority = value; break;
                case WorkJobType.Mining: miningPriority = value; break;
                case WorkJobType.Building: buildingPriority = value; break;
                case WorkJobType.Guarding: guardingPriority = value; break;
                case WorkJobType.Cooking: cookingPriority = value; break;
                case WorkJobType.Crafting: craftingPriority = value; break;
            }
        }
    }
}
