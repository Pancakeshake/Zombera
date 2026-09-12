#region

using TMPro;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class SquadCustomizerTabController
    {
        private void BuildSquadNameRow(RectTransform parent)
        {
            var labelRect = CreateRect("Label", parent);
            Stretch(labelRect, new Vector2(0f, 0f), new Vector2(0.28f, 1f), Vector2.zero, Vector2.zero);

            var label = CreateText(labelRect, "Squad Name", 14f, new Color(0.80f, 0.77f, 0.67f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var inputRect = CreateRect("Input", parent);
            Stretch(inputRect, new Vector2(0.28f, 0f), new Vector2(0.78f, 1f), new Vector2(6f, 0f),
                new Vector2(-6f, 0f));
            _squadNameInput = CreateInputField(inputRect, "Squad 1");

            var buttonRect = CreateRect("ApplyButton", parent);
            Stretch(buttonRect, new Vector2(0.80f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var applyButton = CreateButton(buttonRect, "Apply Name", 13f);
            applyButton.onClick.AddListener(EmitSquadNameChange);
        }

        private void BuildBody(RectTransform body)
        {
            var left = CreateRect("MemberColumn", body);
            Stretch(left, new Vector2(0f, 0f), new Vector2(0.43f, 1f), new Vector2(8f, 8f), new Vector2(-4f, -8f));
            AddImage(left, new Color(0.17f, 0.17f, 0.16f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            var right = CreateRect("ConfigColumn", body);
            Stretch(right, new Vector2(0.43f, 0f), new Vector2(1f, 1f), new Vector2(4f, 8f), new Vector2(-8f, -8f));
            AddImage(right, new Color(0.16f, 0.16f, 0.15f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            BuildMemberColumn(left);
            BuildConfigColumn(right);
        }

        private void BuildMemberColumn(RectTransform left)
        {
            var title = CreateText(left, "MEMBER ORDER", 16f, new Color(0.91f, 0.86f, 0.73f, 1f), FontStyles.Bold,
                TextAlignmentOptions.TopLeft);
            Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -8f),
                new Vector2(-10f, -34f));

            var listFrame = CreateRect("ListFrame", left);
            Stretch(listFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 50f), new Vector2(-8f, -40f));
            AddImage(listFrame, new Color(0.12f, 0.12f, 0.11f, 1f), _panelSprite).type = Image.Type.Sliced;

            BuildMemberList(listFrame);

            var moveRow = CreateRect("MoveRow", left);
            Stretch(moveRow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), new Vector2(-8f, 44f));

            _moveUpButton = CreateButton(CreateRect("MoveUp", moveRow), "Move Up", 13f);
            Stretch(_moveUpButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(-4f, 0f));
            _moveUpButton.onClick.AddListener(() => MoveSelected(-1));

            _moveDownButton = CreateButton(CreateRect("MoveDown", moveRow), "Move Down", 13f);
            Stretch(_moveDownButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                new Vector2(4f, 0f), Vector2.zero);
            _moveDownButton.onClick.AddListener(() => MoveSelected(1));
        }

        private void BuildMemberList(RectTransform parent)
        {
            var scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            AddImage(scrollRoot, new Color(0.10f, 0.10f, 0.09f, 1f), null);
            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 26f;

            var viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.06f), null);
            viewportImage.maskable = true;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _memberListContent = CreateRect("Content", viewport);
            _memberListContent.anchorMin = new Vector2(0f, 1f);
            _memberListContent.anchorMax = new Vector2(1f, 1f);
            _memberListContent.pivot = new Vector2(0.5f, 1f);
            _memberListContent.offsetMin = Vector2.zero;
            _memberListContent.offsetMax = Vector2.zero;

            var layout = _memberListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _memberListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = _memberListContent;
        }

        private void BuildConfigColumn(RectTransform right)
        {
            _selectedMemberText = CreateText(right, "Selected: -", 16f, new Color(0.92f, 0.87f, 0.74f, 1f),
                FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(_selectedMemberText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -8f),
                new Vector2(-10f, -36f));

            var controls = CreateRect("Controls", right);
            Stretch(controls, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 78f), new Vector2(-10f, -42f));
            AddImage(controls, new Color(0.12f, 0.12f, 0.11f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            BuildCycleControl(controls, "Role", _roleOptions, 0, out _roleValueText,
                out var rolePrev, out var roleNext);
            rolePrev.onClick.AddListener(() => ShiftOption(_roleOptions, _roleValueText, -1));
            roleNext.onClick.AddListener(() => ShiftOption(_roleOptions, _roleValueText, 1));

            BuildCycleControl(controls, "Loadout", _loadoutOptions, 1, out _loadoutValueText,
                out var loadoutPrev, out var loadoutNext);
            loadoutPrev.onClick.AddListener(() => ShiftOption(_loadoutOptions, _loadoutValueText, -1));
            loadoutNext.onClick.AddListener(() => ShiftOption(_loadoutOptions, _loadoutValueText, 1));

            BuildCycleControl(controls, "Position", _positionOptions, 2, out _positionValueText,
                out var positionPrev, out var positionNext);
            positionPrev.onClick.AddListener(() => ShiftOption(_positionOptions, _positionValueText, -1));
            positionNext.onClick.AddListener(() => ShiftOption(_positionOptions, _positionValueText, 1));

            var assignButtonRect = CreateRect("AssignButton", controls);
            Stretch(assignButtonRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f),
                new Vector2(-10f, 56f));
            var assignButton = CreateButton(assignButtonRect, "Apply Assignment", 14f);
            assignButton.onClick.AddListener(ApplyAssignment);

            var statusFrame = CreateRect("StatusFrame", right);
            Stretch(statusFrame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 8f),
                new Vector2(-10f, 72f));
            AddImage(statusFrame, new Color(0.12f, 0.10f, 0.09f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            _statusText = CreateText(statusFrame, "No assignment changes yet.", 13f, new Color(0.81f, 0.77f, 0.67f, 1f),
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(_statusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        }

        private void BuildCycleControl(
            RectTransform parent,
            string label,
            string[] options,
            int row,
            out TMP_Text valueText,
            out Button prevButton,
            out Button nextButton)
        {
            const float rowHeight = 0.26f;
            var top = 1f - row * 0.31f - 0.04f;
            var bottom = top - rowHeight;

            var rowRect = CreateRect(label + "Row", parent);
            Stretch(rowRect, new Vector2(0f, bottom), new Vector2(1f, top), new Vector2(8f, 0f), new Vector2(-8f, 0f));
            AddImage(rowRect, new Color(0.18f, 0.18f, 0.17f, 0.98f), _slotSprite).type = Image.Type.Sliced;

            var labelText = CreateText(rowRect, label.ToUpperInvariant(), 12f, new Color(0.80f, 0.76f, 0.66f, 1f),
                FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(labelText.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), new Vector2(10f, 0f),
                new Vector2(-10f, 0f));

            var valueRect = CreateRect(label + "Value", rowRect);
            Stretch(valueRect, new Vector2(0.22f, 0f), new Vector2(0.78f, 0.52f), Vector2.zero, new Vector2(0f, -4f));
            AddImage(valueRect, new Color(0.11f, 0.11f, 0.10f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            valueText = CreateText(valueRect, options.Length > 0 ? options[0] : "-", 14f,
                new Color(0.93f, 0.89f, 0.76f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(valueText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var prevRect = CreateRect(label + "Prev", rowRect);
            Stretch(prevRect, new Vector2(0f, 0f), new Vector2(0.20f, 0.52f), new Vector2(0f, 0f),
                new Vector2(-4f, -4f));
            prevButton = CreateButton(prevRect, "<", 18f);

            var nextRect = CreateRect(label + "Next", rowRect);
            Stretch(nextRect, new Vector2(0.80f, 0f), new Vector2(1f, 0.52f), new Vector2(4f, 0f),
                new Vector2(0f, -4f));
            nextButton = CreateButton(nextRect, ">", 18f);
        }
    }
}
