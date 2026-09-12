using UnityEngine;
using Zombera.Core;
using Zombera.Factions;

namespace Zombera.Systems
{
    public sealed class FactionSaveProvider : MonoBehaviour, ISaveProvider
    {
        [SerializeField] private FactionManager factionManager;

        public int Priority => 90;

        public void OnSave(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData == null) return;

            saveData.factions ??= new FactionSystemSaveData();
            if (factionManager == null)
            {
                saveData.factions.hasData = false;
                return;
            }

            saveData.factions.hasData = true;
            saveData.factions.standings = factionManager.CaptureStandings();
        }

        public void OnLoad(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData?.factions is not { hasData: true }) return;
            if (factionManager == null) return;

            factionManager.RestoreStandings(saveData.factions.standings);
        }

        private void EnsureReferences()
        {
            if (factionManager == null)
                factionManager = FactionManager.Instance ?? Object.FindFirstObjectByType<FactionManager>();
        }
    }
}
