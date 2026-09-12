#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.UI.SquadManagement;

#endregion

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {
        private const float QuickFormationDebounceSeconds = 0.18f;
        private const float QuickFormationSeparatorWidth = 8f;
        private const float SquadPageTabsMinWidthWithQuickFormations = 640f;

        [Header("Quick Formations")]
        [SerializeField] private bool enableQuickFormationStrip = true;

        [SerializeField] [Min(36f)] private float quickFormationButtonWidth = 44f;
        [SerializeField] [Min(24f)] private float quickFormationButtonHeight = 38f;
        [SerializeField] [Min(4f)] private float quickFormationStripSpacing = 4f;

        private readonly Dictionary<FormationType, Button> _quickFormationButtonByType = new(8);
        private readonly Dictionary<Button, TextMeshProUGUI> _quickFormationLabelByButton = new(8);

        private RectTransform _quickFormationStripRoot;
        private CommandSystem _commandSystem;
        private FormationController _formationController;
        private float _lastQuickFormationApplyAt = -999f;
        private FormationType? _lastAppliedQuickFormation;
        private bool _quickFormationButtonsConfigured;
        private bool _quickFormationStripWasVisible;

        public void RefreshQuickFormationState()
        {
            EnsureQuickFormationButtons();
            RefreshQuickFormationVisuals();
        }

        public void SetQuickFormationStripVisible(bool visible)
        {
            if (!IsQuickFormationStripEligible())
            {
                if (_quickFormationStripRoot != null)
                    _quickFormationStripRoot.gameObject.SetActive(false);
                return;
            }

            EnsureQuickFormationButtons();
            if (_quickFormationStripRoot == null) return;

            var show = visible && IsQuickFormationStripEligible();

            if (_quickFormationStripRoot.gameObject.activeSelf != show)
                _quickFormationStripRoot.gameObject.SetActive(show);

            if (show)
            {
                RefreshSquadPageTabsWidth();
                if (!_quickFormationStripWasVisible)
                    EnsureQuickFormationButtons();
                RefreshQuickFormationVisuals();
            }

            _quickFormationStripWasVisible = show;
        }

        private bool IsQuickFormationStripEligible()
        {
            return enableQuickFormationStrip && IsBottomPortraitStrip() && enableSquadTabs;
        }

        private void EnsureQuickFormationButtons()
        {
            if (!IsQuickFormationStripEligible())
            {
                _quickFormationButtonsConfigured = false;
                return;
            }

            var tabsRoot = ResolveSquadPageTabsRoot();
            if (tabsRoot == null) return;

            if (_quickFormationButtonsConfigured && _quickFormationStripRoot != null &&
                _quickFormationButtonByType.Count > 0)
            {
                RefreshQuickFormationVisuals();
                return;
            }

            if (_quickFormationStripRoot == null)
            {
                var existing = tabsRoot.Find("QuickFormationStrip") as RectTransform;
                if (existing != null)
                {
                    _quickFormationStripRoot = existing;
                }
                else
                {
                    var stripGo = new GameObject("QuickFormationStrip", typeof(RectTransform),
                        typeof(HorizontalLayoutGroup));
                    _quickFormationStripRoot = stripGo.GetComponent<RectTransform>();
                    _quickFormationStripRoot.SetParent(tabsRoot, false);

                    var layout = stripGo.GetComponent<HorizontalLayoutGroup>();
                    layout.spacing = quickFormationStripSpacing;
                    layout.childAlignment = TextAnchor.MiddleLeft;
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;

                    var stripLayoutElement = stripGo.AddComponent<LayoutElement>();
                    stripLayoutElement.flexibleWidth = 0f;

                    var sizeFitter = stripGo.AddComponent<ContentSizeFitter>();
                    sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    sizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

                    CreateQuickFormationSeparator(_quickFormationStripRoot);
                }
            }

            _quickFormationButtonByType.Clear();
            _quickFormationLabelByButton.Clear();

            var presets = FormationPresetCatalog.QuickStripPresets;
            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var button = FindOrCreateQuickFormationButton(_quickFormationStripRoot, preset, i);
                if (button == null) continue;

                _quickFormationButtonByType[preset.Type] = button;
            }

            BindOrphanQuickFormationButtons(_quickFormationStripRoot, presets);
            ConfigureQuickFormationButtons();
            LayoutSquadTabRow();
            _quickFormationButtonsConfigured = true;
        }

        private void BindOrphanQuickFormationButtons(RectTransform stripRoot, FormationPresetEntry[] presets)
        {
            if (stripRoot == null || presets == null) return;

            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                if (_quickFormationButtonByType.ContainsKey(preset.Type)) continue;

                var button = stripRoot.Find($"QuickFormation_{preset.ShortLabel}")?.GetComponent<Button>();
                if (button == null) continue;

                _quickFormationButtonByType[preset.Type] = button;
                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = preset.ShortLabel;
                    _quickFormationLabelByButton[button] = label;
                }
            }
        }

        private static void CreateQuickFormationSeparator(RectTransform parent)
        {
            var separatorGo = new GameObject("Separator", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            var separator = separatorGo.GetComponent<RectTransform>();
            separator.SetParent(parent, false);

            var layoutElement = separatorGo.GetComponent<LayoutElement>();
            layoutElement.minWidth = QuickFormationSeparatorWidth;
            layoutElement.preferredWidth = QuickFormationSeparatorWidth;
            layoutElement.flexibleWidth = 0f;

            var image = separatorGo.GetComponent<Image>();
            image.color = new Color(0.35f, 0.38f, 0.42f, 0.55f);
            image.raycastTarget = false;
        }

        private Button FindOrCreateQuickFormationButton(RectTransform parent, FormationPresetEntry preset, int index)
        {
            _ = index;
            var buttonName = $"QuickFormation_{preset.ShortLabel}";
            var existing = parent.Find(buttonName);
            var button = existing != null ? existing.GetComponent<Button>() : null;

            if (button == null)
            {
                var buttonGo = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button),
                    typeof(LayoutElement));
                var buttonRt = buttonGo.GetComponent<RectTransform>();
                buttonRt.SetParent(parent, false);

                var buttonImage = buttonGo.GetComponent<Image>();
                buttonImage.color = squadTabInactiveColor;

                button = buttonGo.GetComponent<Button>();
                button.targetGraphic = buttonImage;

                var layoutElement = buttonGo.GetComponent<LayoutElement>();
                layoutElement.minWidth = quickFormationButtonWidth;
                layoutElement.preferredWidth = quickFormationButtonWidth;
                layoutElement.minHeight = quickFormationButtonHeight;
                layoutElement.preferredHeight = quickFormationButtonHeight;
                layoutElement.flexibleWidth = 0f;

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.SetParent(buttonGo.transform, false);
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                var label = labelGo.GetComponent<TextMeshProUGUI>();
                label.fontSize = 13f;
                label.fontStyle = FontStyles.Bold;
                label.color = new Color(0.90f, 0.92f, 0.94f, 1f);
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
            }

            var resolvedLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (resolvedLabel != null)
            {
                resolvedLabel.text = preset.ShortLabel;
                resolvedLabel.raycastTarget = false;
                _quickFormationLabelByButton[button] = resolvedLabel;
            }

            PrepareQuickFormationButton(button);
            return button;
        }

        private static void PrepareQuickFormationButton(Button button)
        {
            if (button == null) return;

            button.interactable = true;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var buttonImage = button.targetGraphic as Image;
            if (buttonImage == null) buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                button.targetGraphic = buttonImage;
                buttonImage.raycastTarget = true;
            }

            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == buttonImage) continue;
                graphic.raycastTarget = false;
            }
        }

        private void ConfigureQuickFormationButtons()
        {
            foreach (var pair in _quickFormationButtonByType)
            {
                var type = pair.Key;
                var button = pair.Value;
                if (button == null) continue;

                PrepareQuickFormationButton(button);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ApplyQuickFormation(type));
            }

            RefreshQuickFormationVisuals();
        }

        private void ApplyQuickFormation(FormationType type)
        {
            EnsureFormationDependencies();
            if (_formationController == null)
            {
                Debug.LogWarning("[SquadPortraitStrip] Quick formation click ignored: FormationController unavailable.");
                return;
            }

            var now = Time.unscaledTime;
            if (_formationController.ActiveFormation == type
                && _lastAppliedQuickFormation == type
                && now - _lastQuickFormationApplyAt < QuickFormationDebounceSeconds)
            {
                RefreshQuickFormationVisuals();
                return;
            }

            _formationController.SetFormation(type);
            _lastAppliedQuickFormation = type;
            _lastQuickFormationApplyAt = now;

            TryRegroupAfterQuickFormationChange();
            RefreshQuickFormationVisuals();
        }

        private void TryRegroupAfterQuickFormationChange()
        {
            EnsureFormationDependencies();
            if (_commandSystem == null || !SquadManager.HasInstance) return;

            var members = ResolveQuickFormationRegroupMembers();
            if (members.Count > 0)
                _commandSystem.RegroupMembers(members);
        }

        private static List<SquadMember> ResolveQuickFormationRegroupMembers()
        {
            var output = new List<SquadMember>();
            if (!SquadManager.HasInstance) return output;

            if (SquadManager.Instance.HasSelection)
            {
                var selected = SquadManager.Instance.SelectedMembers;
                for (var i = 0; i < selected.Count; i++)
                {
                    if (selected[i] != null) output.Add(selected[i]);
                }

                return output;
            }

            var squadMembers = SquadManager.Instance.SquadMembers;
            for (var i = 0; i < squadMembers.Count; i++)
            {
                if (squadMembers[i] != null) output.Add(squadMembers[i]);
            }

            return output;
        }

        private void RefreshQuickFormationVisuals()
        {
            if (_quickFormationButtonByType.Count == 0) return;

            EnsureFormationDependencies();
            var activeFormation = _formationController != null
                ? _formationController.ActiveFormation
                : FormationType.DefaultMovement;

            foreach (var pair in _quickFormationButtonByType)
            {
                var button = pair.Value;
                if (button == null) continue;

                var isActive = pair.Key == activeFormation;
                var buttonImage = button.targetGraphic as Image;
                if (buttonImage == null) buttonImage = button.GetComponent<Image>();
                if (buttonImage != null)
                    buttonImage.color = isActive ? squadTabActiveColor : squadTabInactiveColor;
            }
        }

        private void EnsureFormationDependencies()
        {
            _commandSystem = SquadManager.ResolveRuntimeCommandSystem();
            _formationController = _commandSystem != null ? _commandSystem.Formation : null;
        }

    }
}
