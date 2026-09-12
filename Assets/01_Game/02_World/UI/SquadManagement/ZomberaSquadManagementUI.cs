#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    // ReSharper disable InvertIf
    // ReSharper disable MergeIntoLogicalPattern
    // ReSharper disable ConvertIfStatementToReturnStatement
    // ReSharper disable MergeConditionalExpression
    // ReSharper disable ConvertIfStatementToSwitchExpression
    // ReSharper disable ConvertIfStatementToSwitchStatement
    // ReSharper disable ForCanBeConvertedToForeach
    // ReSharper disable UseObjectOrCollectionInitializer
    /// <summary>
    ///     Builds and coordinates a gritty squad management screen for Zombera.
    ///     Attach to any GameObject and press Play to auto-build the interface.
    /// </summary>
    public sealed partial class ZomberaSquadManagementUI : MonoBehaviour
    {
        private const float LiveDataUnitScanCacheSeconds = 2f;

        [Header("Build")] [SerializeField] private bool buildOnAwake = true;

        [SerializeField] private bool forceRebuildOnAwake = true;
        [SerializeField] private bool visibleOnStart;
        [SerializeField] private Canvas targetCanvas;

        [Header("Live Data")] [SerializeField] private bool useLiveGameData = true;

        [SerializeField] private bool keepDemoFallbackWhenNoLiveData;
        [SerializeField] private bool includePlayerInRoster = true;
        [SerializeField] private float liveRefreshInterval = 1f;

        [Header("Root")] [SerializeField] private RectTransform screenRoot;

        [Header("Top Bar")] [SerializeField] private TMP_Text squadNameText;

        [SerializeField] private TMP_Text conditionValueText;
        [SerializeField] private TMP_Text suppliesValueText;
        [SerializeField] private TMP_Text threatValueText;

        [Header("Selection Card")] [SerializeField]
        private RawImage selectedPortraitImage;

        [SerializeField] private TMP_Text selectedPortraitInitialText;
        [SerializeField] private TMP_Text selectedNameText;
        [SerializeField] private TMP_Text selectedConditionText;
        [SerializeField] private TMP_Text selectedHealthText;

        [Header("Tabs")] [SerializeField] private Button inventoryTabButton;
        [SerializeField] private Button craftingTabButton;
        [SerializeField] private Button skillsTabButton;
        [SerializeField] private Button squadCustomiserTabButton;
        [SerializeField] private Button formationsTabButton;
        [SerializeField] private Button jobsTabButton;
        [SerializeField] private Button factionsTabButton;
        [SerializeField] private Button mapTabButton;
        [SerializeField] private Button missionsTabButton;
        [SerializeField] private RectTransform inventoryTabRoot;
        [SerializeField] private RectTransform craftingTabRoot;
        [SerializeField] private RectTransform skillsTabRoot;
        [SerializeField] private RectTransform squadCustomiserTabRoot;
        [SerializeField] private RectTransform formationsTabRoot;
        [SerializeField] private RectTransform jobsTabRoot;
        [SerializeField] private RectTransform factionsTabRoot;
        [SerializeField] private RectTransform mapTabRoot;
        [SerializeField] private RectTransform missionsTabRoot;

        [Header("Panels")] [SerializeField] private SquadListPanelController squadListPanel;

        [SerializeField] private InventoryTabController inventoryTab;
        [SerializeField] private GameObject craftingTabPrefab;
        [SerializeField] private CraftingTabController craftingTab;
        [SerializeField] private SkillsTabController skillsTab;
        [SerializeField] private FormationsTabController formationsTab;
        [SerializeField] private JobsTabController jobsTab;
        [SerializeField] private FactionsTabController factionsTab;
        [SerializeField] private WorldMapPanelController mapTab;
        [SerializeField] private SquadCustomizerTabController squadCustomiserTab;

        [Header("Portrait Studio")]
        [SerializeField] private GameObject portraitStudioPrefab;
        [SerializeField] private PortraitStudioManager portraitStudio;
        [SerializeField] private RenderTexture portraitRT;

        private EquipmentSystem _subscribedEquipment;
        private UMA.CharacterSystem.DynamicCharacterAvatar _subscribedAvatar;

        private readonly List<InventoryTabController.InventorySlotData> _inventorySlots = new();

        private readonly List<LiveSurvivorContext> _liveSurvivorContexts = new();

        private readonly List<SkillsTabController.SkillEntryData> _skillEntries = new();

        private readonly List<SquadListPanelController.SurvivorEntryData> _survivors = new();

        private TabId _activeTab;
        private float _allUnitsCacheValidUntil = -1000f;
        private readonly List<Unit> _cachedAllUnits = new();

        private Unit _cachedPlayerUnit;
        private SquadManager _cachedSquadManager;
        private Sprite _cardSprite;

        private TMP_FontAsset _defaultFont;
        private float _liveRefreshTimer;
        private Sprite _panelSprite;
        private bool _pausedByVisibility;
        private float _playerUnitCacheValidUntil = -1000f;
        private TimeSystem _resolvedTimeSystem;
        private int _selectedSurvivorIndex = -1;
        private Sprite _slotSprite;
        private float _squadManagerCacheValidUntil = -1000f;
        private Sprite _stripeSprite;

        public bool IsVisible => screenRoot != null && screenRoot.gameObject.activeSelf;

        [ContextMenu("Rebuild Squad Management UI")]
        public void RebuildNow()
        {
            RebuildNowInternal();
        }

        public void SetVisible(bool visible)
        {
            SetVisibleInternal(visible);
        }

        [ContextMenu("Toggle Squad Management UI")]
        // ReSharper disable once UnusedMember.Global
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

        public void OpenFormationsTab()
        {
            OpenTabAndEnsureVisible(TabId.Formations);
        }

        public void OpenJobsTab()
        {
            OpenTabAndEnsureVisible(TabId.Jobs);
        }

        public void OpenFactionsTab()
        {
            OpenTabAndEnsureVisible(TabId.Factions);
        }

        public void OpenMapTab()
        {
            OpenTabAndEnsureVisible(TabId.Map);
        }

        public void OpenMissionsTab()
        {
            OpenTabAndEnsureVisible(TabId.Missions);
        }

        private enum TabId
        {
            Squad,
            Inventory,
            Crafting,
            Skills,
            Formations,
            Jobs,
            Factions,
            Map,
            Missions
        }
    }
}