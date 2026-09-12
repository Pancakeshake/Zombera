using UnityEngine;
using Zombera.Characters.Work;
using Zombera.Core;

namespace Zombera.Systems
{
    public sealed class JobSaveProvider : MonoBehaviour, ISaveProvider
    {
        [SerializeField] private WorkManager workManager;

        public int Priority => 95;

        public void OnSave(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData == null) return;

            saveData.jobs ??= new JobSystemSaveData();
            if (workManager == null)
            {
                saveData.jobs.hasData = false;
                return;
            }

            workManager.RefreshRosterFromSquad();
            saveData.jobs.hasData = true;
            saveData.jobs.manualPrioritiesEnabled = workManager.ManualPrioritiesEnabled;
            saveData.jobs.memberProfiles = workManager.CaptureProfiles();
        }

        public void OnLoad(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData?.jobs is not { hasData: true }) return;
            if (workManager == null) return;

            workManager.RestoreProfiles(saveData.jobs.memberProfiles, saveData.jobs.manualPrioritiesEnabled);
            workManager.RefreshRosterFromSquad();
        }

        private void EnsureReferences()
        {
            if (workManager == null) workManager = WorkManager.Instance ?? Object.FindFirstObjectByType<WorkManager>();
        }
    }
}
