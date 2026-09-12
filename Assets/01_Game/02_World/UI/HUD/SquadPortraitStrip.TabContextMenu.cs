#region

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#endregion

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {
        private RectTransform _squadTabContextMenuRoot;
        private RectTransform _squadTabContextMenuPanel;
        private Button _squadTabContextRenameButton;
        private Button _squadTabContextDeleteButton;
        private int _squadTabContextMenuTargetIndex = -1;

        internal void ShowSquadTabContextMenu(int tabIndex, PointerEventData eventData)
        {
            if (!IsSquadTabPagingActive()) return;
            if (tabIndex < 0 || tabIndex >= GetConfiguredSquadTabCount()) return;

            EnsureSquadTabContextMenu();
            if (_squadTabContextMenuRoot == null || _squadTabContextMenuPanel == null) return;

            _squadTabContextMenuTargetIndex = tabIndex;
            if (_squadTabContextDeleteButton != null)
                _squadTabContextDeleteButton.interactable = _squadGroups.Count > 1;

            _squadTabContextMenuRoot.gameObject.SetActive(true);
            _squadTabContextMenuRoot.SetAsLastSibling();

            var canvas = _squadTabContextMenuRoot.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.transform as RectTransform : _squadTabContextMenuRoot;
            if (canvasRect != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                _squadTabContextMenuPanel.anchoredPosition = localPoint + new Vector2(8f, -8f);
            }
            else
            {
                _squadTabContextMenuPanel.position = eventData.position + new Vector2(8f, -8f);
            }
        }

        internal void HideSquadTabContextMenu()
        {
            _squadTabContextMenuTargetIndex = -1;
            if (_squadTabContextMenuRoot != null)
                _squadTabContextMenuRoot.gameObject.SetActive(false);
        }

        private void EnsureSquadTabContextMenu()
        {
            if (_squadTabContextMenuRoot != null) return;

            var canvas = GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.transform as RectTransform : transform as RectTransform;
            if (parent == null) return;

            var rootGo = new GameObject("SquadTabContextMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            _squadTabContextMenuRoot = rootGo.GetComponent<RectTransform>();
            _squadTabContextMenuRoot.SetParent(parent, false);
            _squadTabContextMenuRoot.anchorMin = Vector2.zero;
            _squadTabContextMenuRoot.anchorMax = Vector2.one;
            _squadTabContextMenuRoot.offsetMin = Vector2.zero;
            _squadTabContextMenuRoot.offsetMax = Vector2.zero;

            var dismissImage = rootGo.GetComponent<Image>();
            dismissImage.color = new Color(0f, 0f, 0f, 0.01f);

            var dismissButton = rootGo.GetComponent<Button>();
            dismissButton.targetGraphic = dismissImage;
            dismissButton.onClick.AddListener(HideSquadTabContextMenu);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            _squadTabContextMenuPanel = panelGo.GetComponent<RectTransform>();
            _squadTabContextMenuPanel.SetParent(_squadTabContextMenuRoot, false);
            _squadTabContextMenuPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _squadTabContextMenuPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _squadTabContextMenuPanel.pivot = new Vector2(0f, 1f);
            _squadTabContextMenuPanel.sizeDelta = new Vector2(148f, 0f);

            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

            var panelOutline = panelGo.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.22f, 0.26f, 0.30f, 0.95f);
            panelOutline.effectDistance = new Vector2(1f, -1f);

            var panelLayout = panelGo.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(6, 6, 6, 6);
            panelLayout.spacing = 4f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            var panelFitter = panelGo.GetComponent<ContentSizeFitter>();
            panelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _squadTabContextRenameButton = CreateSquadTabContextMenuButton(_squadTabContextMenuPanel, "Rename");
            _squadTabContextDeleteButton = CreateSquadTabContextMenuButton(_squadTabContextMenuPanel, "Delete");

            _squadTabContextRenameButton.onClick.AddListener(HandleSquadTabContextRename);
            _squadTabContextDeleteButton.onClick.AddListener(HandleSquadTabContextDelete);

            _squadTabContextMenuRoot.gameObject.SetActive(false);
        }

        private static Button CreateSquadTabContextMenuButton(RectTransform parent, string labelText)
        {
            var buttonGo = new GameObject(labelText, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.SetParent(parent, false);

            var buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.12f, 0.14f, 0.18f, 1f);

            var layoutElement = buttonGo.GetComponent<LayoutElement>();
            layoutElement.minHeight = 30f;
            layoutElement.preferredHeight = 30f;
            layoutElement.minWidth = 132f;
            layoutElement.preferredWidth = 132f;

            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(buttonGo.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8f, 0f);
            labelRt.offsetMax = new Vector2(-8f, 0f);

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 15f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            label.raycastTarget = false;

            return button;
        }

        private void HandleSquadTabContextRename()
        {
            var tabIndex = _squadTabContextMenuTargetIndex;
            HideSquadTabContextMenu();
            if (tabIndex >= 0) BeginRenameSquadTab(tabIndex);
        }

        private void HandleSquadTabContextDelete()
        {
            var tabIndex = _squadTabContextMenuTargetIndex;
            HideSquadTabContextMenu();
            if (tabIndex >= 0) TryDeleteSquadGroup(tabIndex);
        }
    }
}
