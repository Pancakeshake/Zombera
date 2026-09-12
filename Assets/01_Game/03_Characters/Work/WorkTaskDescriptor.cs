using System;
using UnityEngine;

namespace Zombera.Characters.Work
{
    [Serializable]
    public struct WorkTaskDescriptor
    {
        public string taskId;
        public WorkJobType jobType;
        public Vector3 worldPosition;
        public string reservedMemberId;
        public float urgency;
        public WorkTaskState state;
        public int sourceInstanceId;
        public string requiredUnitId;

        public bool IsValid => !string.IsNullOrWhiteSpace(taskId);

        public WorkTaskDescriptor(
            string taskId,
            WorkJobType jobType,
            Vector3 worldPosition,
            float urgency = 0f,
            int sourceInstanceId = 0)
        {
            this.taskId = taskId;
            this.jobType = jobType;
            this.worldPosition = worldPosition;
            reservedMemberId = string.Empty;
            this.urgency = urgency;
            state = WorkTaskState.Available;
            this.sourceInstanceId = sourceInstanceId;
            requiredUnitId = string.Empty;
        }
    }
}
