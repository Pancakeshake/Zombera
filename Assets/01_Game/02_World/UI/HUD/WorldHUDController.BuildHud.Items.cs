using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {
        private void EnsureBottomBuildItemsStrip()
        {
            if (_bottomBuildItemsRoot != null) return;

            var stripParent = portraitStrip != null ? portraitStrip.transform.parent as RectTransform : bottomBarRoot;
            if (stripParent == null) return;

            _bottomBuildItemsRoot = MakeRect("BuildItemStrip", stripParent);

            if (portraitStrip != null)
            {
                var portraitRect = portraitStrip.transform as RectTransform;
                if (portraitRect != null)
                {
                    _bottomBuildItemsRoot.anchorMin = portraitRect.anchorMin;
                    _bottomBuildItemsRoot.anchorMax = portraitRect.anchorMax;
                    _bottomBuildItemsRoot.pivot = portraitRect.pivot;
                    _bottomBuildItemsRoot.anchoredPosition = portraitRect.anchoredPosition;
                    _bottomBuildItemsRoot.sizeDelta = portraitRect.sizeDelta;
                    _bottomBuildItemsRoot.offsetMin = portraitRect.offsetMin;
                    _bottomBuildItemsRoot.offsetMax = portraitRect.offsetMax;
                }
            }
            else
            {
                _bottomBuildItemsRoot.anchorMin = Vector2.zero;
                _bottomBuildItemsRoot.anchorMax = Vector2.one;
                _bottomBuildItemsRoot.offsetMin = new Vector2(8f, 6f);
                _bottomBuildItemsRoot.offsetMax = new Vector2(-260f, -46f);
            }

            var layout = _bottomBuildItemsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var entries = new[]
            {
                "Wall Full [1]", "Wall Window [2]", "Wall Door [3]", "Wall Damaged [4]", "Skin 1 [5]",
                "Skin 2 [6]", "Skin 3 [7]", "Skin 4 [8]", "Skin 5 [9]", "Cancel [Esc]"
            };

            _bottomBuildItemBoxes.Clear();

            for (var i = 0; i < entries.Length; i++)
            {
                var itemIndex = i;
                var box = MakeRect($"BuildItem_{i + 1}", _bottomBuildItemsRoot);
                var boxImage = box.gameObject.AddComponent<Image>();
                boxImage.color = new Color(0.10f, 0.14f, 0.18f, 0.95f);

                var boxButton = box.gameObject.AddComponent<Button>();
                boxButton.targetGraphic = boxImage;
                boxButton.onClick.AddListener(() => HandleBottomBuildItemClicked(itemIndex));

                var boxLayout = box.gameObject.AddComponent<LayoutElement>();
                boxLayout.minWidth = 108f;
                boxLayout.preferredWidth = 122f;
                boxLayout.flexibleWidth = 1f;
                boxLayout.minHeight = 94f;
                boxLayout.preferredHeight = 94f;

                var border = box.gameObject.AddComponent<Outline>();
                border.effectColor = new Color(0.24f, 0.36f, 0.44f, 0.90f);
                border.effectDistance = new Vector2(1f, -1f);

                var iconRt = MakeRect("Icon", box);
                iconRt.anchorMin = new Vector2(0.06f, 0.44f);
                iconRt.anchorMax = new Vector2(0.94f, 0.94f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;

                var iconImage = iconRt.gameObject.AddComponent<Image>();
                iconImage.raycastTarget = false;
                iconImage.color = new Color(1f, 1f, 1f, 0.85f);

                var label = MakeText("Label", box, entries[i], 14f);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 0.46f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold;
                label.color = new Color(0.90f, 0.92f, 0.94f, 1f);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.margin = new Vector4(4f, 2f, 4f, 2f);

                _bottomBuildItemBoxes.Add(new BuildItemBoxView
                {
                    ItemIndex = itemIndex,
                    SourceItemIndex = itemIndex,
                    Background = boxImage,
                    Button = boxButton,
                    Icon = iconImage,
                    Label = label,
                    DefaultLabelText = entries[i],
                    DefaultBackgroundColor = boxImage.color,
                    DefaultLabelColor = label.color
                });
            }

            RefreshBottomBuildItemVisuals();
            _bottomBuildItemsRoot.gameObject.SetActive(false);
        }


        private void RefreshBottomBuildItemVisuals()
        {
            if (_bottomBuildItemBoxes.Count == 0) return;

            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            _ = EnsureLegacyBuildPlacementController();

            var missingSourceCount = 0;
            var missingIconCount = 0;

            for (var i = 0; i < _bottomBuildItemBoxes.Count; i++)
            {
                var box = _bottomBuildItemBoxes[i];
                if (box == null || box.Icon == null || box.Label == null) continue;

                var sourceIndex = ResolveBuildSourceItemIndex(box.ItemIndex);
                box.SourceItemIndex = sourceIndex;

                if (box.ItemIndex == 9)
                {
                    box.Label.text = box.DefaultLabelText;
                    box.Icon.sprite = null;
                    box.Icon.color = new Color(1f, 1f, 1f, 0.10f);
                    continue;
                }

                if (sourceIndex < 0)
                {
                    missingSourceCount++;
                    box.Label.text = "-";
                    box.Icon.sprite = null;
                    box.Icon.color = new Color(1f, 1f, 1f, 0.08f);
                    continue;
                }

                string labelText = null;
                Texture2D texture = null;

                Sprite spriteFromEasyBuild = null;

                if (_easyBuildRadialMenuInputBridge != null)
                {
                    labelText = GetCachedBuildLabel(sourceIndex);
                    texture = _easyBuildRadialMenuInputBridge.GetHudItemTexture(sourceIndex);
                    if (texture == null)
                        spriteFromEasyBuild = _easyBuildRadialMenuInputBridge.GetHudItemSprite(sourceIndex);
                }

                if (string.IsNullOrWhiteSpace(labelText))
                    labelText = $"Item {sourceIndex + 1}";

                labelText = WithBuildHotkeySuffix(labelText, box.ItemIndex);

                box.Label.text = labelText;

                if (texture == null && _legacyBuildPlacementController != null)
                    texture = _legacyBuildPlacementController.GetHudItemTexture(sourceIndex);

                if (texture == null && spriteFromEasyBuild == null)
                {
                    missingIconCount++;
                    box.Icon.sprite = null;
                    box.Icon.color = new Color(1f, 1f, 1f, 0.10f);
                    continue;
                }

                if (spriteFromEasyBuild != null)
                {
                    box.Icon.sprite = spriteFromEasyBuild;
                }
                else
                {
                    box.Icon.sprite = BuildRuntimeSprite(texture);
                }

                box.Icon.type = Image.Type.Simple;
                box.Icon.preserveAspect = true;
                box.Icon.color = new Color(1f, 1f, 1f, 0.92f);
            }

            BuildLog(
                $"Build visuals refreshed: boxes={_bottomBuildItemBoxes.Count}, missingSource={missingSourceCount}, missingIcons={missingIconCount}",
                true);
        }


        private Sprite BuildRuntimeSprite(Texture2D texture)
        {
            if (texture == null) return null;

            var textureId = texture.GetInstanceID();
            if (_buildIconSpriteCache.TryGetValue(textureId, out var cachedSprite) && cachedSprite != null)
                return cachedSprite;

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            _buildIconSpriteCache[textureId] = sprite;
            return sprite;
        }


        private void RefreshBottomBuildItemHighlights()
        {
            if (_bottomBuildItemBoxes.Count == 0) return;

            var activeIndex = ResolveActiveBuildItemBoxIndex();
            if (activeIndex == _activeBuildItemBoxIndex) return;

            BuildLog($"Highlight changed: {_activeBuildItemBoxIndex} -> {activeIndex}");
            _activeBuildItemBoxIndex = activeIndex;

            for (var i = 0; i < _bottomBuildItemBoxes.Count; i++)
            {
                var box = _bottomBuildItemBoxes[i];
                if (box == null || box.Background == null || box.Label == null) continue;

                var active = i == _activeBuildItemBoxIndex;
                box.Background.color = active
                    ? new Color(0.20f, 0.50f, 0.34f, 0.98f)
                    : box.DefaultBackgroundColor;
                box.Label.color = active
                    ? new Color(0.95f, 0.98f, 0.96f, 1f)
                    : box.DefaultLabelColor;
            }
        }


        private int ResolveActiveBuildItemBoxIndex()
        {
            if (_explicitBuildItemBoxIndex >= -1)
            {
                BuildLog($"Highlight source: explicit index={_explicitBuildItemBoxIndex}", true);
                return _explicitBuildItemBoxIndex;
            }

            var bridgeVisibleIndex = -1;
            if (_easyBuildRadialMenuInputBridge != null
                && _easyBuildRadialMenuInputBridge.isActiveAndEnabled)
            {
                var bridgeIndex = _easyBuildRadialMenuInputBridge.CurrentHudSelectionIndex;
                if (bridgeIndex >= 0)
                {
                    bridgeVisibleIndex = MapBuildSourceToVisibleIndex(bridgeIndex);
                    if (bridgeVisibleIndex >= 0)
                        BuildLog($"Highlight source candidate: bridge source={bridgeIndex} -> visible={bridgeVisibleIndex}", true);
                }
            }

            var binderVisibleIndex = -1;

            if (_easyBuildCursorPlacementBinder != null)
            {
                var easyBuildIndex = _easyBuildCursorPlacementBinder.GetEasyBuildSelectedHudBoxIndex();
                if (easyBuildIndex >= 0)
                {
                    binderVisibleIndex = MapBuildSourceToVisibleIndex(easyBuildIndex);
                    if (binderVisibleIndex >= 0)
                        BuildLog($"Highlight source candidate: cursor binder source={easyBuildIndex} -> visible={binderVisibleIndex}", true);
                }
            }

            if (bridgeVisibleIndex >= 0)
            {
                if (binderVisibleIndex >= 0 && binderVisibleIndex != bridgeVisibleIndex)
                    BuildLog($"Highlight source conflict: bridge visible={bridgeVisibleIndex}, binder visible={binderVisibleIndex}; using bridge", true);

                return bridgeVisibleIndex;
            }

            if (binderVisibleIndex >= 0)
                return binderVisibleIndex;

            if (_explicitBuildItemBoxIndex >= -1)
                return _explicitBuildItemBoxIndex;

            if (_legacyBuildPlacementController != null)
            {
                if (_legacyBuildPlacementController.IsSkinSelectionEnabled)
                {
                    var skinIndex = _legacyBuildPlacementController.SelectedSkinSlotIndex;
                    if (skinIndex >= 0 && skinIndex <= 4)
                        return 4 + skinIndex;
                }

                var wallIndex = _legacyBuildPlacementController.SelectedWallSlotIndex;
                if (wallIndex >= 0 && wallIndex <= 3) return wallIndex;
            }

            return -1;
        }


        private static string WithBuildHotkeySuffix(string label, int visibleItemIndex)
        {
            if (visibleItemIndex < 0 || visibleItemIndex > 8) return label;

            var hotkey = (visibleItemIndex + 1).ToString();
            if (!string.IsNullOrWhiteSpace(label) && label.IndexOf("[", StringComparison.Ordinal) >= 0)
                return label;

            return string.IsNullOrWhiteSpace(label) ? $"[{hotkey}]" : $"{label} [{hotkey}]";
        }
    }
}
