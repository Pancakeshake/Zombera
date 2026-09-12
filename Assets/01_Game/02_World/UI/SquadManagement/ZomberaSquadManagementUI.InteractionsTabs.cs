#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class ZomberaSquadManagementUI
    {
        private void OpenTabAndEnsureVisible(TabId tab)
        {
            if (screenRoot == null)
            {
                BuildOrResolveUI();
                WireInteractions();
            }

            RepairMissingSlicedSprites();
            PopulateInitialData();

            ShowTab(tab);
            SetVisible(true);
        }

        private void RepairMissingSlicedSprites()
        {
            if (screenRoot == null) return;

            var images = screenRoot.GetComponentsInChildren<Image>(true);
            foreach (var image in images)
            {
                if (image == null) continue;

                if (image.type == Image.Type.Sliced && image.sprite == null)
                    // A sliced Image with no sprite renders as an editor placeholder X.
                    // Fall back to Simple so the panel shows as a solid color block.
                    image.type = Image.Type.Simple;
            }
        }

        private void WireInteractions()
        {
            if (inventoryTabButton != null)
            {
                inventoryTabButton.onClick.RemoveAllListeners();
                inventoryTabButton.onClick.AddListener(() => ShowTab(TabId.Inventory));
            }

            if (craftingTabButton != null)
            {
                craftingTabButton.onClick.RemoveAllListeners();
                craftingTabButton.onClick.AddListener(() => ShowTab(TabId.Crafting));
            }

            if (skillsTabButton != null)
            {
                skillsTabButton.onClick.RemoveAllListeners();
                skillsTabButton.onClick.AddListener(() => ShowTab(TabId.Skills));
            }

            if (squadCustomiserTabButton != null)
            {
                squadCustomiserTabButton.onClick.RemoveAllListeners();
                squadCustomiserTabButton.onClick.AddListener(() => ShowTab(TabId.Squad));
            }

            if (formationsTabButton != null)
            {
                formationsTabButton.onClick.RemoveAllListeners();
                formationsTabButton.onClick.AddListener(() => ShowTab(TabId.Formations));
            }

            if (jobsTabButton != null)
            {
                jobsTabButton.onClick.RemoveAllListeners();
                jobsTabButton.onClick.AddListener(() => ShowTab(TabId.Jobs));
            }

            if (factionsTabButton != null)
            {
                factionsTabButton.onClick.RemoveAllListeners();
                factionsTabButton.onClick.AddListener(() => ShowTab(TabId.Factions));
            }

            if (mapTabButton != null)
            {
                mapTabButton.onClick.RemoveAllListeners();
                mapTabButton.onClick.AddListener(() => ShowTab(TabId.Map));
            }

            if (missionsTabButton != null)
            {
                missionsTabButton.onClick.RemoveAllListeners();
                missionsTabButton.onClick.AddListener(() => ShowTab(TabId.Missions));
            }

            if (squadListPanel != null)
            {
                squadListPanel.SelectionChanged -= HandleSurvivorSelection;
                squadListPanel.SelectionChanged += HandleSurvivorSelection;
            }

            if (squadCustomiserTab != null)
            {
                squadCustomiserTab.SquadNameChanged -= HandleSquadNameChanged;
                squadCustomiserTab.SquadNameChanged += HandleSquadNameChanged;
                squadCustomiserTab.MemberOrderChanged -= HandleMemberOrderChanged;
                squadCustomiserTab.MemberOrderChanged += HandleMemberOrderChanged;
            }
        }

        private void HandleSquadNameChanged(string squadName)
        {
            if (string.IsNullOrWhiteSpace(squadName)) return;

            if (squadNameText != null) squadNameText.text = squadName.Trim();
        }

        private void HandleMemberOrderChanged(IReadOnlyList<string> orderedNames)
        {
            if (orderedNames == null || orderedNames.Count == 0 || _survivors.Count == 0) return;

            var reordered = new List<SquadListPanelController.SurvivorEntryData>(orderedNames.Count);
            for (var i = 0; i < orderedNames.Count; i++)
            {
                var expectedName = orderedNames[i];
                for (var j = 0; j < _survivors.Count; j++)
                {
                    if (!string.Equals(_survivors[j].displayName, expectedName, StringComparison.Ordinal)) continue;

                    reordered.Add(_survivors[j]);
                    break;
                }
            }

            if (reordered.Count != _survivors.Count) return;

            _survivors.Clear();
            _survivors.AddRange(reordered);

            if (useLiveGameData && _liveSurvivorContexts.Count == _survivors.Count)
            {
                var reorderedContexts = new List<LiveSurvivorContext>(_liveSurvivorContexts.Count);
                for (var i = 0; i < _survivors.Count; i++)
                {
                    var id = _survivors[i].id;
                    for (var j = 0; j < _liveSurvivorContexts.Count; j++)
                    {
                        if (!string.Equals(_liveSurvivorContexts[j].Id, id, StringComparison.Ordinal)) continue;

                        reorderedContexts.Add(_liveSurvivorContexts[j]);
                        break;
                    }
                }

                if (reorderedContexts.Count == _liveSurvivorContexts.Count)
                {
                    _liveSurvivorContexts.Clear();
                    _liveSurvivorContexts.AddRange(reorderedContexts);
                }
            }

            squadListPanel?.SetEntries(_survivors);
            RefreshTopStatus();
        }

        private void ShowTab(TabId tab)
        {
            if (_activeTab == tab) return;
            _activeTab = tab;

            if (inventoryTabRoot != null) inventoryTabRoot.gameObject.SetActive(tab == TabId.Inventory);

            if (craftingTabRoot != null) craftingTabRoot.gameObject.SetActive(tab == TabId.Crafting);

            if (skillsTabRoot != null) skillsTabRoot.gameObject.SetActive(tab == TabId.Skills);

            if (squadCustomiserTabRoot != null) squadCustomiserTabRoot.gameObject.SetActive(tab == TabId.Squad);

            if (formationsTabRoot != null) formationsTabRoot.gameObject.SetActive(tab == TabId.Formations);

            if (jobsTabRoot != null) jobsTabRoot.gameObject.SetActive(tab == TabId.Jobs);

            if (factionsTabRoot != null) factionsTabRoot.gameObject.SetActive(tab == TabId.Factions);

            if (mapTabRoot != null) mapTabRoot.gameObject.SetActive(tab == TabId.Map);

            if (missionsTabRoot != null) missionsTabRoot.gameObject.SetActive(tab == TabId.Missions);

            if (tab == TabId.Formations) formationsTab?.RefreshFromRuntime();
            if (tab == TabId.Jobs) jobsTab?.RefreshFromRuntime();
            if (tab == TabId.Factions)
            {
                HudDevPlaceholderBootstrap.EnsureForWorldHud(null);
                factionsTab?.RefreshFromRuntime();
            }

            SetTabButtonVisual(squadCustomiserTabButton, tab == TabId.Squad);
            SetTabButtonVisual(inventoryTabButton, tab == TabId.Inventory);
            SetTabButtonVisual(craftingTabButton, tab == TabId.Crafting);
            SetTabButtonVisual(skillsTabButton, tab == TabId.Skills);
            SetTabButtonVisual(formationsTabButton, tab == TabId.Formations);
            SetTabButtonVisual(jobsTabButton, tab == TabId.Jobs);
            SetTabButtonVisual(factionsTabButton, tab == TabId.Factions);
            SetTabButtonVisual(mapTabButton, tab == TabId.Map);
            SetTabButtonVisual(missionsTabButton, tab == TabId.Missions);
        }

        private static void SetTabButtonVisual(Button button, bool active)
        {
            if (button == null) return;

            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = active
                    ? new Color(0.44f, 0.31f, 0.16f, 1f)
                    : new Color(0.23f, 0.21f, 0.18f, 1f);

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.color = active
                    ? new Color(1f, 0.93f, 0.73f, 1f)
                    : new Color(0.72f, 0.67f, 0.56f, 1f);
        }
    }
}
