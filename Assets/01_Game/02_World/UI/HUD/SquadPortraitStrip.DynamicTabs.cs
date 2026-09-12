#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {
        private void EnsureDynamicSquadTabUi()
        {
            if (!IsSquadTabPagingActive()) return;

            var tabsRoot = ResolveSquadPageTabsRoot();
            if (tabsRoot == null) return;

            EnsureSquadTabButtonCapacity(GetConfiguredSquadTabCount());
            EnsureAddSquadTabButton(tabsRoot);
            EnsureSquadTabRenameInput();
            UpdateAddSquadTabButtonState();
            LayoutSquadTabRow();
        }

        private RectTransform ResolveSquadPageTabsRoot()
        {
            var stripParent = transform.parent as RectTransform;
            if (stripParent == null) return null;

            var tabs = stripParent.Find("SquadPageTabs") as RectTransform;
            if (tabs != null) return tabs;

            return stripParent.Find("SquadPageTabs_Runtime") as RectTransform;
        }

        private void EnsureSquadTabButtonCapacity(int requiredCount)
        {
            var tabsRoot = ResolveSquadPageTabsRoot();
            if (tabsRoot == null) return;

            var buttons = squadTabButtons != null
                ? new List<Button>(squadTabButtons)
                : new List<Button>(requiredCount);

            while (buttons.Count < requiredCount)
                buttons.Add(CreateSquadTabButton(tabsRoot, buttons.Count));

            squadTabButtons = buttons.ToArray();
        }

        private Button CreateSquadTabButton(RectTransform tabsRoot, int tabIndex)
        {
            var buttonGo = new GameObject($"SquadTab_{tabIndex + 1}", typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.SetParent(tabsRoot, false);

            var buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = squadTabInactiveColor;

            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;

            var layoutElement = buttonGo.GetComponent<LayoutElement>();
            ApplySquadTabButtonLayout(layoutElement);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(buttonGo.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(6f, 0f);
            labelRt.offsetMax = new Vector2(-6f, 0f);

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            ApplySquadTabLabelStyle(label);
            label.text = GetDefaultSquadGroupName(tabIndex);

            return button;
        }

        private void ApplySquadTabButtonLayout(LayoutElement layoutElement)
        {
            if (layoutElement == null) return;

            layoutElement.minWidth = squadTabWidth;
            layoutElement.preferredWidth = squadTabWidth;
            layoutElement.minHeight = squadTabHeight;
            layoutElement.preferredHeight = squadTabHeight;
            layoutElement.flexibleWidth = 0f;
        }

        private void ApplySquadTabLabelStyle(TextMeshProUGUI label)
        {
            if (label == null) return;

            label.fontSize = 14f;
            label.fontStyle = FontStyles.Bold;
            label.color = squadTabInactiveLabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
        }

        private void ApplySquadTabButtonLayout(Button button)
        {
            if (button == null) return;

            var layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = button.gameObject.AddComponent<LayoutElement>();

            ApplySquadTabButtonLayout(layoutElement);

            var label = GetOrCacheTabButtonLabel(button);
            if (label != null)
                ApplySquadTabLabelStyle(label);
        }

        private void EnsureAddSquadTabButton(RectTransform tabsRoot)
        {
            if (_addSquadTabButton != null) return;

            var existing = tabsRoot.Find("AddSquadTab");
            if (existing != null)
            {
                _addSquadTabButton = existing.GetComponent<Button>();
                if (_addSquadTabButton != null)
                {
                    _addSquadTabButton.onClick.RemoveAllListeners();
                    _addSquadTabButton.onClick.AddListener(() => TryAddSquadGroup());
                    return;
                }
            }

            var buttonGo = new GameObject("AddSquadTab", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.SetParent(tabsRoot, false);

            var buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = squadTabInactiveColor;

            _addSquadTabButton = buttonGo.GetComponent<Button>();
            _addSquadTabButton.targetGraphic = buttonImage;
            _addSquadTabButton.onClick.AddListener(() => TryAddSquadGroup());

            var layoutElement = buttonGo.GetComponent<LayoutElement>();
            layoutElement.minWidth = 44f;
            layoutElement.preferredWidth = 44f;
            layoutElement.minHeight = 38f;
            layoutElement.preferredHeight = 38f;
            layoutElement.flexibleWidth = 0f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(buttonGo.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "+";
            label.fontSize = 24f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.90f, 0.92f, 0.94f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private void LayoutSquadTabRow()
        {
            var tabsRoot = ResolveSquadPageTabsRoot();
            if (tabsRoot == null) return;

            var insertBase = GetSquadTabRowInsertIndex(tabsRoot);
            var configuredTabCount = GetConfiguredSquadTabCount();

            if (squadTabButtons != null)
            {
                for (var i = 0; i < squadTabButtons.Length; i++)
                {
                    var button = squadTabButtons[i];
                    if (button == null) continue;

                    var shouldShow = i < configuredTabCount;
                    if (button.gameObject.activeSelf != shouldShow)
                        button.gameObject.SetActive(shouldShow);

                    if (!shouldShow) continue;

                    button.transform.SetSiblingIndex(insertBase + i);
                }
            }

            var nextIndex = insertBase + configuredTabCount;
            if (_addSquadTabButton != null)
                _addSquadTabButton.transform.SetSiblingIndex(nextIndex);

            var quickStrip = tabsRoot.Find("QuickFormationStrip");
            if (quickStrip != null)
                quickStrip.SetSiblingIndex(nextIndex + 1);

            if (_squadTabRenameRoot != null && _squadTabRenameRoot.parent == tabsRoot)
                _squadTabRenameRoot.SetAsLastSibling();

            RefreshSquadPageTabsWidth();
        }

        private static int GetSquadTabRowInsertIndex(RectTransform tabsRoot)
        {
            for (var i = 0; i < tabsRoot.childCount; i++)
            {
                var childName = tabsRoot.GetChild(i).name;
                if (childName.StartsWith("SquadTab_", StringComparison.Ordinal)
                    || string.Equals(childName, "AddSquadTab", StringComparison.Ordinal)
                    || string.Equals(childName, "QuickFormationStrip", StringComparison.Ordinal)
                    || string.Equals(childName, "SquadTabRenameInput", StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return tabsRoot.childCount;
        }

        private void UpdateAddSquadTabButtonState()
        {
            if (_addSquadTabButton == null) return;

            var canAdd = _squadGroups.Count < MaxSquadGroupCount;
            if (_addSquadTabButton.gameObject.activeSelf != canAdd)
                _addSquadTabButton.gameObject.SetActive(canAdd);

            _addSquadTabButton.interactable = canAdd;
        }

        private void EnsureSquadTabRenameInput()
        {
            if (_squadTabRenameInput != null) return;

            var tabsRoot = ResolveSquadPageTabsRoot();
            if (tabsRoot == null) return;

            var inputGo = new GameObject("SquadTabRenameInput", typeof(RectTransform), typeof(Image),
                typeof(TMP_InputField));
            _squadTabRenameRoot = inputGo.GetComponent<RectTransform>();
            _squadTabRenameRoot.SetParent(tabsRoot, false);
            _squadTabRenameRoot.gameObject.SetActive(false);

            var background = inputGo.GetComponent<Image>();
            background.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(_squadTabRenameRoot, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(6f, 2f);
            textRt.offsetMax = new Vector2(-6f, -2f);

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 14f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            text.alignment = TextAlignmentOptions.Center;

            _squadTabRenameInput = inputGo.GetComponent<TMP_InputField>();
            _squadTabRenameInput.textComponent = text;
            _squadTabRenameInput.lineType = TMP_InputField.LineType.SingleLine;
            _squadTabRenameInput.characterLimit = 24;
            _squadTabRenameInput.onSubmit.AddListener(CommitSquadTabRename);
            _squadTabRenameInput.onDeselect.AddListener(CommitSquadTabRename);
        }

        private void EnsureSquadTabInteractions()
        {
            if (squadTabButtons == null) return;

            var configuredTabCount = GetConfiguredSquadTabCount();
            for (var i = 0; i < squadTabButtons.Length; i++)
            {
                var button = squadTabButtons[i];
                if (button == null) continue;

                if (!_squadTabInteractionByButton.TryGetValue(button, out var interaction) || interaction == null)
                {
                    interaction = button.gameObject.GetComponent<SquadTabInteraction>();
                    if (interaction == null)
                        interaction = button.gameObject.AddComponent<SquadTabInteraction>();

                    _squadTabInteractionByButton[button] = interaction;
                }

                interaction.Configure(this, i);

                if (!_squadTabDropByButton.TryGetValue(button, out var dropInteraction) || dropInteraction == null)
                {
                    dropInteraction = button.gameObject.GetComponent<SquadTabDropInteraction>();
                    if (dropInteraction == null)
                        dropInteraction = button.gameObject.AddComponent<SquadTabDropInteraction>();

                    _squadTabDropByButton[button] = dropInteraction;
                }

                dropInteraction.Configure(this, i);

                ApplySquadTabButtonLayout(button);
                EnsureHudButtonHitArea(button);

                if (i >= configuredTabCount)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                button.onClick.RemoveAllListeners();
            }
        }

        internal void EnsureBottomHudTabHitAreas()
        {
            if (!IsBottomPortraitStrip()) return;

            EnsureSquadTabInteractions();

            if (_addSquadTabButton != null)
                EnsureHudButtonHitArea(_addSquadTabButton);
        }

        private static void EnsureHudButtonHitArea(Button button)
        {
            if (button == null) return;

            var targetGraphic = button.targetGraphic;
            if (targetGraphic != null)
                targetGraphic.raycastTarget = true;

            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || graphic == targetGraphic) continue;
                graphic.raycastTarget = false;
            }
        }

        private void UpdateSquadTabLabels()
        {
            if (squadTabButtons == null) return;

            var configuredTabCount = GetConfiguredSquadTabCount();
            for (var i = 0; i < squadTabButtons.Length; i++)
            {
                if (i >= configuredTabCount) continue;

                var button = squadTabButtons[i];
                if (button == null) continue;

                var label = GetOrCacheTabButtonLabel(button);
                if (label != null)
                    label.text = _squadGroups[i].DisplayName;
            }
        }

        private void RefreshSquadPageTabsWidth()
        {
            var tabsRoot = ResolveSquadPageTabsRoot();
            if (tabsRoot == null) return;

            var targetWidth = Mathf.Max(
                SquadPageTabsMinWidthWithQuickFormations,
                EstimateSquadPageTabsWidth(tabsRoot));

            tabsRoot.sizeDelta = new Vector2(targetWidth, tabsRoot.sizeDelta.y);

            LayoutRebuilder.ForceRebuildLayoutImmediate(tabsRoot);

            if (_quickFormationStripRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_quickFormationStripRoot);
        }

        private float EstimateSquadPageTabsWidth(RectTransform tabsRoot)
        {
            var tabsLayout = tabsRoot.GetComponent<HorizontalLayoutGroup>();
            var spacing = tabsLayout != null ? tabsLayout.spacing : 8f;
            var width = tabsLayout != null
                ? tabsLayout.padding.left + tabsLayout.padding.right
                : 0f;

            for (var i = 0; i < tabsRoot.childCount; i++)
            {
                var child = tabsRoot.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                width += LayoutUtility.GetPreferredWidth(child);
                if (i < tabsRoot.childCount - 1) width += spacing;
            }

            return width;
        }
    }
}
