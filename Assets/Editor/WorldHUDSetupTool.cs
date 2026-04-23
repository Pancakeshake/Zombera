using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Zombera.UI;

namespace Zombera.Editor
{
    /// <summary>
    /// Builds the full WorldHUDCanvas hierarchy in the active scene.
    /// Menu: Tools/Zombera/World UI/Build World HUD
    ///
    /// Layout produced:
    ///   WorldHUDCanvas
    ///     TopBar          — full-width 52 px strip at top
    ///     DimOverlay      — full-screen transparent black (shown when a panel is open)
    ///     Panels          — clip region between bars; contains all 5 overlay panels
    ///     AlertBanner     — 48 px strip that toggles just below top bar
    ///     BottomBar       — full-width 90 px strip at bottom
    /// </summary>
    public static class WorldHUDSetupTool
    {
        // ── Palette ───────────────────────────────────────────────────────────

        static readonly Color C_Bar     = new Color(0.06f, 0.07f, 0.09f, 0.97f);
        static readonly Color C_Panel   = new Color(0.08f, 0.08f, 0.10f, 0.93f);
        static readonly Color C_Tab     = new Color(0.12f, 0.13f, 0.16f, 1f);
        static readonly Color C_Btn     = new Color(0.14f, 0.15f, 0.18f, 1f);
        static readonly Color C_Text    = new Color(0.85f, 0.82f, 0.74f, 1f);
        static readonly Color C_TextDim = new Color(0.50f, 0.48f, 0.42f, 1f);
        static readonly Color C_Accent  = new Color(0.22f, 0.62f, 0.40f, 1f);
        static readonly Color C_HP      = new Color(0.22f, 0.65f, 0.30f, 1f);
        static readonly Color C_HPBg    = new Color(0.25f, 0.08f, 0.06f, 1f);
        static readonly Color C_Stam    = new Color(0.28f, 0.52f, 0.78f, 1f);
        static readonly Color C_StamBg  = new Color(0.06f, 0.11f, 0.22f, 1f);
        static readonly Color C_Port    = new Color(0.11f, 0.11f, 0.14f, 1f);
        static readonly Color C_Border  = new Color(0.26f, 0.29f, 0.32f, 1f);

        // ── Dimensions ────────────────────────────────────────────────────────

        const float TOP_H  = 120f;
        const float BOT_H  = 200f;
        const float PORT_W = 140f;
        const float PORT_H = 140f;
        const float TBTN   = 100f;

        enum EquipmentSlotVisual
        {
            Head,
            Face,
            Chest,
            Back,
            LeftHand,
            RightHand,
            Belt,
            Legs,
            Feet
        }

        // ── Menu Entries ──────────────────────────────────────────────────────

        /// <summary>
        /// Remove any WorldHUDCanvas found in the currently open scene.
        /// Use this to clean up a canvas that was accidentally built into the wrong scene.
        /// </summary>
        [MenuItem("Tools/Zombera/World UI/Remove World HUD From This Scene")]
        static void RemoveWorldHUDFromScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            var controllers = Object.FindObjectsByType<WorldHUDController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int removed = 0;
            foreach (var ctrl in controllers)
            {
                if (ctrl == null || ctrl.gameObject.scene != activeScene) continue;
                Undo.DestroyObjectImmediate(ctrl.gameObject);
                removed++;
            }

            if (removed == 0)
            {
                EditorUtility.DisplayDialog("Remove World HUD", "No World HUD objects found in the active scene.", "OK");
                return;
            }

            Debug.Log($"[WorldHUDSetupTool] Removed {removed} World HUD object(s) from scene '{activeScene.name}'.");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
        }

        /// <summary>
        /// Finds every DynamicCharacterAvatar in the active scene and nulls any
        /// umaGenerator / context field that points to an object in a different scene
        /// (cross-scene serialized references Unity refuses to save).
        ///
        /// Run this from the MainMenu scene whenever the console shows:
        ///   "Cross scene references are not supported: 'UMAPreviewAvatar' … 'UMA_GLIB' …"
        /// </summary>
        [MenuItem("Tools/Zombera/UMA/Clear Cross-Scene UMA References (Active Scene)")]
        static void ClearCrossSceneUmaReferences()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            DynamicCharacterAvatar[] avatars = Object.FindObjectsByType<DynamicCharacterAvatar>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int cleared = 0;
            foreach (DynamicCharacterAvatar avatar in avatars)
            {
                if (avatar == null || avatar.gameObject.scene != activeScene) continue;

                SerializedObject so = new SerializedObject(avatar);

                // Clear umaGenerator if it points outside this scene.
                SerializedProperty genProp = so.FindProperty("umaGenerator");
                if (genProp != null &&
                    genProp.objectReferenceValue != null &&
                    genProp.objectReferenceValue is Component genComp &&
                    genComp.gameObject.scene != activeScene)
                {
                    genProp.objectReferenceValue = null;
                    cleared++;
                }

                // Clear context if it points outside this scene.
                SerializedProperty ctxProp = so.FindProperty("context");
                if (ctxProp != null &&
                    ctxProp.objectReferenceValue != null &&
                    ctxProp.objectReferenceValue is Component ctxComp &&
                    ctxComp.gameObject.scene != activeScene)
                {
                    ctxProp.objectReferenceValue = null;
                    cleared++;
                }

                if (so.hasModifiedProperties)
                {
                    so.ApplyModifiedProperties();
                    Undo.RecordObject(avatar, "Clear Cross-Scene UMA Refs");
                    Debug.Log($"[WorldHUDSetupTool] Cleared cross-scene UMA ref(s) on '{avatar.gameObject.name}'.", avatar);
                }
            }

