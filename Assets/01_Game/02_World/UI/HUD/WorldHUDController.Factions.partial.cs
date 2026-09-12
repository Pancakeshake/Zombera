#region

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI.SquadManagement;

#endregion

namespace Zombera.UI
{
#pragma warning disable S101 // HUD is an intentional acronym in this project's naming convention
    public sealed partial class WorldHUDController
    {
        private void EnsureFormationsJobsFactionsPanels()
        {
            EnsureFormationsJobsPanels();
            EnsureFactionsPanelController();
        }

        private void ResolveFactionsReferences()
        {
            if (panelsRoot == null)
                panelsRoot = transform.Find("Panels") as RectTransform;

            if (panelsRoot == null || factionsPanel != null) return;

            var factions = panelsRoot.Find("FactionsPanel");
            if (factions != null) factionsPanel = factions.gameObject;
        }

        private void EnsureFactionsPanelController()
        {
            ResolveFactionsReferences();
            factionsPanel = EnsurePanelShell(factionsPanel, "FactionsPanel", "FACTIONS");
            if (factionsPanel == null) return;

            _factionsTab = factionsPanel.GetComponent<FactionsTabController>();
            var host = ResolveOrCreateTabContentHost(factionsPanel.transform);
            if (_factionsTab == null)
                _factionsTab = factionsPanel.AddComponent<FactionsTabController>();

            if (host.childCount == 0)
                _factionsTab.Build(host, TMP_Settings.defaultFontAsset, null, null);
        }

        private void RefreshFactionsTab(TabId tab)
        {
            if (tab == TabId.Factions) _factionsTab?.RefreshFromRuntime();
        }
    }
}
