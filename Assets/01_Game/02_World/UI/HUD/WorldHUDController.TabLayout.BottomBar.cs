using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {
        // ──────────────────────────────────────────────
        //  Bottom bar: squads drop-tab, build search, visual state
        // ──────────────────────────────────────────────

        private void InitializeBottomSquadsDropTab()
        {
            if (!enableBottomSquadsDropTab || bottomBarRoot == null) return;

            _bottomBarExpandedHeight = Mathf.Max(1f,
                bottomBarRoot.sizeDelta.y > 0f ? bottomBarRoot.sizeDelta.y : bottomBarRoot.rect.height);
            _bottomBarExpanded = startWithBottomBarExpanded;

            EnsureBottomSquadsDropTabVisual();
            ApplyBottomSquadsExpandedVisualState();
        }

        private void EnsureBottomSquadsDropTabVisual()
        {
            if (_bottomSquadsTabRoot != null) return;

            var tabsRoot = bottomBarRoot != null ? bottomBarRoot.Find("SquadPageTabs") as RectTransform : null;
            if (tabsRoot != null)
            {
                _bottomSquadsTabRoot = MakeRect("SquadsToggle", tabsRoot);
                var layoutElement = _bottomSquadsTabRoot.gameObject.AddComponent<LayoutElement>();
                layoutElement.minWidth = Mathf.Max(72f, bottomSquadsTabButtonWidth);
                layoutElement.preferredWidth = Mathf.Max(72f, bottomSquadsTabButtonWidth);
                layoutElement.minHeight = Mathf.Max(24f, bottomSquadsTabHeight);
                layoutElement.preferredHeight = Mathf.Max(24f, bottomSquadsTabHeight);
                layoutElement.flexibleWidth = 0f;

                _bottomSquadsTabRoot.SetSiblingIndex(0);
            }
            else
            {
                RectTransform fallbackParent;
                if (bottomBarRoot != null)
                    fallbackParent = bottomBarRoot;
                else if (_canvas != null)
                    fallbackParent = _canvas.transform as RectTransform;
                else
                    fallbackParent = transform as RectTransform;

                _bottomSquadsTabRoot = MakeRect("SquadsToggle", fallbackParent);
                _bottomSquadsTabRoot.anchorMin = new Vector2(0f, 0f);
                _bottomSquadsTabRoot.anchorMax = new Vector2(0f, 0f);
                _bottomSquadsTabRoot.pivot = new Vector2(0f, 0f);
                _bottomSquadsTabRoot.anchoredPosition = new Vector2(8f, 6f);
                _bottomSquadsTabRoot.sizeDelta = new Vector2(Mathf.Max(72f, bottomSquadsTabButtonWidth),
                    Mathf.Max(24f, bottomSquadsTabHeight));
            }

            _bottomSquadsTabBackground = _bottomSquadsTabRoot.gameObject.AddComponent<Image>();
            _bottomSquadsTabBackground.color = bottomSquadsTabExpandedColor;

            var tabButton = _bottomSquadsTabRoot.gameObject.AddComponent<Button>();
            tabButton.targetGraphic = _bottomSquadsTabBackground;
            tabButton.onClick.AddListener(ToggleBottomSquadsExpandedState);

            _bottomSquadsTabText = MakeText("Label", _bottomSquadsTabRoot,
                string.IsNullOrWhiteSpace(bottomSquadsTabLabel) ? "Squads" : bottomSquadsTabLabel, 20f);
            _bottomSquadsTabText.alignment = TextAlignmentOptions.Center;
            _bottomSquadsTabText.fontStyle = FontStyles.Bold;
            _bottomSquadsTabText.color = bottomSquadsTabTextColor;

            EnsureBottomHudButtonHitArea(_bottomSquadsTabRoot);
        }

        private static void EnsureBottomHudButtonHitArea(RectTransform buttonRoot)
        {
            if (buttonRoot == null) return;

            var button = buttonRoot.GetComponent<Button>();
            if (button == null) return;

            var targetGraphic = button.targetGraphic;
            if (targetGraphic != null)
                targetGraphic.raycastTarget = true;

            var graphics = buttonRoot.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || graphic == targetGraphic) continue;
                graphic.raycastTarget = false;
            }
        }

        private void EnsureBuildSearchFieldVisual()
        {
            if (_buildSearchFieldRoot != null || _bottomSquadsTabRoot == null) return;

            var tabsRoot = _bottomSquadsTabRoot.parent as RectTransform;
            if (tabsRoot == null) return;

            _buildSearchFieldRoot = MakeRect("BuildSearchField", tabsRoot);
            var layoutElement = _buildSearchFieldRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = Mathf.Max(140f, buildSearchFieldWidth);
            layoutElement.preferredWidth = Mathf.Max(140f, buildSearchFieldWidth);
            layoutElement.minHeight = Mathf.Max(24f, bottomSquadsTabHeight);
            layoutElement.preferredHeight = Mathf.Max(24f, bottomSquadsTabHeight);
            layoutElement.flexibleWidth = 0f;

            _buildSearchFieldRoot.SetSiblingIndex(1);

            var background = _buildSearchFieldRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.10f, 0.12f, 0.16f, 0.94f);

            var input = _buildSearchFieldRoot.gameObject.AddComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 40;

            var text = MakeText("Text", _buildSearchFieldRoot, string.Empty, 16f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = new Color(0.92f, 0.94f, 0.96f, 0.98f);
            text.margin = new Vector4(10f, 4f, 10f, 4f);

            var placeholder = MakeText("Placeholder", _buildSearchFieldRoot,
                string.IsNullOrWhiteSpace(buildSearchPlaceholder) ? "Search builds..." : buildSearchPlaceholder, 15f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.color = new Color(0.62f, 0.66f, 0.72f, 0.88f);
            placeholder.margin = new Vector4(10f, 4f, 10f, 4f);

            input.textComponent = text;
            input.placeholder = placeholder;
            input.onValueChanged.AddListener(OnBuildSearchQueryChanged);
            _buildSearchInput = input;

            _buildSearchFieldRoot.gameObject.SetActive(false);
        }

        private void ApplyBuildSearchFieldState(bool visible)
        {
            EnsureBuildSearchFieldVisual();
            if (_buildSearchFieldRoot == null) return;

            var layoutElement = _buildSearchFieldRoot.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minWidth = Mathf.Max(140f, buildSearchFieldWidth);
                layoutElement.preferredWidth = Mathf.Max(140f, buildSearchFieldWidth);
                layoutElement.minHeight = Mathf.Max(24f, bottomSquadsTabHeight);
                layoutElement.preferredHeight = Mathf.Max(24f, bottomSquadsTabHeight);
            }

            if (_buildSearchInput != null)
            {
                var placeholderText = string.IsNullOrWhiteSpace(buildSearchPlaceholder)
                    ? "Search builds..."
                    : buildSearchPlaceholder;
                if (_buildSearchInput.placeholder is TextMeshProUGUI tmpPlaceholder)
                    tmpPlaceholder.text = placeholderText;
            }

            if (_buildSearchFieldRoot.gameObject.activeSelf != visible)
                _buildSearchFieldRoot.gameObject.SetActive(visible);
        }

        private void OnBuildSearchQueryChanged(string value)
        {
            _pendingBuildSearchQuery = value ?? string.Empty;
            _hasPendingBuildSearchQuery = true;
            _buildSearchApplyAt = Time.unscaledTime + BuildSearchApplyDelaySeconds;
        }

        private void ToggleBottomSquadsExpandedState()
        {
            _bottomBarExpanded = !_bottomBarExpanded;
            ApplyBottomSquadsExpandedVisualState();
            UpdateLayoutForTabState(ActiveTab != TabId.None);
        }

        private void ApplyBottomSquadsExpandedVisualState()
        {
            if (_bottomSquadsTabBackground != null)
                _bottomSquadsTabBackground.color = _bottomBarExpanded
                    ? bottomSquadsTabExpandedColor
                    : bottomSquadsTabCollapsedColor;

            if (_bottomSquadsTabText == null) return;

            var tabName = ResolveBottomSquadsDropTabName();
            _bottomSquadsTabText.text = _bottomBarExpanded ? tabName + " ▼" : tabName + " ▲";
            _bottomSquadsTabText.color = bottomSquadsTabTextColor;
        }

        private string ResolveBottomSquadsDropTabName()
        {
            if (_showingBuildItems)
                return string.IsNullOrWhiteSpace(bottomBuildsTabLabel) ? "Builds" : bottomBuildsTabLabel.Trim();

            return string.IsNullOrWhiteSpace(bottomSquadsTabLabel) ? "Squads" : bottomSquadsTabLabel.Trim();
        }

        private void ResetBottomBarToSquadView()
        {
            _showingBuildItems = false;
            SetAllBuildItemStripsActive(false);
            ApplyBottomSquadsExpandedVisualState();
        }

        private void SetAllBuildItemStripsActive(bool active)
        {
            TryResolveExistingBuildItemStrips();

            if (_bottomBuildItemsRoot != null)
                _bottomBuildItemsRoot.gameObject.SetActive(active);

            if (bottomBarRoot == null) return;

            var stripRoots = bottomBarRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < stripRoots.Length; i++)
            {
                if (stripRoots[i].name != "BuildItemStrip") continue;
                stripRoots[i].gameObject.SetActive(active);
            }
        }

        private void TryResolveExistingBuildItemStrips()
        {
            if (_bottomBuildItemsRoot != null) return;

            var stripParent = portraitStrip != null ? portraitStrip.transform.parent : bottomBarRoot;
            if (stripParent == null) return;

            var existing = stripParent.Find("BuildItemStrip") as RectTransform;
            if (existing != null)
                _bottomBuildItemsRoot = existing;
        }
    }
}
