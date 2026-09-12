#region

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class ZomberaSquadManagementUI
    {
        private void BuildOrResolveUI()
        {
            _defaultFont = TMP_Settings.defaultFontAsset;
            EnsureCanvas();
            EnsurePortraitStudio();

            if (screenRoot != null && Application.isPlaying)
            {
                var hasResponsiveLayout = screenRoot.Find("Body/MainColumn/MainColumnStack") != null;
                if (!hasResponsiveLayout) forceRebuildOnAwake = true;
            }

            if (screenRoot != null && forceRebuildOnAwake)
            {
                DestroySafely(screenRoot.gameObject);
                screenRoot = null;
            }

            if (screenRoot == null)
            {
                BuildStyleSprites();
                BuildLayout();
            }
        }

        private void EnsureCanvas()
        {
            if (targetCanvas != null)
            {
                EnsureCanvasIsInteractive(targetCanvas);
                return;
            }

            var existingCanvasObject = GameObject.Find("ZomberaSquadCanvas");
            if (existingCanvasObject != null) targetCanvas = existingCanvasObject.GetComponent<Canvas>();

            if (targetCanvas == null)
            {
                var canvasObject = new GameObject("ZomberaSquadCanvas", typeof(RectTransform));
                targetCanvas = canvasObject.AddComponent<Canvas>();
            }

            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (targetCanvas.sortingOrder < ZomberaCanvasLayer.Screens)
                targetCanvas.sortingOrder = ZomberaCanvasLayer.Screens;

            EnsureCanvasIsInteractive(targetCanvas);
        }

        private static void EnsureCanvasIsInteractive(Canvas canvas)
        {
            if (canvas == null) return;

            if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);

            canvas.enabled = true;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();

            raycaster.enabled = true;
        }

        private void BuildStyleSprites()
        {
            _panelSprite = CreateDistressedSprite(
                96,
                96,
                new Color(0.12f, 0.12f, 0.12f, 1f),
                new Color(0.06f, 0.07f, 0.06f, 1f),
                new Color(0.25f, 0.20f, 0.14f, 1f),
                1129);

            _cardSprite = CreateDistressedSprite(
                96,
                96,
                new Color(0.18f, 0.18f, 0.17f, 1f),
                new Color(0.09f, 0.10f, 0.09f, 1f),
                new Color(0.20f, 0.14f, 0.12f, 1f),
                8711);

            _slotSprite = CreateDistressedSprite(
                96,
                96,
                new Color(0.22f, 0.22f, 0.20f, 1f),
                new Color(0.10f, 0.10f, 0.09f, 1f),
                new Color(0.34f, 0.28f, 0.19f, 1f),
                4123);

            _stripeSprite = CreateWarningStripeSprite(
                128,
                new Color(0.41f, 0.25f, 0.10f, 0.48f),
                new Color(0.58f, 0.46f, 0.19f, 0.55f),
                10);
        }

        private void BuildLayout()
        {
            var canvasRect = targetCanvas.transform as RectTransform;
            screenRoot = CreateRect("ZomberaSquadManagementScreen", canvasRect);
            StretchToParent(screenRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var screenImage = AddImage(screenRoot, new Color(0.18f, 0.18f, 0.17f, 0.98f), _panelSprite, true);
            screenImage.type = Image.Type.Sliced;

            var grimeOverlay = CreateRect("GrimeOverlay", screenRoot);
            StretchToParent(grimeOverlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var grimeImage = AddImage(grimeOverlay, new Color(1f, 1f, 1f, 0.20f), _cardSprite);
            grimeImage.type = Image.Type.Sliced;

            var topBar = CreateRect("TopBar", screenRoot);
            StretchToParent(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 92f));
            AddImage(topBar, new Color(0.14f, 0.14f, 0.13f, 0.98f), _cardSprite).type = Image.Type.Sliced;

            var warningStrip = CreateRect("WarningStrip", topBar);
            StretchToParent(warningStrip, new Vector2(0.82f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            AddImage(warningStrip, new Color(1f, 1f, 1f, 0.85f), _stripeSprite);

            BuildTopIdentity(topBar);
            BuildTopStatus(topBar);

            var body = CreateRect("Body", screenRoot);
            StretchToParent(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f),
                new Vector2(-10f, -96f));

            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.padding = new RectOffset(0, 0, 0, 0);
            bodyLayout.spacing = 8f;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = true;

            var leftColumn = CreateRect("SquadListColumn", body);
            var leftLayout = leftColumn.gameObject.AddComponent<LayoutElement>();
            leftLayout.preferredWidth = 392f;
            leftLayout.minWidth = 280f;
            leftLayout.flexibleWidth = 0f;
            AddImage(leftColumn, new Color(0.15f, 0.15f, 0.14f, 0.98f), _cardSprite).type = Image.Type.Sliced;

            var rightColumn = CreateRect("MainColumn", body);
            var rightLayout = rightColumn.gameObject.AddComponent<LayoutElement>();
            rightLayout.minWidth = 420f;
            rightLayout.flexibleWidth = 1f;
            AddImage(rightColumn, new Color(0.13f, 0.14f, 0.13f, 0.96f), _panelSprite).type = Image.Type.Sliced;

            BuildLeftColumn(leftColumn);
            BuildRightColumn(rightColumn);
        }

        private void BuildTopIdentity(RectTransform topBar)
        {
            var identity = CreateRect("SquadIdentity", topBar);
            StretchToParent(identity, new Vector2(0f, 0f), new Vector2(0.40f, 1f), new Vector2(18f, 0f),
                new Vector2(-8f, 0f));

            TMP_Text label = CreateText(identity, "SQUAD DESIGNATION", 16f, new Color(0.73f, 0.67f, 0.50f, 1f),
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            StretchToParent(label.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), Vector2.zero,
                Vector2.zero);

            squadNameText = CreateText(identity, "Squad 1", 34f, new Color(0.95f, 0.92f, 0.80f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            StretchToParent(squadNameText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.58f), Vector2.zero,
                Vector2.zero);
        }

        private void BuildTopStatus(RectTransform topBar)
        {
            var statusStrip = CreateRect("StatusStrip", topBar);
            StretchToParent(statusStrip, new Vector2(0.40f, 0f), new Vector2(1f, 1f), new Vector2(8f, 12f),
                new Vector2(-12f, -12f));

            var layout = statusStrip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            conditionValueText =
                BuildStatusChip(statusStrip, "Condition", "Holding", new Color(0.79f, 0.73f, 0.50f, 1f));
            suppliesValueText = BuildStatusChip(statusStrip, "Supplies", "Scarce", new Color(0.80f, 0.58f, 0.28f, 1f));
            threatValueText = BuildStatusChip(statusStrip, "Threat", "Elevated", new Color(0.58f, 0.32f, 0.28f, 1f));
        }

        private TMP_Text BuildStatusChip(RectTransform parent, string label, string value, Color valueColor)
        {
            var chip = CreateRect(label + "Chip", parent);
            AddImage(chip, new Color(0.20f, 0.20f, 0.18f, 0.95f), _cardSprite).type = Image.Type.Sliced;

            var layout = chip.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var fitter = chip.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            CreateText(chip, label.ToUpperInvariant(), 12f, new Color(0.56f, 0.56f, 0.52f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            TMP_Text valueText = CreateText(chip, value, 17f, valueColor, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            return valueText;
        }

        private void BuildLeftColumn(RectTransform leftColumn)
        {
            var host = CreateRect("SquadListHost", leftColumn);
            StretchToParent(host, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            squadListPanel = host.gameObject.AddComponent<SquadListPanelController>();
            squadListPanel.Build(host, _defaultFont, _panelSprite, _slotSprite);
        }

        private void BuildRightColumn(RectTransform rightColumn)
        {
            var stack = CreateRect("MainColumnStack", rightColumn);
            StretchToParent(stack, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -8f));

            var stackLayout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            stackLayout.padding = new RectOffset(0, 0, 0, 0);
            stackLayout.spacing = 8f;
            stackLayout.childControlWidth = true;
            stackLayout.childControlHeight = true;
            stackLayout.childForceExpandWidth = true;
            stackLayout.childForceExpandHeight = false;

            var selectedCard = CreateRect("SelectedSurvivorCard", stack);
            var selectedLayout = selectedCard.gameObject.AddComponent<LayoutElement>();
            selectedLayout.preferredHeight = 132f;
            selectedLayout.minHeight = 110f;
            selectedLayout.flexibleHeight = 0f;
            AddImage(selectedCard, new Color(0.20f, 0.20f, 0.19f, 0.98f), _cardSprite).type = Image.Type.Sliced;

            BuildSelectedCard(selectedCard);

            var tabBar = CreateRect("TabBar", stack);
            var tabBarLayout = tabBar.gameObject.AddComponent<LayoutElement>();
            tabBarLayout.preferredHeight = 50f;
            tabBarLayout.minHeight = 46f;
            tabBarLayout.flexibleHeight = 0f;
            AddImage(tabBar, new Color(0.16f, 0.16f, 0.15f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            var tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 6f;
            tabLayout.padding = new RectOffset(6, 6, 6, 6);
            tabLayout.childControlHeight = true;
            tabLayout.childControlWidth = true;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            squadCustomiserTabButton = CreateTabButton(tabBar, "Squad");
            inventoryTabButton = CreateTabButton(tabBar, "Inventory");
            craftingTabButton = CreateTabButton(tabBar, "Crafting");
            skillsTabButton = CreateTabButton(tabBar, "Skills");
            formationsTabButton = CreateTabButton(tabBar, "Formations");
            jobsTabButton = CreateTabButton(tabBar, "Jobs");
            factionsTabButton = CreateTabButton(tabBar, "Factions");
            mapTabButton = CreateTabButton(tabBar, "Map");
            missionsTabButton = CreateTabButton(tabBar, "Missions");

            var tabContent = CreateRect("TabContent", stack);
            var tabContentLayout = tabContent.gameObject.AddComponent<LayoutElement>();
            tabContentLayout.minHeight = 220f;
            tabContentLayout.flexibleHeight = 1f;
            AddImage(tabContent, new Color(0.14f, 0.15f, 0.14f, 0.96f), _panelSprite).type = Image.Type.Sliced;

            squadCustomiserTabRoot = CreateRect("SquadTab", tabContent);
            inventoryTabRoot = CreateRect("InventoryTab", tabContent);
            craftingTabRoot = CreateRect("CraftingTab", tabContent);
            skillsTabRoot = CreateRect("SkillsTab", tabContent);
            formationsTabRoot = CreateRect("FormationsTab", tabContent);
            jobsTabRoot = CreateRect("JobsTab", tabContent);
            factionsTabRoot = CreateRect("FactionsTab", tabContent);
            mapTabRoot = CreateRect("MapTab", tabContent);
            missionsTabRoot = CreateRect("MissionsTab", tabContent);

            StretchToParent(squadCustomiserTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f),
                new Vector2(-8f, -8f));
            StretchToParent(inventoryTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(craftingTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(skillsTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(formationsTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(jobsTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(factionsTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(mapTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(missionsTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            squadCustomiserTab = squadCustomiserTabRoot.gameObject.AddComponent<SquadCustomizerTabController>();
            squadCustomiserTab.Build(squadCustomiserTabRoot, _defaultFont, _panelSprite, _slotSprite);

            inventoryTab = inventoryTabRoot.gameObject.AddComponent<InventoryTabController>();
            inventoryTab.Build(inventoryTabRoot, _defaultFont, _panelSprite, _slotSprite);

            if (craftingTabPrefab != null)
            {
                var craftingGo = Instantiate(craftingTabPrefab, craftingTabRoot);
                craftingTab = craftingGo.GetComponent<CraftingTabController>();
                // Optionally call Build or Initialize
            }
            else
            {
                craftingTab = craftingTabRoot.gameObject.AddComponent<CraftingTabController>();
                craftingTab.Build(craftingTabRoot, _defaultFont, _panelSprite, _slotSprite);
            }

            skillsTab = skillsTabRoot.gameObject.AddComponent<SkillsTabController>();
            skillsTab.Build(skillsTabRoot, _defaultFont, _panelSprite, _slotSprite);

            formationsTab = formationsTabRoot.gameObject.AddComponent<FormationsTabController>();
            formationsTab.Build(formationsTabRoot, _defaultFont, _panelSprite, _slotSprite);

            jobsTab = jobsTabRoot.gameObject.AddComponent<JobsTabController>();
            jobsTab.Build(jobsTabRoot, _defaultFont, _panelSprite, _slotSprite);

            factionsTab = factionsTabRoot.gameObject.AddComponent<FactionsTabController>();
            factionsTab.Build(factionsTabRoot, _defaultFont, _panelSprite, _slotSprite);

            mapTab = mapTabRoot.gameObject.AddComponent<WorldMapPanelController>();
            mapTab.Build(mapTabRoot, _defaultFont, _panelSprite);

            BuildPlaceholderTab(
                missionsTabRoot,
                "Missions",
                "Mission tracking and objectives appear here. Press F5 to open this tab.");
        }

        private void BuildSelectedCard(RectTransform selectedCard)
        {
            var portraitFrame = CreateRect("PortraitFrame", selectedCard);
            StretchToParent(portraitFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 10f),
                new Vector2(114f, -10f));
            AddImage(portraitFrame, new Color(0.24f, 0.24f, 0.22f, 1f), _slotSprite).type = Image.Type.Sliced;

            var portrait = CreateRect("Portrait", portraitFrame);
            StretchToParent(portrait, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            selectedPortraitImage = AddRawImage(portrait, new Color(1f, 1f, 1f, 1f), portraitRT);

            selectedPortraitInitialText = CreateText(portrait, "A", 40f, new Color(0.89f, 0.86f, 0.74f, 0.85f),
                FontStyles.Bold, TextAlignmentOptions.Center);
            StretchToParent(selectedPortraitInitialText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero,
                Vector2.zero);

            var info = CreateRect("SelectionInfo", selectedCard);
            StretchToParent(info, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(124f, 10f),
                new Vector2(-10f, -10f));

            selectedNameText = CreateText(info, "Selected Survivor", 28f, new Color(0.93f, 0.89f, 0.78f, 1f),
                FontStyles.Bold, TextAlignmentOptions.TopLeft);
            StretchToParent(selectedNameText.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), Vector2.zero,
                Vector2.zero);

            selectedConditionText = CreateText(info, "Condition: Stable", 17f, new Color(0.74f, 0.68f, 0.55f, 1f),
                FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            StretchToParent(selectedConditionText.rectTransform, new Vector2(0f, 0.22f), new Vector2(1f, 0.52f),
                Vector2.zero, Vector2.zero);

            selectedHealthText = CreateText(info, "Health: 100%", 16f, new Color(0.82f, 0.76f, 0.62f, 1f),
                FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            StretchToParent(selectedHealthText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.24f), Vector2.zero,
                Vector2.zero);
        }

        private void BuildPlaceholderTab(RectTransform tabRoot, string title, string body)
        {
            AddImage(tabRoot, new Color(0.12f, 0.13f, 0.12f, 0.92f), _panelSprite).type = Image.Type.Sliced;

            var titleRect = CreateRect("PlaceholderTitle", tabRoot);
            StretchToParent(titleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -16f),
                new Vector2(-18f, -72f));
            TMP_Text titleText = CreateText(titleRect, title, 28f, new Color(0.93f, 0.89f, 0.78f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            StretchToParent(titleText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var bodyRect = CreateRect("PlaceholderBody", tabRoot);
            StretchToParent(bodyRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, 20f),
                new Vector2(-18f, -84f));
            var bodyText = CreateText(
                bodyRect,
                body,
                18f,
                new Color(0.72f, 0.67f, 0.56f, 1f),
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            StretchToParent(bodyText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
        }

        private Button CreateTabButton(RectTransform parent, string label)
        {
            var buttonRect = CreateRect(label + "TabButton", parent);
            var image = AddImage(buttonRect, new Color(0.20f, 0.20f, 0.18f, 1f), _slotSprite, true);
            image.type = Image.Type.Sliced;

            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.89f, 0.80f, 0.58f, 1f);
            colors.pressedColor = new Color(0.76f, 0.58f, 0.29f, 1f);
            colors.selectedColor = new Color(0.82f, 0.64f, 0.34f, 1f);
            colors.disabledColor = new Color(0.48f, 0.45f, 0.38f, 0.7f);
            button.colors = colors;

            TMP_Text text = CreateText(buttonRect, label, 17f, new Color(0.88f, 0.84f, 0.72f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center);
            StretchToParent(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return button;
        }
    }
}
