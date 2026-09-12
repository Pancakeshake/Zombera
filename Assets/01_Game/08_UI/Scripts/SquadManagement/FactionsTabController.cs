#region

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Factions;
using Zombera.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed class FactionsTabController : MonoBehaviour
    {
        private static readonly Color PanelDark = new(0.13f, 0.13f, 0.12f, 0.98f);
        private static readonly Color PanelMid = new(0.16f, 0.16f, 0.15f, 0.98f);
        private static readonly Color TextPrimary = new(0.94f, 0.90f, 0.78f, 1f);
        private static readonly Color TextMuted = new(0.68f, 0.65f, 0.58f, 1f);
        private static readonly Color AccentGreen = new(0.36f, 0.58f, 0.30f, 1f);
        private static readonly Color AccentRed = new(0.62f, 0.24f, 0.20f, 1f);

        private readonly List<Button> _factionButtons = new();
        private readonly List<FactionListEntry> _factionEntries = new();

        private FactionManager _factionManager;
        private TMP_FontAsset _fontAsset;
        private RectTransform _hostRoot;
        private Sprite _panelSprite;
        private int _selectedIndex = -1;

        private TMP_Text _summaryText;
        private TMP_Text _detailTitleText;
        private TMP_Text _detailCategoryText;
        private TMP_Text _detailDiplomacyText;
        private TMP_Text _detailStandingText;
        private TMP_Text _detailDescriptionText;
        private TMP_Text _detailRegionText;
        private TMP_Text _detailInteractionText;
        private TMP_Text _intelTitleText;
        private TMP_Text _intelBodyText;
        private RectTransform _factionListContent;
        private Image _standingBarFill;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            _hostRoot = host;
            _fontAsset = font;
            _panelSprite = panelBackground;
            _ = slotBackground;

            ClearChildren(_hostRoot);
            BuildLayout();
            EnsureRuntimeReferences();
            RefreshFromRuntime();
        }

        public void RefreshFromRuntime()
        {
            EnsureRuntimeReferences();
            RebuildFactionList();
            RefreshSummary();
            RefreshDetailPanel();
            RefreshIntelPanel();
        }

        private void OnEnable()
        {
            EnsureRuntimeReferences();
            if (_factionManager != null) _factionManager.FactionStateChanged += HandleFactionStateChanged;
        }

        private void OnDisable()
        {
            if (_factionManager != null) _factionManager.FactionStateChanged -= HandleFactionStateChanged;
        }

        private void HandleFactionStateChanged() => RefreshFromRuntime();

        private void BuildLayout()
        {
            var body = CreateRect("Body", _hostRoot);
            Stretch(body, Vector2.zero, Vector2.one, new Vector2(4f, 52f), new Vector2(-4f, -4f));

            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 6f;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = true;

            BuildFactionListColumn(body);
            BuildDetailColumn(body);
            BuildIntelColumn(body);

            var footer = CreateRect("Footer", _hostRoot);
            Stretch(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), new Vector2(-8f, 44f));
            AddImage(footer, new Color(0.10f, 0.10f, 0.09f, 0.98f), _panelSprite).type = Image.Type.Sliced;
            _summaryText = CreateText(footer, "Discovered: 0 | Hostile: 0 | Allied: 0 | Unknown: 0",
                13f, TextMuted, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(_summaryText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
        }

        private void BuildFactionListColumn(RectTransform parent)
        {
            var column = CreatePanelColumn(parent, 240f, PanelMid);
            var header = CreateText(column, "FACTIONS", 20f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -44f));

            BuildVerticalScroll(column, new Vector2(8f, 8f), new Vector2(-8f, -52f), out _factionListContent);
        }

        private void BuildDetailColumn(RectTransform parent)
        {
            var column = CreatePanelColumn(parent, 0f, PanelDark, flexibleWidth: true);

            _detailTitleText = CreateText(column, "SELECT A FACTION", 22f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(_detailTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -14f), new Vector2(-14f, -48f));

            _detailCategoryText = CreateText(column, string.Empty, 13f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Stretch(_detailCategoryText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -52f), new Vector2(-14f, -72f));

            _detailDiplomacyText = CreateText(column, string.Empty, 16f, AccentGreen, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(_detailDiplomacyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -78f), new Vector2(-14f, -104f));

            var barFrame = CreateRect("StandingBar", column);
            Stretch(barFrame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -112f), new Vector2(-14f, -132f));
            AddImage(barFrame, new Color(0.18f, 0.18f, 0.17f, 1f), _panelSprite);

            var fill = CreateRect("Fill", barFrame);
            Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _standingBarFill = AddImage(fill, AccentGreen, null);
            _standingBarFill.type = Image.Type.Filled;
            _standingBarFill.fillMethod = Image.FillMethod.Horizontal;
            _standingBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _standingBarFill.fillAmount = 0.5f;

            _detailStandingText = CreateText(column, string.Empty, 12f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Stretch(_detailStandingText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -138f), new Vector2(-14f, -158f));

            _detailDescriptionText = CreateText(column, string.Empty, 13f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _detailDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(_detailDescriptionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -166f), new Vector2(-14f, -280f));

            _detailRegionText = CreateText(column, string.Empty, 12f, TextPrimary, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Stretch(_detailRegionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -288f), new Vector2(-14f, -312f));

            _detailInteractionText = CreateText(column, string.Empty, 12f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _detailInteractionText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(_detailInteractionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -318f), new Vector2(-14f, -380f));
        }

        private void BuildIntelColumn(RectTransform parent)
        {
            var column = CreatePanelColumn(parent, 250f, PanelMid);

            _intelTitleText = CreateText(column, "INTEL", 18f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(_intelTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -12f), new Vector2(-12f, -40f));

            _intelBodyText = CreateText(column,
                "Encounter factions in the world to reveal contacts, standing, and last known activity.",
                12f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _intelBodyText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(_intelBodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -48f), new Vector2(-12f, -200f));

            var markerButton = CreateRect("MapMarkerButton", column);
            Stretch(markerButton, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 64f), new Vector2(-12f, 108f));
            CreateActionButton(markerButton, "SET MAP MARKER", true, new Color(0.20f, 0.24f, 0.19f, 1f), OnSetMapMarker);

            var tradeButton = CreateRect("TradeButton", column);
            Stretch(tradeButton, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 12f), new Vector2(-12f, 56f));
            CreateActionButton(tradeButton, "TRADE STATUS", true, AccentGreen * 0.85f, OnTradeStatus);
        }

        private void RebuildFactionList()
        {
            ClearChildren(_factionListContent);
            _factionButtons.Clear();
            _factionEntries.Clear();

            if (_factionManager != null)
                _factionEntries.AddRange(_factionManager.BuildDiscoveredFactionList());

            if (_factionEntries.Count == 0)
                _factionEntries.AddRange(HudDevPlaceholderData.BuildFactionListEntries());
            for (var i = 0; i < _factionEntries.Count; i++)
            {
                var index = i;
                var entry = _factionEntries[i];
                var button = CreateFactionRow(_factionListContent, entry, () => SelectFaction(index));
                _factionButtons.Add(button);
            }

            if (_selectedIndex < 0 && _factionEntries.Count > 0)
                SelectFaction(0);
            else if (_selectedIndex >= _factionEntries.Count)
                SelectFaction(_factionEntries.Count - 1);
        }

        private Button CreateFactionRow(RectTransform parent, FactionListEntry entry, System.Action onClick)
        {
            var root = CreateRect(entry.DisplayName, parent);
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 58f;

            var background = AddImage(root, new Color(0.20f, 0.20f, 0.18f, 0.98f), _panelSprite);
            background.type = Image.Type.Sliced;

            var swatch = CreateRect("Swatch", root);
            Stretch(swatch, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 8f), new Vector2(20f, -8f));
            AddImage(swatch, entry.PrimaryColor, null);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => onClick?.Invoke());

            var title = CreateText(root, entry.DisplayName, 14f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(28f, 2f), new Vector2(-8f, -2f));

            var subtitle = CreateText(root,
                $"{entry.Category} | {FormatDiplomacy(entry.DiplomacyState)}",
                11f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Stretch(subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(28f, 2f), new Vector2(-8f, -2f));

            return button;
        }

        private void SelectFaction(int index)
        {
            _selectedIndex = index;
            for (var i = 0; i < _factionButtons.Count; i++)
            {
                var image = _factionButtons[i]?.targetGraphic as Image;
                if (image == null) continue;
                image.color = i == _selectedIndex
                    ? new Color(0.30f, 0.34f, 0.24f, 0.98f)
                    : new Color(0.20f, 0.20f, 0.18f, 0.98f);
            }

            RefreshDetailPanel();
            RefreshIntelPanel();
        }

        private void RefreshSummary()
        {
            if (_summaryText == null) return;

            if (_factionManager != null)
            {
                var summary = _factionManager.BuildPanelSummary();
                _summaryText.text =
                    $"Discovered: {summary.DiscoveredCount} | Hostile: {summary.HostileCount} | Allied: {summary.AlliedCount} | Unknown: {summary.UnknownCount}";
                return;
            }

            var placeholderCount = _factionEntries.Count;
            var hostile = 0;
            var allied = 0;
            for (var i = 0; i < _factionEntries.Count; i++)
            {
                var diplomacy = _factionEntries[i].DiplomacyState;
                if (diplomacy is FactionDiplomacyState.Hostile or FactionDiplomacyState.AtWar) hostile++;
                if (diplomacy is FactionDiplomacyState.Friendly or FactionDiplomacyState.Allied) allied++;
            }

            _summaryText.text =
                $"Discovered: {placeholderCount} | Hostile: {hostile} | Allied: {allied} | Unknown: 1";
        }

        private void RefreshDetailPanel()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _factionEntries.Count)
            {
                _detailTitleText.text = "SELECT A FACTION";
                _detailCategoryText.text = string.Empty;
                _detailDiplomacyText.text = string.Empty;
                _detailStandingText.text = string.Empty;
                _detailDescriptionText.text = string.Empty;
                _detailRegionText.text = string.Empty;
                _detailInteractionText.text = string.Empty;
                if (_standingBarFill != null) _standingBarFill.fillAmount = 0f;
                return;
            }

            var entry = _factionEntries[_selectedIndex];
            _detailTitleText.text = entry.DisplayName;
            _detailCategoryText.text = entry.Category.ToString().ToUpper();
            _detailDiplomacyText.text = FormatDiplomacy(entry.DiplomacyState);
            _detailDiplomacyText.color = entry.DiplomacyState is FactionDiplomacyState.Hostile or FactionDiplomacyState.AtWar
                ? AccentRed
                : AccentGreen;
            _detailStandingText.text = $"Standing: {entry.StandingValue}";
            _detailDescriptionText.text = string.IsNullOrWhiteSpace(entry.Description)
                ? "No additional intel available."
                : entry.Description;
            _detailRegionText.text = string.IsNullOrWhiteSpace(entry.LastKnownRegionId)
                ? "Last known region: Unknown"
                : $"Last known region: {entry.LastKnownRegionId}";
            _detailInteractionText.text = string.IsNullOrWhiteSpace(entry.LastInteractionSummary)
                ? "Recent incidents: None recorded."
                : $"Recent incidents: {entry.LastInteractionSummary}";

            if (_standingBarFill != null)
                _standingBarFill.fillAmount = Mathf.InverseLerp(-100f, 100f, entry.StandingValue);
        }

        private void RefreshIntelPanel()
        {
            if (_intelBodyText == null) return;

            if (_selectedIndex < 0 || _selectedIndex >= _factionEntries.Count)
            {
                _intelBodyText.text =
                    "Encounter factions in the world to reveal contacts, standing, and last known activity.";
                return;
            }

            var entry = _factionEntries[_selectedIndex];
            var hostile = entry.DiplomacyState is FactionDiplomacyState.Hostile or FactionDiplomacyState.AtWar;
            var trade = entry.Category is FactionCategory.Settlement or FactionCategory.Trader;

            _intelBodyText.text =
                $"Threat level: {(hostile ? "High" : "Low")}\n" +
                $"Trade access: {(trade ? "Possible" : "Unavailable")}\n" +
                $"Standing trend: {entry.StandingValue}\n\n" +
                "Actions below are placeholders until map markers and trade hooks are wired.";
        }

        private void OnSetMapMarker()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _factionEntries.Count) return;
            Debug.Log($"[FactionsTab] Map marker requested for {_factionEntries[_selectedIndex].FactionId}.");
        }

        private void OnTradeStatus()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _factionEntries.Count) return;
            Debug.Log($"[FactionsTab] Trade status requested for {_factionEntries[_selectedIndex].FactionId}.");
        }

        private void EnsureRuntimeReferences()
        {
            if (_factionManager == null)
                _factionManager = FactionManager.Instance ?? FindFirstObjectByType<FactionManager>();
        }

        private static string FormatDiplomacy(FactionDiplomacyState state)
        {
            return state switch
            {
                FactionDiplomacyState.AtWar => "AT WAR",
                FactionDiplomacyState.Hostile => "HOSTILE",
                FactionDiplomacyState.Suspicious => "SUSPICIOUS",
                FactionDiplomacyState.Friendly => "FRIENDLY",
                FactionDiplomacyState.Allied => "ALLIED",
                FactionDiplomacyState.Neutral => "NEUTRAL",
                _ => "UNKNOWN"
            };
        }

        private static RectTransform CreatePanelColumn(RectTransform parent, float width, Color color, bool flexibleWidth = false)
        {
            var column = CreateRect("Column", parent);
            var layout = column.gameObject.AddComponent<LayoutElement>();
            if (flexibleWidth)
                layout.flexibleWidth = 1f;
            else
                layout.preferredWidth = width;

            AddImage(column, color, null).type = Image.Type.Sliced;
            return column;
        }

        private static void BuildVerticalScroll(RectTransform parent, Vector2 offsetMin, Vector2 offsetMax,
            out RectTransform content)
        {
            var scrollRoot = CreateRect("Scroll", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, offsetMin, offsetMax);

            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
        }

        private void CreateActionButton(RectTransform parent, string label, bool interactable, Color color,
            System.Action onClick)
        {
            var background = AddImage(parent, color, _panelSprite);
            background.type = Image.Type.Sliced;

            var button = parent.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.interactable = interactable;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = CreateText(parent, label, 13f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private TMP_Text CreateText(Transform parent, string text, float size, Color color, FontStyles style,
            TextAlignmentOptions alignment)
        {
            var label = CreateRect("Text", parent);
            var tmp = label.gameObject.AddComponent<TextMeshProUGUI>();
            if (_fontAsset != null) tmp.font = _fontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = true;
            return image;
        }
    }
}
