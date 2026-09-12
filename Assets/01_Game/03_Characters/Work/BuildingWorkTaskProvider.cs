using System.Collections.Generic;
using UnityEngine;
using Zombera.BaseBuilding;

namespace Zombera.Characters.Work
{
    public sealed class BuildingWorkTaskProvider : IWorkTaskProvider
    {
        private readonly List<ConstructionJob> _jobScratch = new();

        public void CollectAvailableTasks(List<WorkTaskDescriptor> buffer)
        {
            _jobScratch.Clear();
            var jobs = Object.FindObjectsByType<ConstructionJob>(FindObjectsSortMode.None);
            for (var i = 0; i < jobs.Length; i++)
            {
                var job = jobs[i];
                if (job == null || job.IsCompleted) continue;
                _jobScratch.Add(job);
            }

            for (var i = 0; i < _jobScratch.Count; i++)
            {
                var job = _jobScratch[i];
                var position = job.TargetBlueprint != null
                    ? job.TargetBlueprint.transform.position
                    : job.transform.position;

                buffer.Add(new WorkTaskDescriptor(
                    BuildTaskId(job),
                    WorkJobType.Building,
                    position,
                    urgency: job.HasAllMaterials ? 2f : 1f,
                    sourceInstanceId: job.GetInstanceID()));
            }
        }

        public static string BuildTaskId(ConstructionJob job)
        {
            return job == null ? string.Empty : "build:" + job.GetInstanceID();
        }

        public static ConstructionJob ResolveJob(WorkTaskDescriptor task)
        {
            if (!task.IsValid || task.jobType != WorkJobType.Building) return null;

            if (task.sourceInstanceId != 0)
            {
                var instance = Object.FindObjectsByType<ConstructionJob>(FindObjectsSortMode.None);
                for (var i = 0; i < instance.Length; i++)
                {
                    if (instance[i] != null && instance[i].GetInstanceID() == task.sourceInstanceId)
                        return instance[i];
                }
            }

            if (!task.taskId.StartsWith("build:", System.StringComparison.Ordinal)) return null;
            var idText = task.taskId.Substring("build:".Length);
            if (!int.TryParse(idText, out var instanceId)) return null;

            var jobs = Object.FindObjectsByType<ConstructionJob>(FindObjectsSortMode.None);
            for (var i = 0; i < jobs.Length; i++)
            {
                if (jobs[i] != null && jobs[i].GetInstanceID() == instanceId)
                    return jobs[i];
            }

            return null;
        }
    }
}
