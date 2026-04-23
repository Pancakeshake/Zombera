using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.UI;
using Zombera.Inventory;
using Zombera.Systems;

namespace Zombera.UI.SquadManagement
{
    /// <summary>
    /// Builds and coordinates a gritty squad management screen for Zombera.
    /// Attach to any GameObject and press Play to auto-build the interface.
    /// </summary>
    public sealed class ZomberaSquadManagementUI : MonoBehaviour
    {
        private sealed class LiveSurvivorContext
        {
            public string Id;
            public Unit Unit;
            public SquadMember SquadMember;
            public SurvivorAI SurvivorAI;
        }

        private enum TabId
        {
            Squad,
            Inventory,
            Crafting,
            Map,
            Missions
        }

        [Header("Build")]
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private bool forceRebuildOnAwake = true;
        [SerializeField] private bool visibleOnStart;
        [SerializeField] private Canvas targetCanvas;

        [Header("Live Data")]
        [SerializeField] private bool useLiveGameData = true;
        [SerializeField] private bool keepDemoFallbackWhenNoLiveData;
        [SerializeField] private bool includePlayerInRoster = true;
        [SerializeField] private float liveRefreshInterval = 1f;

        [Header("Root")]
        [SerializeField] private RectTransform screenRoot;

        [Header("Top Bar")]
        [SerializeField] private TMP_Text squadNameText;
        [SerializeField] private TMP_Text conditionValueText;
        [SerializeField] private TMP_Text suppliesValueText;
        [SerializeField] private TMP_Text threatValueText;

        [Header("Selection Card")]
        [SerializeField] private Image selectedPortraitImage;
        [SerializeField] private TMP_Text selectedPortraitInitialText;
        [SerializeField] private TMP_Text selectedNameText;
        [SerializeField] private TMP_Text selectedConditionText;
        [SerializeField] private TMP_Text selectedHealthText;

        [Header("Tabs")]
        [SerializeField] private Button inventoryTabButton;
        [SerializeField] private Button skillsTabButton;
        [SerializeField] private Button squadCustomiserTabButton;
        [SerializeField] private Button mapTabButton;
        [SerializeField] private Button missionsTabButton;
        [SerializeField] private RectTransform inventoryTabRoot;
        [SerializeField] private RectTransform skillsTabRoot;
        [SerializeField] private RectTransform squadCustomiserTabRoot;
        [SerializeField] private RectTransform mapTabRoot;
        [SerializeField] private RectTransform missionsTabRoot;

        [Header("Panels")]
        [SerializeField] private SquadListPanelController squadListPanel;
        [SerializeField] private InventoryTabController inventoryTab;
        [SerializeField] private SkillsTabController skillsTab;
        [SerializeField] private SquadCustomizerTabController squadCustomiserTab;

        private readonly List<SquadListPanelController.SurvivorEntryData> survivors =
            new List<SquadListPanelController.SurvivorEntryData>();

        private readonly List<InventoryTabController.InventorySlotData> inventorySlots =
            new List<InventoryTabController.InventorySlotData>();

        private readonly List<SkillsTabController.SkillEntryData> skillEntries =
            new List<SkillsTabController.SkillEntryData>();

        private readonly List<LiveSurvivorContext> liveSurvivorContexts =
            new List<LiveSurvivorContext>();

        private TMP_FontAsset defaultFont;
        private Sprite panelSprite;
        private Sprite cardSprite;
        private Sprite slotSprite;
        private Sprite stripeSprite;
        private TabId activeTab;
        private int selectedSurvivorIndex = -1;
        private float liveRefreshTimer;
        private TimeSystem resolvedTimeSystem;
        private bool pausedByVisibility;

        public bool IsVisible => screenRoot != null && screenRoot.gameObject.activeSelf;

        private void Awake()
        {
            if (!buildOnAwake)
            {
                return;
            }

            BuildOrResolveUI();
            WireInteractions();
            PopulateInitialData();
            ShowTab(TabId.Squad);
            SetVisible(visibleOnStart && IsGameplayUiAllowed());
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            TryReleaseVisibilityPause();
        }

        private void Update()
        {
            if (!useLiveGameData || liveRefreshInterval <= 0f || !IsVisible)
            {
                return;
            }

            liveRefreshTimer += Time.unscaledDeltaTime;
            if (liveRefreshTimer < liveRefreshInterval)
            {
                return;
            }

            liveRefreshTimer = 0f;
            _ = TrySyncFromLiveData();
        }

        [ContextMenu("Rebuild Squad Management UI")]
        public void RebuildNow()
        {
            forceRebuildOnAwake = true;
            BuildOrResolveUI();
            WireInteractions();
            PopulateInitialData();
            ShowTab(TabId.Squad);
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            bool wasVisible = IsVisible;

            if (visible)
            {
                RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
            }

            if (screenRoot == null)
            {
                if (!visible)
                {
                    if (wasVisible)
                    {
                        TryReleaseVisibilityPause();
                    }

                    return;
                }

                BuildOrResolveUI();
                WireInteractions();
                PopulateInitialData();
                ShowTab(TabId.Squad);
            }

            if (screenRoot != null)
            {
                RepairMissingSlicedSprites();
                screenRoot.gameObject.SetActive(visible);
            }

            if (!wasVisible && visible)
            {
                TryApplyVisibilityPause();
            }
            else if (wasVisible && !visible)
            {
                TryReleaseVisibilityPause();
            }
        }

        public void ToggleVisible()
        {
            SetVisible(!IsVisible);
        }

        public void OpenSquadTab()
        {
            OpenTabAndEnsureVisible(TabId.Squad);
        }

        public void OpenInventoryTab()
        {
            OpenTabAndEnsureVisible(TabId.Inventory);
        }

        public void OpenCraftingTab()
        {
            OpenTabAndEnsureVisible(TabId.Crafting);
        }

        public void OpenMapTab()
        {
            OpenTabAndEnsureVisible(TabId.Map);
        }

        public void OpenMissionsTab()
        {
            OpenTabAndEnsureVisible(TabId.Missions);
        }

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
            if (screenRoot == null)
            {
                return;
            }

