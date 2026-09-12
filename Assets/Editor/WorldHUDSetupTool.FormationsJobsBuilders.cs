using UnityEditor;
using UnityEngine;
using Zombera.UI;

namespace Zombera.Editor
{
    public static partial class WorldHudSetupTool
    {
        private const string TabContentChildName = "TabContent";

        private static GameObject BuildFormationsPanel(Transform parent)
        {
            return BuildFormationsJobsPanelShell(parent, "FormationsPanel", "FORMATIONS");
        }

        private static GameObject BuildJobsPanel(Transform parent)
        {
            return BuildFormationsJobsPanelShell(parent, "JobsPanel", "JOBS");
        }

        private static GameObject BuildFactionsPanel(Transform parent)
        {
            return BuildFormationsJobsPanelShell(parent, "FactionsPanel", "FACTIONS");
        }

        private static GameObject BuildFormationsJobsPanelShell(Transform parent, string panelName, string headerTitle)
        {
            var panel = MakePanel(parent, panelName);
            BuildPanelHeader(panel.transform, headerTitle);

            var content = MakeRect(TabContentChildName, panel.transform);
            var contentRT = RT(content);
            contentRT.anchorMin = Vector2.zero;
            contentRT.anchorMax = Vector2.one;
            contentRT.offsetMin = new Vector2(8f, 8f);
            contentRT.offsetMax = new Vector2(-8f, -52f);

            return panel;
        }
    }
}
