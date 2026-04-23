using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using UnityEngine.UI;
using Zombera.Core;
using TMPro;

namespace Zombera.UI
{
    /// <summary>
    /// Manages the bottom-bar portrait strip. Discovers SquadPortraitSlot children, binds live
    /// squad units to them, and ticks HP/stamina bars every 0.1 s.
    /// </summary>
    [AddComponentMenu("Zombera/UI/Squad Portrait Strip")]
    [DisallowMultipleComponent]
    public sealed class SquadPortraitStrip : MonoBehaviour
    {
        // ── Serialized ────────────────────────────────────────────────────────

        [Header("Colors")]
        [SerializeField] private Color hpFull    = new Color(0.22f, 0.65f, 0.30f, 1f);
        [SerializeField] private Color hpLow     = new Color(0.72f, 0.22f, 0.12f, 1f);
        [SerializeField] private Color selectTint = new Color(0.20f, 0.55f, 0.38f, 0.50f);

        [Header("Portrait Framing")]
        [SerializeField, Range(0.10f, 1f)] private float faceCropScale = 0.40f;
        [SerializeField, Range(0f, 1f)] private float faceCropCenterX = 0.50f;
        [SerializeField, Range(0f, 1f)] private float faceCropCenterY = 0.87f;
        [SerializeField, Range(0f, 1f)] private float alphaDetectionThreshold = 0.02f;

        [Header("Portrait Headshot Capture")]
        [SerializeField] private bool useRuntimeHeadshotCapture = true;
        [SerializeField, Range(64, 512)] private int runtimeHeadshotResolution = 256;
        [SerializeField, Min(0.20f)] private float runtimeHeadshotDistance = 0.62f;
        [SerializeField] private float runtimeHeadshotVerticalOffset = 0.03f;
        [SerializeField] private float runtimeHeadshotLookOffset = 0.05f;
        [SerializeField, Range(15f, 70f)] private float runtimeHeadshotFieldOfView = 24f;
        [SerializeField] private bool runtimeHeadshotUseFillLight = true;
        [SerializeField, Range(0f, 4f)] private float runtimeHeadshotFillLightIntensity = 1.2f;
        [SerializeField] private Color runtimeHeadshotFillLightColor = new Color(1f, 0.98f, 0.94f, 1f);

        [Header("Squad Paging Tabs")]
        [SerializeField] private bool enableSquadTabs = true;
        [SerializeField] private bool autoEnableTabsForBottomStrip = true;
        [SerializeField, Min(1)] private int squadTabCount = 4;
        [SerializeField, Min(1)] private int slotsPerSquadTab = 5;
        [SerializeField] private Button[] squadTabButtons = Array.Empty<Button>();
        [SerializeField] private Color squadTabActiveColor = new Color(0.20f, 0.55f, 0.38f, 1f);
        [SerializeField] private Color squadTabInactiveColor = new Color(0.11f, 0.13f, 0.17f, 1f);

        // ── State ─────────────────────────────────────────────────────────────

        private SquadPortraitSlot[] _slots;
        private int _selected = -1;
        private int _activeSquadTabIndex;
        private float _ticker;
        private float _portraitRetryTicker;
        private readonly List<Sprite> _capturedPortraitSprites = new List<Sprite>(24);
        private readonly List<Texture2D> _capturedPortraitTextures = new List<Texture2D>(24);
        private readonly List<Unit> _rosterUnits = new List<Unit>(24);
        private readonly Dictionary<int, Sprite> _headshotCacheByUnitId = new Dictionary<int, Sprite>(24);
        private readonly Dictionary<int, Texture2D> _headshotCacheTexturesByUnitId = new Dictionary<int, Texture2D>(24);
        private readonly HashSet<int> _rosterUnitIds = new HashSet<int>();
        private readonly List<int> _cacheRemovalBuffer = new List<int>(24);
        private static bool _runtimeHeadshotCaptureDisabledForSession;
        private static bool _runtimeHeadshotCaptureDisableLogged;

        /// <summary>Fired when the player clicks a portrait. Unit may be null if slot is empty.</summary>
        public event System.Action<Unit> OnPortraitClicked;

        /// <summary>Current selected unit for this strip, or null if nothing is selected.</summary>
        public Unit SelectedUnit =>
            _slots != null && _selected >= 0 && _selected < _slots.Length
                ? _slots[_selected].BoundUnit
                : null;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Start()
        {
            _slots = GetComponentsInChildren<SquadPortraitSlot>(true);
            EnsureRosterHeaderTabsNearLabel();
            EnsureRuntimeSquadTabButtons();
            ConfigureSquadTabButtons();
            RefreshBindings();
        }

        private void OnDestroy()
        {
            ReleaseCapturedPortraits();
            ClearHeadshotCache();
        }

        private void OnEnable()
        {
            EnsureRosterHeaderTabsNearLabel();

            if (_slots != null && _slots.Length > 0)
            {
                EnsureRuntimeSquadTabButtons();
                ConfigureSquadTabButtons();
                RefreshBindings();
            }
        }