            Image[] images = screenRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null)
                {
                    continue;
                }

                if (image.type == Image.Type.Sliced && image.sprite == null)
                {
                    // A sliced Image with no sprite renders as an editor placeholder X.
                    // Fall back to Simple so the panel shows as a solid color block.
                    image.type = Image.Type.Simple;
                }
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;

            if (IsGameplayUiAllowed())
            {
                return;
            }

            SetVisible(false);
        }

        private static bool IsGameplayUiAllowed()
        {
            if (GameManager.Instance != null)
            {
                GameState state = GameManager.Instance.CurrentState;
                return state == GameState.Playing || state == GameState.Paused;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && string.Equals(activeScene.name, "World", StringComparison.OrdinalIgnoreCase);
        }

        private void TryApplyVisibilityPause()
        {
            TimeSystem timeSystem = ResolveTimeSystem();
            if (timeSystem == null)
            {
                pausedByVisibility = false;
                return;
            }

            if (!timeSystem.IsPaused)
            {
                timeSystem.PauseGame();
                pausedByVisibility = true;
                return;
            }

            pausedByVisibility = false;
        }

        private void TryReleaseVisibilityPause()
        {
            if (!pausedByVisibility)
            {
                return;
            }

            TimeSystem timeSystem = ResolveTimeSystem();
            if (timeSystem != null)
            {
                timeSystem.ResumeGame();
            }

            pausedByVisibility = false;
        }

        private TimeSystem ResolveTimeSystem()
        {
            if (resolvedTimeSystem == null)
            {
                resolvedTimeSystem = FindFirstObjectByType<TimeSystem>();
            }

            return resolvedTimeSystem;
        }

        private void PopulateInitialData()
        {
            if (useLiveGameData && TrySyncFromLiveData())
            {
                return;
            }

            if (keepDemoFallbackWhenNoLiveData)
            {
                SeedDemoData();
            }
        }

        private void BuildOrResolveUI()
        {
            defaultFont = TMP_Settings.defaultFontAsset;
            EnsureCanvas();

            if (screenRoot != null && Application.isPlaying)
            {
                bool hasResponsiveLayout = screenRoot.Find("Body/MainColumn/MainColumnStack") != null;
                if (!hasResponsiveLayout)
                {
                    forceRebuildOnAwake = true;
                }
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

            GameObject existingCanvasObject = GameObject.Find("ZomberaSquadCanvas");
            if (existingCanvasObject != null)
            {
                targetCanvas = existingCanvasObject.GetComponent<Canvas>();
            }

            if (targetCanvas == null)
            {
                GameObject canvasObject = new GameObject("ZomberaSquadCanvas", typeof(RectTransform));
                targetCanvas = canvasObject.AddComponent<Canvas>();
            }

            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (targetCanvas.sortingOrder < ZomberaCanvasLayer.Screens)
            {
                targetCanvas.sortingOrder = ZomberaCanvasLayer.Screens;
            }

            EnsureCanvasIsInteractive(targetCanvas);
        }

        private static void EnsureCanvasIsInteractive(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            if (!canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(true);
            }

            canvas.enabled = true;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            raycaster.enabled = true;
        }

        private void BuildStyleSprites()
        {
            panelSprite = CreateDistressedSprite(
                96,
                96,
                new Color(0.12f, 0.12f, 0.12f, 1f),
                new Color(0.06f, 0.07f, 0.06f, 1f),
                new Color(0.25f, 0.20f, 0.14f, 1f),
                1129);

            cardSprite = CreateDistressedSprite(
                96,
                96,
                new Color(0.18f, 0.18f, 0.17f, 1f),
                new Color(0.09f, 0.10f, 0.09f, 1f),
                new Color(0.20f, 0.14f, 0.12f, 1f),
                8711);

            slotSprite = CreateDistressedSprite(
                96,
                96,
                new Color(0.22f, 0.22f, 0.20f, 1f),
                new Color(0.10f, 0.10f, 0.09f, 1f),
                new Color(0.34f, 0.28f, 0.19f, 1f),
                4123);

            stripeSprite = CreateWarningStripeSprite(
                128,
                new Color(0.41f, 0.25f, 0.10f, 0.48f),
                new Color(0.58f, 0.46f, 0.19f, 0.55f),
                10);
        }

        private void BuildLayout()
        {
            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            screenRoot = CreateRect("ZomberaSquadManagementScreen", canvasRect);
            StretchToParent(screenRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image screenImage = AddImage(screenRoot, new Color(0.18f, 0.18f, 0.17f, 0.98f), panelSprite, true);
            screenImage.type = Image.Type.Sliced;

            RectTransform grimeOverlay = CreateRect("GrimeOverlay", screenRoot);
            StretchToParent(grimeOverlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image grimeImage = AddImage(grimeOverlay, new Color(1f, 1f, 1f, 0.20f), cardSprite);
            grimeImage.type = Image.Type.Sliced;

            RectTransform topBar = CreateRect("TopBar", screenRoot);
            StretchToParent(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 92f));
            AddImage(topBar, new Color(0.14f, 0.14f, 0.13f, 0.98f), cardSprite).type = Image.Type.Sliced;

            RectTransform warningStrip = CreateRect("WarningStrip", topBar);
            StretchToParent(warningStrip, new Vector2(0.82f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            AddImage(warningStrip, new Color(1f, 1f, 1f, 0.85f), stripeSprite);

            BuildTopIdentity(topBar);
            BuildTopStatus(topBar);

            RectTransform body = CreateRect("Body", screenRoot);
            StretchToParent(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-10f, -96f));

            HorizontalLayoutGroup bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.padding = new RectOffset(0, 0, 0, 0);
            bodyLayout.spacing = 8f;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = true;

            RectTransform leftColumn = CreateRect("SquadListColumn", body);
            LayoutElement leftLayout = leftColumn.gameObject.AddComponent<LayoutElement>();
            leftLayout.preferredWidth = 392f;
            leftLayout.minWidth = 280f;
            leftLayout.flexibleWidth = 0f;
            AddImage(leftColumn, new Color(0.15f, 0.15f, 0.14f, 0.98f), cardSprite).type = Image.Type.Sliced;

            RectTransform rightColumn = CreateRect("MainColumn", body);
            LayoutElement rightLayout = rightColumn.gameObject.AddComponent<LayoutElement>();
            rightLayout.minWidth = 420f;
            rightLayout.flexibleWidth = 1f;
            AddImage(rightColumn, new Color(0.13f, 0.14f, 0.13f, 0.96f), panelSprite).type = Image.Type.Sliced;

            BuildLeftColumn(leftColumn);
            BuildRightColumn(rightColumn);
        }

        private void BuildTopIdentity(RectTransform topBar)
        {
            RectTransform identity = CreateRect("SquadIdentity", topBar);
            StretchToParent(identity, new Vector2(0f, 0f), new Vector2(0.40f, 1f), new Vector2(18f, 0f), new Vector2(-8f, 0f));

            TMP_Text label = CreateText(identity, "SQUAD DESIGNATION", 16f, new Color(0.73f, 0.67f, 0.50f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            StretchToParent(label.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            squadNameText = CreateText(identity, "Squad 1", 34f, new Color(0.95f, 0.92f, 0.80f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            StretchToParent(squadNameText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.58f), Vector2.zero, Vector2.zero);
        }

        private void BuildTopStatus(RectTransform topBar)
        {
            RectTransform statusStrip = CreateRect("StatusStrip", topBar);
            StretchToParent(statusStrip, new Vector2(0.40f, 0f), new Vector2(1f, 1f), new Vector2(8f, 12f), new Vector2(-12f, -12f));

            HorizontalLayoutGroup layout = statusStrip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            conditionValueText = BuildStatusChip(statusStrip, "Condition", "Holding", new Color(0.79f, 0.73f, 0.50f, 1f));
            suppliesValueText = BuildStatusChip(statusStrip, "Supplies", "Scarce", new Color(0.80f, 0.58f, 0.28f, 1f));
            threatValueText = BuildStatusChip(statusStrip, "Threat", "Elevated", new Color(0.58f, 0.32f, 0.28f, 1f));
        }

        private TMP_Text BuildStatusChip(RectTransform parent, string label, string value, Color valueColor)
        {
            RectTransform chip = CreateRect(label + "Chip", parent);
            AddImage(chip, new Color(0.20f, 0.20f, 0.18f, 0.95f), cardSprite).type = Image.Type.Sliced;

            VerticalLayoutGroup layout = chip.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            ContentSizeFitter fitter = chip.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            TMP_Text labelText = CreateText(chip, label.ToUpperInvariant(), 12f, new Color(0.56f, 0.56f, 0.52f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            TMP_Text valueText = CreateText(chip, value, 17f, valueColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            return valueText;
        }

        private void BuildLeftColumn(RectTransform leftColumn)
        {
            RectTransform host = CreateRect("SquadListHost", leftColumn);
            StretchToParent(host, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            squadListPanel = host.gameObject.AddComponent<SquadListPanelController>();
            squadListPanel.Build(host, defaultFont, panelSprite, slotSprite);
        }

        private void BuildRightColumn(RectTransform rightColumn)
        {
            RectTransform stack = CreateRect("MainColumnStack", rightColumn);
            StretchToParent(stack, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -8f));

            VerticalLayoutGroup stackLayout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            stackLayout.padding = new RectOffset(0, 0, 0, 0);
            stackLayout.spacing = 8f;
            stackLayout.childControlWidth = true;
            stackLayout.childControlHeight = true;
            stackLayout.childForceExpandWidth = true;
            stackLayout.childForceExpandHeight = false;

            RectTransform selectedCard = CreateRect("SelectedSurvivorCard", stack);
            LayoutElement selectedLayout = selectedCard.gameObject.AddComponent<LayoutElement>();
            selectedLayout.preferredHeight = 132f;
            selectedLayout.minHeight = 110f;
            selectedLayout.flexibleHeight = 0f;
            AddImage(selectedCard, new Color(0.20f, 0.20f, 0.19f, 0.98f), cardSprite).type = Image.Type.Sliced;

            BuildSelectedCard(selectedCard);

            RectTransform tabBar = CreateRect("TabBar", stack);
            LayoutElement tabBarLayout = tabBar.gameObject.AddComponent<LayoutElement>();
            tabBarLayout.preferredHeight = 50f;
            tabBarLayout.minHeight = 46f;
            tabBarLayout.flexibleHeight = 0f;
            AddImage(tabBar, new Color(0.16f, 0.16f, 0.15f, 0.98f), panelSprite).type = Image.Type.Sliced;

            HorizontalLayoutGroup tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 6f;
            tabLayout.padding = new RectOffset(6, 6, 6, 6);
            tabLayout.childControlHeight = true;
            tabLayout.childControlWidth = true;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            squadCustomiserTabButton = CreateTabButton(tabBar, "Squad");
            inventoryTabButton = CreateTabButton(tabBar, "Inventory");
            skillsTabButton = CreateTabButton(tabBar, "Crafting");
            mapTabButton = CreateTabButton(tabBar, "Map");
            missionsTabButton = CreateTabButton(tabBar, "Missions");

            RectTransform tabContent = CreateRect("TabContent", stack);
            LayoutElement tabContentLayout = tabContent.gameObject.AddComponent<LayoutElement>();
            tabContentLayout.minHeight = 220f;
            tabContentLayout.flexibleHeight = 1f;
            AddImage(tabContent, new Color(0.14f, 0.15f, 0.14f, 0.96f), panelSprite).type = Image.Type.Sliced;

            squadCustomiserTabRoot = CreateRect("SquadTab", tabContent);
            inventoryTabRoot = CreateRect("InventoryTab", tabContent);
            skillsTabRoot = CreateRect("CraftingTab", tabContent);
            mapTabRoot = CreateRect("MapTab", tabContent);
            missionsTabRoot = CreateRect("MissionsTab", tabContent);

            StretchToParent(squadCustomiserTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(inventoryTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(skillsTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(mapTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            StretchToParent(missionsTabRoot, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            squadCustomiserTab = squadCustomiserTabRoot.gameObject.AddComponent<SquadCustomizerTabController>();
            squadCustomiserTab.Build(squadCustomiserTabRoot, defaultFont, panelSprite, slotSprite);

            inventoryTab = inventoryTabRoot.gameObject.AddComponent<InventoryTabController>();
            inventoryTab.Build(inventoryTabRoot, defaultFont, panelSprite, slotSprite);

            skillsTab = skillsTabRoot.gameObject.AddComponent<SkillsTabController>();
            skillsTab.Build(skillsTabRoot, defaultFont, panelSprite, slotSprite);

            BuildPlaceholderTab(
                mapTabRoot,
                "World Map",
                "Map systems hook in here. Press F4 to jump back to this tab at any time.");

            BuildPlaceholderTab(
                missionsTabRoot,
                "Missions",
                "Mission tracking and objectives appear here. Press F5 to open this tab.");
        }

        private void BuildSelectedCard(RectTransform selectedCard)
        {
            RectTransform portraitFrame = CreateRect("PortraitFrame", selectedCard);
            StretchToParent(portraitFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 10f), new Vector2(114f, -10f));
            AddImage(portraitFrame, new Color(0.24f, 0.24f, 0.22f, 1f), slotSprite).type = Image.Type.Sliced;

            RectTransform portrait = CreateRect("Portrait", portraitFrame);
            StretchToParent(portrait, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            selectedPortraitImage = AddImage(portrait, new Color(0.26f, 0.31f, 0.26f, 1f), null);

            selectedPortraitInitialText = CreateText(portrait, "A", 40f, new Color(0.89f, 0.86f, 0.74f, 0.85f), FontStyles.Bold, TextAlignmentOptions.Center);
            StretchToParent(selectedPortraitInitialText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform info = CreateRect("SelectionInfo", selectedCard);
            StretchToParent(info, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(124f, 10f), new Vector2(-10f, -10f));

            selectedNameText = CreateText(info, "Selected Survivor", 28f, new Color(0.93f, 0.89f, 0.78f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            StretchToParent(selectedNameText.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            selectedConditionText = CreateText(info, "Condition: Stable", 17f, new Color(0.74f, 0.68f, 0.55f, 1f), FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            StretchToParent(selectedConditionText.rectTransform, new Vector2(0f, 0.22f), new Vector2(1f, 0.52f), Vector2.zero, Vector2.zero);

            selectedHealthText = CreateText(info, "Health: 100%", 16f, new Color(0.82f, 0.76f, 0.62f, 1f), FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            StretchToParent(selectedHealthText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.24f), Vector2.zero, Vector2.zero);
        }

        private void BuildPlaceholderTab(RectTransform tabRoot, string title, string body)
        {
            AddImage(tabRoot, new Color(0.12f, 0.13f, 0.12f, 0.92f), panelSprite).type = Image.Type.Sliced;

            RectTransform titleRect = CreateRect("PlaceholderTitle", tabRoot);
            StretchToParent(titleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -16f), new Vector2(-18f, -72f));
            TMP_Text titleText = CreateText(titleRect, title, 28f, new Color(0.93f, 0.89f, 0.78f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            StretchToParent(titleText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform bodyRect = CreateRect("PlaceholderBody", tabRoot);
            StretchToParent(bodyRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, 20f), new Vector2(-18f, -84f));
            TextMeshProUGUI bodyText = CreateText(
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
            RectTransform buttonRect = CreateRect(label + "TabButton", parent);
            Image image = AddImage(buttonRect, new Color(0.20f, 0.20f, 0.18f, 1f), slotSprite, true);
            image.type = Image.Type.Sliced;

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.89f, 0.80f, 0.58f, 1f);
            colors.pressedColor = new Color(0.76f, 0.58f, 0.29f, 1f);
            colors.selectedColor = new Color(0.82f, 0.64f, 0.34f, 1f);
            colors.disabledColor = new Color(0.48f, 0.45f, 0.38f, 0.7f);
            button.colors = colors;

            TMP_Text text = CreateText(buttonRect, label, 17f, new Color(0.88f, 0.84f, 0.72f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            StretchToParent(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return button;
        }

        private void WireInteractions()
        {
            if (inventoryTabButton != null)
            {
                inventoryTabButton.onClick.RemoveAllListeners();
                inventoryTabButton.onClick.AddListener(() => ShowTab(TabId.Inventory));
            }

            if (skillsTabButton != null)
            {
                skillsTabButton.onClick.RemoveAllListeners();
                skillsTabButton.onClick.AddListener(() => ShowTab(TabId.Crafting));
            }

            if (squadCustomiserTabButton != null)
            {
                squadCustomiserTabButton.onClick.RemoveAllListeners();
                squadCustomiserTabButton.onClick.AddListener(() => ShowTab(TabId.Squad));
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

        private bool TrySyncFromLiveData()
        {
            liveSurvivorContexts.Clear();
            ResolveLiveContexts(liveSurvivorContexts);

            if (liveSurvivorContexts.Count == 0)
            {
                return false;
            }

            string previouslySelectedId = TryGetSelectedSurvivorId();

            List<string> previousIds = new List<string>(survivors.Count);
            for (int i = 0; i < survivors.Count; i++)
            {
                previousIds.Add(survivors[i].Id);
            }

            survivors.Clear();
            for (int i = 0; i < liveSurvivorContexts.Count; i++)
            {
                survivors.Add(BuildSurvivorEntryFromContext(liveSurvivorContexts[i]));
            }

            bool rosterChanged = previousIds.Count != survivors.Count;
            if (!rosterChanged)
            {
                for (int i = 0; i < survivors.Count; i++)
                {
                    if (string.Equals(previousIds[i], survivors[i].Id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    rosterChanged = true;
                    break;
                }
            }

            squadListPanel?.SetEntries(survivors);

            string squadName = BuildSquadNameFromLiveData();
            if (squadNameText != null)
            {
                squadNameText.text = squadName;
            }

            List<string> names = new List<string>(survivors.Count);
            for (int i = 0; i < survivors.Count; i++)
            {
                names.Add(survivors[i].DisplayName);
            }

            squadCustomiserTab?.SetSquadName(squadName);
            if (rosterChanged)
            {
                squadCustomiserTab?.SetMembers(names);
            }

            if (survivors.Count > 0)
            {
                int indexToSelect = 0;
                if (!string.IsNullOrWhiteSpace(previouslySelectedId))
                {
                    for (int i = 0; i < survivors.Count; i++)
                    {
                        if (string.Equals(survivors[i].Id, previouslySelectedId, StringComparison.Ordinal))
                        {
                            indexToSelect = i;
                            break;
                        }
                    }
                }
                else if (selectedSurvivorIndex >= 0 && selectedSurvivorIndex < survivors.Count)
                {
                    indexToSelect = selectedSurvivorIndex;
                }

                selectedSurvivorIndex = Mathf.Clamp(indexToSelect, 0, survivors.Count - 1);
                squadListPanel?.SelectIndex(selectedSurvivorIndex);
                HandleSurvivorSelection(selectedSurvivorIndex, survivors[selectedSurvivorIndex]);
            }
            else
            {
                selectedSurvivorIndex = -1;
            }

            RefreshTopStatus();
            return true;
        }

        private void ResolveLiveContexts(List<LiveSurvivorContext> target)
        {
            HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

            if (includePlayerInRoster)
            {
                Unit player = ResolvePlayerUnit();
                TryAddLiveContext(target, seenIds, player, null);
            }

            SquadManager manager = SquadManager.Instance != null
                ? SquadManager.Instance
                : FindFirstObjectByType<SquadManager>();

            if (manager != null)
            {
                manager.RefreshSquadRoster();

                IReadOnlyList<SquadMember> members = manager.SquadMembers;
                for (int i = 0; i < members.Count; i++)
                {
                    SquadMember member = members[i];
                    if (member == null)
                    {
                        continue;
                    }

                    Unit memberUnit = member.Unit != null
                        ? member.Unit
                        : member.GetComponent<Unit>();

                    TryAddLiveContext(target, seenIds, memberUnit, member);
                }
            }

            if (target.Count > 0)
            {
                return;
            }

            Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            for (int i = 0; i < allUnits.Length; i++)
            {
                Unit unit = allUnits[i];
                if (unit == null)
                {
                    continue;
                }

                if (unit.Role != UnitRole.SquadMember && (!includePlayerInRoster || unit.Role != UnitRole.Player))
                {
                    continue;
                }

                TryAddLiveContext(target, seenIds, unit, unit.GetComponent<SquadMember>());
            }
        }

        private Unit ResolvePlayerUnit()
        {
            PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null && spawner.SpawnedPlayer != null)
            {
                return spawner.SpawnedPlayer;
            }

            Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            for (int i = 0; i < allUnits.Length; i++)
            {
                if (allUnits[i] != null && allUnits[i].Role == UnitRole.Player)
                {
                    return allUnits[i];
                }
            }

            return null;
        }

        private static bool TryAddLiveContext(
            List<LiveSurvivorContext> target,
            HashSet<string> seenIds,
            Unit unit,
            SquadMember member)
        {
            if (unit == null)
            {
                return false;
            }

            string id = !string.IsNullOrWhiteSpace(unit.UnitId)
                ? unit.UnitId
                : !string.IsNullOrWhiteSpace(member != null ? member.MemberId : null)
                    ? member.MemberId
                    : unit.gameObject.GetInstanceID().ToString();

            if (!seenIds.Add(id))
            {
                return false;
            }

            target.Add(new LiveSurvivorContext
            {
                Id = id,
                Unit = unit,
                SquadMember = member,
                SurvivorAI = unit.GetComponent<SurvivorAI>()
            });

            return true;
        }

        private SquadListPanelController.SurvivorEntryData BuildSurvivorEntryFromContext(LiveSurvivorContext context)
        {
            Unit unit = context != null ? context.Unit : null;
            UnitHealth health = unit != null ? unit.Health : null;
            UnitStats stats = unit != null ? unit.Stats : null;

            float health01 = 1f;
            if (health != null && health.MaxHealth > 0f)
            {
                health01 = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
            }

            float stamina01 = stats != null
                ? Mathf.Clamp01(stats.Stamina / 100f)
                : 1f;

            return new SquadListPanelController.SurvivorEntryData(
                context != null ? context.Id : string.Empty,
                ResolveDisplayName(unit),
                ResolvePortrait(unit),
                health01,
                stamina01,
                ResolveCondition(health01));
        }

        private static string ResolveDisplayName(Unit unit)
        {
            if (unit == null || unit.gameObject == null)
            {
                return "Unknown";
            }

            if (unit.Role == UnitRole.Player && CharacterSelectionState.HasSelection && !string.IsNullOrWhiteSpace(CharacterSelectionState.SelectedCharacterName))
            {
                return CharacterSelectionState.SelectedCharacterName.Trim();
            }

            string name = unit.gameObject.name;
            return string.IsNullOrWhiteSpace(name) ? "Survivor" : name.Trim();
        }

        private static Sprite ResolvePortrait(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (unit.Role == UnitRole.Player && CharacterSelectionState.SelectedPortraitSprite != null)
            {
                return CharacterSelectionState.SelectedPortraitSprite;
            }

            SpriteRenderer spriteRenderer = unit.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                return spriteRenderer.sprite;
            }

            return null;
        }

        private static SquadListPanelController.SurvivorCondition ResolveCondition(float health01)
        {
            if (health01 > 0.72f)
            {
                return SquadListPanelController.SurvivorCondition.Stable;
            }

            if (health01 > 0.45f)
            {
                return SquadListPanelController.SurvivorCondition.Wounded;
            }

            if (health01 > 0.25f)
            {
                return SquadListPanelController.SurvivorCondition.Exhausted;
            }

            return SquadListPanelController.SurvivorCondition.Critical;
        }

        private string BuildSquadNameFromLiveData()
        {
            if (CharacterSelectionState.HasSelection && !string.IsNullOrWhiteSpace(CharacterSelectionState.SelectedCharacterName))
            {
                return CharacterSelectionState.SelectedCharacterName + "'s Squad";
            }

            return "Squad " + Mathf.Max(1, liveSurvivorContexts.Count);
        }

        private string TryGetSelectedSurvivorId()
        {
            if (selectedSurvivorIndex >= 0 && selectedSurvivorIndex < liveSurvivorContexts.Count)
            {
                return liveSurvivorContexts[selectedSurvivorIndex].Id;
            }

            if (selectedSurvivorIndex >= 0 && selectedSurvivorIndex < survivors.Count)
            {
                return survivors[selectedSurvivorIndex].Id;
            }

            return string.Empty;
        }

        private void ApplySelectionDataFromContext(LiveSurvivorContext context)
        {
            RefreshSelectionCardFromContext(context);
            BuildInventoryForContext(context);
            BuildSkillsForContext(context);
            inventoryTab?.SetSlots(inventorySlots);
            skillsTab?.SetSkills(skillEntries);
        }

        private void RefreshSelectionCardFromEntry(SquadListPanelController.SurvivorEntryData data)
        {
            if (selectedConditionText != null)
            {
                selectedConditionText.text = "Condition: " + data.Condition;
            }

            if (selectedHealthText != null)
            {
                int healthPercent = Mathf.RoundToInt(Mathf.Clamp01(data.Health01) * 100f);
                selectedHealthText.text = "Health: " + healthPercent + "%";
            }
        }

        private void RefreshSelectionCardFromContext(LiveSurvivorContext context)
        {
            Unit unit = context != null ? context.Unit : null;
            UnitHealth health = unit != null ? unit.Health : null;

            if (health == null || health.MaxHealth <= 0f)
            {
                if (selectedSurvivorIndex >= 0 && selectedSurvivorIndex < survivors.Count)
                {
                    RefreshSelectionCardFromEntry(survivors[selectedSurvivorIndex]);
                }

                return;
            }

            float health01 = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);

            if (selectedConditionText != null)
            {
                selectedConditionText.text = "Condition: " + ResolveCondition(health01);
            }

            if (selectedHealthText != null)
            {
                selectedHealthText.text = "Health: "
                    + Mathf.CeilToInt(health.CurrentHealth)
                    + " / "
                    + Mathf.CeilToInt(health.MaxHealth);
            }
        }

        private void BuildInventoryForContext(LiveSurvivorContext context)
        {
            inventorySlots.Clear();

            Unit unit = context != null ? context.Unit : null;
            UnitInventory unitInventory = unit != null ? unit.Inventory : null;
            if (unitInventory == null)
            {
                return;
            }

            IReadOnlyList<ItemStack> stacks = unitInventory.Items;
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack.item == null || stack.quantity <= 0)
                {
                    continue;
                }

                string itemName = ResolveItemName(stack.item);
                inventorySlots.Add(new InventoryTabController.InventorySlotData(
                    itemName,
                    stack.item.inventoryIcon,
                    stack.quantity,
                    InventoryTabController.InventorySlotState.Occupied));
            }
        }

        private static string ResolveItemName(ItemDefinition item)
        {
            if (item == null)
            {
                return "Unknown Item";
            }

            if (!string.IsNullOrWhiteSpace(item.displayName))
            {
                return item.displayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(item.itemId))
            {
                return item.itemId.Trim();
            }

            return item.name;
        }

        private void BuildSkillsForContext(LiveSurvivorContext context)
        {
            skillEntries.Clear();

            Unit unit = context != null ? context.Unit : null;
            UnitStats stats = unit != null ? unit.Stats : null;
            if (stats != null)
            {
                AddStatSkill("Shooting", "Combat", stats.Shooting, "Ranged proficiency and recoil control.");
                AddStatSkill("Melee", "Combat", stats.Melee, "Close-quarters efficiency and timing.");
                AddStatSkill("Strength", "Combat", stats.Strength, "Carry power and physical output.");
                AddStatSkill("Medical", "Support", stats.Medical, "Healing and stabilisation capability.");
                AddStatSkill("Engineering", "Support", stats.Engineering, "Repair and technical handling.");
            }

            SurvivorAI survivorAI = context != null ? context.SurvivorAI : null;
            if (survivorAI != null)
            {
                IReadOnlyList<string> traits = survivorAI.Traits;
                for (int i = 0; i < traits.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(traits[i]))
                    {
                        continue;
                    }

                    string trait = traits[i].Trim();
                    skillEntries.Add(new SkillsTabController.SkillEntryData(
                        trait,
                        "Traits",
                        SkillsTabController.SkillState.Passive,
                        true,
                        1,
                        "Innate survivor trait."));
                }
            }

            if (skillEntries.Count == 0)
            {
                skillEntries.Add(new SkillsTabController.SkillEntryData(
                    "Untrained",
                    "General",
                    SkillsTabController.SkillState.Unlocked,
                    false,
                    1,
                    "No advanced skill profile is available for this unit."));
            }
        }

        private void AddStatSkill(string name, string category, int value, string description)
        {
            int clamped = Mathf.Clamp(value, 0, 100);
            int rank = Mathf.Clamp(Mathf.RoundToInt(clamped / 20f), 0, 5);

            skillEntries.Add(new SkillsTabController.SkillEntryData(
                name,
                category,
                SkillStateFromValue(clamped),
                !string.Equals(category, "Combat", StringComparison.Ordinal),
                rank,
                description + " Value: " + clamped + "."));
        }

        private static SkillsTabController.SkillState SkillStateFromValue(int value)
        {
            if (value >= 75)
            {
                return SkillsTabController.SkillState.Active;
            }

            if (value >= 35)
            {
                return SkillsTabController.SkillState.Unlocked;
            }

            return SkillsTabController.SkillState.Locked;
        }

        private void SeedDemoData()
        {
            selectedSurvivorIndex = -1;
            survivors.Clear();
            survivors.Add(new SquadListPanelController.SurvivorEntryData("u01", "Mara Voss", null, 0.84f, 0.72f, SquadListPanelController.SurvivorCondition.Stable));
            survivors.Add(new SquadListPanelController.SurvivorEntryData("u02", "Eli Griggs", null, 0.58f, 0.46f, SquadListPanelController.SurvivorCondition.Wounded));
            survivors.Add(new SquadListPanelController.SurvivorEntryData("u03", "Noa Pike", null, 0.93f, 0.88f, SquadListPanelController.SurvivorCondition.Stable));
            survivors.Add(new SquadListPanelController.SurvivorEntryData("u04", "Rook Hale", null, 0.44f, 0.29f, SquadListPanelController.SurvivorCondition.Exhausted));
            survivors.Add(new SquadListPanelController.SurvivorEntryData("u05", "Iris Dune", null, 0.27f, 0.34f, SquadListPanelController.SurvivorCondition.Critical));

            squadListPanel?.SetEntries(survivors);

            inventorySlots.Clear();
            inventorySlots.Add(new InventoryTabController.InventorySlotData("9mm Ammo", null, 48, InventoryTabController.InventorySlotState.Occupied));
            inventorySlots.Add(new InventoryTabController.InventorySlotData("Bandage Roll", null, 5, InventoryTabController.InventorySlotState.Equipped));
            inventorySlots.Add(new InventoryTabController.InventorySlotData("Rust Knife", null, 1, InventoryTabController.InventorySlotState.Equipped));
            inventorySlots.Add(new InventoryTabController.InventorySlotData("Water Flask", null, 2, InventoryTabController.InventorySlotState.Occupied));
            inventorySlots.Add(new InventoryTabController.InventorySlotData("Scrap Metal", null, 17, InventoryTabController.InventorySlotState.Occupied));
            inventorySlots.Add(new InventoryTabController.InventorySlotData("Molotov", null, 1, InventoryTabController.InventorySlotState.Occupied));
            inventorySlots.Add(new InventoryTabController.InventorySlotData("Broken Radio", null, 1, InventoryTabController.InventorySlotState.Damaged));
            inventorySlots.Add(new InventoryTabController.InventorySlotData("Painkillers", null, 4, InventoryTabController.InventorySlotState.Occupied));
            inventorySlots.Add(new InventoryTabController.InventorySlotData("Ration Pack", null, 6, InventoryTabController.InventorySlotState.Occupied));

            inventoryTab?.SetSlots(inventorySlots);

            skillEntries.Clear();
            skillEntries.Add(new SkillsTabController.SkillEntryData("Steady Aim", "Combat", SkillsTabController.SkillState.Active, false, 2, "Improves ranged accuracy while crouched."));
            skillEntries.Add(new SkillsTabController.SkillEntryData("Bleed Control", "Survival", SkillsTabController.SkillState.Passive, true, 1, "Bandages stop bleed effects faster."));
            skillEntries.Add(new SkillsTabController.SkillEntryData("Silent Steps", "Survival", SkillsTabController.SkillState.Unlocked, false, 1, "Lowers movement noise by 20 percent."));
            skillEntries.Add(new SkillsTabController.SkillEntryData("Command Presence", "Leadership", SkillsTabController.SkillState.Passive, true, 3, "Nearby squad members perform better under pressure."));
            skillEntries.Add(new SkillsTabController.SkillEntryData("Frontline Push", "Leadership", SkillsTabController.SkillState.Locked, false, 0, "Temporarily boosts squad aggression."));
            skillEntries.Add(new SkillsTabController.SkillEntryData("Field Repair", "Support", SkillsTabController.SkillState.Unlocked, false, 1, "Repair damaged gear from scrap."));
            skillsTab?.SetSkills(skillEntries);

            List<string> names = new List<string>(survivors.Count);
            for (int i = 0; i < survivors.Count; i++)
            {
                names.Add(survivors[i].DisplayName);
            }

            squadCustomiserTab?.SetSquadName("Squad 1");
            squadCustomiserTab?.SetMembers(names);

            RefreshTopStatus();
            if (survivors.Count > 0)
            {
                selectedSurvivorIndex = 0;
                HandleSurvivorSelection(0, survivors[0]);
                squadListPanel?.SelectIndex(0);
            }
        }

        private void HandleSquadNameChanged(string squadName)
        {
            if (string.IsNullOrWhiteSpace(squadName))
            {
                return;
            }

            if (squadNameText != null)
            {
                squadNameText.text = squadName.Trim();
            }
        }

        private void HandleMemberOrderChanged(IReadOnlyList<string> orderedNames)
        {
            if (orderedNames == null || orderedNames.Count == 0 || survivors.Count == 0)
            {
                return;
            }

            List<SquadListPanelController.SurvivorEntryData> reordered = new List<SquadListPanelController.SurvivorEntryData>(orderedNames.Count);
            for (int i = 0; i < orderedNames.Count; i++)
            {
                string expectedName = orderedNames[i];
                for (int j = 0; j < survivors.Count; j++)
                {
                    if (!string.Equals(survivors[j].DisplayName, expectedName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    reordered.Add(survivors[j]);
                    break;
                }
            }

            if (reordered.Count != survivors.Count)
            {
                return;
            }

            survivors.Clear();
            survivors.AddRange(reordered);

            if (useLiveGameData && liveSurvivorContexts.Count == survivors.Count)
            {
                List<LiveSurvivorContext> reorderedContexts = new List<LiveSurvivorContext>(liveSurvivorContexts.Count);
                for (int i = 0; i < survivors.Count; i++)
                {
                    string id = survivors[i].Id;
                    for (int j = 0; j < liveSurvivorContexts.Count; j++)
                    {
                        if (!string.Equals(liveSurvivorContexts[j].Id, id, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        reorderedContexts.Add(liveSurvivorContexts[j]);
                        break;
                    }
                }

                if (reorderedContexts.Count == liveSurvivorContexts.Count)
                {
                    liveSurvivorContexts.Clear();
                    liveSurvivorContexts.AddRange(reorderedContexts);
                }
            }

            squadListPanel?.SetEntries(survivors);
            RefreshTopStatus();
        }

        private void HandleSurvivorSelection(int index, SquadListPanelController.SurvivorEntryData data)
        {
            selectedSurvivorIndex = index;

            if (selectedNameText != null)
            {
                selectedNameText.text = data.DisplayName;
            }

            RefreshSelectionCardFromEntry(data);

            if (selectedPortraitImage != null)
            {
                selectedPortraitImage.sprite = data.Portrait;
                selectedPortraitImage.color = data.Portrait != null
                    ? Color.white
                    : new Color(0.24f, 0.30f, 0.24f, 1f);
            }

            if (selectedPortraitInitialText != null)
            {
                selectedPortraitInitialText.gameObject.SetActive(data.Portrait == null);
                selectedPortraitInitialText.text = GetInitial(data.DisplayName);
            }

            inventoryTab?.SetContextSurvivor(data.DisplayName);
            skillsTab?.SetContextSurvivor(data.DisplayName);
            squadCustomiserTab?.SetSelectedIndex(index);

            if (useLiveGameData && index >= 0 && index < liveSurvivorContexts.Count)
            {
                ApplySelectionDataFromContext(liveSurvivorContexts[index]);
            }
        }

        private void RefreshTopStatus()
        {
            if (survivors.Count == 0)
            {
                return;
            }

            float health = 0f;
            int criticalCount = 0;
            for (int i = 0; i < survivors.Count; i++)
            {
                health += survivors[i].Health01;
                if (survivors[i].Condition == SquadListPanelController.SurvivorCondition.Critical)
                {
                    criticalCount++;
                }
            }

            health /= survivors.Count;

            if (conditionValueText != null)
            {
                if (health > 0.72f)
                {
                    conditionValueText.text = "Holding";
                }
                else if (health > 0.42f)
                {
                    conditionValueText.text = "Strained";
                }
                else
                {
                    conditionValueText.text = "Fragile";
                }
            }

            if (suppliesValueText != null)
            {
                int usableSlotCount = 0;

                if (useLiveGameData && liveSurvivorContexts.Count > 0)
                {
                    for (int i = 0; i < liveSurvivorContexts.Count; i++)
                    {
                        Unit unit = liveSurvivorContexts[i].Unit;
                        UnitInventory inventory = unit != null ? unit.Inventory : null;
                        if (inventory == null)
                        {
                            continue;
                        }

                        IReadOnlyList<ItemStack> stacks = inventory.Items;
                        for (int j = 0; j < stacks.Count; j++)
                        {
                            if (stacks[j].item != null && stacks[j].quantity > 0)
                            {
                                usableSlotCount++;
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < inventorySlots.Count; i++)
                    {
                        if (inventorySlots[i].State != InventoryTabController.InventorySlotState.Empty)
                        {
                            usableSlotCount++;
                        }
                    }
                }

                suppliesValueText.text = usableSlotCount >= 8 ? "Stable" : usableSlotCount >= 4 ? "Scarce" : "Critical";
            }

            if (threatValueText != null)
            {
                threatValueText.text = criticalCount >= 2 ? "Severe" : criticalCount == 1 ? "Elevated" : "Watchful";
            }
        }

        private void ShowTab(TabId tab)
        {
            activeTab = tab;

            if (inventoryTabRoot != null)
            {
                inventoryTabRoot.gameObject.SetActive(tab == TabId.Inventory);
            }

            if (skillsTabRoot != null)
            {
                skillsTabRoot.gameObject.SetActive(tab == TabId.Crafting);
            }

            if (squadCustomiserTabRoot != null)
            {
                squadCustomiserTabRoot.gameObject.SetActive(tab == TabId.Squad);
            }

            if (mapTabRoot != null)
            {
                mapTabRoot.gameObject.SetActive(tab == TabId.Map);
            }

            if (missionsTabRoot != null)
            {
                missionsTabRoot.gameObject.SetActive(tab == TabId.Missions);
            }

            SetTabButtonVisual(squadCustomiserTabButton, tab == TabId.Squad);
            SetTabButtonVisual(inventoryTabButton, tab == TabId.Inventory);
            SetTabButtonVisual(skillsTabButton, tab == TabId.Crafting);
            SetTabButtonVisual(mapTabButton, tab == TabId.Map);
            SetTabButtonVisual(missionsTabButton, tab == TabId.Missions);
        }

        private static void SetTabButtonVisual(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = active
                    ? new Color(0.44f, 0.31f, 0.16f, 1f)
                    : new Color(0.23f, 0.21f, 0.18f, 1f);
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.color = active
                    ? new Color(1f, 0.93f, 0.73f, 1f)
                    : new Color(0.72f, 0.67f, 0.56f, 1f);
            }
        }

        private TextMeshProUGUI CreateText(
            RectTransform parent,
            string text,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect("Text", parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = defaultFont;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite, bool raycastTarget = false)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static void StretchToParent(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void DestroySafely(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static string GetInitial(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            return value.Substring(0, 1).ToUpperInvariant();
        }

        private static Sprite CreateDistressedSprite(
            int width,
            int height,
            Color baseColor,
            Color shadowColor,
            Color rustColor,
            int seed)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            System.Random random = new System.Random(seed);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float noise = (float)random.NextDouble();
                    float rustNoise = (float)random.NextDouble();
                    Color color = Color.Lerp(baseColor, shadowColor, noise * 0.40f);
                    color = Color.Lerp(color, rustColor, rustNoise * 0.12f);

                    if (x < 2 || y < 2 || x > width - 3 || y > height - 3)
                    {
                        color *= 0.74f;
                    }

                    if (((x * 13) + (y * 7) + seed) % 41 == 0)
                    {
                        color = Color.Lerp(color, new Color(0.73f, 0.70f, 0.62f, 1f), 0.16f);
                    }

                    if (((x * 5) + (y * 11) + seed) % 53 == 0)
                    {
                        color = Color.Lerp(color, new Color(0.09f, 0.05f, 0.03f, 1f), 0.26f);
                    }

                    pixels[(y * width) + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateWarningStripeSprite(int size, Color a, Color b, int stripeWidth)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int stripe = ((x + y) / Mathf.Max(2, stripeWidth)) % 2;
                    Color color = stripe == 0 ? a : b;
                    pixels[(y * size) + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
