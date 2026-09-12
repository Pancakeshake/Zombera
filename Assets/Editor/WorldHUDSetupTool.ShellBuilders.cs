using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI;

namespace Zombera.Editor
{
    public static partial class WorldHudSetupTool
    {
        // ── Top Bar ───────────────────────────────────────────────────────────

        private static GameObject BuildTopBar(Transform parent, WorldHUDController hud)
        {
            var go = MakeImage("TopBar", parent, C_Bar);
            AnchorTop(RT(go), TOP_H);

            var tbc = go.AddComponent<TopBarController>();

            // Left: tabs group
            var tabsGO = MakeRect("TabsGroup", go.transform);
            var tabsRT = RT(tabsGO);
            tabsRT.anchorMin = Vector2.zero;
            tabsRT.anchorMax = new Vector2(0.72f, 1f);
            tabsRT.offsetMin = Vector2.zero;
            tabsRT.offsetMax = Vector2.zero;
            var tabHLG = tabsGO.AddComponent<HorizontalLayoutGroup>();
            tabHLG.spacing = 2f;
            tabHLG.childAlignment = TextAnchor.MiddleLeft;
            tabHLG.childForceExpandWidth = false;
            tabHLG.childForceExpandHeight = false;
            tabHLG.childControlWidth = true;
            tabHLG.childControlHeight = true;
            tabHLG.padding = new RectOffset(6, 0, 4, 4);

            var tabSquad = MakeTab(tabsGO.transform, "Tab_Squad", "F1  SQUAD", 180f);
            var tabInv = MakeTab(tabsGO.transform, "Tab_Inventory", "F2  INVENTORY", 215f);
            var tabCrt = MakeTab(tabsGO.transform, "Tab_Crafting", "F3  CRAFTING", 205f);
            var tabMap = MakeTab(tabsGO.transform, "Tab_Map", "F4  MAP", 160f);
            var tabMis = MakeTab(tabsGO.transform, "Tab_Missions", "F5  MISSIONS", 190f);
            var tabForm = MakeTab(tabsGO.transform, "Tab_Formations", "F6  FORM", 165f);
            var tabJobs = MakeTab(tabsGO.transform, "Tab_Jobs", "F7  JOBS", 145f);
            var tabFactions = MakeTab(tabsGO.transform, "Tab_Factions", "F8  FACTIONS", 175f);

            // Right: speed controls group (no extra background panel)
            var timeGO = MakeRect("TimeGroup", go.transform);
            var timeRT = RT(timeGO);
            timeRT.anchorMin = new Vector2(0.72f, 0f);
            timeRT.anchorMax = Vector2.one;
            timeRT.offsetMin = Vector2.zero;
            timeRT.offsetMax = Vector2.zero;
            var timeHLG = timeGO.AddComponent<HorizontalLayoutGroup>();
            timeHLG.spacing = 4f;
            timeHLG.childAlignment = TextAnchor.MiddleRight;
            timeHLG.childForceExpandWidth = false;
            timeHLG.childForceExpandHeight = false;
            timeHLG.childControlWidth = true;
            timeHLG.childControlHeight = true;
            timeHLG.padding = new RectOffset(8, 8, 6, 6);

            // Speed buttons
            var btnPause = MakeTimeBtn(timeGO.transform, "Btn_Pause", "II");
            var btn1x = MakeTimeBtn(timeGO.transform, "Btn_1x", "1×");
            var btn2x = MakeTimeBtn(timeGO.transform, "Btn_2x", "2×");
            var btn4x = MakeTimeBtn(timeGO.transform, "Btn_4x", "4×");

            // Wire TopBarController
            var so = new SerializedObject(tbc);
            so.FindProperty("pauseButton").objectReferenceValue = btnPause;
            so.FindProperty("speed1xButton").objectReferenceValue = btn1x;
            so.FindProperty("speed2xButton").objectReferenceValue = btn2x;
            so.FindProperty("speed4xButton").objectReferenceValue = btn4x;
            so.FindProperty("squadTabButton").objectReferenceValue = tabSquad;
            so.FindProperty("inventoryTabButton").objectReferenceValue = tabInv;
            so.FindProperty("craftingTabButton").objectReferenceValue = tabCrt;
            so.FindProperty("mapTabButton").objectReferenceValue = tabMap;
            so.FindProperty("missionsTabButton").objectReferenceValue = tabMis;
            so.FindProperty("formationsTabButton").objectReferenceValue = tabForm;
            so.FindProperty("jobsTabButton").objectReferenceValue = tabJobs;
            so.FindProperty("factionsTabButton").objectReferenceValue = tabFactions;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Also wire topBar into hud now (will be overwritten next in BuildWorldHUD, harmless)
            var hudSO = new SerializedObject(hud);
            hudSO.FindProperty("topBar").objectReferenceValue = tbc;
            hudSO.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ── Dim overlay ───────────────────────────────────────────────────────

        private static GameObject BuildDimOverlay(Transform parent)
        {
            var go = MakeImage("DimOverlay", parent, new Color(0f, 0f, 0f, 0f));
            FillParent(RT(go));
            go.SetActive(false);
            return go;
        }

        // ── Panels root ───────────────────────────────────────────────────────

        private static GameObject BuildPanelsRoot(Transform parent)
        {
            var go = MakeRect("Panels", parent);
            var rt = RT(go);
            FillParent(rt);
            rt.offsetMin = new Vector2(0f, BOT_H);
            rt.offsetMax = new Vector2(0f, -TOP_H);
            return go;
        }

        // ── Alert banner ──────────────────────────────────────────────────────

        private static GameObject BuildAlertBanner(Transform parent)
        {
            var go = MakeImage("AlertBanner", parent, new Color(0.07f, 0.04f, 0.04f, 0.95f));
            var rt = RT(go);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -TOP_H);
            rt.sizeDelta = new Vector2(0f, 48f);

            var iconGO = MakeImage("AlertIcon", go.transform, new Color(0.88f, 0.48f, 0.12f, 1f));
            var iconRT = RT(iconGO);
            iconRT.anchorMin = new Vector2(0f, 0f);
            iconRT.anchorMax = new Vector2(0f, 1f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.offsetMin = new Vector2(12f, 8f);
            iconRT.offsetMax = new Vector2(44f, -8f);

            var txtGO = MakeRect("AlertText", go.transform);
            var txtRT = RT(txtGO);
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(56f, 4f);
            txtRT.offsetMax = new Vector2(-12f, -4f);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.text = "Alert";
            txt.fontSize = 18f;
            txt.color = C_Text;
            txt.alignment = TextAlignmentOptions.MidlineLeft;

            go.SetActive(false);
            return go;
        }

        // ── Bottom bar ────────────────────────────────────────────────────────

        private static GameObject BuildBottomBar(Transform parent, WorldHUDController hud, TopBarController topBar)
        {
            var go = MakeImage("BottomBar", parent, C_Bar);
            AnchorBottom(RT(go), BOT_H);

            // Portrait strip (full width)
            var stripGO = MakeRect("PortraitStrip", go.transform);
            var stripRT = RT(stripGO);
            stripRT.anchorMin = Vector2.zero;
            stripRT.anchorMax = Vector2.one;
            stripRT.offsetMin = new Vector2(8f, 6f);
            stripRT.offsetMax = new Vector2(-260f, -46f);
            var hlg = stripGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var strip = stripGO.AddComponent<SquadPortraitStrip>();

            for (var i = 0; i < 10; i++)
                BuildPortraitSlot(stripGO.transform, i);

            var squadPageTabs = BuildBottomBarSquadTabs(go.transform);

            var stripSO = new SerializedObject(strip);
            stripSO.FindProperty("enableSquadTabs").boolValue = true;
            stripSO.FindProperty("autoEnableTabsForBottomStrip").boolValue = true;
            stripSO.FindProperty("squadTabCount").intValue = 4;
            stripSO.FindProperty("slotsPerSquadTab").intValue = 10;
            var tabButtonsProperty = stripSO.FindProperty("squadTabButtons");
            tabButtonsProperty.arraySize = squadPageTabs.Length;
            for (var i = 0; i < squadPageTabs.Length; i++)
                tabButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = squadPageTabs[i];
            stripSO.ApplyModifiedPropertiesWithoutUndo();

            // Day-time display aligned with the squad tab row (TopBarController drives text each frame).
            var dtGO = MakeRect("DayTimeBottomRight", go.transform);
            var dtRT = RT(dtGO);
            dtRT.anchorMin = new Vector2(1f, 1f);
            dtRT.anchorMax = new Vector2(1f, 1f);
            dtRT.pivot = new Vector2(1f, 1f);
            dtRT.anchoredPosition = new Vector2(-12f, -6f);
            dtRT.sizeDelta = new Vector2(240f, 38f);

            var dtTMP = dtGO.AddComponent<TextMeshProUGUI>();
            dtTMP.text = "DAY 1  |  08:00";
            dtTMP.fontSize = 22f;
            dtTMP.fontStyle = FontStyles.Bold;
            dtTMP.color = C_Text;
            dtTMP.alignment = TextAlignmentOptions.MidlineRight;
            dtTMP.raycastTarget = false;

            if (topBar != null)
            {
                var topSO = new SerializedObject(topBar);
                topSO.FindProperty("dayTimeText").objectReferenceValue = dtTMP;
                topSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // Wire strip into hud  (also done in main wire block but harmless here)
            var so = new SerializedObject(hud);
            so.FindProperty("portraitStrip").objectReferenceValue = strip;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        private static Button[] BuildBottomBarSquadTabs(Transform parent)
        {
            var tabsRoot = MakeRect("SquadPageTabs", parent);
            var tabsRT = RT(tabsRoot);
            tabsRT.anchorMin = new Vector2(0f, 1f);
            tabsRT.anchorMax = new Vector2(0f, 1f);
            tabsRT.pivot = new Vector2(0f, 1f);
            tabsRT.anchoredPosition = new Vector2(8f, -6f);
            tabsRT.sizeDelta = new Vector2(560f, 38f);

            var tabsHLG = tabsRoot.AddComponent<HorizontalLayoutGroup>();
            tabsHLG.spacing = 8f;
            tabsHLG.childAlignment = TextAnchor.MiddleLeft;
            tabsHLG.childControlWidth = true;
            tabsHLG.childControlHeight = true;
            tabsHLG.childForceExpandWidth = false;
            tabsHLG.childForceExpandHeight = false;

            var buttons = new Button[4];
            for (var i = 0; i < buttons.Length; i++)
            {
                var tabButtonGO = MakeImage($"SquadTab_{i + 1}", tabsRoot.transform, C_Btn);

                var tabButtonLE = tabButtonGO.AddComponent<LayoutElement>();
                tabButtonLE.minWidth = 100f;
                tabButtonLE.preferredWidth = 100f;
                tabButtonLE.minHeight = 38f;
                tabButtonLE.preferredHeight = 38f;
                tabButtonLE.flexibleWidth = 0f;

                var tabButton = tabButtonGO.AddComponent<Button>();
                tabButton.targetGraphic = tabButtonGO.GetComponent<Image>();

                var tabLabel = MakeRect("Label", tabButtonGO.transform).AddComponent<TextMeshProUGUI>();
                FillParent(RT(tabLabel.gameObject));
                tabLabel.text = $"Squad {i + 1}";
                tabLabel.fontSize = 14f;
                tabLabel.textWrappingMode = TextWrappingModes.NoWrap;
                tabLabel.overflowMode = TextOverflowModes.Truncate;
                tabLabel.fontStyle = FontStyles.Bold;
                tabLabel.color = C_Text;
                tabLabel.alignment = TextAlignmentOptions.Center;
                tabLabel.raycastTarget = false;

                buttons[i] = tabButton;
            }

            return buttons;
        }

        private static Button[] BuildRosterHeaderSquadTabs(Transform parent, float fontScale)
        {
            var tabsRoot = MakeRect("SquadPageTabs", parent);
            var tabsRootLE = tabsRoot.AddComponent<LayoutElement>();
            tabsRootLE.minWidth = 204f;
            tabsRootLE.preferredWidth = 204f;
            tabsRootLE.flexibleWidth = 0f;

            var tabsHLG = tabsRoot.AddComponent<HorizontalLayoutGroup>();
            tabsHLG.spacing = 4f;
            tabsHLG.childAlignment = TextAnchor.MiddleRight;
            tabsHLG.childControlWidth = true;
            tabsHLG.childControlHeight = true;
            tabsHLG.childForceExpandWidth = false;
            tabsHLG.childForceExpandHeight = false;

            var buttons = new Button[4];
            for (var i = 0; i < buttons.Length; i++)
            {
                var tabButtonGO = MakeImage($"SquadTab_{i + 1}", tabsRoot.transform, C_Btn);

                var tabButtonLE = tabButtonGO.AddComponent<LayoutElement>();
                tabButtonLE.minWidth = 48f;
                tabButtonLE.preferredWidth = 48f;
                tabButtonLE.minHeight = 30f;
                tabButtonLE.preferredHeight = 30f;
                tabButtonLE.flexibleWidth = 0f;

                var tabButton = tabButtonGO.AddComponent<Button>();
                tabButton.targetGraphic = tabButtonGO.GetComponent<Image>();

                var tabLabel = MakeRect("Label", tabButtonGO.transform).AddComponent<TextMeshProUGUI>();
                FillParent(RT(tabLabel.gameObject));
                tabLabel.text = (i + 1).ToString();
                tabLabel.fontSize = 9f * fontScale;
                tabLabel.fontStyle = FontStyles.Bold;
                tabLabel.color = C_Text;
                tabLabel.alignment = TextAlignmentOptions.Center;
                tabLabel.raycastTarget = false;

                buttons[i] = tabButton;
            }

            return buttons;
        }

        private static void BuildPortraitSlot(Transform parent, int idx)
        {
            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var slotGO = MakeImage($"Portrait_{idx}", parent, C_Port);
            var slotRT = RT(slotGO);
            slotRT.sizeDelta = new Vector2(PORT_W, PORT_H);
            var slotLE = slotGO.AddComponent<LayoutElement>();
            slotLE.minWidth = PORT_W;
            slotLE.preferredWidth = PORT_W;
            slotLE.minHeight = PORT_H;
            slotLE.preferredHeight = PORT_H;
            slotLE.flexibleWidth = 0f;
            var slot = slotGO.AddComponent<SquadPortraitSlot>();
            var btn = slotGO.AddComponent<Button>();
            btn.targetGraphic = slotGO.GetComponent<Image>();

            // Portrait image
            var portGO = MakeImage("PortraitImage", slotGO.transform, new Color(0.20f, 0.20f, 0.25f, 1f));
            var portRT = RT(portGO);
            portRT.anchorMin = new Vector2(0f, 0.24f);
            portRT.anchorMax = new Vector2(1f, 0.74f);
            portRT.offsetMin = new Vector2(3f, 2f);
            portRT.offsetMax = new Vector2(-3f, -2f);
            portGO.GetComponent<Image>().raycastTarget = false;

            // HP bar BG
            var hpBgGO = MakeImage("HPBarBG", slotGO.transform, C_HPBg);
            var hpBgRT = RT(hpBgGO);
            hpBgRT.anchorMin = new Vector2(0f, 0.14f);
            hpBgRT.anchorMax = new Vector2(1f, 0.22f);
            hpBgRT.offsetMin = new Vector2(3f, 0f);
            hpBgRT.offsetMax = new Vector2(-3f, 0f);
            hpBgGO.GetComponent<Image>().raycastTarget = false;

            var hpFillGO = MakeImage("HPFill", hpBgGO.transform, C_HP);
            var hpImg = hpFillGO.GetComponent<Image>();
            if (uiSprite != null) hpImg.sprite = uiSprite;
            hpImg.type = Image.Type.Filled;
            hpImg.fillMethod = Image.FillMethod.Horizontal;
            hpImg.fillAmount = 1f;
            hpImg.raycastTarget = false;
            FillParent(RT(hpFillGO));

            // Stamina bar BG
            var stBgGO = MakeImage("StaminaBarBG", slotGO.transform, C_StamBg);
            var stBgRT = RT(stBgGO);
            stBgRT.anchorMin = new Vector2(0f, 0.04f);
            stBgRT.anchorMax = new Vector2(1f, 0.12f);
            stBgRT.offsetMin = new Vector2(3f, 0f);
            stBgRT.offsetMax = new Vector2(-3f, 0f);
            stBgGO.GetComponent<Image>().raycastTarget = false;

            var stFillGO = MakeImage("StaminaFill", stBgGO.transform, C_Stam);
            var stImg = stFillGO.GetComponent<Image>();
            if (uiSprite != null) stImg.sprite = uiSprite;
            stImg.type = Image.Type.Filled;
            stImg.fillMethod = Image.FillMethod.Horizontal;
            stImg.fillAmount = 1f;
            stImg.raycastTarget = false;
            FillParent(RT(stFillGO));

            // Select overlay
            var selGO = MakeImage("SelectOverlay", slotGO.transform, new Color(0.22f, 0.58f, 0.40f, 0.45f));
            FillParent(RT(selGO));
            selGO.GetComponent<Image>().raycastTarget = false;
            selGO.SetActive(false);

            // Name plate + label
            var namePlateGO = MakeImage("NamePlate", slotGO.transform, new Color(0f, 0f, 0f, 0.65f));
            var nameRT = RT(namePlateGO);
            nameRT.anchorMin = new Vector2(0f, 0.74f);
            nameRT.anchorMax = Vector2.one;
            nameRT.offsetMin = new Vector2(2f, 2f);
            nameRT.offsetMax = new Vector2(-2f, -2f);
            namePlateGO.GetComponent<Image>().raycastTarget = false;

            var nameGO = MakeRect("NameLabel", namePlateGO.transform);
            FillParent(RT(nameGO));
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text = $"Unit {idx + 1}";
            nameTMP.fontSize = 16f;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color = C_Text;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
            nameTMP.overflowMode = TextOverflowModes.Truncate;
            nameTMP.raycastTarget = false;

            // Wire SquadPortraitSlot references
            var sso = new SerializedObject(slot);
            sso.FindProperty("portraitImage").objectReferenceValue = portGO.GetComponent<Image>();
            sso.FindProperty("hpFill").objectReferenceValue = hpFillGO.GetComponent<Image>();
            sso.FindProperty("staminaFill").objectReferenceValue = stFillGO.GetComponent<Image>();
            sso.FindProperty("nameLabel").objectReferenceValue = nameTMP;
            sso.FindProperty("selectOverlay").objectReferenceValue = selGO.GetComponent<Image>();
            sso.FindProperty("slotButton").objectReferenceValue = btn;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
