#region

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI.SquadManagement;

#endregion

namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {
        private const string TabContentChildName = "TabContent";

        private void EnsureFormationsJobsPanels()
        {
            ResolveFormationsJobsReferences();
            EnsureFormationsPanelController();
            EnsureJobsPanelController();
        }

        private void ResolveFormationsJobsReferences()
        {
            if (panelsRoot == null)
                panelsRoot = transform.Find("Panels") as RectTransform;

            if (panelsRoot == null) return;

            if (formationsPanel == null)
            {
                var formations = panelsRoot.Find("FormationsPanel");
                if (formations != null) formationsPanel = formations.gameObject;
            }

            if (jobsPanel == null)
            {
                var jobs = panelsRoot.Find("JobsPanel");
                if (jobs != null) jobsPanel = jobs.gameObject;
            }
        }

        private void EnsureFormationsPanelController()
        {
            formationsPanel = EnsurePanelShell(formationsPanel, "FormationsPanel", "FORMATIONS");
            if (formationsPanel == null) return;

            _formationsTab = formationsPanel.GetComponent<FormationsTabController>();
            var host = ResolveOrCreateTabContentHost(formationsPanel.transform);
            if (_formationsTab == null)
                _formationsTab = formationsPanel.AddComponent<FormationsTabController>();

            if (host.childCount == 0)
                _formationsTab.Build(host, TMP_Settings.defaultFontAsset, null, null);
        }

        private void EnsureJobsPanelController()
        {
            jobsPanel = EnsurePanelShell(jobsPanel, "JobsPanel", "JOBS");
            if (jobsPanel == null) return;

            _jobsTab = jobsPanel.GetComponent<JobsTabController>();
            var host = ResolveOrCreateTabContentHost(jobsPanel.transform);
            if (_jobsTab == null)
                _jobsTab = jobsPanel.AddComponent<JobsTabController>();

            if (host.childCount == 0)
                _jobsTab.Build(host, TMP_Settings.defaultFontAsset, null, null);
        }

        private GameObject EnsurePanelShell(GameObject existing, string panelName, string headerTitle)
        {
            if (existing != null) return existing;
            if (panelsRoot == null) return null;

            var panelRect = MakeRect(panelName, panelsRoot);
            StretchPanelRect(panelRect);

            var image = panelRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.10f, 0.93f);
            image.raycastTarget = true;

            var header = MakeRect("PanelHeader", panelRect);
            var headerRect = header;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 44f);

            var headerImage = header.gameObject.AddComponent<Image>();
            headerImage.color = new Color(0.05f, 0.05f, 0.07f, 1f);

            var title = MakeText("Title", header, headerTitle, 20f);
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.85f, 0.82f, 0.74f, 1f);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var titleRect = title.rectTransform;
            titleRect.offsetMin = new Vector2(12f, 0f);
            titleRect.offsetMax = new Vector2(-12f, 0f);

            var content = MakeRect(TabContentChildName, panelRect);
            StretchTabContentRect(content);

            panelRect.gameObject.SetActive(false);
            return panelRect.gameObject;
        }

        private static RectTransform ResolveOrCreateTabContentHost(Transform panelTransform)
        {
            var existing = panelTransform.Find(TabContentChildName) as RectTransform;
            if (existing != null) return existing;

            var content = MakeRect(TabContentChildName, panelTransform);
            StretchTabContentRect(content);
            return content;
        }

        private static void StretchPanelRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void StretchTabContentRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -52f);
        }

        private void RefreshFormationsJobsTab(TabId tab)
        {
            if (tab == TabId.Formations) _formationsTab?.RefreshFromRuntime();
            if (tab == TabId.Jobs) _jobsTab?.RefreshFromRuntime();
        }
    }
}