            if (cleared == 0)
            {
                EditorUtility.DisplayDialog("Clear Cross-Scene UMA Refs",
                    $"No cross-scene UMA references found in scene '{activeScene.name}'.", "OK");
            }
            else
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                EditorUtility.DisplayDialog("Clear Cross-Scene UMA Refs",
                    $"Cleared {cleared} cross-scene UMA reference(s) in scene '{activeScene.name}'.\n\nSave the scene to persist the fix.", "OK");
            }
        }

        [MenuItem("Tools/Zombera/World UI/Build World HUD")]
        static void BuildWorldHUD()
        {
            // Warn if the active scene doesn't look like the World scene.
            string sceneName = SceneManager.GetActiveScene().name;
            bool looksLikeWorldScene = sceneName.IndexOf("World", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!looksLikeWorldScene)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Wrong Scene?",
                    $"The active scene is '{sceneName}', not the World scene.\n\n" +
                    "Building the HUD here will add it to this scene and may interfere with other UI (e.g. the character creator).\n\n" +
                    "Open the World scene first, then run this tool.",
                    "Build Anyway", "Cancel");
                if (!proceed) return;
            }

            // Destroy ALL existing WorldHUDController roots, regardless of name.
            // Old builds may be named "WorldHUD" instead of "WorldHUDCanvas".
            var existingControllers = Object.FindObjectsByType<WorldHUDController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existingControllers.Length > 0)
            {
                if (!EditorUtility.DisplayDialog("Rebuild World HUD",
                        $"{existingControllers.Length} existing World HUD object(s) found. Destroy and rebuild from scratch?",
                        "Rebuild", "Cancel"))
                    return;
                foreach (var ctrl in existingControllers)
                    if (ctrl != null) Undo.DestroyObjectImmediate(ctrl.gameObject);
            }

            // ── Root canvas ───────────────────────────────────────────────────
            var canvasGO = new GameObject("WorldHUDCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build World HUD");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ZomberaCanvasLayer.Hud;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            var hud = canvasGO.AddComponent<WorldHUDController>();

            // Disable the canvas at build time — WorldHUDController re-enables it on
            // GameState.Playing so it never renders in non-world scenes or during menus.
            canvas.enabled = false;

            // ── Sections (order = draw order) ────────────────────────────────
            // Dim overlay must be below the top/bottom bars so tab buttons remain
            // clickable while a panel is open.
            var dimGO     = BuildDimOverlay(canvasGO.transform);
            var topBarGO  = BuildTopBar(canvasGO.transform, hud);
            var panelsGO  = BuildPanelsRoot(canvasGO.transform);
            var alertGO   = BuildAlertBanner(canvasGO.transform);
            var bottomGO  = BuildBottomBar(canvasGO.transform, hud, topBarGO.GetComponent<TopBarController>());

            // ── Panels ────────────────────────────────────────────────────────
            var sqPanel  = BuildSquadPanel(panelsGO.transform);
            var invPanel = BuildInventoryPanel(panelsGO.transform);
            var crtPanel = MakePlaceholder(panelsGO.transform, "CraftingPanel",
                               "CRAFTING", "Crafting system coming soon.");
            var mapPanel = MakePlaceholder(panelsGO.transform, "MapPanel",
                               "TACTICAL MAP", "Overhead map coming soon.");
            var misPanel = BuildMissionsPanel(panelsGO.transform);

            foreach (var p in new[] { sqPanel, invPanel, crtPanel, mapPanel, misPanel })
                p.SetActive(false);

            // ── Wire WorldHUDController ───────────────────────────────────────
            var so = new SerializedObject(hud);
            so.FindProperty("topBar").objectReferenceValue          = topBarGO.GetComponent<TopBarController>();
            so.FindProperty("portraitStrip").objectReferenceValue   = bottomGO.GetComponentInChildren<SquadPortraitStrip>();
            so.FindProperty("squadPanel").objectReferenceValue      = sqPanel;
            so.FindProperty("inventoryPanel").objectReferenceValue  = invPanel;
            so.FindProperty("craftingPanel").objectReferenceValue   = crtPanel;
            so.FindProperty("mapPanel").objectReferenceValue        = mapPanel;
            so.FindProperty("missionsPanel").objectReferenceValue   = misPanel;
            so.FindProperty("panelsRoot").objectReferenceValue      = panelsGO.GetComponent<RectTransform>();
            so.FindProperty("bottomBarRoot").objectReferenceValue   = bottomGO.GetComponent<RectTransform>();
            so.FindProperty("dimOverlay").objectReferenceValue      = dimGO.GetComponent<Image>();
            so.FindProperty("alertBanner").objectReferenceValue     = alertGO.GetComponent<RectTransform>();
            so.FindProperty("alertText").objectReferenceValue       = alertGO.GetComponentInChildren<TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = canvasGO;
            Debug.Log("[WorldHUDSetupTool] WorldHUDCanvas built successfully.", canvasGO);
        }

        // ── Top Bar ───────────────────────────────────────────────────────────

        static GameObject BuildTopBar(Transform parent, WorldHUDController hud)
        {
            var go = MakeImage("TopBar", parent, C_Bar);
            AnchorTop(RT(go), TOP_H);

            var tbc = go.AddComponent<TopBarController>();

            // Left: tabs group
            var tabsGO  = MakeRect("TabsGroup", go.transform);
            var tabsRT  = RT(tabsGO);
            tabsRT.anchorMin = Vector2.zero;
            tabsRT.anchorMax = new Vector2(0.72f, 1f);
            tabsRT.offsetMin = Vector2.zero;
            tabsRT.offsetMax = Vector2.zero;
            var tabHLG = tabsGO.AddComponent<HorizontalLayoutGroup>();
            tabHLG.spacing             = 2f;
            tabHLG.childAlignment      = TextAnchor.MiddleLeft;
            tabHLG.childForceExpandWidth  = false;
            tabHLG.childForceExpandHeight = false;
            tabHLG.childControlWidth   = true;
            tabHLG.childControlHeight  = true;
            tabHLG.padding = new RectOffset(6, 0, 4, 4);

            var tabSquad = MakeTab(tabsGO.transform, "Tab_Squad",     "F1  SQUAD",     180f);
            var tabInv   = MakeTab(tabsGO.transform, "Tab_Inventory", "F2  INVENTORY", 215f);
            var tabCrt   = MakeTab(tabsGO.transform, "Tab_Crafting",  "F3  CRAFTING",  205f);
            var tabMap   = MakeTab(tabsGO.transform, "Tab_Map",       "F4  MAP",       160f);
            var tabMis   = MakeTab(tabsGO.transform, "Tab_Missions",  "F5  MISSIONS",  210f);

            // Right: speed controls group (no extra background panel)
            var timeGO = MakeRect("TimeGroup", go.transform);
            var timeRT = RT(timeGO);
            timeRT.anchorMin = new Vector2(0.72f, 0f);
            timeRT.anchorMax = Vector2.one;
            timeRT.offsetMin = Vector2.zero;
            timeRT.offsetMax = Vector2.zero;
            var timeHLG = timeGO.AddComponent<HorizontalLayoutGroup>();
            timeHLG.spacing             = 4f;
            timeHLG.childAlignment      = TextAnchor.MiddleRight;
            timeHLG.childForceExpandWidth  = false;
            timeHLG.childForceExpandHeight = false;
            timeHLG.childControlWidth   = true;
            timeHLG.childControlHeight  = true;
            timeHLG.padding = new RectOffset(8, 8, 6, 6);

            // Speed buttons
            var btnPause = MakeTimeBtn(timeGO.transform, "Btn_Pause", "II");
            var btn1x    = MakeTimeBtn(timeGO.transform, "Btn_1x",    "1×");
            var btn2x    = MakeTimeBtn(timeGO.transform, "Btn_2x",    "2×");
            var btn4x    = MakeTimeBtn(timeGO.transform, "Btn_4x",    "4×");

            // Wire TopBarController
            var so = new SerializedObject(tbc);
            so.FindProperty("pauseButton").objectReferenceValue        = btnPause;
            so.FindProperty("speed1xButton").objectReferenceValue      = btn1x;
            so.FindProperty("speed2xButton").objectReferenceValue      = btn2x;
            so.FindProperty("speed4xButton").objectReferenceValue      = btn4x;
            so.FindProperty("squadTabButton").objectReferenceValue     = tabSquad;
            so.FindProperty("inventoryTabButton").objectReferenceValue = tabInv;
            so.FindProperty("craftingTabButton").objectReferenceValue  = tabCrt;
            so.FindProperty("mapTabButton").objectReferenceValue       = tabMap;
            so.FindProperty("missionsTabButton").objectReferenceValue  = tabMis;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Also wire topBar into hud now (will be overwritten next in BuildWorldHUD, harmless)
            var hudSO = new SerializedObject(hud);
            hudSO.FindProperty("topBar").objectReferenceValue = tbc;
            hudSO.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ── Dim overlay ───────────────────────────────────────────────────────

        static GameObject BuildDimOverlay(Transform parent)
        {
            var go = MakeImage("DimOverlay", parent, new Color(0f, 0f, 0f, 0f));
            FillParent(RT(go));
            go.SetActive(false);
            return go;
        }

        // ── Panels root ───────────────────────────────────────────────────────

        static GameObject BuildPanelsRoot(Transform parent)
        {
            var go = MakeRect("Panels", parent);
            var rt = RT(go);
            FillParent(rt);
            rt.offsetMin = new Vector2(0f, BOT_H);
            rt.offsetMax = new Vector2(0f, -TOP_H);
            return go;
        }

        // ── Alert banner ──────────────────────────────────────────────────────

        static GameObject BuildAlertBanner(Transform parent)
        {
            var go = MakeImage("AlertBanner", parent, new Color(0.07f, 0.04f, 0.04f, 0.95f));
            var rt = RT(go);
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -TOP_H);
            rt.sizeDelta        = new Vector2(0f, 48f);

            var iconGO = MakeImage("AlertIcon", go.transform, new Color(0.88f, 0.48f, 0.12f, 1f));
            var iconRT = RT(iconGO);
            iconRT.anchorMin = new Vector2(0f, 0f);
            iconRT.anchorMax = new Vector2(0f, 1f);
            iconRT.pivot     = new Vector2(0f, 0.5f);
            iconRT.offsetMin = new Vector2(12f, 8f);
            iconRT.offsetMax = new Vector2(44f, -8f);

            var txtGO = MakeRect("AlertText", go.transform);
            var txtRT = RT(txtGO);
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(56f, 4f);
            txtRT.offsetMax = new Vector2(-12f, -4f);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.text      = "Alert";
            txt.fontSize  = 18f;
            txt.color     = C_Text;
            txt.alignment = TextAlignmentOptions.MidlineLeft;

            go.SetActive(false);
            return go;
        }

        // ── Bottom bar ────────────────────────────────────────────────────────

        static GameObject BuildBottomBar(Transform parent, WorldHUDController hud, TopBarController topBar)
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
            hlg.spacing             = 6f;
            hlg.childAlignment      = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            var strip = stripGO.AddComponent<SquadPortraitStrip>();

            for (int i = 0; i < 5; i++)
                BuildPortraitSlot(stripGO.transform, i);

            Button[] squadPageTabs = BuildBottomBarSquadTabs(go.transform);

            var stripSO = new SerializedObject(strip);
            stripSO.FindProperty("enableSquadTabs").boolValue = true;
            stripSO.FindProperty("autoEnableTabsForBottomStrip").boolValue = true;
            stripSO.FindProperty("squadTabCount").intValue = 4;
            stripSO.FindProperty("slotsPerSquadTab").intValue = 5;
            SerializedProperty tabButtonsProperty = stripSO.FindProperty("squadTabButtons");
            tabButtonsProperty.arraySize = squadPageTabs.Length;
            for (int i = 0; i < squadPageTabs.Length; i++)
            {
                tabButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = squadPageTabs[i];
            }
            stripSO.ApplyModifiedPropertiesWithoutUndo();

            // Bottom-right day-time display (driven by TopBarController each frame)
            var dtGO = MakeRect("DayTimeBottomRight", go.transform);
            var dtRT = RT(dtGO);
            dtRT.anchorMin = new Vector2(1f, 0f);
            dtRT.anchorMax = new Vector2(1f, 0f);
            dtRT.pivot = new Vector2(1f, 0f);
            dtRT.anchoredPosition = new Vector2(-12f, 8f);
            dtRT.sizeDelta = new Vector2(240f, 30f);

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

        static Button[] BuildBottomBarSquadTabs(Transform parent)
        {
            var tabsRoot = MakeRect("SquadPageTabs", parent);
            var tabsRT = RT(tabsRoot);
            tabsRT.anchorMin = new Vector2(0f, 1f);
            tabsRT.anchorMax = new Vector2(0f, 1f);
            tabsRT.pivot = new Vector2(0f, 1f);
            tabsRT.anchoredPosition = new Vector2(8f, -6f);
            tabsRT.sizeDelta = new Vector2(312f, 38f);

            var tabsHLG = tabsRoot.AddComponent<HorizontalLayoutGroup>();
            tabsHLG.spacing = 8f;
            tabsHLG.childAlignment = TextAnchor.MiddleLeft;
            tabsHLG.childControlWidth = true;
            tabsHLG.childControlHeight = true;
            tabsHLG.childForceExpandWidth = false;
            tabsHLG.childForceExpandHeight = false;

            Button[] buttons = new Button[4];
            for (int i = 0; i < buttons.Length; i++)
            {
                var tabButtonGO = MakeImage($"SquadTab_{i + 1}", tabsRoot.transform, C_Btn);

                var tabButtonLE = tabButtonGO.AddComponent<LayoutElement>();
                tabButtonLE.minWidth = 72f;
                tabButtonLE.preferredWidth = 72f;
                tabButtonLE.minHeight = 38f;
                tabButtonLE.preferredHeight = 38f;
                tabButtonLE.flexibleWidth = 0f;

                var tabButton = tabButtonGO.AddComponent<Button>();
                tabButton.targetGraphic = tabButtonGO.GetComponent<Image>();

                var tabLabel = MakeRect("Label", tabButtonGO.transform).AddComponent<TextMeshProUGUI>();
                FillParent(RT(tabLabel.gameObject));
                tabLabel.text = (i + 1).ToString();
                tabLabel.fontSize = 18f;
                tabLabel.fontStyle = FontStyles.Bold;
                tabLabel.color = C_Text;
                tabLabel.alignment = TextAlignmentOptions.Center;
                tabLabel.raycastTarget = false;

                buttons[i] = tabButton;
            }

            return buttons;
        }

        static Button[] BuildRosterHeaderSquadTabs(Transform parent, float fontScale)
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

            Button[] buttons = new Button[4];
            for (int i = 0; i < buttons.Length; i++)
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

        static void BuildPortraitSlot(Transform parent, int idx)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var slotGO = MakeImage($"Portrait_{idx}", parent, C_Port);
            var slotRT = RT(slotGO);
            slotRT.sizeDelta = new Vector2(PORT_W, PORT_H);
            var slotLE = slotGO.AddComponent<LayoutElement>();
            slotLE.minWidth     = PORT_W; slotLE.preferredWidth  = PORT_W;
            slotLE.minHeight    = PORT_H; slotLE.preferredHeight = PORT_H;
            slotLE.flexibleWidth = 0f;
            var slot = slotGO.AddComponent<SquadPortraitSlot>();
            var btn  = slotGO.AddComponent<Button>();
            btn.targetGraphic = slotGO.GetComponent<Image>();

            // Portrait image
            var portGO = MakeImage("PortraitImage", slotGO.transform, new Color(0.20f, 0.20f, 0.25f, 1f));
            var portRT = RT(portGO);
            portRT.anchorMin = new Vector2(0f, 0.24f);
            portRT.anchorMax = new Vector2(1f, 0.74f);
            portRT.offsetMin = new Vector2(3f, 2f);
            portRT.offsetMax = new Vector2(-3f, -2f);

            // HP bar BG
            var hpBgGO = MakeImage("HPBarBG", slotGO.transform, C_HPBg);
            var hpBgRT = RT(hpBgGO);
            hpBgRT.anchorMin = new Vector2(0f, 0.14f);
            hpBgRT.anchorMax = new Vector2(1f, 0.22f);
            hpBgRT.offsetMin = new Vector2(3f, 0f);
            hpBgRT.offsetMax = new Vector2(-3f, 0f);

            var hpFillGO = MakeImage("HPFill", hpBgGO.transform, C_HP);
            var hpImg    = hpFillGO.GetComponent<Image>();
            if (uiSprite != null) hpImg.sprite = uiSprite;
            hpImg.type       = Image.Type.Filled;
            hpImg.fillMethod = Image.FillMethod.Horizontal;
            hpImg.fillAmount = 1f;
            FillParent(RT(hpFillGO));

            // Stamina bar BG
            var stBgGO = MakeImage("StaminaBarBG", slotGO.transform, C_StamBg);
            var stBgRT = RT(stBgGO);
            stBgRT.anchorMin = new Vector2(0f, 0.04f);
            stBgRT.anchorMax = new Vector2(1f, 0.12f);
            stBgRT.offsetMin = new Vector2(3f, 0f);
            stBgRT.offsetMax = new Vector2(-3f, 0f);

            var stFillGO = MakeImage("StaminaFill", stBgGO.transform, C_Stam);
            var stImg    = stFillGO.GetComponent<Image>();
            if (uiSprite != null) stImg.sprite = uiSprite;
            stImg.type       = Image.Type.Filled;
            stImg.fillMethod = Image.FillMethod.Horizontal;
            stImg.fillAmount = 1f;
            FillParent(RT(stFillGO));

            // Select overlay
            var selGO = MakeImage("SelectOverlay", slotGO.transform, new Color(0.22f, 0.58f, 0.40f, 0.45f));
            FillParent(RT(selGO));
            selGO.GetComponent<Image>().raycastTarget = false;
            selGO.SetActive(false);

            // Name plate + label
            var namePlateGO = MakeImage("NamePlate", slotGO.transform, new Color(0f, 0f, 0f, 0.65f));
            var nameRT  = RT(namePlateGO);
            nameRT.anchorMin = new Vector2(0f, 0.74f);
            nameRT.anchorMax = Vector2.one;
            nameRT.offsetMin = new Vector2(2f, 2f);
            nameRT.offsetMax = new Vector2(-2f, -2f);

            var nameGO = MakeRect("NameLabel", namePlateGO.transform);
            FillParent(RT(nameGO));
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text         = $"Unit {idx + 1}";
            nameTMP.fontSize     = 16f;
            nameTMP.fontStyle    = FontStyles.Bold;
            nameTMP.color        = C_Text;
            nameTMP.alignment    = TextAlignmentOptions.Center;
            nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
            nameTMP.overflowMode = TextOverflowModes.Truncate;
            nameTMP.raycastTarget = false;

            // Wire SquadPortraitSlot references
            var sso = new SerializedObject(slot);
            sso.FindProperty("portraitImage").objectReferenceValue = portGO.GetComponent<Image>();
            sso.FindProperty("hpFill").objectReferenceValue        = hpFillGO.GetComponent<Image>();
            sso.FindProperty("staminaFill").objectReferenceValue   = stFillGO.GetComponent<Image>();
            sso.FindProperty("nameLabel").objectReferenceValue     = nameTMP;
            sso.FindProperty("selectOverlay").objectReferenceValue = selGO.GetComponent<Image>();
            sso.FindProperty("slotButton").objectReferenceValue    = btn;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Squad Panel ───────────────────────────────────────────────────────

        static GameObject BuildSquadPanel(Transform parent)
        {
            var panel = MakePanel(parent, "SquadPanel");
            BuildPanelHeader(panel.transform, "SQUAD OVERVIEW");

            const float squadFontScale = 2f;

            var squadHeaderRT = panel.transform.Find("PanelHeader") as RectTransform;
            if (squadHeaderRT != null)
            {
                squadHeaderRT.sizeDelta = new Vector2(0f, 84f);
            }

            var squadHeaderTitle = panel.transform.Find("PanelHeader/Title")?.GetComponent<TextMeshProUGUI>();
            if (squadHeaderTitle != null)
            {
                squadHeaderTitle.fontSize = 24f * squadFontScale;
            }

            var body = MakeImage("Body", panel.transform, new Color(0.06f, 0.06f, 0.08f, 1f));
            var bodyRT = RT(body);
            FillParent(bodyRT);
            bodyRT.offsetMin = new Vector2(6f, 6f);
            bodyRT.offsetMax = new Vector2(-6f, -88f);

            var rosterBG = MakeImage("RosterBG", body.transform, new Color(0.08f, 0.09f, 0.11f, 1f));
            var rosterBGRT = RT(rosterBG);
            rosterBGRT.anchorMin = new Vector2(0f, 1f);
            rosterBGRT.anchorMax = new Vector2(1f, 1f);
            rosterBGRT.pivot = new Vector2(0.5f, 1f);
            rosterBGRT.offsetMin = new Vector2(0f, -176f);
            rosterBGRT.offsetMax = Vector2.zero;

            var rosterHeader = MakeRect("RosterHeader", rosterBG.transform);
            var rosterHeaderRT = RT(rosterHeader);
            rosterHeaderRT.anchorMin = new Vector2(0f, 1f);
            rosterHeaderRT.anchorMax = new Vector2(1f, 1f);
            rosterHeaderRT.pivot = new Vector2(0.5f, 1f);
            rosterHeaderRT.offsetMin = new Vector2(8f, -40f);
            rosterHeaderRT.offsetMax = new Vector2(-8f, 0f);
            var rosterHeaderHLG = rosterHeader.AddComponent<HorizontalLayoutGroup>();
            rosterHeaderHLG.spacing = 8f;
            rosterHeaderHLG.childAlignment = TextAnchor.MiddleLeft;
            rosterHeaderHLG.childControlWidth = true;
            rosterHeaderHLG.childControlHeight = true;
            rosterHeaderHLG.childForceExpandWidth = false;
            rosterHeaderHLG.childForceExpandHeight = false;
            rosterHeaderHLG.padding = new RectOffset(0, 0, 0, 0);

            var rosterLabelGO = MakeRect("RosterLabel", rosterHeader.transform);
            var rosterLabelLE = rosterLabelGO.AddComponent<LayoutElement>();
            rosterLabelLE.flexibleWidth = 0f;

            var rosterLabel = rosterLabelGO.AddComponent<TextMeshProUGUI>();
            rosterLabel.text = "ROSTER";
            rosterLabel.fontSize = 16f * squadFontScale;
            rosterLabel.fontStyle = FontStyles.Bold;
            rosterLabel.color = C_TextDim;
            rosterLabel.alignment = TextAlignmentOptions.MidlineLeft;
            rosterLabel.raycastTarget = false;

            Button[] rosterSquadPageTabs = BuildRosterHeaderSquadTabs(rosterHeader.transform, squadFontScale);

            var rosterStripRoot = MakeRect("RosterStrip", rosterBG.transform);
            var rosterStripRT = RT(rosterStripRoot);
            rosterStripRT.anchorMin = new Vector2(0f, 0f);
            rosterStripRT.anchorMax = new Vector2(1f, 1f);
            rosterStripRT.offsetMin = new Vector2(8f, 8f);
            rosterStripRT.offsetMax = new Vector2(-8f, -48f);
            var rosterHLG = rosterStripRoot.AddComponent<HorizontalLayoutGroup>();
            rosterHLG.spacing = 8f;
            rosterHLG.childAlignment = TextAnchor.MiddleLeft;
            rosterHLG.childControlWidth = true;
            rosterHLG.childControlHeight = true;
            rosterHLG.childForceExpandWidth = false;
            rosterHLG.childForceExpandHeight = false;
            rosterHLG.padding = new RectOffset(0, 0, 0, 0);

            for (int i = 0; i < 6; i++)
            {
                BuildInventoryMemberSlot(rosterStripRoot.transform, i, i == 0, 120f, "Loading...", true, 13f * squadFontScale);
            }

            var rosterStrip = rosterStripRoot.AddComponent<SquadPortraitStrip>();
            var rosterStripSO = new SerializedObject(rosterStrip);
            rosterStripSO.FindProperty("enableSquadTabs").boolValue = true;
            rosterStripSO.FindProperty("autoEnableTabsForBottomStrip").boolValue = false;
            rosterStripSO.FindProperty("squadTabCount").intValue = 4;
            rosterStripSO.FindProperty("slotsPerSquadTab").intValue = 5;
            rosterStripSO.FindProperty("squadTabActiveColor").colorValue = C_Accent;
            rosterStripSO.FindProperty("squadTabInactiveColor").colorValue = C_Btn;
            SerializedProperty rosterTabButtonsProperty = rosterStripSO.FindProperty("squadTabButtons");
            rosterTabButtonsProperty.arraySize = rosterSquadPageTabs.Length;
            for (int i = 0; i < rosterSquadPageTabs.Length; i++)
            {
                rosterTabButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = rosterSquadPageTabs[i];
            }
            rosterStripSO.ApplyModifiedPropertiesWithoutUndo();

            var details = MakeImage("DetailsArea", body.transform, new Color(0.05f, 0.06f, 0.08f, 1f));
            var detailsRT = RT(details);
            detailsRT.anchorMin = new Vector2(0f, 0f);
            detailsRT.anchorMax = new Vector2(1f, 1f);
            detailsRT.offsetMin = new Vector2(0f, 112f);
            detailsRT.offsetMax = new Vector2(0f, -184f);

            var summary = MakeImage("SummaryCard", details.transform, new Color(0.08f, 0.09f, 0.12f, 1f));
            var summaryRT = RT(summary);
            summaryRT.anchorMin = new Vector2(0f, 0f);
            summaryRT.anchorMax = new Vector2(0.34f, 1f);
            summaryRT.offsetMin = new Vector2(0f, 0f);
            summaryRT.offsetMax = new Vector2(-4f, 0f);

            var selectedName = MakeRect("SelectedName", summary.transform).AddComponent<TextMeshProUGUI>();
            var selectedNameRT = RT(selectedName.gameObject);
            selectedNameRT.anchorMin = new Vector2(0f, 1f);
            selectedNameRT.anchorMax = new Vector2(1f, 1f);
            selectedNameRT.pivot = new Vector2(0.5f, 1f);
            selectedNameRT.offsetMin = new Vector2(8f, -64f);
            selectedNameRT.offsetMax = new Vector2(-8f, -8f);
            selectedName.text = "No Unit Selected";
            selectedName.fontSize = 24f * squadFontScale;
            selectedName.fontStyle = FontStyles.Bold;
            selectedName.color = C_Text;
            selectedName.alignment = TextAlignmentOptions.MidlineLeft;
            selectedName.raycastTarget = false;

            var roleText = MakeRect("RoleText", summary.transform).AddComponent<TextMeshProUGUI>();
            var roleTextRT = RT(roleText.gameObject);
            roleTextRT.anchorMin = new Vector2(0f, 1f);
            roleTextRT.anchorMax = new Vector2(1f, 1f);
            roleTextRT.pivot = new Vector2(0.5f, 1f);
            roleTextRT.offsetMin = new Vector2(8f, -118f);
            roleTextRT.offsetMax = new Vector2(-8f, -70f);
            roleText.text = "Role: -";
            roleText.fontSize = 17f * squadFontScale;
            roleText.color = C_TextDim;
            roleText.alignment = TextAlignmentOptions.MidlineLeft;
            roleText.raycastTarget = false;

            var healthText = MakeRect("HealthText", summary.transform).AddComponent<TextMeshProUGUI>();
            var healthTextRT = RT(healthText.gameObject);
            healthTextRT.anchorMin = new Vector2(0f, 1f);
            healthTextRT.anchorMax = new Vector2(1f, 1f);
            healthTextRT.pivot = new Vector2(0.5f, 1f);
            healthTextRT.offsetMin = new Vector2(8f, -170f);
            healthTextRT.offsetMax = new Vector2(-8f, -122f);
            healthText.text = "Health: -";
            healthText.fontSize = 17f * squadFontScale;
            healthText.color = C_Text;
            healthText.alignment = TextAlignmentOptions.MidlineLeft;
            healthText.raycastTarget = false;

            var staminaText = MakeRect("StaminaText", summary.transform).AddComponent<TextMeshProUGUI>();
            var staminaTextRT = RT(staminaText.gameObject);
            staminaTextRT.anchorMin = new Vector2(0f, 1f);
            staminaTextRT.anchorMax = new Vector2(1f, 1f);
            staminaTextRT.pivot = new Vector2(0.5f, 1f);
            staminaTextRT.offsetMin = new Vector2(8f, -222f);
            staminaTextRT.offsetMax = new Vector2(-8f, -174f);
            staminaText.text = "Stamina: -";
            staminaText.fontSize = 17f * squadFontScale;
            staminaText.color = C_Text;
            staminaText.alignment = TextAlignmentOptions.MidlineLeft;
            staminaText.raycastTarget = false;

            var skills = MakeImage("SkillsCard", details.transform, new Color(0.07f, 0.08f, 0.11f, 1f));
            var skillsRT = RT(skills);
            skillsRT.anchorMin = new Vector2(0.34f, 0f);
            skillsRT.anchorMax = Vector2.one;
            skillsRT.offsetMin = new Vector2(4f, 0f);
            skillsRT.offsetMax = Vector2.zero;

            var skillsHeader = MakeRect("SkillsHeader", skills.transform).AddComponent<TextMeshProUGUI>();
            var skillsHeaderRT = RT(skillsHeader.gameObject);
            skillsHeaderRT.anchorMin = new Vector2(0f, 1f);
            skillsHeaderRT.anchorMax = new Vector2(1f, 1f);
            skillsHeaderRT.pivot = new Vector2(0.5f, 1f);
            skillsHeaderRT.offsetMin = new Vector2(8f, -44f);
            skillsHeaderRT.offsetMax = new Vector2(-8f, 0f);
            skillsHeader.text = "SKILLS";
            skillsHeader.fontSize = 16f * squadFontScale;
            skillsHeader.fontStyle = FontStyles.Bold;
            skillsHeader.color = C_TextDim;
            skillsHeader.alignment = TextAlignmentOptions.MidlineLeft;
            skillsHeader.raycastTarget = false;

            var statsGrid = MakeRect("StatsGrid", skills.transform);
            var statsGridRT = RT(statsGrid);
            statsGridRT.anchorMin = new Vector2(0f, 0f);
            statsGridRT.anchorMax = new Vector2(1f, 1f);
            statsGridRT.offsetMin = new Vector2(8f, 8f);
            statsGridRT.offsetMax = new Vector2(-8f, -52f);
            var statsGLG = statsGrid.AddComponent<GridLayoutGroup>();
            statsGLG.cellSize = new Vector2(252f, 62f);
            statsGLG.spacing = new Vector2(10f, 10f);
            statsGLG.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            statsGLG.constraintCount = 2;
            statsGLG.childAlignment = TextAnchor.UpperLeft;

            Button strengthButton;
            Button shootingButton;
            Button meleeButton;
            Button medicalButton;
            Button engineeringButton;
            Button toughnessButton;
            Button constitutionButton;
            Button agilityButton;
            Button enduranceButton;
            Button scavengingButton;
            Button stealthButton;

            TextMeshProUGUI strengthValue = BuildStatChip(statsGrid.transform, "Strength", squadFontScale, out strengthButton);
            TextMeshProUGUI shootingValue = BuildStatChip(statsGrid.transform, "Shooting", squadFontScale, out shootingButton);
            TextMeshProUGUI meleeValue = BuildStatChip(statsGrid.transform, "Melee", squadFontScale, out meleeButton);
            TextMeshProUGUI medicalValue = BuildStatChip(statsGrid.transform, "Medical", squadFontScale, out medicalButton);
            TextMeshProUGUI engineeringValue = BuildStatChip(statsGrid.transform, "Engineering", squadFontScale, out engineeringButton);
            TextMeshProUGUI toughnessValue = BuildStatChip(statsGrid.transform, "Toughness", squadFontScale, out toughnessButton);
            TextMeshProUGUI constitutionValue = BuildStatChip(statsGrid.transform, "Constitution", squadFontScale, out constitutionButton);
            TextMeshProUGUI agilityValue = BuildStatChip(statsGrid.transform, "Agility", squadFontScale, out agilityButton);
            TextMeshProUGUI enduranceValue = BuildStatChip(statsGrid.transform, "Endurance", squadFontScale, out enduranceButton);
            TextMeshProUGUI scavengingValue = BuildStatChip(statsGrid.transform, "Scavenging", squadFontScale, out scavengingButton);
            TextMeshProUGUI stealthValue = BuildStatChip(statsGrid.transform, "Stealth", squadFontScale, out stealthButton);

            var skillInfoModal = MakeImage("SkillInfoModal", skills.transform, new Color(0.03f, 0.04f, 0.06f, 0.98f));
            var skillInfoModalRT = RT(skillInfoModal);
            skillInfoModalRT.anchorMin = new Vector2(0.56f, 0.10f);
            skillInfoModalRT.anchorMax = new Vector2(0.99f, 0.90f);
            skillInfoModalRT.offsetMin = Vector2.zero;
            skillInfoModalRT.offsetMax = Vector2.zero;
            var modalOutline = skillInfoModal.AddComponent<Outline>();
            modalOutline.effectColor = new Color(0.30f, 0.35f, 0.40f, 0.95f);
            modalOutline.effectDistance = new Vector2(1f, -1f);

            var modalTitle = MakeRect("Title", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var modalTitleRT = RT(modalTitle.gameObject);
            modalTitleRT.anchorMin = new Vector2(0f, 1f);
            modalTitleRT.anchorMax = new Vector2(1f, 1f);
            modalTitleRT.pivot = new Vector2(0.5f, 1f);
            modalTitleRT.offsetMin = new Vector2(10f, -48f);
            modalTitleRT.offsetMax = new Vector2(-54f, -8f);
            modalTitle.text = "SKILL DETAILS";
            modalTitle.fontSize = 12f * squadFontScale;
            modalTitle.fontStyle = FontStyles.Bold;
            modalTitle.color = C_Text;
            modalTitle.alignment = TextAlignmentOptions.MidlineLeft;
            modalTitle.raycastTarget = false;

            var modalCloseGO = MakeImage("CloseButton", skillInfoModal.transform, C_Btn);
            var modalCloseRT = RT(modalCloseGO);
            modalCloseRT.anchorMin = new Vector2(1f, 1f);
            modalCloseRT.anchorMax = new Vector2(1f, 1f);
            modalCloseRT.pivot = new Vector2(1f, 1f);
            modalCloseRT.anchoredPosition = new Vector2(-8f, -8f);
            modalCloseRT.sizeDelta = new Vector2(40f, 36f);
            var modalCloseButton = modalCloseGO.AddComponent<Button>();
            modalCloseButton.targetGraphic = modalCloseGO.GetComponent<Image>();
            var modalCloseLabel = MakeRect("Label", modalCloseGO.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(modalCloseLabel.gameObject));
            modalCloseLabel.text = "X";
            modalCloseLabel.fontSize = 10f * squadFontScale;
            modalCloseLabel.fontStyle = FontStyles.Bold;
            modalCloseLabel.color = C_Text;
            modalCloseLabel.alignment = TextAlignmentOptions.Center;
            modalCloseLabel.raycastTarget = false;

            var modalLevel = MakeRect("Level", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var modalLevelRT = RT(modalLevel.gameObject);
            modalLevelRT.anchorMin = new Vector2(0f, 1f);
            modalLevelRT.anchorMax = new Vector2(1f, 1f);
            modalLevelRT.pivot = new Vector2(0.5f, 1f);
            modalLevelRT.offsetMin = new Vector2(10f, -88f);
            modalLevelRT.offsetMax = new Vector2(-10f, -52f);
            modalLevel.text = "Level: -";
            modalLevel.fontSize = 9f * squadFontScale;
            modalLevel.fontStyle = FontStyles.Bold;
            modalLevel.color = C_Text;
            modalLevel.alignment = TextAlignmentOptions.MidlineLeft;
            modalLevel.raycastTarget = false;

            var modalXp = MakeRect("Xp", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var modalXpRT = RT(modalXp.gameObject);
            modalXpRT.anchorMin = new Vector2(0f, 1f);
            modalXpRT.anchorMax = new Vector2(1f, 1f);
            modalXpRT.pivot = new Vector2(0.5f, 1f);
            modalXpRT.offsetMin = new Vector2(10f, -126f);
            modalXpRT.offsetMax = new Vector2(-10f, -90f);
            modalXp.text = "XP: -";
            modalXp.fontSize = 8f * squadFontScale;
            modalXp.color = C_TextDim;
            modalXp.alignment = TextAlignmentOptions.MidlineLeft;
            modalXp.raycastTarget = false;

            var howToHeader = MakeRect("HowToHeader", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var howToHeaderRT = RT(howToHeader.gameObject);
            howToHeaderRT.anchorMin = new Vector2(0f, 1f);
            howToHeaderRT.anchorMax = new Vector2(1f, 1f);
            howToHeaderRT.pivot = new Vector2(0.5f, 1f);
            howToHeaderRT.offsetMin = new Vector2(10f, -166f);
            howToHeaderRT.offsetMax = new Vector2(-10f, -132f);
            howToHeader.text = "HOW TO LEVEL";
            howToHeader.fontSize = 8f * squadFontScale;
            howToHeader.fontStyle = FontStyles.Bold;
            howToHeader.color = C_TextDim;
            howToHeader.alignment = TextAlignmentOptions.MidlineLeft;
            howToHeader.raycastTarget = false;

            var howToBody = MakeRect("HowToBody", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var howToBodyRT = RT(howToBody.gameObject);
            howToBodyRT.anchorMin = new Vector2(0f, 1f);
            howToBodyRT.anchorMax = new Vector2(1f, 1f);
            howToBodyRT.pivot = new Vector2(0.5f, 1f);
            howToBodyRT.offsetMin = new Vector2(10f, -258f);
            howToBodyRT.offsetMax = new Vector2(-10f, -168f);
            howToBody.text = "Select a skill to view progression details.";
            howToBody.fontSize = 8f * squadFontScale;
            howToBody.color = C_Text;
            howToBody.alignment = TextAlignmentOptions.TopLeft;
            howToBody.textWrappingMode = TMPro.TextWrappingModes.Normal;
            howToBody.overflowMode = TextOverflowModes.Ellipsis;
            howToBody.raycastTarget = false;

            var effectsHeader = MakeRect("EffectsHeader", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var effectsHeaderRT = RT(effectsHeader.gameObject);
            effectsHeaderRT.anchorMin = new Vector2(0f, 1f);
            effectsHeaderRT.anchorMax = new Vector2(1f, 1f);
            effectsHeaderRT.pivot = new Vector2(0.5f, 1f);
            effectsHeaderRT.offsetMin = new Vector2(10f, -294f);
            effectsHeaderRT.offsetMax = new Vector2(-10f, -262f);
            effectsHeader.text = "EFFECTS";
            effectsHeader.fontSize = 8f * squadFontScale;
            effectsHeader.fontStyle = FontStyles.Bold;
            effectsHeader.color = C_TextDim;
            effectsHeader.alignment = TextAlignmentOptions.MidlineLeft;
            effectsHeader.raycastTarget = false;

            var effectsBody = MakeRect("EffectsBody", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var effectsBodyRT = RT(effectsBody.gameObject);
            effectsBodyRT.anchorMin = new Vector2(0f, 0f);
            effectsBodyRT.anchorMax = new Vector2(1f, 1f);
            effectsBodyRT.offsetMin = new Vector2(10f, 10f);
            effectsBodyRT.offsetMax = new Vector2(-10f, -298f);
            effectsBody.text = "Click any skill tile to inspect what it changes in gameplay.";
            effectsBody.fontSize = 8f * squadFontScale;
            effectsBody.color = C_Text;
            effectsBody.alignment = TextAlignmentOptions.TopLeft;
            effectsBody.textWrappingMode = TMPro.TextWrappingModes.Normal;
            effectsBody.overflowMode = TextOverflowModes.Ellipsis;
            effectsBody.raycastTarget = false;

            skillInfoModal.SetActive(false);

            var bottom = MakeImage("BottomActions", body.transform, new Color(0.07f, 0.08f, 0.10f, 1f));
            var bottomRT = RT(bottom);
            bottomRT.anchorMin = Vector2.zero;
            bottomRT.anchorMax = new Vector2(1f, 0f);
            bottomRT.pivot = new Vector2(0.5f, 0f);
            bottomRT.anchoredPosition = Vector2.zero;
            bottomRT.sizeDelta = new Vector2(0f, 118f);
            var bottomHLG = bottom.AddComponent<HorizontalLayoutGroup>();
            bottomHLG.spacing = 6f;
            bottomHLG.childAlignment = TextAnchor.MiddleCenter;
            bottomHLG.childControlWidth = true;
            bottomHLG.childControlHeight = true;
            bottomHLG.childForceExpandWidth = true;
            bottomHLG.childForceExpandHeight = true;
            bottomHLG.padding = new RectOffset(10, 10, 10, 10);

            foreach (string action in new[] { "RALLY", "DEFEND", "SCOUT", "ASSIST" })
            {
                var actionGO = MakeImage($"Action_{action}", bottom.transform, C_Btn);
                actionGO.AddComponent<Button>().targetGraphic = actionGO.GetComponent<Image>();
                var actionTMP = MakeRect("Label", actionGO.transform).AddComponent<TextMeshProUGUI>();
                FillParent(RT(actionTMP.gameObject));
                actionTMP.text = action;
                actionTMP.fontSize = 16f * squadFontScale;
                actionTMP.fontStyle = FontStyles.Bold;
                actionTMP.color = C_Text;
                actionTMP.alignment = TextAlignmentOptions.Center;
                actionTMP.raycastTarget = false;
            }

            var squadStatsCtrl = panel.AddComponent<SquadTabUnitStatsController>();
            var squadStatsSO = new SerializedObject(squadStatsCtrl);
            squadStatsSO.FindProperty("rosterStrip").objectReferenceValue = rosterStrip;
            squadStatsSO.FindProperty("selectedNameText").objectReferenceValue = selectedName;
            squadStatsSO.FindProperty("roleText").objectReferenceValue = roleText;
            squadStatsSO.FindProperty("healthText").objectReferenceValue = healthText;
            squadStatsSO.FindProperty("staminaText").objectReferenceValue = staminaText;
            squadStatsSO.FindProperty("strengthValueText").objectReferenceValue = strengthValue;
            squadStatsSO.FindProperty("shootingValueText").objectReferenceValue = shootingValue;
            squadStatsSO.FindProperty("meleeValueText").objectReferenceValue = meleeValue;
            squadStatsSO.FindProperty("medicalValueText").objectReferenceValue = medicalValue;
            squadStatsSO.FindProperty("engineeringValueText").objectReferenceValue = engineeringValue;
            squadStatsSO.FindProperty("toughnessValueText").objectReferenceValue = toughnessValue;
            squadStatsSO.FindProperty("constitutionValueText").objectReferenceValue = constitutionValue;
            squadStatsSO.FindProperty("agilityValueText").objectReferenceValue = agilityValue;
            squadStatsSO.FindProperty("enduranceValueText").objectReferenceValue = enduranceValue;
            squadStatsSO.FindProperty("scavengingValueText").objectReferenceValue = scavengingValue;
            squadStatsSO.FindProperty("stealthValueText").objectReferenceValue = stealthValue;
            squadStatsSO.FindProperty("strengthButton").objectReferenceValue = strengthButton;
            squadStatsSO.FindProperty("shootingButton").objectReferenceValue = shootingButton;
            squadStatsSO.FindProperty("meleeButton").objectReferenceValue = meleeButton;
            squadStatsSO.FindProperty("medicalButton").objectReferenceValue = medicalButton;
            squadStatsSO.FindProperty("engineeringButton").objectReferenceValue = engineeringButton;
            squadStatsSO.FindProperty("toughnessButton").objectReferenceValue = toughnessButton;
            squadStatsSO.FindProperty("constitutionButton").objectReferenceValue = constitutionButton;
            squadStatsSO.FindProperty("agilityButton").objectReferenceValue = agilityButton;
            squadStatsSO.FindProperty("enduranceButton").objectReferenceValue = enduranceButton;
            squadStatsSO.FindProperty("scavengingButton").objectReferenceValue = scavengingButton;
            squadStatsSO.FindProperty("stealthButton").objectReferenceValue = stealthButton;
            squadStatsSO.FindProperty("skillInfoModalRoot").objectReferenceValue = skillInfoModal;
            squadStatsSO.FindProperty("skillInfoCloseButton").objectReferenceValue = modalCloseButton;
            squadStatsSO.FindProperty("skillInfoTitleText").objectReferenceValue = modalTitle;
            squadStatsSO.FindProperty("skillInfoLevelText").objectReferenceValue = modalLevel;
            squadStatsSO.FindProperty("skillInfoXpText").objectReferenceValue = modalXp;
            squadStatsSO.FindProperty("skillInfoHowToLevelText").objectReferenceValue = howToBody;
            squadStatsSO.FindProperty("skillInfoEffectsText").objectReferenceValue = effectsBody;
            squadStatsSO.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        static TextMeshProUGUI BuildStatChip(Transform parent, string label, float fontScale, out Button chipButton)
        {
            var chip = MakeImage($"Stat_{label}", parent, new Color(0.10f, 0.11f, 0.14f, 1f));
            chipButton = chip.AddComponent<Button>();
            chipButton.targetGraphic = chip.GetComponent<Image>();
            var chipColors = chipButton.colors;
            chipColors.normalColor = Color.white;
            chipColors.highlightedColor = new Color(0.86f, 0.96f, 0.90f, 1f);
            chipColors.pressedColor = new Color(0.74f, 0.88f, 0.80f, 1f);
            chipColors.selectedColor = chipColors.highlightedColor;
            chipColors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.85f);
            chipColors.colorMultiplier = 1f;
            chipColors.fadeDuration = 0.08f;
            chipButton.colors = chipColors;

            var chipHLG = chip.AddComponent<HorizontalLayoutGroup>();
            chipHLG.spacing = 4f * fontScale;
            chipHLG.childAlignment = TextAnchor.MiddleLeft;
            chipHLG.childControlWidth = true;
            chipHLG.childControlHeight = true;
            chipHLG.childForceExpandWidth = false;
            chipHLG.childForceExpandHeight = true;
            chipHLG.padding = new RectOffset(8, 8, 6, 6);

            var labelTMP = MakeRect("Label", chip.transform).AddComponent<TextMeshProUGUI>();
            labelTMP.text = label;
            labelTMP.fontSize = 15f * fontScale;
            labelTMP.color = C_TextDim;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            labelTMP.raycastTarget = false;
            var labelLE = labelTMP.gameObject.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1f;

            var valueTMP = MakeRect("Value", chip.transform).AddComponent<TextMeshProUGUI>();
            valueTMP.text = "-";
            valueTMP.fontSize = 15f * fontScale;
            valueTMP.fontStyle = FontStyles.Bold;
            valueTMP.color = C_Text;
            valueTMP.alignment = TextAlignmentOptions.MidlineRight;
            valueTMP.raycastTarget = false;
            var valueLE = valueTMP.gameObject.AddComponent<LayoutElement>();
            valueLE.minWidth = 64f;

            return valueTMP;
        }

        static void BuildMemberRow(Transform parent, string unitName, float hpFrac, bool selected)
        {
            var row   = MakeImage($"Row_{parent.childCount}", parent,
                selected ? new Color(0.14f, 0.28f, 0.20f, 1f)
                         : new Color(0.10f, 0.11f, 0.13f, 1f));
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.minHeight = 54f; rowLE.preferredHeight = 54f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing             = 6f;
            hlg.childAlignment      = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(6, 6, 4, 4);

            // Mini portrait
            var portGO = MakeImage("Port", row.transform, new Color(0.18f, 0.18f, 0.22f, 1f));
            var portLE = portGO.AddComponent<LayoutElement>();
            portLE.minWidth = 44f; portLE.preferredWidth = 44f;
            portLE.minHeight = 44f; portLE.preferredHeight = 44f;
            portLE.flexibleWidth = 0f;

            // Info column
            var infoGO = MakeRect("Info", row.transform);
            infoGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var infoVLG = infoGO.AddComponent<VerticalLayoutGroup>();
            infoVLG.spacing             = 3f;
            infoVLG.childForceExpandWidth  = true;
            infoVLG.childForceExpandHeight = false;

            var nameTMP = MakeRect("Name", infoGO.transform).AddComponent<TextMeshProUGUI>();
            nameTMP.text     = unitName;
            nameTMP.fontSize = 16f;
            nameTMP.color    = C_Text;

            var hpBg = MakeImage("HPBg", infoGO.transform, C_HPBg);
            hpBg.AddComponent<LayoutElement>().minHeight = 6f;
            var fill = MakeImage("HPFill", hpBg.transform, C_HP);
            Sprite spr = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var fillImg = fill.GetComponent<Image>();
            if (spr != null) fillImg.sprite = spr;
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = hpFrac;
            FillParent(RT(fill));

            // Role icon — Image on root, TMP on child (two Graphic components can't share one GO)
            var roleGO = MakeImage("RoleIcon", row.transform, C_Btn);
            var roleLE = roleGO.AddComponent<LayoutElement>();
            roleLE.minWidth  = 30f; roleLE.preferredWidth  = 30f;
            roleLE.flexibleWidth = 0f;
            var roleLblGO = MakeRect("Label", roleGO.transform);
            FillParent(RT(roleLblGO));
            var roleTxt = roleLblGO.AddComponent<TextMeshProUGUI>();
            roleTxt.text      = "⚔";
            roleTxt.fontSize  = 18f;
            roleTxt.color     = C_Accent;
            roleTxt.alignment = TextAlignmentOptions.Center;
            roleTxt.raycastTarget = false;
        }

        static void BuildFormationSection(Transform parent)
        {
            var sec = MakeImage("FormationSection", parent, new Color(0.06f, 0.06f, 0.08f, 1f));
            var rt  = RT(sec);
            rt.anchorMin = new Vector2(0f, 0.45f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 8f);
            rt.offsetMax = new Vector2(-8f, -8f);

            var hdr = MakeRect("Label", sec.transform).AddComponent<TextMeshProUGUI>();
            hdr.text = "FORMATION"; hdr.fontSize = 11f; hdr.color = C_TextDim;
            var hdrRT = RT(hdr.gameObject);
            hdrRT.anchorMin = new Vector2(0f, 0.87f); hdrRT.anchorMax = Vector2.one;
            hdrRT.offsetMin = new Vector2(6f, 0f);    hdrRT.offsetMax = Vector2.zero;
            hdr.raycastTarget = false;

            var grid = MakeRect("FormationGrid", sec.transform);
            var gridRT = RT(grid);
            gridRT.anchorMin = new Vector2(0.05f, 0.04f);
            gridRT.anchorMax = new Vector2(0.95f, 0.85f);
            gridRT.offsetMin = Vector2.zero; gridRT.offsetMax = Vector2.zero;
            var glg = grid.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(44f, 44f);
            glg.spacing         = new Vector2(6f, 6f);
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
            glg.childAlignment  = TextAnchor.MiddleCenter;

            for (int i = 0; i < 6; i++)
            {
                bool occupied = i < 3;
                var s = MakeImage($"FSlot_{i}", grid.transform,
                    occupied ? new Color(0.14f, 0.16f, 0.20f, 1f)
                             : new Color(0.10f, 0.11f, 0.13f, 1f));
                s.AddComponent<Button>().targetGraphic = s.GetComponent<Image>();
                var t = MakeRect("Dot", s.transform).AddComponent<TextMeshProUGUI>();
                FillParent(RT(t.gameObject));
                t.text = occupied ? "⬤" : "○";
                t.fontSize = 16f;
                t.color = occupied ? C_Accent : C_TextDim;
                t.alignment = TextAlignmentOptions.Center;
                t.raycastTarget = false;
            }
        }

        static void BuildBehaviorSection(Transform parent)
        {
            var sec = MakeImage("BehaviorSection", parent, new Color(0.06f, 0.06f, 0.08f, 1f));
            var rt  = RT(sec);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0.44f);
            rt.offsetMin = new Vector2(8f, 8f);
            rt.offsetMax = new Vector2(-8f, -4f);

            var hdr = MakeRect("Label", sec.transform).AddComponent<TextMeshProUGUI>();
            hdr.text = "BEHAVIOR"; hdr.fontSize = 11f; hdr.color = C_TextDim;
            var hdrRT = RT(hdr.gameObject);
            hdrRT.anchorMin = new Vector2(0f, 0.73f); hdrRT.anchorMax = Vector2.one;
            hdrRT.offsetMin = new Vector2(6f, 0f);    hdrRT.offsetMax = Vector2.zero;
            hdr.raycastTarget = false;

            // Stance row
            var row1 = MakeRect("StanceRow", sec.transform);
            var r1RT = RT(row1);
            r1RT.anchorMin = new Vector2(0f, 0.38f); r1RT.anchorMax = new Vector2(1f, 0.71f);
            r1RT.offsetMin = new Vector2(6f, 0f);    r1RT.offsetMax = new Vector2(-6f, 0f);
            var r1HLG = row1.AddComponent<HorizontalLayoutGroup>();
            r1HLG.spacing = 4f; r1HLG.childForceExpandWidth = true; r1HLG.childForceExpandHeight = true;

            string[] stances = { "AGGRO", "DEFEND", "PASSIVE" };
            Color[]  sCols   = {
                new Color(0.52f, 0.13f, 0.09f, 1f),
                new Color(0.10f, 0.23f, 0.46f, 1f),
                C_Btn
            };
            for (int i = 0; i < stances.Length; i++)
            {
                var b = MakeImage($"Stance_{stances[i]}", row1.transform, sCols[i]);
                b.AddComponent<Button>().targetGraphic = b.GetComponent<Image>();
                var t = MakeRect("Label", b.transform).AddComponent<TextMeshProUGUI>();
                FillParent(RT(t.gameObject));
                t.text = stances[i]; t.fontSize = 11f; t.color = C_Text;
                t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            }

            // Order row
            var row2 = MakeRect("OrderRow", sec.transform);
            var r2RT = RT(row2);
            r2RT.anchorMin = new Vector2(0f, 0.03f); r2RT.anchorMax = new Vector2(1f, 0.36f);
            r2RT.offsetMin = new Vector2(6f, 0f);    r2RT.offsetMax = new Vector2(-6f, 0f);
            var r2HLG = row2.AddComponent<HorizontalLayoutGroup>();
            r2HLG.spacing = 4f; r2HLG.childForceExpandWidth = true; r2HLG.childForceExpandHeight = true;

            foreach (string order in new[] { "HOLD", "FOLLOW", "SPREAD" })
            {
                var b = MakeImage($"Order_{order}", row2.transform, C_Btn);
                b.AddComponent<Button>().targetGraphic = b.GetComponent<Image>();
                var t = MakeRect("Label", b.transform).AddComponent<TextMeshProUGUI>();
                FillParent(RT(t.gameObject));
                t.text = order; t.fontSize = 11f; t.color = C_Text;
                t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            }
        }

        // ── Inventory Panel ───────────────────────────────────────────────────

        static GameObject BuildInventoryPanel(Transform parent)
        {
            var panel = MakePanel(parent, "InventoryPanel");
            BuildPanelHeader(panel.transform, "INVENTORY");

            var panelHeader = panel.transform.Find("PanelHeader") as RectTransform;
            if (panelHeader != null)
            {
                panelHeader.gameObject.SetActive(false);
            }

            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.06f, 0.06f, 0.08f, 1f);
            }

            var panelTitle = panel.transform.Find("PanelHeader/Title")?.GetComponent<TextMeshProUGUI>();
            if (panelTitle != null)
            {
                panelTitle.gameObject.SetActive(false);
            }
            var headerStrip = default(SquadPortraitStrip);

            // Inventory grid area (left ~62 %)
            var gridArea = MakeImage("InventoryGrid", panel.transform, new Color(0.06f, 0.06f, 0.08f, 1f));
            var gridAreaRT = RT(gridArea);
            gridAreaRT.anchorMin = new Vector2(0f, 0f);
            gridAreaRT.anchorMax = new Vector2(0.62f, 1f);
            gridAreaRT.offsetMin = new Vector2(4f, 4f);
            gridAreaRT.offsetMax = new Vector2(-3f, -8f);

            var gridScroll = gridArea.AddComponent<ScrollRect>();
            gridScroll.horizontal = false;
            gridScroll.vertical = true;
            gridScroll.scrollSensitivity = 24f;
            gridScroll.movementType = ScrollRect.MovementType.Clamped;

            var controls = MakeRect("InventoryControls", gridArea.transform);
            var controlsRT = RT(controls);
            controlsRT.anchorMin = new Vector2(0f, 1f);
            controlsRT.anchorMax = new Vector2(1f, 1f);
            controlsRT.pivot = new Vector2(0.5f, 1f);
            controlsRT.offsetMin = new Vector2(6f, -208f);
            controlsRT.offsetMax = new Vector2(-6f, -164f);
            var controlsHLG = controls.AddComponent<HorizontalLayoutGroup>();
            controlsHLG.spacing = 4f;
            controlsHLG.childAlignment = TextAnchor.MiddleLeft;
            controlsHLG.childControlWidth = true;
            controlsHLG.childControlHeight = true;
            controlsHLG.childForceExpandWidth = false;
            controlsHLG.childForceExpandHeight = true;

            var searchGO = MakeImage("SearchBar", controls.transform, new Color(0.10f, 0.11f, 0.14f, 1f));
            var searchLE = searchGO.AddComponent<LayoutElement>();
            searchLE.flexibleWidth = 2f;
            searchLE.minHeight = 42f;
            searchLE.preferredHeight = 42f;
            var searchOutline = searchGO.AddComponent<Outline>();
            searchOutline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.95f);
            searchOutline.effectDistance = new Vector2(1f, -1f);
            var searchInput = searchGO.AddComponent<TMP_InputField>();

            var searchViewport = MakeRect("TextViewport", searchGO.transform);
            var searchViewportRT = RT(searchViewport);
            FillParent(searchViewportRT);
            searchViewportRT.offsetMin = new Vector2(10f, 6f);
            searchViewportRT.offsetMax = new Vector2(-10f, -6f);
            searchViewport.AddComponent<RectMask2D>();

            var searchPlaceholder = MakeRect("Placeholder", searchViewport.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(searchPlaceholder.gameObject));
            searchPlaceholder.text = "Search items...";
            searchPlaceholder.fontSize = 14f;
            searchPlaceholder.color = new Color(0.50f, 0.48f, 0.42f, 0.78f);
            searchPlaceholder.alignment = TextAlignmentOptions.MidlineLeft;
            searchPlaceholder.raycastTarget = false;

            var searchText = MakeRect("Text", searchViewport.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(searchText.gameObject));
            searchText.text = string.Empty;
            searchText.fontSize = 14f;
            searchText.color = C_Text;
            searchText.alignment = TextAlignmentOptions.MidlineLeft;
            searchText.raycastTarget = false;

            searchInput.textViewport = searchViewportRT;
            searchInput.textComponent = searchText;
            searchInput.placeholder = searchPlaceholder;
            searchInput.lineType = TMP_InputField.LineType.SingleLine;

            var filterGO = MakeImage("FilterButton", controls.transform, C_Btn);
            var filterLE = filterGO.AddComponent<LayoutElement>();
            filterLE.minWidth = 132f;
            filterLE.preferredWidth = 132f;
            filterLE.minHeight = 42f;
            filterLE.preferredHeight = 42f;
            var filterBtn = filterGO.AddComponent<Button>();
            filterBtn.targetGraphic = filterGO.GetComponent<Image>();
            var filterLabel = MakeRect("Label", filterGO.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(filterLabel.gameObject));
            filterLabel.text = "FILTER: ALL";
            filterLabel.fontSize = 14f;
            filterLabel.fontStyle = FontStyles.Bold;
            filterLabel.color = C_Text;
            filterLabel.alignment = TextAlignmentOptions.Center;
            filterLabel.raycastTarget = false;

            var selectorRow = MakeRect("HeaderCharacterSelector", gridArea.transform).transform;
            var selectorRowRT = RT(selectorRow.gameObject);
            selectorRowRT.anchorMin = new Vector2(0f, 1f);
            selectorRowRT.anchorMax = new Vector2(1f, 1f);
            selectorRowRT.pivot = new Vector2(0.5f, 1f);
            selectorRowRT.offsetMin = new Vector2(6f, -160f);
            selectorRowRT.offsetMax = new Vector2(-6f, 0f);

            var selectorHLG = selectorRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            selectorHLG.spacing = 4f;
            selectorHLG.childAlignment = TextAnchor.MiddleLeft;
            selectorHLG.childControlWidth = true;
            selectorHLG.childControlHeight = true;
            selectorHLG.childForceExpandWidth = false;
            selectorHLG.childForceExpandHeight = false;
            selectorHLG.padding = new RectOffset(0, 0, 0, 0);

            for (int i = 0; i < 6; i++)
            {
                BuildInventoryMemberSlot(selectorRow, i, i == 0, 150f, null, true);
            }

            headerStrip = selectorRow.gameObject.AddComponent<SquadPortraitStrip>();

            var squadBtnGO = MakeImage("OpenSquadRosterButton", gridArea.transform, C_Btn);
            var squadBtnRT = RT(squadBtnGO);
            squadBtnRT.anchorMin = new Vector2(0f, 1f);
            squadBtnRT.anchorMax = new Vector2(0f, 1f);
            squadBtnRT.pivot = new Vector2(0f, 1f);
            squadBtnRT.anchoredPosition = new Vector2(6f, -214f);
            squadBtnRT.sizeDelta = new Vector2(220f, 30f);
            var squadBtn = squadBtnGO.AddComponent<Button>();
            squadBtn.targetGraphic = squadBtnGO.GetComponent<Image>();
            var squadBtnLabel = MakeRect("Label", squadBtnGO.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(squadBtnLabel.gameObject));
            squadBtnLabel.text = "SQUAD ROSTER";
            squadBtnLabel.fontSize = 12f;
            squadBtnLabel.fontStyle = FontStyles.Bold;
            squadBtnLabel.color = C_Text;
            squadBtnLabel.alignment = TextAlignmentOptions.Center;
            squadBtnLabel.raycastTarget = false;

            var editingLabel = MakeRect("EditingUnitLabel", gridArea.transform).AddComponent<TextMeshProUGUI>();
            var editingLabelRT = RT(editingLabel.gameObject);
            editingLabelRT.anchorMin = new Vector2(0f, 1f);
            editingLabelRT.anchorMax = new Vector2(1f, 1f);
            editingLabelRT.pivot = new Vector2(0.5f, 1f);
            editingLabelRT.offsetMin = new Vector2(236f, -244f);
            editingLabelRT.offsetMax = new Vector2(-6f, -214f);
            editingLabel.text = "Editing: None";
            editingLabel.fontSize = 12f;
            editingLabel.fontStyle = FontStyles.Bold;
            editingLabel.color = C_TextDim;
            editingLabel.alignment = TextAlignmentOptions.MidlineLeft;
            editingLabel.raycastTarget = false;

            const float invGap = 4f;
            const int visibleRows = 5;
            const float viewportTopInset = 252f;

            var viewport = MakeRect("Viewport", gridArea.transform);
            var viewportRT = RT(viewport);
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = new Vector2(6f, 6f);
            viewportRT.offsetMax = new Vector2(-6f, -viewportTopInset);
            viewport.AddComponent<RectMask2D>();
            gridScroll.viewport = viewportRT;

            var slotGrid = MakeRect("SlotGrid", viewport.transform);
            var slotRT   = RT(slotGrid);
            slotRT.anchorMin = new Vector2(0f, 1f);
            slotRT.anchorMax = new Vector2(1f, 1f);
            slotRT.pivot = new Vector2(0.5f, 1f);
            slotRT.offsetMin = Vector2.zero;
            slotRT.offsetMax = Vector2.zero;

            var glg = slotGrid.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(40f, 40f);
            glg.spacing         = new Vector2(invGap, invGap);
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 10;
            glg.childAlignment  = TextAnchor.UpperLeft;
            glg.padding         = new RectOffset(0, 0, 0, 0);

            gridScroll.content = slotRT;

            for (int i = 0; i < 300; i++)
            {
                var s = MakeImage($"InvSlot_{i}", slotGrid.transform, new Color(0.11f, 0.11f, 0.14f, 1f));
                s.AddComponent<Button>().targetGraphic = s.GetComponent<Image>();
            }

            var gridFitter = slotGrid.AddComponent<InventoryGridViewportFitter>();
            gridFitter.Configure(viewportRT, slotRT, glg, 10, visibleRows);

            var squadModal = MakeImage("SquadRosterModal", panel.transform, new Color(0f, 0f, 0f, 0.72f));
            FillParent(RT(squadModal));
            var modalBackdrop = squadModal.AddComponent<Button>();
            modalBackdrop.targetGraphic = squadModal.GetComponent<Image>();

            var modalCard = MakeImage("Card", squadModal.transform, new Color(0.07f, 0.08f, 0.11f, 0.98f));
            var modalCardRT = RT(modalCard);
            modalCardRT.anchorMin = new Vector2(0.5f, 0.5f);
            modalCardRT.anchorMax = new Vector2(0.5f, 0.5f);
            modalCardRT.pivot = new Vector2(0.5f, 0.5f);
            modalCardRT.sizeDelta = new Vector2(640f, 360f);
            var modalOutline = modalCard.AddComponent<Outline>();
            modalOutline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.95f);
            modalOutline.effectDistance = new Vector2(1f, -1f);

            var modalHdr = MakeRect("Header", modalCard.transform).AddComponent<TextMeshProUGUI>();
            var modalHdrRT = RT(modalHdr.gameObject);
            modalHdrRT.anchorMin = new Vector2(0f, 1f);
            modalHdrRT.anchorMax = Vector2.one;
            modalHdrRT.pivot = new Vector2(0.5f, 1f);
            modalHdrRT.offsetMin = new Vector2(14f, -40f);
            modalHdrRT.offsetMax = new Vector2(-52f, -8f);
            modalHdr.text = "SQUAD CHARACTERS";
            modalHdr.fontSize = 18f;
            modalHdr.fontStyle = FontStyles.Bold;
            modalHdr.color = C_Text;
            modalHdr.alignment = TextAlignmentOptions.MidlineLeft;
            modalHdr.raycastTarget = false;

            var closeBtnGO = MakeImage("CloseButton", modalCard.transform, C_Btn);
            var closeBtnRT = RT(closeBtnGO);
            closeBtnRT.anchorMin = new Vector2(1f, 1f);
            closeBtnRT.anchorMax = new Vector2(1f, 1f);
            closeBtnRT.pivot = new Vector2(1f, 1f);
            closeBtnRT.anchoredPosition = new Vector2(-10f, -10f);
            closeBtnRT.sizeDelta = new Vector2(34f, 30f);
            var closeBtn = closeBtnGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnGO.GetComponent<Image>();
            var closeLbl = MakeRect("Label", closeBtnGO.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(closeLbl.gameObject));
            closeLbl.text = "X";
            closeLbl.fontSize = 16f;
            closeLbl.fontStyle = FontStyles.Bold;
            closeLbl.color = C_Text;
            closeLbl.alignment = TextAlignmentOptions.Center;
            closeLbl.raycastTarget = false;

            var modalGrid = MakeRect("RosterGrid", modalCard.transform);
            var modalGridRT = RT(modalGrid);
            modalGridRT.anchorMin = new Vector2(0.5f, 0.5f);
            modalGridRT.anchorMax = new Vector2(0.5f, 0.5f);
            modalGridRT.pivot = new Vector2(0.5f, 0.5f);
            modalGridRT.anchoredPosition = new Vector2(0f, -8f);
            modalGridRT.sizeDelta = new Vector2(560f, 250f);
            var modalGLG = modalGrid.AddComponent<GridLayoutGroup>();
            modalGLG.cellSize = new Vector2(168f, 110f);
            modalGLG.spacing = new Vector2(12f, 12f);
            modalGLG.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            modalGLG.constraintCount = 3;
            modalGLG.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < 6; i++)
            {
                BuildInventoryMemberSlot(modalGrid.transform, i, i == 0, 110f, "Loading...", true);
            }

            var modalStrip = modalGrid.AddComponent<SquadPortraitStrip>();

            squadModal.SetActive(false);

            var modalCtrl = panel.AddComponent<SquadRosterModalController>();
            var modalSO = new SerializedObject(modalCtrl);
            modalSO.FindProperty("modalRoot").objectReferenceValue = squadModal;
            modalSO.FindProperty("openButton").objectReferenceValue = squadBtn;
            modalSO.FindProperty("closeButton").objectReferenceValue = closeBtn;
            modalSO.FindProperty("backdropButton").objectReferenceValue = modalBackdrop;
            modalSO.FindProperty("modalPortraitStrip").objectReferenceValue = modalStrip;
            modalSO.ApplyModifiedPropertiesWithoutUndo();

            var editCtrl = panel.AddComponent<InventoryEditTargetController>();
            var editSO = new SerializedObject(editCtrl);
            editSO.FindProperty("headerPortraitStrip").objectReferenceValue = headerStrip;
            editSO.FindProperty("modalPortraitStrip").objectReferenceValue = modalStrip;
            editSO.FindProperty("editingUnitLabel").objectReferenceValue = editingLabel;
            editSO.ApplyModifiedPropertiesWithoutUndo();

            var inventoryCtrl = panel.AddComponent<InventoryPanelController>();
            var inventorySO = new SerializedObject(inventoryCtrl);
            inventorySO.FindProperty("editTargetController").objectReferenceValue = editCtrl;
            inventorySO.FindProperty("searchInputField").objectReferenceValue = searchInput;
            inventorySO.FindProperty("filterButton").objectReferenceValue = filterBtn;
            inventorySO.FindProperty("filterLabel").objectReferenceValue = filterLabel;
            inventorySO.FindProperty("slotGrid").objectReferenceValue = slotGrid.transform;
            inventorySO.ApplyModifiedPropertiesWithoutUndo();

            // Equipment panel (right ~38 %)
            var equipGO = MakeImage("EquipmentPanel", panel.transform, new Color(0.06f, 0.06f, 0.08f, 1f));
            var equipRT = RT(equipGO);
            equipRT.anchorMin = new Vector2(0.62f, 0f);
            equipRT.anchorMax = Vector2.one;
            equipRT.offsetMin = new Vector2(3f, 4f);
            equipRT.offsetMax = new Vector2(-4f, -96f);
            BuildEquipmentSlots(equipGO.transform);

            squadModal.transform.SetAsLastSibling();

            return panel;
        }

        static void BuildInventoryMemberSlot(
            Transform parent,
            int idx,
            bool selected,
            float slotSize = 58f,
            string label = null,
            bool bindToSquadSlot = false,
            float nameFontSizeOverride = 0f)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var slotGO = MakeImage($"MemberPort_{idx}", parent,
                selected ? new Color(0.15f, 0.30f, 0.20f, 1f) : C_Port);
            var slotRT = RT(slotGO);
            slotRT.sizeDelta = new Vector2(slotSize, slotSize);

            var le = slotGO.AddComponent<LayoutElement>();
            le.minWidth = slotSize;
            le.preferredWidth = slotSize;
            le.minHeight = slotSize;
            le.preferredHeight = slotSize;
            le.flexibleWidth = 0f;

            var outline = slotGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            var btn = slotGO.AddComponent<Button>();
            btn.targetGraphic = slotGO.GetComponent<Image>();

            var portGO = MakeImage("PortraitImage", slotGO.transform, new Color(0.20f, 0.20f, 0.25f, 1f));
            var portRT = RT(portGO);
            portRT.anchorMin = new Vector2(0f, 0.24f);
            portRT.anchorMax = new Vector2(1f, 0.76f);
            portRT.offsetMin = new Vector2(2f, 2f);
            portRT.offsetMax = new Vector2(-2f, -2f);

            var hpBgGO = MakeImage("HPBarBG", slotGO.transform, C_HPBg);
            var hpBgRT = RT(hpBgGO);
            hpBgRT.anchorMin = new Vector2(0f, 0.14f);
            hpBgRT.anchorMax = new Vector2(1f, 0.22f);
            hpBgRT.offsetMin = new Vector2(2f, 0f);
            hpBgRT.offsetMax = new Vector2(-2f, 0f);

            var hpFillGO = MakeImage("HPFill", hpBgGO.transform, C_HP);
            var hpImg = hpFillGO.GetComponent<Image>();
            if (uiSprite != null) hpImg.sprite = uiSprite;
            hpImg.type = Image.Type.Filled;
            hpImg.fillMethod = Image.FillMethod.Horizontal;
            hpImg.fillAmount = 1f;
            FillParent(RT(hpFillGO));

            var stBgGO = MakeImage("StaminaBarBG", slotGO.transform, C_StamBg);
            var stBgRT = RT(stBgGO);
            stBgRT.anchorMin = new Vector2(0f, 0.04f);
            stBgRT.anchorMax = new Vector2(1f, 0.12f);
            stBgRT.offsetMin = new Vector2(2f, 0f);
            stBgRT.offsetMax = new Vector2(-2f, 0f);

            var stFillGO = MakeImage("StaminaFill", stBgGO.transform, C_Stam);
            var stImg = stFillGO.GetComponent<Image>();
            if (uiSprite != null) stImg.sprite = uiSprite;
            stImg.type = Image.Type.Filled;
            stImg.fillMethod = Image.FillMethod.Horizontal;
            stImg.fillAmount = 1f;
            FillParent(RT(stFillGO));

            var selGO = MakeImage("SelectOverlay", slotGO.transform, new Color(0.22f, 0.58f, 0.40f, 0.45f));
            FillParent(RT(selGO));
            selGO.GetComponent<Image>().raycastTarget = false;
            selGO.SetActive(selected);

            var namePlateGO = MakeImage("NamePlate", slotGO.transform, new Color(0f, 0f, 0f, 0.65f));
            var nameRT = RT(namePlateGO);
            nameRT.anchorMin = new Vector2(0f, 0.76f);
            nameRT.anchorMax = Vector2.one;
            nameRT.offsetMin = new Vector2(1f, 1f);
            nameRT.offsetMax = new Vector2(-1f, -1f);

            var nameGO = MakeRect("NameLabel", namePlateGO.transform);
            FillParent(RT(nameGO));
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text = string.IsNullOrEmpty(label) ? (idx + 1).ToString() : label;
            nameTMP.fontSize = nameFontSizeOverride > 0f ? nameFontSizeOverride : (slotSize >= 90f ? 14f : 9f);
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color = C_Text;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
            nameTMP.overflowMode = TextOverflowModes.Truncate;
            nameTMP.raycastTarget = false;

            if (bindToSquadSlot)
            {
                var slot = slotGO.AddComponent<SquadPortraitSlot>();
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

        static void BuildEquipmentSlots(Transform parent)
        {
            var area = MakeImage("EquipmentLayoutArea", parent, new Color(0.04f, 0.05f, 0.07f, 0.98f));
            var areaRT = RT(area);
            areaRT.anchorMin = Vector2.zero;
            areaRT.anchorMax = Vector2.one;
            areaRT.offsetMin = new Vector2(4f, 4f);
            areaRT.offsetMax = new Vector2(-4f, -4f);
            var areaOutline = area.AddComponent<Outline>();
            areaOutline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.95f);
            areaOutline.effectDistance = new Vector2(1f, -1f);

            BuildEquipmentSilhouette(area.transform);

            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Head,      "HEAD",       "Helmet",          new Vector2(0.46f, 0.94f), new Vector2(164f, 60f), false);
            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Face,      "FACE",       "Mask / Glasses",  new Vector2(0.46f, 0.84f), new Vector2(168f, 58f), false);
            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Chest,     "CHEST",      "Armor / Jacket",  new Vector2(0.46f, 0.72f), new Vector2(220f, 76f), true);
            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Back,      "BACK",       "Backpack",        new Vector2(0.46f, 0.60f), new Vector2(210f, 70f), true);
            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.LeftHand,  "LEFT HAND",  "Weapon",          new Vector2(0.18f, 0.56f), new Vector2(178f, 66f), false);
            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.RightHand, "RIGHT HAND", "Weapon",          new Vector2(0.72f, 0.56f), new Vector2(178f, 66f), false);
            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Belt,      "BELT",       "Utility / Tools", new Vector2(0.46f, 0.46f), new Vector2(196f, 62f), false);
            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Legs,      "LEGS",       "Pants",           new Vector2(0.46f, 0.34f), new Vector2(192f, 68f), true);
            BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Feet,      "FEET",       "Boots",           new Vector2(0.46f, 0.21f), new Vector2(182f, 60f), false);

            var weightTMP = MakeRect("WeightReadout", area.transform).AddComponent<TextMeshProUGUI>();
            var weightRT = RT(weightTMP.gameObject);
            weightRT.anchorMin = new Vector2(0f, 0f);
            weightRT.anchorMax = new Vector2(1f, 0.12f);
            weightRT.offsetMin = new Vector2(8f, 2f);
            weightRT.offsetMax = new Vector2(-8f, -2f);
            weightTMP.text = "WEIGHT 32 / 50 kg";
            weightTMP.fontSize = 14f;
            weightTMP.fontStyle = FontStyles.Bold;
            weightTMP.color = C_Text;
            weightTMP.alignment = TextAlignmentOptions.Center;
            weightTMP.raycastTarget = false;
        }

        static void BuildEquipmentSilhouette(Transform parent)
        {
            var root = MakeRect("BodySilhouette", parent);
            var rootRT = RT(root);
            rootRT.anchorMin = new Vector2(0.46f, 0.56f);
            rootRT.anchorMax = new Vector2(0.46f, 0.56f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            rootRT.sizeDelta = new Vector2(402f, 705f);

            Color silhouette = new Color(0.62f, 0.66f, 0.72f, 0.12f);
            MakeSilhouettePiece(root.transform, "Head",   silhouette, new Vector2(0.40f, 0.86f), new Vector2(0.60f, 1.00f));
            MakeSilhouettePiece(root.transform, "Torso",  silhouette, new Vector2(0.30f, 0.48f), new Vector2(0.70f, 0.86f));
            MakeSilhouettePiece(root.transform, "Pelvis", silhouette, new Vector2(0.36f, 0.38f), new Vector2(0.64f, 0.48f));
            MakeSilhouettePiece(root.transform, "ArmL",   silhouette, new Vector2(0.14f, 0.52f), new Vector2(0.30f, 0.76f));
            MakeSilhouettePiece(root.transform, "ArmR",   silhouette, new Vector2(0.70f, 0.52f), new Vector2(0.86f, 0.76f));
            MakeSilhouettePiece(root.transform, "LegL",   silhouette, new Vector2(0.36f, 0.10f), new Vector2(0.49f, 0.38f));
            MakeSilhouettePiece(root.transform, "LegR",   silhouette, new Vector2(0.51f, 0.10f), new Vector2(0.64f, 0.38f));
        }

        static void MakeSilhouettePiece(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var piece = MakeImage(name, parent, color);
            var rt = RT(piece);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = piece.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }

        static void BuildEquipmentSlotCard(
            Transform parent,
            EquipmentSlotVisual slot,
            string title,
            string itemHint,
            Vector2 anchor,
            Vector2 size,
            bool emphasize)
        {
            Color baseColor = emphasize
                ? new Color(0.12f, 0.13f, 0.16f, 0.98f)
                : new Color(0.10f, 0.11f, 0.14f, 0.96f);

            var slotGO = MakeImage($"Slot_{slot}", parent, baseColor);
            var slotRT = RT(slotGO);
            slotRT.anchorMin = anchor;
            slotRT.anchorMax = anchor;
            slotRT.pivot = new Vector2(0.5f, 0.5f);
            slotRT.sizeDelta = size;

            var slotImg = slotGO.GetComponent<Image>();
            var outline = slotGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.28f, 0.31f, 0.35f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            var btn = slotGO.AddComponent<Button>();
            btn.targetGraphic = slotImg;
            var cb = btn.colors;
            cb.normalColor = baseColor;
            cb.highlightedColor = new Color(0.12f, 0.28f, 0.22f, 1f);
            cb.pressedColor = new Color(0.12f, 0.36f, 0.28f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.08f, 0.08f, 0.10f, 0.85f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var grime = MakeImage("Grime", slotGO.transform, new Color(0f, 0f, 0f, 0.10f));
            FillParent(RT(grime));
            grime.GetComponent<Image>().raycastTarget = false;

            var icon = MakeImage("Icon", slotGO.transform, new Color(0.15f, 0.16f, 0.19f, 1f));
            var iconRT = RT(icon);
            iconRT.anchorMin = new Vector2(0f, 0f);
            iconRT.anchorMax = new Vector2(0f, 1f);
            iconRT.offsetMin = new Vector2(6f, 6f);
            iconRT.offsetMax = new Vector2(40f, -6f);
            icon.GetComponent<Image>().raycastTarget = false;

            var titleTMP = MakeRect("SlotTitle", slotGO.transform).AddComponent<TextMeshProUGUI>();
            var titleRT = RT(titleTMP.gameObject);
            titleRT.anchorMin = new Vector2(0f, 0.48f);
            titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = new Vector2(48f, 2f);
            titleRT.offsetMax = new Vector2(-8f, -2f);
            titleTMP.text = title;
            titleTMP.fontSize = emphasize ? 16f : 14f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = C_Text;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            titleTMP.raycastTarget = false;

            var hintTMP = MakeRect("ItemHint", slotGO.transform).AddComponent<TextMeshProUGUI>();
            var hintRT = RT(hintTMP.gameObject);
            hintRT.anchorMin = new Vector2(0f, 0f);
            hintRT.anchorMax = new Vector2(1f, 0.48f);
            hintRT.offsetMin = new Vector2(48f, 2f);
            hintRT.offsetMax = new Vector2(-8f, -2f);
            hintTMP.text = itemHint;
            hintTMP.fontSize = 12f;
            hintTMP.color = C_TextDim;
            hintTMP.alignment = TextAlignmentOptions.MidlineLeft;
            hintTMP.raycastTarget = false;

            var durBg = MakeImage("DurabilityBG", slotGO.transform, new Color(0.07f, 0.08f, 0.10f, 1f));
            var durBgRT = RT(durBg);
            durBgRT.anchorMin = new Vector2(0f, 0f);
            durBgRT.anchorMax = new Vector2(1f, 0f);
            durBgRT.offsetMin = new Vector2(6f, 2f);
            durBgRT.offsetMax = new Vector2(-6f, 6f);
            durBg.GetComponent<Image>().raycastTarget = false;

            var durFill = MakeImage("DurabilityFill", durBg.transform, new Color(0.18f, 0.56f, 0.44f, 1f));
            var durFillRT = RT(durFill);
            durFillRT.anchorMin = Vector2.zero;
            durFillRT.anchorMax = new Vector2(0.66f, 1f);
            durFillRT.offsetMin = Vector2.zero;
            durFillRT.offsetMax = Vector2.zero;
            durFill.GetComponent<Image>().raycastTarget = false;
        }

        // ── Missions Panel ────────────────────────────────────────────────────

        static GameObject BuildMissionsPanel(Transform parent)
        {
            var panel = MakePanel(parent, "MissionsPanel");
            BuildPanelHeader(panel.transform, "MISSIONS & OBJECTIVES");

            var scrollGO = MakeRect("MissionScroll", panel.transform);
            var scrollRT = RT(scrollGO);
            scrollRT.anchorMin = new Vector2(0f, 0f);
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(8f, 8f);
            scrollRT.offsetMax = new Vector2(-8f, -44f);

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = MakeRect("Viewport", scrollGO.transform);
            FillParent(RT(viewport));
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = RT(viewport);

            var content = MakeRect("Content", viewport.transform);
            var cRT = RT(content);
            cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot = new Vector2(0.5f, 1f);
            cRT.offsetMin = Vector2.zero; cRT.offsetMax = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = cRT;

            BuildMissionRow(content.transform, "Secure the Outpost",  "Build 5 walls around Outpost Alpha", 0.40f, false);
            BuildMissionRow(content.transform, "Gather Supplies",      "Scavenge 20 food items",            0.65f, false);
            BuildMissionRow(content.transform, "Survivor Escort",      "Guide Smith family to base camp",   0f,    false);
            BuildMissionRow(content.transform, "Clear the Highway",    "Eliminate 30 zombies on Route 9",   0.12f, false);

            return panel;
        }

        static void BuildMissionRow(Transform parent, string title, string desc, float prog, bool done)
        {
            var row   = MakeImage($"Mission_{parent.childCount}", parent, new Color(0.10f, 0.11f, 0.13f, 1f));
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.minHeight = 68f; rowLE.preferredHeight = 68f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.padding = new RectOffset(8, 8, 6, 6);

            var iconGO = MakeImage("Status", row.transform, done ? C_Accent : C_Btn);
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.minWidth = 36f; iconLE.preferredWidth = 36f;
            iconLE.minHeight = 36f; iconLE.preferredHeight = 36f;
            iconLE.flexibleWidth = 0f;
            var iconLblGO = MakeRect("Label", iconGO.transform);
            FillParent(RT(iconLblGO));
            var iconTMP = iconLblGO.AddComponent<TextMeshProUGUI>();
            iconTMP.text = done ? "✓" : "!"; iconTMP.fontSize = 18f;
            iconTMP.color = done ? C_Text : new Color(0.95f, 0.75f, 0.20f, 1f);
            iconTMP.alignment = TextAlignmentOptions.Center; iconTMP.raycastTarget = false;

            var info = MakeRect("Info", row.transform);
            info.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var infoVLG = info.AddComponent<VerticalLayoutGroup>();
            infoVLG.spacing = 3f; infoVLG.childForceExpandWidth = true; infoVLG.childForceExpandHeight = false;

            var titleTMP = MakeRect("Title", info.transform).AddComponent<TextMeshProUGUI>();
            titleTMP.text = title; titleTMP.fontSize = 13f; titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = done ? C_TextDim : C_Text; titleTMP.raycastTarget = false;

            var descTMP = MakeRect("Desc", info.transform).AddComponent<TextMeshProUGUI>();
            descTMP.text = desc; descTMP.fontSize = 11f;
            descTMP.color = C_TextDim; descTMP.raycastTarget = false;

            var progBg = MakeImage("ProgressBG", info.transform, new Color(0.06f, 0.06f, 0.08f, 1f));
            progBg.AddComponent<LayoutElement>().minHeight = 6f;
            Sprite spr     = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var progFill   = MakeImage("ProgressFill", progBg.transform, done ? C_Accent : C_Stam);
            var progImg    = progFill.GetComponent<Image>();
            if (spr != null) progImg.sprite = spr;
            progImg.type       = Image.Type.Filled;
            progImg.fillMethod = Image.FillMethod.Horizontal;
            progImg.fillAmount = done ? 1f : prog;
            FillParent(RT(progFill));
        }

        // ── Placeholder Panel ─────────────────────────────────────────────────

        static GameObject MakePlaceholder(Transform parent, string name, string title, string body)
        {
            var panel = MakePanel(parent, name);
            BuildPanelHeader(panel.transform, title);

            var txt = MakeRect("PlaceholderText", panel.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(txt.gameObject));
            txt.text = body; txt.fontSize = 22f; txt.color = C_TextDim;
            txt.alignment = TextAlignmentOptions.Center; txt.raycastTarget = false;
            return panel;
        }

        // ── Shared panel helpers ──────────────────────────────────────────────

        static GameObject MakePanel(Transform parent, string name)
        {
            var p = MakeImage(name, parent, C_Panel);
            FillParent(RT(p));
            p.AddComponent<CanvasGroup>();
            return p;
        }

        static void BuildPanelHeader(Transform parent, string title)
        {
            var hdr = MakeImage("PanelHeader", parent, new Color(0.05f, 0.05f, 0.07f, 1f));
            var rt  = RT(hdr);
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = Vector2.one;
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, 42f);

            var txt = MakeRect("Title", hdr.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(txt.gameObject));
            RT(txt.gameObject).offsetMin = new Vector2(14f, 0f);
            txt.text = title; txt.fontSize = 20f; txt.fontStyle = FontStyles.Bold;
            txt.color = C_Text; txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.raycastTarget = false;
        }

        // ── Button factories ──────────────────────────────────────────────────

        static Button MakeTab(Transform parent, string name, string label, float width)
        {
            var go  = MakeImage(name, parent, C_Tab);
            var le  = go.AddComponent<LayoutElement>();
            le.minWidth = width; le.preferredWidth = width;
            le.minHeight = 100f;  le.preferredHeight = 100f;
            le.flexibleWidth = 0f;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            var txtGO = MakeRect("Label", go.transform);
            FillParent(RT(txtGO));
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 20f; tmp.fontStyle = FontStyles.Bold;
            tmp.color = C_Text; tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return btn;
        }

        static Button MakeTimeBtn(Transform parent, string name, string label)
        {
            var go  = MakeImage(name, parent, C_Btn);
            var le  = go.AddComponent<LayoutElement>();
            le.minWidth = TBTN; le.preferredWidth = TBTN;
            le.minHeight = 100f; le.preferredHeight = 100f;
            le.flexibleWidth = 0f;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            var txtGO = MakeRect("Label", go.transform);
            FillParent(RT(txtGO));
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 40f; tmp.fontStyle = FontStyles.Bold;
            tmp.color = C_Text; tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return btn;
        }

        static void MakeQABtn(Transform parent, string name, string label, Color color)
        {
            var go  = MakeImage(name, parent, color);
            var le  = go.AddComponent<LayoutElement>();
            le.minWidth = 96f; le.preferredWidth = 96f;
            le.flexibleWidth = 0f;
            go.AddComponent<Button>().targetGraphic = go.GetComponent<Image>();

            var txtGO = MakeRect("Label", go.transform);
            FillParent(RT(txtGO));
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 14f; tmp.fontStyle = FontStyles.Bold;
            tmp.color = C_Text; tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        // ── Primitive helpers ─────────────────────────────────────────────────

        static GameObject MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
            return go;
        }

        static GameObject MakeImage(string name, Transform parent, Color color)
        {
            var go  = MakeRect(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        static RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();

        static void FillParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void AnchorTop(RectTransform rt, float height)
        {
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = Vector2.one;
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, height);
        }

        static void AnchorBottom(RectTransform rt, float height)
        {
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = new Vector2(1f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, height);
        }

    }
}
