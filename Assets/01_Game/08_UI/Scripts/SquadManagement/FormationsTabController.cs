#region

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Systems;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed class FormationsTabController : MonoBehaviour
    {
        private static FormationPresetEntry[] Presets => FormationPresetCatalog.PanelPresets;

        private readonly List<Button> _presetButtons = new();
        private readonly List<Vector3> _previewSlots = new();

        private CommandSystem _commandSystem;
        private FormationController _formationController;
        private TMP_FontAsset _fontAsset;
        private RectTransform _hostRoot;
        private Sprite _panelSprite;
        private TMP_Text _summaryText;
        private TMP_Text _previewText;
        private TMP_Text _presetDescriptionText;
        private Slider _spacingSlider;
        private Slider _widthSlider;
        private Slider _depthSlider;
        private Toggle _wholeSquadToggle;
        private FormationType _selectedPreset = FormationType.DefaultMovement;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            _hostRoot = host;
            _fontAsset = font;
            _panelSprite = panelBackground;
            _ = slotBackground;

            ClearChildren(_hostRoot);
            BuildLayout();
            EnsureRuntimeReferences();
            BindFromController();
            RefreshUi();
        }

        public void RefreshFromRuntime()
        {
            EnsureRuntimeReferences();
            BindFromController();
            RefreshUi();
        }

        private void BuildLayout()
        {
            var body = CreateRect("Body", _hostRoot);
            Stretch(body, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 8f;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = true;

            var presetColumn = CreateRect("PresetColumn", body);
            var presetLayout = presetColumn.gameObject.AddComponent<LayoutElement>();
            presetLayout.minWidth = 220f;
            presetLayout.preferredWidth = 260f;
            presetLayout.flexibleWidth = 0f;
            AddImage(presetColumn, new Color(0.15f, 0.15f, 0.14f, 0.98f), _panelSprite).type = Image.Type.Sliced;
            BuildPresetList(presetColumn);

            var detailColumn = CreateRect("DetailColumn", body);
            var detailLayout = detailColumn.gameObject.AddComponent<LayoutElement>();
            detailLayout.flexibleWidth = 1f;
            AddImage(detailColumn, new Color(0.13f, 0.14f, 0.13f, 0.98f), _panelSprite).type = Image.Type.Sliced;
            BuildDetailPanel(detailColumn);
        }

        private void BuildPresetList(RectTransform parent)
        {
            var header = CreateText(parent, "FORMATIONS", 20f, new Color(0.95f, 0.91f, 0.78f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -44f));

            var listFrame = CreateRect("PresetList", parent);
            Stretch(listFrame, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -52f));

            var layout = listFrame.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            for (var i = 0; i < Presets.Length; i++)
            {
                var preset = Presets[i];
                var button = CreatePresetButton(listFrame, preset);
                _presetButtons.Add(button);
            }
        }

        private Button CreatePresetButton(RectTransform parent, FormationPresetEntry preset)
        {
            var root = CreateRect(preset.Title, parent);
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 54f;

            var background = AddImage(root, new Color(0.20f, 0.20f, 0.18f, 0.98f), _panelSprite);
            background.type = Image.Type.Sliced;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => SelectPreset(preset.Type));

            var title = CreateText(root, preset.Title, 15f, new Color(0.94f, 0.90f, 0.76f, 1f), FontStyles.Bold,
                TextAlignmentOptions.TopLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(10f, 2f), new Vector2(-8f, -2f));

            var desc = CreateText(root, preset.Description, 11f, new Color(0.72f, 0.69f, 0.60f, 1f), FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            Stretch(desc.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(10f, 2f), new Vector2(-8f, -2f));

            return button;
        }

        private void BuildDetailPanel(RectTransform parent)
        {
            var header = CreateText(parent, "FORMATION SETUP", 20f, new Color(0.95f, 0.91f, 0.78f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -44f));

            _presetDescriptionText = CreateText(parent, string.Empty, 13f, new Color(0.78f, 0.74f, 0.64f, 1f),
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            Stretch(_presetDescriptionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -48f),
                new Vector2(-10f, -74f));

            _summaryText = CreateText(parent, string.Empty, 13f, new Color(0.86f, 0.82f, 0.72f, 1f), FontStyles.Bold,
                TextAlignmentOptions.TopLeft);
            _summaryText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(_summaryText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -78f),
                new Vector2(-10f, -150f));

            _spacingSlider = CreateLabeledSlider(parent, "Spacing", 0.5f, 6f, 1.4f, OnSpacingChanged, -158f, -188f);
            _widthSlider = CreateLabeledSlider(parent, "Width", 0.5f, 3f, 1f, OnWidthChanged, -196f, -226f);
            _depthSlider = CreateLabeledSlider(parent, "Depth", 0.5f, 3f, 1f, OnDepthChanged, -234f, -264f);

            var toggleRoot = CreateRect("WholeSquadToggle", parent);
            Stretch(toggleRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -272f), new Vector2(-10f, -302f));
            _wholeSquadToggle = toggleRoot.gameObject.AddComponent<Toggle>();
            _wholeSquadToggle.isOn = true;
            _wholeSquadToggle.onValueChanged.AddListener(OnWholeSquadToggleChanged);

            var toggleBg = AddImage(CreateRect("Background", toggleRoot), new Color(0.18f, 0.18f, 0.17f, 1f), _panelSprite);
            toggleBg.type = Image.Type.Sliced;
            _wholeSquadToggle.targetGraphic = toggleBg;

            var check = AddImage(CreateRect("Checkmark", toggleRoot), new Color(0.35f, 0.75f, 0.45f, 1f), null);
            var checkRect = check.rectTransform;
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.sizeDelta = new Vector2(18f, 18f);
            checkRect.anchoredPosition = new Vector2(12f, 0f);
            _wholeSquadToggle.graphic = check;

            var toggleLabel = CreateText(toggleRoot, "Apply profile to whole squad on move", 13f,
                new Color(0.84f, 0.80f, 0.70f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(toggleLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(34f, 0f), Vector2.zero);

            var applyRow = CreateRect("ApplyRow", parent);
            Stretch(applyRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -310f), new Vector2(-10f, -346f));
            var applyLayout = applyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            applyLayout.spacing = 8f;
            applyLayout.childForceExpandWidth = true;
            applyLayout.childForceExpandHeight = true;

            CreateActionButton(applyRow, "APPLY SELECTED", ApplyToSelected);
            CreateActionButton(applyRow, "APPLY WHOLE SQUAD", ApplyToWholeSquad);

            _previewText = CreateText(parent, string.Empty, 12f, new Color(0.76f, 0.73f, 0.66f, 1f), FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            _previewText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(_previewText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f),
                new Vector2(-10f, 120f));
        }

        private Slider CreateLabeledSlider(
            RectTransform parent,
            string label,
            float min,
            float max,
            float value,
            UnityEngine.Events.UnityAction<float> onChanged,
            float topInset,
            float bottomInset)
        {
            var row = CreateRect(label + "Row", parent);
            Stretch(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -bottomInset), new Vector2(-10f, -topInset));

            var text = CreateText(row, label, 13f, new Color(0.84f, 0.80f, 0.70f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform, new Vector2(0f, 0f), new Vector2(0.22f, 1f), Vector2.zero, Vector2.zero);

            var sliderRoot = CreateRect("Slider", row);
            Stretch(sliderRoot, new Vector2(0.22f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(onChanged);

            var bg = AddImage(CreateRect("Background", sliderRoot), new Color(0.12f, 0.12f, 0.11f, 1f), null);
            Stretch(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slider.targetGraphic = bg;

            var fillArea = CreateRect("Fill Area", sliderRoot);
            Stretch(fillArea, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            var fill = AddImage(CreateRect("Fill", fillArea), new Color(0.35f, 0.62f, 0.42f, 1f), null);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slider.fillRect = fill.rectTransform;

            var handle = AddImage(CreateRect("Handle", sliderRoot), new Color(0.90f, 0.86f, 0.72f, 1f), null);
            handle.rectTransform.sizeDelta = new Vector2(14f, 18f);
            slider.handleRect = handle.rectTransform;

            return slider;
        }

        private void CreateActionButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var root = CreateRect(label, parent);
            var image = AddImage(root, new Color(0.24f, 0.36f, 0.28f, 0.98f), _panelSprite);
            image.type = Image.Type.Sliced;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateText(root, label, 12f, new Color(0.95f, 0.92f, 0.80f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void SelectPreset(FormationType type)
        {
            _selectedPreset = type;
            _formationController?.SetFormation(type);
            RefreshUi();
        }

        private void OnSpacingChanged(float value)
        {
            _formationController?.SetSlotSpacing(value);
            RefreshUi();
        }

        private void OnWidthChanged(float value)
        {
            _formationController?.SetLateralBias(value);
            RefreshUi();
        }

        private void OnDepthChanged(float value)
        {
            _formationController?.SetDepthBias(value);
            RefreshUi();
        }

        private void OnWholeSquadToggleChanged(bool wholeSquad)
        {
            _formationController?.SetApplyToWholeSquad(wholeSquad);
            RefreshUi();
        }

        private void ApplyToSelected()
        {
            EnsureRuntimeReferences();
            if (_commandSystem == null || _formationController == null) return;

            PushUiToController();
            var members = ResolveTargetMembers(selectedOnly: true);
            if (members.Count == 0) return;

            _commandSystem.RegroupMembers(members);
            RefreshUi();
        }

        private void ApplyToWholeSquad()
        {
            EnsureRuntimeReferences();
            if (_commandSystem == null || _formationController == null) return;

            _formationController.SetApplyToWholeSquad(true);
            if (_wholeSquadToggle != null) _wholeSquadToggle.isOn = true;

            PushUiToController();
            var members = ResolveTargetMembers(selectedOnly: false);
            if (members.Count == 0) return;

            _commandSystem.RegroupMembers(members);
            RefreshUi();
        }

        private void PushUiToController()
        {
            if (_formationController == null) return;

            _formationController.SetFormation(_selectedPreset);
            if (_spacingSlider != null) _formationController.SetSlotSpacing(_spacingSlider.value);
            if (_widthSlider != null) _formationController.SetLateralBias(_widthSlider.value);
            if (_depthSlider != null) _formationController.SetDepthBias(_depthSlider.value);
            if (_wholeSquadToggle != null) _formationController.SetApplyToWholeSquad(_wholeSquadToggle.isOn);
        }

        private List<SquadMember> ResolveTargetMembers(bool selectedOnly)
        {
            var output = new List<SquadMember>();
            if (!SquadManager.HasInstance) return output;

            if (selectedOnly && SquadManager.Instance.HasSelection)
            {
                var selected = SquadManager.Instance.SelectedMembers;
                for (var i = 0; i < selected.Count; i++)
                {
                    if (selected[i] != null) output.Add(selected[i]);
                }

                return output;
            }

            var members = SquadManager.Instance.SquadMembers;
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i] != null) output.Add(members[i]);
            }

            return output;
        }

        private void EnsureRuntimeReferences()
        {
            _commandSystem = SquadManager.ResolveRuntimeCommandSystem();
            _formationController = SquadManager.ResolveRuntimeFormationController();
        }

        private void BindFromController()
        {
            if (_formationController == null) return;

            _selectedPreset = _formationController.ActiveFormation;
            if (_spacingSlider != null) _spacingSlider.SetValueWithoutNotify(_formationController.SlotSpacing);
            if (_widthSlider != null) _widthSlider.SetValueWithoutNotify(_formationController.LateralBias);
            if (_depthSlider != null) _depthSlider.SetValueWithoutNotify(_formationController.DepthBias);
            if (_wholeSquadToggle != null) _wholeSquadToggle.SetIsOnWithoutNotify(_formationController.ApplyToWholeSquad);
        }

        private void RefreshUi()
        {
            RefreshPresetButtonVisuals();
            RefreshSummary();
            RefreshPreview();
        }

        private void RefreshPresetButtonVisuals()
        {
            for (var i = 0; i < _presetButtons.Count && i < Presets.Length; i++)
            {
                var active = Presets[i].Type == _selectedPreset;
                var image = _presetButtons[i].targetGraphic as Image;
                if (image != null)
                {
                    image.color = active
                        ? new Color(0.28f, 0.48f, 0.34f, 0.98f)
                        : new Color(0.20f, 0.20f, 0.18f, 0.98f);
                }
            }

            if (_presetDescriptionText == null) return;

            for (var i = 0; i < Presets.Length; i++)
            {
                if (Presets[i].Type != _selectedPreset) continue;
                _presetDescriptionText.text = Presets[i].Description;
                return;
            }
        }

        private void RefreshSummary()
        {
            if (_summaryText == null) return;
            if (_formationController == null)
            {
                _summaryText.text = "Formation controller not found in scene.";
                return;
            }

            _summaryText.text =
                $"Active: {_formationController.ActiveFormation}\n" +
                $"Spacing {_formationController.SlotSpacing:0.0} · " +
                $"Width {_formationController.LateralBias:0.0} · " +
                $"Depth {_formationController.DepthBias:0.0}\n" +
                $"Move scope: {(_formationController.ApplyToWholeSquad ? "Whole squad" : "Selected members")}";
        }

        private void RefreshPreview()
        {
            if (_previewText == null || _formationController == null) return;

            var memberCount = SquadManager.HasInstance ? Mathf.Max(1, SquadManager.Instance.SquadMembers.Count) : 4;
            _previewSlots.Clear();
            var slots = _formationController.CalculateFormationSlots(Vector3.zero, Vector3.forward, memberCount);
            for (var i = 0; i < slots.Count; i++) _previewSlots.Add(slots[i]);

            var builder = new StringBuilder();
            builder.AppendLine("Slot preview (local offsets):");
            for (var i = 0; i < _previewSlots.Count; i++)
            {
                var slot = _previewSlots[i];
                builder.Append("  #").Append(i + 1).Append(" (")
                    .Append(slot.x.ToString("0.0")).Append(", ")
                    .Append(slot.z.ToString("0.0")).AppendLine(")");
            }

            _previewText.text = builder.ToString();
        }

        private static void ClearChildren(RectTransform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
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

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        private TMP_Text CreateText(
            RectTransform parent,
            string text,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            var rect = CreateRect("Text", parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            if (_fontAsset != null) tmp.font = _fontAsset;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
