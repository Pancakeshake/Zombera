using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI;

namespace Zombera.Editor
{
    public static partial class WorldHudSetupTool
    {
        [MenuItem("Tools/World/Patch World HUD Factions", priority = -500)]
        public static void PatchWorldHudPrefabFactions()
        {
            const string prefabPath = "Assets/02_Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab";
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[WorldHUDSetupTool] Could not load prefab at '{prefabPath}'.");
                return;
            }

            try
            {
                var hud = prefabRoot.GetComponent<WorldHUDController>();
                var topBar = prefabRoot.GetComponentInChildren<TopBarController>(true);
                var panelsRoot = prefabRoot.transform.Find("Panels");

                if (hud == null || topBar == null || panelsRoot == null)
                {
                    Debug.LogError("[WorldHUDSetupTool] Prefab is missing WorldHUDController, TopBarController, or Panels root.");
                    return;
                }

                var factionsButton = FindTabButton(prefabRoot.transform, "Tab_Factions", "Tab_Faction");
                if (factionsButton != null)
                {
                    factionsButton.gameObject.name = "Tab_Factions";
                    var label = factionsButton.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null) label.text = "F8  FACTIONS";
                }

                var formationsPanel = EnsurePanelShell(panelsRoot, "FormationsPanel", "FORMATIONS");
                var jobsPanel = EnsurePanelShell(panelsRoot, "JobsPanel", "JOBS");
                var factionsPanel = EnsurePanelShell(panelsRoot, "FactionsPanel", "FACTIONS");

                foreach (var panel in new[] { formationsPanel, jobsPanel, factionsPanel })
                    panel.SetActive(false);

                var hudSo = new SerializedObject(hud);
                hudSo.FindProperty("formationsPanel").objectReferenceValue = formationsPanel;
                hudSo.FindProperty("jobsPanel").objectReferenceValue = jobsPanel;
                hudSo.FindProperty("factionsPanel").objectReferenceValue = factionsPanel;
                hudSo.ApplyModifiedPropertiesWithoutUndo();

                var topSo = new SerializedObject(topBar);
                topSo.FindProperty("factionsTabButton").objectReferenceValue = factionsButton;
                topSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log("[WorldHUDSetupTool] Patched WorldHUDCanvas prefab with Factions tab + panels.", prefabRoot);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Button FindTabButton(Transform root, params string[] names)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null) continue;

                var buttonName = button.name;
                for (var j = 0; j < names.Length; j++)
                {
                    if (string.Equals(buttonName, names[j], System.StringComparison.Ordinal))
                        return button;
                }
            }

            return null;
        }

        private static GameObject EnsurePanelShell(Transform panelsRoot, string panelName, string headerTitle)
        {
            var existing = panelsRoot.Find(panelName);
            if (existing != null) return existing.gameObject;

            var panel = BuildFormationsJobsPanelShell(panelsRoot, panelName, headerTitle);
            panel.SetActive(false);
            return panel;
        }
    }
}