        private void Update()
        {
            _ticker += Time.unscaledDeltaTime;
            if (_ticker < 0.1f) return;
            _ticker = 0f;
            TickBars();

            _portraitRetryTicker += 0.1f;
            if (_portraitRetryTicker >= 2f)
            {
                _portraitRetryTicker = 0f;
                RetryMissingPortraits();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Re-scan the scene for squad units and rebind portrait slots.</summary>
        public void RefreshBindings()
        {
            if (_slots == null || _slots.Length == 0) return;

            Unit previousSelection = SelectedUnit;

            ReleaseCapturedPortraits();

            Unit[] all = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            _rosterUnits.Clear();

            foreach (Unit u in all)
            {
                if (!u.IsAlive) continue;
                if (u.Role == UnitRole.Player       ||
                    u.Role == UnitRole.SquadMember  ||
                    u.Role == UnitRole.Survivor)
                {
                    _rosterUnits.Add(u);
                }
            }

            _rosterUnits.Sort(CompareUnitsForRoster);
            PruneHeadshotCacheToRoster();

            EnsureRuntimeSquadTabButtons();
            ConfigureSquadTabButtons();

            bool useSquadTabs = IsSquadTabPagingActive();
            if (useSquadTabs)
            {
                int pageSize = GetSquadTabPageSize();
                int maxTabIndex = Mathf.Max(0, GetConfiguredSquadTabCount() - 1);

                if (previousSelection != null)
                {
                    int previousRosterIndex = _rosterUnits.IndexOf(previousSelection);
                    if (previousRosterIndex >= 0)
                    {
                        _activeSquadTabIndex = Mathf.Clamp(previousRosterIndex / pageSize, 0, maxTabIndex);
                    }
                    else
                    {
                        _activeSquadTabIndex = Mathf.Clamp(_activeSquadTabIndex, 0, maxTabIndex);
                    }
                }
                else
                {
                    _activeSquadTabIndex = Mathf.Clamp(_activeSquadTabIndex, 0, maxTabIndex);
                }

                BindRosterSliceToSlots(_activeSquadTabIndex * pageSize, pageSize);
            }
            else
            {
                _activeSquadTabIndex = 0;
                BindRosterSliceToSlots(0, _slots.Length);
            }

            UpdateSquadTabVisuals();

            if (previousSelection != null && previousSelection.IsAlive && TrySelectUnit(previousSelection))
            {
                return;
            }

            SelectPlayerOrFirstBoundUnit();
        }

        public void SetActiveSquadTab(int tabIndex)
        {
            if (!IsSquadTabPagingActive())
            {
                return;
            }

            int clampedTabIndex = Mathf.Clamp(tabIndex, 0, Mathf.Max(0, GetConfiguredSquadTabCount() - 1));
            if (clampedTabIndex == _activeSquadTabIndex)
            {
                return;
            }

            Unit previouslySelected = SelectedUnit;
            _activeSquadTabIndex = clampedTabIndex;
            BindRosterSliceToSlots(_activeSquadTabIndex * GetSquadTabPageSize(), GetSquadTabPageSize());
            UpdateSquadTabVisuals();

            if (previouslySelected != null && TrySelectUnitInVisibleSlots(previouslySelected, false))
            {
                return;
            }

            SelectSlot(-1, false);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void BindRosterSliceToSlots(int startIndex, int visibleCapacity)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                SquadPortraitSlot slot = _slots[i];
                int rosterIndex = startIndex + i;
                bool showSlot = i < visibleCapacity && rosterIndex >= 0 && rosterIndex < _rosterUnits.Count;

                if (showSlot)
                {
                    Unit boundUnit = _rosterUnits[rosterIndex];

                    slot.BoundUnit = boundUnit;
                    slot.gameObject.SetActive(true);
                    if (slot.nameLabel != null)
                        slot.nameLabel.text = boundUnit.gameObject.name;

                    if (!TryApplySelectionPortrait(slot, boundUnit))
                        ApplyPortraitFromUnitHead(slot, boundUnit);

                    int captured = i;
                    slot.slotButton?.onClick.RemoveAllListeners();
                    slot.slotButton?.onClick.AddListener(() => SelectSlot(captured));
                }
                else
                {
                    slot.BoundUnit = null;
                    slot.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Select the first active slot that has a bound unit.</summary>
        public bool SelectFirstBoundUnit()
        {
            if (_slots == null || _slots.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                SquadPortraitSlot slot = _slots[i];
                if (slot == null || !slot.gameObject.activeSelf || slot.BoundUnit == null)
                {
                    continue;
                }

                SelectSlot(i);
                return true;
            }

            SelectSlot(-1);
            return false;
        }

        /// <summary>Select player unit if present; otherwise select the first bound unit.</summary>
        public bool SelectPlayerOrFirstBoundUnit()
        {
            if (TrySelectFirstRole(UnitRole.Player))
            {
                return true;
            }

            return SelectFirstBoundUnit();
        }

        /// <summary>Select a specific bound unit if present in this strip.</summary>
        public bool TrySelectUnit(Unit unit)
        {
            if (unit == null || _slots == null || _slots.Length == 0)
            {
                return false;
            }

            if (IsSquadTabPagingActive())
            {
                int rosterIndex = _rosterUnits.IndexOf(unit);
                if (rosterIndex >= 0)
                {
                    int pageSize = GetSquadTabPageSize();
                    int maxTabIndex = Mathf.Max(0, GetConfiguredSquadTabCount() - 1);
                    int desiredTabIndex = Mathf.Clamp(rosterIndex / pageSize, 0, maxTabIndex);

                    if (desiredTabIndex != _activeSquadTabIndex)
                    {
                        _activeSquadTabIndex = desiredTabIndex;
                        BindRosterSliceToSlots(_activeSquadTabIndex * pageSize, pageSize);
                        UpdateSquadTabVisuals();
                    }
                }
            }

            return TrySelectUnitInVisibleSlots(unit, true);
        }

        private bool TrySelectUnitInVisibleSlots(Unit unit, bool invokeEvent)
        {
            if (unit == null || _slots == null || _slots.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                SquadPortraitSlot slot = _slots[i];
                if (slot == null || !slot.gameObject.activeSelf)
                {
                    continue;
                }

                if (slot.BoundUnit == unit)
                {
                    SelectSlot(i, invokeEvent);
                    return true;
                }
            }

            return false;
        }

        private void TickBars()
        {
            if (_slots == null) return;
            foreach (SquadPortraitSlot slot in _slots)
            {
                if (!slot.gameObject.activeSelf || slot.BoundUnit == null) continue;
                Unit u = slot.BoundUnit;

                if (slot.hpFill != null && u.Health != null)
                {
                    float frac = u.Health.MaxHealth > 0f
                        ? Mathf.Clamp01(u.Health.CurrentHealth / u.Health.MaxHealth)
                        : 0f;
                    slot.hpFill.fillAmount = frac;
                    slot.hpFill.color = Color.Lerp(hpLow, hpFull, frac);
                }

                if (slot.staminaFill != null && u.Stats != null)
                    slot.staminaFill.fillAmount = u.Stats.StaminaRatio;
            }
        }

        private void SelectSlot(int idx, bool invokeEvent = true)
        {
            _selected = idx;
            for (int i = 0; i < _slots.Length; i++)
            {
                var ovl = _slots[i].selectOverlay;
                if (ovl == null) continue;
                ovl.color = selectTint;
                ovl.gameObject.SetActive(i == idx);
            }

            Unit selected = idx >= 0 && idx < _slots.Length ? _slots[idx].BoundUnit : null;
            if (invokeEvent)
            {
                OnPortraitClicked?.Invoke(selected);
            }
        }

        private bool TrySelectFirstRole(UnitRole role)
        {
            if (_rosterUnits == null || _rosterUnits.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _rosterUnits.Count; i++)
            {
                Unit unit = _rosterUnits[i];
                if (unit != null && unit.Role == role)
                {
                    return TrySelectUnit(unit);
                }
            }

            return false;
        }

        private bool IsSquadTabPagingActive()
        {
            if (!enableSquadTabs || _slots == null || _slots.Length == 0)
            {
                return false;
            }

            if (autoEnableTabsForBottomStrip &&
                !string.Equals(gameObject.name, "PortraitStrip", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private int GetConfiguredSquadTabCount()
        {
            return Mathf.Max(1, squadTabCount);
        }

        private int GetSquadTabPageSize()
        {
            if (_slots == null || _slots.Length == 0)
            {
                return 1;
            }

            return Mathf.Clamp(slotsPerSquadTab, 1, _slots.Length);
        }

        private void EnsureRuntimeSquadTabButtons()
        {
            if (!IsSquadTabPagingActive() || HasAnyConfiguredTabButton())
            {
                return;
            }

            RectTransform stripParent = transform.parent as RectTransform;
            if (stripParent == null)
            {
                return;
            }

            RectTransform tabsRoot = stripParent.Find("SquadPageTabs_Runtime") as RectTransform;
            if (tabsRoot == null)
            {
                GameObject root = new GameObject("SquadPageTabs_Runtime", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                tabsRoot = root.GetComponent<RectTransform>();
                tabsRoot.SetParent(stripParent, false);
                tabsRoot.anchorMin = new Vector2(0f, 1f);
                tabsRoot.anchorMax = new Vector2(0f, 1f);
                tabsRoot.pivot = new Vector2(0f, 1f);
                tabsRoot.anchoredPosition = new Vector2(8f, -6f);
                tabsRoot.sizeDelta = new Vector2(312f, 38f);

                HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            int configuredTabCount = GetConfiguredSquadTabCount();
            var runtimeButtons = new List<Button>(configuredTabCount);

            for (int i = 0; i < configuredTabCount; i++)
            {
                Button button = null;
                if (i < tabsRoot.childCount)
                {
                    button = tabsRoot.GetChild(i).GetComponent<Button>();
                }

                if (button == null)
                {
                    GameObject buttonGO = new GameObject($"Tab_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                    RectTransform buttonRT = buttonGO.GetComponent<RectTransform>();
                    buttonRT.SetParent(tabsRoot, false);

                    Image buttonImage = buttonGO.GetComponent<Image>();
                    buttonImage.color = squadTabInactiveColor;

                    button = buttonGO.GetComponent<Button>();
                    button.targetGraphic = buttonImage;

                    LayoutElement layoutElement = buttonGO.GetComponent<LayoutElement>();
                    layoutElement.minWidth = 72f;
                    layoutElement.preferredWidth = 72f;
                    layoutElement.minHeight = 38f;
                    layoutElement.preferredHeight = 38f;
                    layoutElement.flexibleWidth = 0f;

                    GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    RectTransform labelRT = labelGO.GetComponent<RectTransform>();
                    labelRT.SetParent(buttonGO.transform, false);
                    labelRT.anchorMin = Vector2.zero;
                    labelRT.anchorMax = Vector2.one;
                    labelRT.offsetMin = Vector2.zero;
                    labelRT.offsetMax = Vector2.zero;

                    TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
                    label.fontSize = 18f;
                    label.fontStyle = FontStyles.Bold;
                    label.color = new Color(0.90f, 0.92f, 0.94f, 1f);
                    label.alignment = TextAlignmentOptions.Center;
                    label.raycastTarget = false;
                }

                runtimeButtons.Add(button);
            }

            for (int i = configuredTabCount; i < tabsRoot.childCount; i++)
            {
                tabsRoot.GetChild(i).gameObject.SetActive(false);
            }

            squadTabButtons = runtimeButtons.ToArray();
        }

        private void EnsureRosterHeaderTabsNearLabel()
        {
            Transform stripParent = transform.parent;
            if (stripParent == null)
            {
                return;
            }

            RectTransform rosterHeader = stripParent.Find("RosterHeader") as RectTransform;
            if (rosterHeader == null)
            {
                return;
            }

            bool changed = false;

            Transform rosterLabelTransform = rosterHeader.Find("RosterLabel");
            if (rosterLabelTransform != null)
            {
                LayoutElement rosterLabelLayout = rosterLabelTransform.GetComponent<LayoutElement>();
                if (rosterLabelLayout != null && !Mathf.Approximately(rosterLabelLayout.flexibleWidth, 0f))
                {
                    rosterLabelLayout.flexibleWidth = 0f;
                    changed = true;
                }
            }

            Transform tabsTransform = rosterHeader.Find("SquadPageTabs");
            if (tabsTransform != null)
            {
                LayoutElement tabsLayout = tabsTransform.GetComponent<LayoutElement>();
                if (tabsLayout != null && !Mathf.Approximately(tabsLayout.flexibleWidth, 0f))
                {
                    tabsLayout.flexibleWidth = 0f;
                    changed = true;
                }
            }

            if (changed)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rosterHeader);
            }
        }

        private bool HasAnyConfiguredTabButton()
        {
            if (squadTabButtons == null)
            {
                return false;
            }

            for (int i = 0; i < squadTabButtons.Length; i++)
            {
                if (squadTabButtons[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ConfigureSquadTabButtons()
        {
            if (squadTabButtons == null || squadTabButtons.Length == 0)
            {
                return;
            }

            bool tabsActive = IsSquadTabPagingActive();
            int configuredTabCount = GetConfiguredSquadTabCount();

            for (int i = 0; i < squadTabButtons.Length; i++)
            {
                Button button = squadTabButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool shouldBeVisible = tabsActive && i < configuredTabCount;
                if (button.gameObject.activeSelf != shouldBeVisible)
                {
                    button.gameObject.SetActive(shouldBeVisible);
                }

                if (!shouldBeVisible)
                {
                    continue;
                }

                int capturedTabIndex = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SetActiveSquadTab(capturedTabIndex));

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = (capturedTabIndex + 1).ToString();
                }
            }

            _activeSquadTabIndex = Mathf.Clamp(_activeSquadTabIndex, 0, Mathf.Max(0, configuredTabCount - 1));
            UpdateSquadTabVisuals();
        }

        private void UpdateSquadTabVisuals()
        {
            if (squadTabButtons == null || squadTabButtons.Length == 0)
            {
                return;
            }

            bool tabsActive = IsSquadTabPagingActive();
            int configuredTabCount = GetConfiguredSquadTabCount();

            for (int i = 0; i < squadTabButtons.Length; i++)
            {
                Button button = squadTabButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool shouldBeVisible = tabsActive && i < configuredTabCount;
                if (button.gameObject.activeSelf != shouldBeVisible)
                {
                    button.gameObject.SetActive(shouldBeVisible);
                }

                Image buttonImage = button.targetGraphic as Image;
                if (buttonImage == null)
                {
                    buttonImage = button.GetComponent<Image>();
                }

                if (buttonImage != null)
                {
                    buttonImage.color = i == _activeSquadTabIndex ? squadTabActiveColor : squadTabInactiveColor;
                }
            }
        }

        private static int CompareUnitsForRoster(Unit a, Unit b)
        {
            int roleCompare = GetRolePriority(a.Role).CompareTo(GetRolePriority(b.Role));
            if (roleCompare != 0)
            {
                return roleCompare;
            }

            return string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.OrdinalIgnoreCase);
        }

        private static int GetRolePriority(UnitRole role)
        {
            if (role == UnitRole.Player)
            {
                return 0;
            }

            if (role == UnitRole.SquadMember)
            {
                return 1;
            }

            if (role == UnitRole.Survivor)
            {
                return 2;
            }

            return 3;
        }

        private void RetryMissingPortraits()
        {
            if (_slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                SquadPortraitSlot slot = _slots[i];
                if (slot == null || !slot.gameObject.activeSelf || slot.BoundUnit == null || slot.portraitImage == null)
                    continue;

                if (slot.portraitImage.sprite == null)
                {
                    if (!TryApplySelectionPortrait(slot, slot.BoundUnit))
                        ApplyPortraitFromUnitHead(slot, slot.BoundUnit);
                }
            }
        }

        private bool TryApplySelectionPortrait(SquadPortraitSlot slot, Unit unit)
        {
            if (slot == null || slot.portraitImage == null || unit == null)
                return false;

            if (unit.Role != UnitRole.Player)
                return false;

            Sprite selectedPortrait = CharacterSelectionState.SelectedPortraitSprite;
            if (selectedPortrait == null)
                return false;

            Sprite portraitSprite = selectedPortrait;
            Sprite cropped = CreateFaceCropSprite(selectedPortrait);
            if (cropped != null)
            {
                _capturedPortraitSprites.Add(cropped);
                portraitSprite = cropped;
            }

            slot.portraitImage.sprite = portraitSprite;
            slot.portraitImage.color = Color.white;
            slot.portraitImage.type = Image.Type.Simple;
            slot.portraitImage.preserveAspect = true;
            return true;
        }

        private void ApplyPortraitFromUnitHead(SquadPortraitSlot slot, Unit unit)
        {
            if (slot == null || unit == null || slot.portraitImage == null) return;

            slot.portraitImage.sprite = null;
            slot.portraitImage.color = new Color(0.20f, 0.20f, 0.25f, 1f);

            int unitInstanceId = unit.GetInstanceID();
            if (TryGetCachedHeadshot(unitInstanceId, out Sprite cachedHeadshot))
            {
                slot.portraitImage.sprite = cachedHeadshot;
                slot.portraitImage.color = Color.white;
                slot.portraitImage.type = Image.Type.Simple;
                slot.portraitImage.preserveAspect = true;
                return;
            }

            Transform head = FindHeadTransform(unit.transform);
            if (head == null)
            {
                return;
            }

            if (TryCaptureRuntimeHeadshot(unit, head, out Sprite headshotSprite, out Texture2D headshotTexture))
            {
                CacheHeadshot(unitInstanceId, headshotSprite, headshotTexture);

                slot.portraitImage.sprite = headshotSprite;
                slot.portraitImage.color = Color.white;
                slot.portraitImage.type = Image.Type.Simple;
                slot.portraitImage.preserveAspect = true;
                return;
            }

            if (!TryResolvePortraitTexture(unit.transform, head, out Texture tex) || tex == null)
                return;

            Sprite sprite = CreateFaceCropSprite(tex);
            if (sprite == null)
            {
                if (!TryCreateSpriteFromTexture(tex, out sprite))
                {
                    return;
                }
            }

            _capturedPortraitSprites.Add(sprite);

            slot.portraitImage.sprite = sprite;
            slot.portraitImage.color = Color.white;
            slot.portraitImage.type = Image.Type.Simple;
            slot.portraitImage.preserveAspect = true;
        }

        private bool TryGetCachedHeadshot(int unitInstanceId, out Sprite headshotSprite)
        {
            headshotSprite = null;

            if (!_headshotCacheByUnitId.TryGetValue(unitInstanceId, out Sprite cachedSprite))
            {
                return false;
            }

            if (cachedSprite == null)
            {
                RemoveCachedHeadshot(unitInstanceId);
                return false;
            }

            headshotSprite = cachedSprite;
            return true;
        }

        private void CacheHeadshot(int unitInstanceId, Sprite headshotSprite, Texture2D headshotTexture)
        {
            if (headshotSprite == null)
            {
                if (headshotTexture != null)
                {
                    Destroy(headshotTexture);
                }

                return;
            }

            RemoveCachedHeadshot(unitInstanceId);

            _headshotCacheByUnitId[unitInstanceId] = headshotSprite;
            if (headshotTexture != null)
            {
                _headshotCacheTexturesByUnitId[unitInstanceId] = headshotTexture;
            }
        }

        private void RemoveCachedHeadshot(int unitInstanceId)
        {
            if (_headshotCacheByUnitId.TryGetValue(unitInstanceId, out Sprite cachedSprite) && cachedSprite != null)
            {
                Destroy(cachedSprite);
            }

            _headshotCacheByUnitId.Remove(unitInstanceId);

            if (_headshotCacheTexturesByUnitId.TryGetValue(unitInstanceId, out Texture2D cachedTexture) && cachedTexture != null)
            {
                Destroy(cachedTexture);
            }

            _headshotCacheTexturesByUnitId.Remove(unitInstanceId);
        }

        private void PruneHeadshotCacheToRoster()
        {
            if (_headshotCacheByUnitId.Count == 0)
            {
                return;
            }

            _rosterUnitIds.Clear();
            for (int i = 0; i < _rosterUnits.Count; i++)
            {
                Unit unit = _rosterUnits[i];
                if (unit != null)
                {
                    _rosterUnitIds.Add(unit.GetInstanceID());
                }
            }

            _cacheRemovalBuffer.Clear();
            foreach (KeyValuePair<int, Sprite> pair in _headshotCacheByUnitId)
            {
                if (!_rosterUnitIds.Contains(pair.Key))
                {
                    _cacheRemovalBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < _cacheRemovalBuffer.Count; i++)
            {
                RemoveCachedHeadshot(_cacheRemovalBuffer[i]);
            }

            _cacheRemovalBuffer.Clear();
            _rosterUnitIds.Clear();
        }

        private void ClearHeadshotCache()
        {
            if (_headshotCacheByUnitId.Count == 0)
            {
                _headshotCacheTexturesByUnitId.Clear();
                return;
            }

            _cacheRemovalBuffer.Clear();
            foreach (KeyValuePair<int, Sprite> pair in _headshotCacheByUnitId)
            {
                _cacheRemovalBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _cacheRemovalBuffer.Count; i++)
            {
                RemoveCachedHeadshot(_cacheRemovalBuffer[i]);
            }

            _cacheRemovalBuffer.Clear();
            _headshotCacheByUnitId.Clear();
            _headshotCacheTexturesByUnitId.Clear();
        }

        private bool TryCaptureRuntimeHeadshot(Unit unit, Transform head, out Sprite portraitSprite, out Texture2D portraitTexture)
        {
            portraitSprite = null;
            portraitTexture = null;

            if (!Application.isPlaying
                || !useRuntimeHeadshotCapture
                || _runtimeHeadshotCaptureDisabledForSession
                || unit == null
                || head == null)
            {
                return false;
            }

            int resolution = Mathf.Clamp(runtimeHeadshotResolution, 64, 512);
            RenderTexture headshotRenderTexture = RenderTexture.GetTemporary(
                resolution,
                resolution,
                16,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);

            headshotRenderTexture.name = "SquadPortraitHeadshotRT";

            GameObject cameraObject = null;
            GameObject lightObject = null;
            Texture2D capturedTexture = null;

            try
            {
                cameraObject = new GameObject("SquadPortraitCaptureCamera", typeof(Camera));
                Camera captureCamera = cameraObject.GetComponent<Camera>();
                captureCamera.enabled = false;
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                captureCamera.cullingMask = ~0;
                captureCamera.nearClipPlane = 0.01f;
                captureCamera.farClipPlane = 8f;
                captureCamera.fieldOfView = runtimeHeadshotFieldOfView;
                captureCamera.allowHDR = false;
                captureCamera.allowMSAA = false;
                captureCamera.targetTexture = headshotRenderTexture;

                Vector3 targetPosition = head.position;
                Vector3 lookPosition = targetPosition + Vector3.up * runtimeHeadshotLookOffset;
                Vector3 forward = ResolvePortraitForward(head, unit.transform);
                Vector3 cameraPosition = targetPosition + Vector3.up * runtimeHeadshotVerticalOffset - forward * runtimeHeadshotDistance;

                captureCamera.transform.position = cameraPosition;
                captureCamera.transform.rotation = Quaternion.LookRotation(lookPosition - cameraPosition, Vector3.up);

                if (runtimeHeadshotUseFillLight)
                {
                    lightObject = new GameObject("SquadPortraitCaptureLight", typeof(Light));
                    Light fillLight = lightObject.GetComponent<Light>();
                    fillLight.type = LightType.Directional;
                    fillLight.color = runtimeHeadshotFillLightColor;
                    fillLight.intensity = runtimeHeadshotFillLightIntensity;
                    fillLight.shadows = LightShadows.None;
                    fillLight.transform.rotation = captureCamera.transform.rotation * Quaternion.Euler(12f, -20f, 0f);
                }

                captureCamera.Render();

                if (!TryCopyRenderTextureToTexture2D(headshotRenderTexture, out capturedTexture) || capturedTexture == null)
                {
                    return false;
                }

                RectInt sourceRect = new RectInt(0, 0, capturedTexture.width, capturedTexture.height);
                RectInt subjectRect = TryFindOpaqueBounds(capturedTexture, sourceRect, alphaDetectionThreshold, out RectInt detected)
                    ? detected
                    : sourceRect;
                RectInt cropRect = ComputeFaceCropRect(sourceRect, subjectRect);

                portraitSprite = Sprite.Create(
                    capturedTexture,
                    new Rect(cropRect.x, cropRect.y, cropRect.width, cropRect.height),
                    new Vector2(0.5f, 0.5f),
                    100f);

                if (portraitSprite == null)
                {
                    Destroy(capturedTexture);
                    return false;
                }

                portraitTexture = capturedTexture;
                capturedTexture = null;
                return true;
            }
            catch (Exception)
            {
                DisableRuntimeHeadshotCaptureForSession();

                if (capturedTexture != null)
                {
                    Destroy(capturedTexture);
                }

                return false;
            }
            finally
            {
                if (cameraObject != null)
                {
                    Destroy(cameraObject);
                }

                if (lightObject != null)
                {
                    Destroy(lightObject);
                }

                RenderTexture.ReleaseTemporary(headshotRenderTexture);
            }
        }

        private void DisableRuntimeHeadshotCaptureForSession()
        {
            _runtimeHeadshotCaptureDisabledForSession = true;
            if (_runtimeHeadshotCaptureDisableLogged)
            {
                return;
            }

            _runtimeHeadshotCaptureDisableLogged = true;
            Debug.LogWarning(
                "[SquadPortraitStrip] Runtime headshot capture disabled for this play session after a render failure. Falling back to texture portraits.",
                this);
        }

        private static Vector3 ResolvePortraitForward(Transform head, Transform unitRoot)
        {
            Vector3 forward = head != null ? head.forward : Vector3.forward;

            if (forward.sqrMagnitude <= 0.0001f && unitRoot != null)
            {
                forward = unitRoot.forward;
            }

            forward = Vector3.ProjectOnPlane(forward, Vector3.up);

            if (forward.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return forward.normalized;
        }

        private Sprite CreateFaceCropSprite(Sprite sourceSprite)
        {
            if (sourceSprite == null || sourceSprite.texture == null)
                return null;

            Rect textureRect = sourceSprite.textureRect;
            RectInt sourceRect = new RectInt(
                Mathf.RoundToInt(textureRect.x),
                Mathf.RoundToInt(textureRect.y),
                Mathf.RoundToInt(textureRect.width),
                Mathf.RoundToInt(textureRect.height));

            if (sourceRect.width <= 0 || sourceRect.height <= 0)
                return null;

            RectInt subjectRect = TryFindOpaqueBounds(sourceSprite.texture, sourceRect, alphaDetectionThreshold, out RectInt detected)
                ? detected
                : sourceRect;

            RectInt cropRect = ComputeFaceCropRect(sourceRect, subjectRect);

            return Sprite.Create(
                sourceSprite.texture,
                new Rect(cropRect.x, cropRect.y, cropRect.width, cropRect.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static bool TryResolvePortraitTexture(Transform unitRoot, Transform head, out Texture texture)
        {
            texture = null;

            // First, prefer renderer nearest the head transform.
            Renderer headRenderer = head.GetComponentInChildren<Renderer>(true);
            if (headRenderer != null && TryGetTextureFromRenderer(headRenderer, out texture))
                return true;

            // Next, scan renderers and prefer those with head/face in the name.
            Renderer[] renderers = unitRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                string n = r.gameObject.name;
                if (!string.IsNullOrEmpty(n))
                {
                    string lower = n.ToLowerInvariant();
                    if ((lower.Contains("head") || lower.Contains("face")) &&
                        TryGetTextureFromRenderer(r, out texture))
                        return true;
                }
            }

            // Fallback: first renderer with a usable texture.
            for (int i = 0; i < renderers.Length; i++)
            {
                if (TryGetTextureFromRenderer(renderers[i], out texture))
                    return true;
            }

            return false;
        }

        private static bool TryGetTextureFromRenderer(Renderer renderer, out Texture texture)
        {
            texture = null;
            if (renderer == null) return false;

            Material[] materials = renderer.sharedMaterials;
            if (materials != null)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (TryGetTextureFromMaterial(materials[i], out texture))
                    {
                        return true;
                    }
                }
            }

            return TryGetTextureFromMaterial(renderer.sharedMaterial, out texture);
        }

        private static bool TryGetTextureFromMaterial(Material mat, out Texture texture)
        {
            texture = null;
            if (mat == null)
            {
                return false;
            }

            if (mat.HasProperty("_BaseMap")) texture = mat.GetTexture("_BaseMap");
            if (texture == null && mat.HasProperty("_MainTex")) texture = mat.GetTexture("_MainTex");
            if (texture == null) texture = mat.mainTexture;

            return texture != null && texture.width > 0 && texture.height > 0;
        }

        private Sprite CreateFaceCropSprite(Texture texture)
        {
            if (!TryGetTexture2D(texture, out Texture2D texture2D, out bool createdRuntimeTexture))
                return null;

            RectInt sourceRect = new RectInt(0, 0, texture2D.width, texture2D.height);
            RectInt subjectRect = TryFindOpaqueBounds(texture2D, sourceRect, alphaDetectionThreshold, out RectInt detected)
                ? detected
                : sourceRect;

            RectInt cropRect = ComputeFaceCropRect(sourceRect, subjectRect);

            Sprite sprite = Sprite.Create(
                texture2D,
                new Rect(cropRect.x, cropRect.y, cropRect.width, cropRect.height),
                new Vector2(0.5f, 0.5f),
                100f);

            if (sprite != null && createdRuntimeTexture)
            {
                _capturedPortraitTextures.Add(texture2D);
            }

            return sprite;
        }

        private bool TryCreateSpriteFromTexture(Texture texture, out Sprite sprite)
        {
            sprite = null;
            if (!TryGetTexture2D(texture, out Texture2D texture2D, out bool createdRuntimeTexture))
            {
                return false;
            }

            sprite = Sprite.Create(
                texture2D,
                new Rect(0f, 0f, texture2D.width, texture2D.height),
                new Vector2(0.5f, 0.5f),
                100f);

            if (sprite != null && createdRuntimeTexture)
            {
                _capturedPortraitTextures.Add(texture2D);
            }

            return sprite != null;
        }

        private static bool TryGetTexture2D(Texture sourceTexture, out Texture2D texture2D, out bool createdRuntimeTexture)
        {
            texture2D = null;
            createdRuntimeTexture = false;

            if (sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0)
            {
                return false;
            }

            if (sourceTexture is Texture2D directTexture)
            {
                texture2D = directTexture;
                return true;
            }

            RenderTexture readbackSource = null;
            bool temporarySource = false;

            if (sourceTexture is RenderTexture renderTexture)
            {
                readbackSource = renderTexture;
            }
            else
            {
                readbackSource = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                temporarySource = true;
                Graphics.Blit(sourceTexture, readbackSource);
            }

            bool copied = TryCopyRenderTextureToTexture2D(readbackSource, out texture2D);

            if (temporarySource)
            {
                RenderTexture.ReleaseTemporary(readbackSource);
            }

            if (!copied || texture2D == null)
            {
                return false;
            }

            createdRuntimeTexture = true;
            return true;
        }

        private static bool TryCopyRenderTextureToTexture2D(RenderTexture source, out Texture2D texture2D)
        {
            texture2D = null;
            if (source == null || source.width <= 0 || source.height <= 0)
            {
                return false;
            }

            RenderTexture previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                texture2D = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
                texture2D.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                texture2D.Apply(false, false);
                return true;
            }
            catch (Exception)
            {
                if (texture2D != null)
                {
                    UnityEngine.Object.Destroy(texture2D);
                    texture2D = null;
                }

                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private RectInt ComputeFaceCropRect(RectInt sourceRect, RectInt subjectRect)
        {
            int baseSize = Mathf.Min(subjectRect.width, subjectRect.height);
            int cropSize = Mathf.Clamp(Mathf.RoundToInt(baseSize * Mathf.Clamp(faceCropScale, 0.10f, 1f)), 8, baseSize);

            int centerX = subjectRect.xMin + Mathf.RoundToInt(subjectRect.width * Mathf.Clamp01(faceCropCenterX));
            int centerY = subjectRect.yMin + Mathf.RoundToInt(subjectRect.height * Mathf.Clamp01(faceCropCenterY));

            int minX = sourceRect.xMin;
            int maxX = sourceRect.xMax - cropSize;
            int minY = sourceRect.yMin;
            int maxY = sourceRect.yMax - cropSize;

            int cropX = Mathf.Clamp(centerX - (cropSize / 2), minX, maxX);
            int cropY = Mathf.Clamp(centerY - (cropSize / 2), minY, maxY);

            return new RectInt(cropX, cropY, cropSize, cropSize);
        }

        private static bool TryFindOpaqueBounds(Texture2D texture, RectInt sourceRect, float alphaThreshold, out RectInt bounds)
        {
            bounds = sourceRect;
            if (texture == null || sourceRect.width <= 0 || sourceRect.height <= 0)
                return false;

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException)
            {
                // Non-readable textures cannot be scanned for alpha; fallback to source rect.
                return false;
            }

            int width = texture.width;
            byte alphaCutoff = (byte)Mathf.Clamp(Mathf.RoundToInt(alphaThreshold * 255f), 0, 255);

            int minX = sourceRect.xMax;
            int minY = sourceRect.yMax;
            int maxX = sourceRect.xMin;
            int maxY = sourceRect.yMin;
            bool found = false;

            for (int y = sourceRect.yMin; y < sourceRect.yMax; y++)
            {
                int row = y * width;
                for (int x = sourceRect.xMin; x < sourceRect.xMax; x++)
                {
                    if (pixels[row + x].a <= alphaCutoff) continue;
                    if (!found)
                    {
                        minX = maxX = x;
                        minY = maxY = y;
                        found = true;
                    }
                    else
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (!found)
                return false;

            bounds = new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
            return true;
        }

        private static Transform FindHeadTransform(Transform root)
        {
            if (root == null) return null;

            Animator animator = root.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null) return headBone;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (string.IsNullOrEmpty(n)) continue;
                string lower = n.ToLowerInvariant();
                if (lower.Contains("head") || lower.Contains("face"))
                    return all[i];
            }

            return root;
        }

        private void ReleaseCapturedPortraits()
        {
            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null && _slots[i].portraitImage != null)
                    {
                        _slots[i].portraitImage.sprite = null;
                        _slots[i].portraitImage.color = new Color(0.20f, 0.20f, 0.25f, 1f);
                    }
                }
            }

            for (int i = 0; i < _capturedPortraitSprites.Count; i++)
            {
                if (_capturedPortraitSprites[i] != null)
                    Destroy(_capturedPortraitSprites[i]);
            }
            _capturedPortraitSprites.Clear();

            for (int i = 0; i < _capturedPortraitTextures.Count; i++)
            {
                if (_capturedPortraitTextures[i] != null)
                {
                    Destroy(_capturedPortraitTextures[i]);
                }
            }
            _capturedPortraitTextures.Clear();
        }
    }
}
