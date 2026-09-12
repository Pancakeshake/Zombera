using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Random = UnityEngine.Random;

namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {
        private void EnsureBuildControlsPopupVisual()
        {
            if (!showBuildControlsPopup || _buildControlsPopupRoot != null) return;

            RectTransform parent = null;
            if (_canvas != null)
                parent = _canvas.transform as RectTransform;
            else if (transform is RectTransform rt)
                parent = rt;

            if (parent == null) return;

            _buildControlsPopupRoot = MakeRect("BuildControlsPopup", parent);
            _buildControlsPopupRoot.anchorMin = new Vector2(1f, 1f);
            _buildControlsPopupRoot.anchorMax = new Vector2(1f, 1f);
            _buildControlsPopupRoot.pivot = new Vector2(1f, 1f);
            _buildControlsPopupRoot.anchoredPosition = buildControlsPopupOffset + new Vector2(0f, -50f);
            _buildControlsPopupRoot.sizeDelta = buildControlsPopupSize;

            var bg = _buildControlsPopupRoot.gameObject.AddComponent<Image>();
            bg.color = buildControlsPopupBackgroundColor;

            var outline = _buildControlsPopupRoot.gameObject.AddComponent<Outline>();
            outline.effectColor = buildControlsPopupBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);

            _buildControlsPopupLabel = MakeText("Label", _buildControlsPopupRoot, buildControlsPopupText,
                buildControlsPopupFontSize);
            _buildControlsPopupLabel.alignment = TextAlignmentOptions.TopLeft;
            _buildControlsPopupLabel.fontStyle = FontStyles.Bold;
            _buildControlsPopupLabel.color = buildControlsPopupTextColor;
            _buildControlsPopupLabel.textWrappingMode = TextWrappingModes.Normal;
            _buildControlsPopupLabel.margin = new Vector4(10f, 10f, 10f, 10f);

            if (showBuildHeightButtons)
            {
                var labelRt = _buildControlsPopupLabel.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0.22f);
                labelRt.anchorMax = new Vector2(1f, 1f);

                _buildHeightButtonsRow = MakeRect("HeightButtons", _buildControlsPopupRoot);
                _buildHeightButtonsRow.anchorMin = new Vector2(0f, 0f);
                _buildHeightButtonsRow.anchorMax = new Vector2(1f, 0.22f);
                _buildHeightButtonsRow.offsetMin = new Vector2(10f, 8f);
                _buildHeightButtonsRow.offsetMax = new Vector2(-10f, -8f);

                _buildHeightDownButton = CreatePopupButton("HeightDown", _buildHeightButtonsRow, "Height -");
                var downRt = _buildHeightDownButton.GetComponent<RectTransform>();
                downRt.anchorMin = new Vector2(0f, 0f);
                downRt.anchorMax = new Vector2(0.28f, 1f);
                downRt.offsetMin = Vector2.zero;
                downRt.offsetMax = Vector2.zero;
                _buildHeightDownButton.onClick.AddListener(() => HandleBuildHeightButtonClicked(-1));

                var valueRt = MakeRect("HeightValue", _buildHeightButtonsRow);
                valueRt.anchorMin = new Vector2(0.31f, 0f);
                valueRt.anchorMax = new Vector2(0.69f, 1f);
                valueRt.offsetMin = Vector2.zero;
                valueRt.offsetMax = Vector2.zero;

                _buildHeightValueLabel = MakeText("Label", valueRt, "Height 0.00m", 14f);
                _buildHeightValueLabel.alignment = TextAlignmentOptions.Midline;
                _buildHeightValueLabel.color = new Color(0.85f, 0.90f, 0.96f, 0.98f);
                _buildHeightValueLabel.fontStyle = FontStyles.Bold;

                _buildHeightUpButton = CreatePopupButton("HeightUp", _buildHeightButtonsRow, "Height +");
                var upRt = _buildHeightUpButton.GetComponent<RectTransform>();
                upRt.anchorMin = new Vector2(0.72f, 0f);
                upRt.anchorMax = new Vector2(1f, 1f);
                upRt.offsetMin = Vector2.zero;
                upRt.offsetMax = Vector2.zero;
                _buildHeightUpButton.onClick.AddListener(() => HandleBuildHeightButtonClicked(1));
            }

            _buildControlsPopupRoot.gameObject.SetActive(false);
        }


        private void ApplyBuildControlsPopupState(bool visible)
        {
            if (!showBuildControlsPopup) return;

            EnsureBuildControlsPopupVisual();
            if (_buildControlsPopupRoot == null) return;

            _buildControlsPopupRoot.anchoredPosition = buildControlsPopupOffset + new Vector2(0f, -50f);
            _buildControlsPopupRoot.sizeDelta = buildControlsPopupSize;

            if (_buildControlsPopupLabel != null)
            {
                _buildControlsPopupLabel.fontSize = buildControlsPopupFontSize;
                _buildControlsPopupLabel.color = buildControlsPopupTextColor;
                _buildControlsPopupLabel.text = ResolveBuildControlsPopupText();
            }

            UpdateBuildHeightButtonsState(visible);

            var background = _buildControlsPopupRoot.GetComponent<Image>();
            if (background != null) background.color = buildControlsPopupBackgroundColor;

            var show = visible && gameObject.activeInHierarchy;
            if (_buildControlsPopupRoot.gameObject.activeSelf != show)
                _buildControlsPopupRoot.gameObject.SetActive(show);
        }


        private string ResolveBuildControlsPopupText()
        {
            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            if (_easyBuildRadialMenuInputBridge == null)
                return buildControlsPopupText;

            var resolved = _easyBuildRadialMenuInputBridge.GetBuildControlsHelpText();
            if (string.IsNullOrWhiteSpace(resolved))
                return buildControlsPopupText;

            // Keep custom HUD shortcuts visible alongside EasyBuild control bindings.
            return resolved + "\nItems: 1-9\nPage: [ ]";
        }


        private void HandleBuildHeightButtonClicked(int direction)
        {
            if (direction == 0 || !_showingBuildItems) return;

            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            if (_easyBuildRadialMenuInputBridge == null) return;

            _easyBuildRadialMenuInputBridge.TryAdjustPlacementHeight(direction * buildHeightStepMeters);
            UpdateBuildHeightButtonsState(true);
        }


        private void UpdateBuildHeightButtonsState(bool popupVisible)
        {
            if (!showBuildHeightButtons) return;
            if (_buildHeightButtonsRow == null) return;

            var canShow = popupVisible && _showingBuildItems && ActiveTab == TabId.None;
            if (_buildHeightButtonsRow.gameObject.activeSelf != canShow)
                _buildHeightButtonsRow.gameObject.SetActive(canShow);

            if (!canShow) return;

            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            var hasBridge = _easyBuildRadialMenuInputBridge != null;

            if (_buildHeightDownButton != null) _buildHeightDownButton.interactable = hasBridge;
            if (_buildHeightUpButton != null) _buildHeightUpButton.interactable = hasBridge;

            if (_buildHeightValueLabel == null) return;

            var offset = hasBridge ? _easyBuildRadialMenuInputBridge.GetPlacementHeightOffset() : 0f;
            _buildHeightValueLabel.text = $"Height {offset:+0.00;-0.00;0.00}m";
        }


        private void EnsureBuildPageIndicatorVisual()
        {
            if (_buildPageIndicatorRoot != null || _bottomSquadsTabRoot == null) return;

            var tabsRoot = _bottomSquadsTabRoot.parent as RectTransform;
            if (tabsRoot == null) return;

            _buildPageIndicatorRoot = MakeRect("BuildPageIndicator", tabsRoot);
            var layoutElement = _buildPageIndicatorRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = 120f;
            layoutElement.preferredWidth = 138f;
            layoutElement.minHeight = Mathf.Max(24f, bottomSquadsTabHeight);
            layoutElement.preferredHeight = Mathf.Max(24f, bottomSquadsTabHeight);
            layoutElement.flexibleWidth = 0f;

            if (_buildSearchFieldRoot != null)
                _buildPageIndicatorRoot.SetSiblingIndex(_buildSearchFieldRoot.GetSiblingIndex() + 1);
            else
                _buildPageIndicatorRoot.SetSiblingIndex(1);

            _buildPageIndicatorText = MakeText("PageLabel", _buildPageIndicatorRoot, string.Empty, 14f);
            _buildPageIndicatorText.alignment = TextAlignmentOptions.MidlineRight;
            _buildPageIndicatorText.color = new Color(0.78f, 0.82f, 0.86f, 0.92f);
            _buildPageIndicatorText.margin = new Vector4(4f, 4f, 8f, 4f);

            _buildPageIndicatorRoot.gameObject.SetActive(false);
        }


        private void RefreshBuildPageIndicator()
        {
            if (!_showingBuildItems)
            {
                if (_buildPageIndicatorRoot != null) _buildPageIndicatorRoot.gameObject.SetActive(false);

                return;
            }

            EnsureBuildPageIndicatorVisual();
            if (_buildPageIndicatorRoot == null || _buildPageIndicatorText == null) return;

            var count = _filteredBuildSourceIndices.Count;
            if (count <= BuildHudItemsPerPage)
            {
                _buildPageIndicatorRoot.gameObject.SetActive(false);
                return;
            }

            var totalPages = Mathf.CeilToInt(count / (float)BuildHudItemsPerPage);
            var currentPage = _buildItemPageOffset / BuildHudItemsPerPage + 1;
            _buildPageIndicatorText.text = $"Page {currentPage}/{totalPages}  [ ]";
            _buildPageIndicatorRoot.gameObject.SetActive(true);
        }


        private void EnsureBuildCommandStripVisual()
        {
            if (_buildCommandStripRoot != null || _bottomSquadsTabRoot == null) return;

            var tabsRoot = _bottomSquadsTabRoot.parent as RectTransform;
            if (tabsRoot == null) return;

            _buildCommandStripRoot = MakeRect("BuildCommandStrip", tabsRoot);
            var layout = _buildCommandStripRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var stripLayout = _buildCommandStripRoot.gameObject.AddComponent<LayoutElement>();
            stripLayout.minHeight = Mathf.Max(24f, bottomSquadsTabHeight);
            stripLayout.preferredHeight = Mathf.Max(24f, bottomSquadsTabHeight);
            stripLayout.flexibleWidth = 0f;

            _buildCommandStripRoot.SetSiblingIndex(2);
            _buildCommandButtons.Clear();

            for (var i = 0; i < BuildCommandLabels.Length; i++)
            {
                var capturedIndex = i;
                var button = CreatePopupButton($"BuildCmd_{i + 1}", _buildCommandStripRoot, BuildCommandLabels[i]);
                var buttonRt = button.GetComponent<RectTransform>();
                buttonRt.offsetMin = Vector2.zero;
                buttonRt.offsetMax = Vector2.zero;

                var buttonLayout = button.gameObject.AddComponent<LayoutElement>();
                buttonLayout.minHeight = Mathf.Max(24f, bottomSquadsTabHeight);
                buttonLayout.preferredHeight = Mathf.Max(24f, bottomSquadsTabHeight);
                buttonLayout.minWidth = i < 2 ? 46f : 86f;
                buttonLayout.preferredWidth = i < 2 ? 52f : 94f;
                buttonLayout.flexibleWidth = 0f;

                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.fontSize = 15f;
                    label.text = BuildCommandLabels[i];
                }

                button.onClick.AddListener(() => HandleBuildCommandButtonClicked(capturedIndex));

                var background = button.targetGraphic as Image;
                var defaultColor = background != null ? background.color : new Color(0.15f, 0.22f, 0.30f, 0.96f);

                _buildCommandButtons.Add(new BuildCommandButtonView
                {
                    Index = i,
                    Button = button,
                    Background = background,
                    Label = label,
                    DefaultBackgroundColor = defaultColor
                });
            }

            _buildCommandStripRoot.gameObject.SetActive(false);
        }


        private void ApplyBuildCommandStripState(bool visible)
        {
            EnsureBuildCommandStripVisual();
            if (_buildCommandStripRoot == null) return;

            if (_buildCommandStripRoot.gameObject.activeSelf != visible)
                _buildCommandStripRoot.gameObject.SetActive(visible);

            if (!visible) return;

            UpdateBuildCommandButtonVisuals();
        }


        private void UpdateBuildCommandButtonVisuals()
        {
            if (_buildCommandButtons.Count == 0) return;

            var activeModeCommand = ResolveActiveBuildModeCommandIndex();
            BuildCommandModeHelper.UpdateBuildCommandButtonVisuals(_buildCommandButtons, activeModeCommand);
        }


        private int ResolveActiveBuildModeCommandIndex()
        {
            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            if (_easyBuildRadialMenuInputBridge == null) return -1;

            return BuildCommandModeHelper.ResolveActiveBuildModeCommandIndex(_easyBuildRadialMenuInputBridge);
        }


    }
}
