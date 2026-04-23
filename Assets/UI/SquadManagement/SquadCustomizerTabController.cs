using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.SquadManagement
{
    public sealed class SquadCustomizerTabController : MonoBehaviour
    {
        private sealed class MemberView
        {
            public RectTransform Root;
            public Button Button;
            public Image Background;
            public TMP_Text Name;
        }

        private sealed class MemberAssignment
        {
            public int RoleIndex;
            public int LoadoutIndex;
            public int PositionIndex;
        }

        private readonly List<string> members = new List<string>();
        private readonly List<MemberView> memberViews = new List<MemberView>();
        private readonly Dictionary<string, MemberAssignment> assignmentByMember =
            new Dictionary<string, MemberAssignment>(StringComparer.Ordinal);

        private readonly string[] roleOptions = { "Scout", "Medic", "Bruiser", "Technician", "Leader" };
        private readonly string[] loadoutOptions = { "Balanced Kit", "Rifle Kit", "Shotgun Kit", "Support Kit", "Stealth Kit" };
        private readonly string[] positionOptions = { "Frontline", "Flank Left", "Flank Right", "Rear Guard", "Reserve" };

        private RectTransform hostRoot;
        private RectTransform memberListContent;
        private TMP_FontAsset fontAsset;
        private Sprite panelSprite;
        private Sprite slotSprite;

        private TMP_InputField squadNameInput;
        private TMP_Text selectedMemberText;
        private TMP_Text roleValueText;
        private TMP_Text loadoutValueText;
        private TMP_Text positionValueText;
        private TMP_Text statusText;

        private Button moveUpButton;
        private Button moveDownButton;
        private int selectedIndex = -1;

        public event Action<string> SquadNameChanged;
        public event Action<IReadOnlyList<string>> MemberOrderChanged;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            hostRoot = host;
            fontAsset = font;
            panelSprite = panelBackground;
            slotSprite = slotBackground;

            ClearChildren(hostRoot);

            RectTransform header = CreateRect("Header", hostRoot);
            Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-6f, -76f));
            AddImage(header, new Color(0.20f, 0.20f, 0.18f, 0.98f), panelSprite).type = Image.Type.Sliced;

            TMP_Text title = CreateText(header, "SQUAD CUSTOMISER", 24f, new Color(0.95f, 0.91f, 0.78f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.32f, 1f), new Vector2(10f, 0f), Vector2.zero);

            RectTransform nameRow = CreateRect("NameRow", header);
            Stretch(nameRow, new Vector2(0.34f, 0f), new Vector2(1f, 1f), new Vector2(0f, 12f), new Vector2(-10f, -12f));
            BuildSquadNameRow(nameRow);

            RectTransform body = CreateRect("Body", hostRoot);
            Stretch(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -82f));
            AddImage(body, new Color(0.14f, 0.14f, 0.13f, 0.98f), panelSprite).type = Image.Type.Sliced;

            BuildBody(body);
        }

        public void SetSquadName(string squadName)
        {
            if (squadNameInput == null)
            {
                return;
            }

            squadNameInput.text = string.IsNullOrWhiteSpace(squadName) ? string.Empty : squadName.Trim();
        }

        public void SetMembers(IReadOnlyList<string> orderedNames)
        {
            members.Clear();
            assignmentByMember.Clear();

            if (orderedNames != null)
            {
                for (int i = 0; i < orderedNames.Count; i++)
                {
                    string name = string.IsNullOrWhiteSpace(orderedNames[i])
                        ? "Survivor " + (i + 1)
                        : orderedNames[i].Trim();

                    members.Add(name);
                    assignmentByMember[name] = new MemberAssignment();
                }
            }

            RebuildMemberList();

            if (members.Count > 0)
            {
                SetSelectedIndex(0);
            }
            else
            {
                selectedIndex = -1;
                UpdateSelectionDetails();
            }
        }

        public void SetSelectedIndex(int index)
        {
            if (members.Count == 0)
            {
                selectedIndex = -1;
                UpdateSelectionDetails();
                return;
            }

            selectedIndex = Mathf.Clamp(index, 0, members.Count - 1);
            UpdateMemberListVisuals();
            LoadAssignmentIntoControls();
            UpdateSelectionDetails();
        }

        private void BuildSquadNameRow(RectTransform parent)
        {
            RectTransform labelRect = CreateRect("Label", parent);
            Stretch(labelRect, new Vector2(0f, 0f), new Vector2(0.28f, 1f), Vector2.zero, Vector2.zero);

            TMP_Text label = CreateText(labelRect, "Squad Name", 14f, new Color(0.80f, 0.77f, 0.67f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform inputRect = CreateRect("Input", parent);
            Stretch(inputRect, new Vector2(0.28f, 0f), new Vector2(0.78f, 1f), new Vector2(6f, 0f), new Vector2(-6f, 0f));
            squadNameInput = CreateInputField(inputRect, "Squad 1");

            RectTransform buttonRect = CreateRect("ApplyButton", parent);
            Stretch(buttonRect, new Vector2(0.80f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            Button applyButton = CreateButton(buttonRect, "Apply Name", 13f);
            applyButton.onClick.AddListener(EmitSquadNameChange);
        }

        private void BuildBody(RectTransform body)
        {
            RectTransform left = CreateRect("MemberColumn", body);
            Stretch(left, new Vector2(0f, 0f), new Vector2(0.43f, 1f), new Vector2(8f, 8f), new Vector2(-4f, -8f));
            AddImage(left, new Color(0.17f, 0.17f, 0.16f, 0.98f), panelSprite).type = Image.Type.Sliced;

            RectTransform right = CreateRect("ConfigColumn", body);
            Stretch(right, new Vector2(0.43f, 0f), new Vector2(1f, 1f), new Vector2(4f, 8f), new Vector2(-8f, -8f));
            AddImage(right, new Color(0.16f, 0.16f, 0.15f, 0.98f), panelSprite).type = Image.Type.Sliced;

            BuildMemberColumn(left);
            BuildConfigColumn(right);
        }

        private void BuildMemberColumn(RectTransform left)
        {
            TMP_Text title = CreateText(left, "MEMBER ORDER", 16f, new Color(0.91f, 0.86f, 0.73f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -8f), new Vector2(-10f, -34f));

            RectTransform listFrame = CreateRect("ListFrame", left);
            Stretch(listFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 50f), new Vector2(-8f, -40f));
            AddImage(listFrame, new Color(0.12f, 0.12f, 0.11f, 1f), panelSprite).type = Image.Type.Sliced;

            BuildMemberList(listFrame);

            RectTransform moveRow = CreateRect("MoveRow", left);
            Stretch(moveRow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), new Vector2(-8f, 44f));

            moveUpButton = CreateButton(CreateRect("MoveUp", moveRow), "Move Up", 13f);
            Stretch(moveUpButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-4f, 0f));
            moveUpButton.onClick.AddListener(() => MoveSelected(-1));

            moveDownButton = CreateButton(CreateRect("MoveDown", moveRow), "Move Down", 13f);
            Stretch(moveDownButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), Vector2.zero);
            moveDownButton.onClick.AddListener(() => MoveSelected(1));
        }

        private void BuildMemberList(RectTransform parent)
        {
            RectTransform scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            AddImage(scrollRoot, new Color(0.10f, 0.10f, 0.09f, 1f), null);
            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 26f;

            RectTransform viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.06f), null);
            viewportImage.maskable = true;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            memberListContent = CreateRect("Content", viewport);
            memberListContent.anchorMin = new Vector2(0f, 1f);
            memberListContent.anchorMax = new Vector2(1f, 1f);
            memberListContent.pivot = new Vector2(0.5f, 1f);
            memberListContent.offsetMin = Vector2.zero;
            memberListContent.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = memberListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = memberListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = memberListContent;
        }

        private void BuildConfigColumn(RectTransform right)
        {
            selectedMemberText = CreateText(right, "Selected: -", 16f, new Color(0.92f, 0.87f, 0.74f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(selectedMemberText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -8f), new Vector2(-10f, -36f));

            RectTransform controls = CreateRect("Controls", right);
            Stretch(controls, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 78f), new Vector2(-10f, -42f));
            AddImage(controls, new Color(0.12f, 0.12f, 0.11f, 0.98f), panelSprite).type = Image.Type.Sliced;

            BuildCycleControl(controls, "Role", roleOptions, 0, out roleValueText,
                out Button rolePrev, out Button roleNext);
            rolePrev.onClick.AddListener(() => ShiftOption(roleOptions, roleValueText, -1));
            roleNext.onClick.AddListener(() => ShiftOption(roleOptions, roleValueText, 1));

            BuildCycleControl(controls, "Loadout", loadoutOptions, 1, out loadoutValueText,
                out Button loadoutPrev, out Button loadoutNext);
            loadoutPrev.onClick.AddListener(() => ShiftOption(loadoutOptions, loadoutValueText, -1));
            loadoutNext.onClick.AddListener(() => ShiftOption(loadoutOptions, loadoutValueText, 1));

            BuildCycleControl(controls, "Position", positionOptions, 2, out positionValueText,
                out Button positionPrev, out Button positionNext);
            positionPrev.onClick.AddListener(() => ShiftOption(positionOptions, positionValueText, -1));
            positionNext.onClick.AddListener(() => ShiftOption(positionOptions, positionValueText, 1));

            RectTransform assignButtonRect = CreateRect("AssignButton", controls);
            Stretch(assignButtonRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f), new Vector2(-10f, 56f));
            Button assignButton = CreateButton(assignButtonRect, "Apply Assignment", 14f);
            assignButton.onClick.AddListener(ApplyAssignment);

            RectTransform statusFrame = CreateRect("StatusFrame", right);
            Stretch(statusFrame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 8f), new Vector2(-10f, 72f));
            AddImage(statusFrame, new Color(0.12f, 0.10f, 0.09f, 0.98f), panelSprite).type = Image.Type.Sliced;

            statusText = CreateText(statusFrame, "No assignment changes yet.", 13f, new Color(0.81f, 0.77f, 0.67f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(statusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
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
            float rowHeight = 0.26f;
            float top = 1f - (row * 0.31f) - 0.04f;
            float bottom = top - rowHeight;

            RectTransform rowRect = CreateRect(label + "Row", parent);
            Stretch(rowRect, new Vector2(0f, bottom), new Vector2(1f, top), new Vector2(8f, 0f), new Vector2(-8f, 0f));
            AddImage(rowRect, new Color(0.18f, 0.18f, 0.17f, 0.98f), slotSprite).type = Image.Type.Sliced;

            TMP_Text labelText = CreateText(rowRect, label.ToUpperInvariant(), 12f, new Color(0.80f, 0.76f, 0.66f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(labelText.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, 0f));

            RectTransform valueRect = CreateRect(label + "Value", rowRect);
            Stretch(valueRect, new Vector2(0.22f, 0f), new Vector2(0.78f, 0.52f), Vector2.zero, new Vector2(0f, -4f));
            AddImage(valueRect, new Color(0.11f, 0.11f, 0.10f, 0.98f), panelSprite).type = Image.Type.Sliced;

            valueText = CreateText(valueRect, options.Length > 0 ? options[0] : "-", 14f, new Color(0.93f, 0.89f, 0.76f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(valueText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform prevRect = CreateRect(label + "Prev", rowRect);
            Stretch(prevRect, new Vector2(0f, 0f), new Vector2(0.20f, 0.52f), new Vector2(0f, 0f), new Vector2(-4f, -4f));
            prevButton = CreateButton(prevRect, "<", 18f);

            RectTransform nextRect = CreateRect(label + "Next", rowRect);
            Stretch(nextRect, new Vector2(0.80f, 0f), new Vector2(1f, 0.52f), new Vector2(4f, 0f), new Vector2(0f, -4f));
            nextButton = CreateButton(nextRect, ">", 18f);
        }

        private TMP_InputField CreateInputField(RectTransform parent, string defaultValue)
        {
            AddImage(parent, new Color(0.11f, 0.11f, 0.10f, 0.98f), panelSprite).type = Image.Type.Sliced;

            TMP_InputField input = parent.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = parent;

            RectTransform textRect = CreateRect("Text", parent);
            Stretch(textRect, Vector2.zero, Vector2.one, new Vector2(8f, 6f), new Vector2(-8f, -6f));

            TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = 14f;
            text.color = new Color(0.94f, 0.89f, 0.77f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform placeholderRect = CreateRect("Placeholder", parent);
            Stretch(placeholderRect, Vector2.zero, Vector2.one, new Vector2(8f, 6f), new Vector2(-8f, -6f));

            TextMeshProUGUI placeholder = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
            placeholder.font = fontAsset;
            placeholder.fontSize = 14f;
            placeholder.color = new Color(0.58f, 0.56f, 0.50f, 1f);
            placeholder.text = "Enter squad name";
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;

            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = defaultValue;
            input.caretColor = new Color(0.96f, 0.90f, 0.74f, 1f);
            input.selectionColor = new Color(0.46f, 0.34f, 0.18f, 0.5f);

            return input;
        }

        private Button CreateButton(RectTransform rect, string label, float fontSize)
        {
            Image image = AddImage(rect, new Color(0.24f, 0.21f, 0.17f, 1f), slotSprite);
            image.type = Image.Type.Sliced;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.89f, 0.81f, 0.60f, 1f);
            colors.pressedColor = new Color(0.76f, 0.59f, 0.31f, 1f);
            colors.selectedColor = new Color(0.81f, 0.62f, 0.33f, 1f);
            colors.disabledColor = new Color(0.46f, 0.43f, 0.38f, 0.7f);
            button.colors = colors;

            TMP_Text text = CreateText(rect, label, fontSize, new Color(0.92f, 0.87f, 0.74f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return button;
        }

        private void RebuildMemberList()
        {
            memberViews.Clear();
            if (memberListContent == null)
            {
                return;
            }

            ClearChildren(memberListContent);

            for (int i = 0; i < members.Count; i++)
            {
                int captured = i;
                MemberView view = BuildMemberView(memberListContent, i, members[i]);
                view.Button.onClick.AddListener(() => SetSelectedIndex(captured));
                memberViews.Add(view);
            }

            UpdateMemberListVisuals();
        }

        private MemberView BuildMemberView(RectTransform parent, int order, string memberName)
        {
            MemberView view = new MemberView();
            view.Root = CreateRect("Member_" + order, parent);
            LayoutElement element = view.Root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 52f;

            view.Background = AddImage(view.Root, new Color(0.20f, 0.19f, 0.17f, 0.98f), slotSprite);
            view.Background.type = Image.Type.Sliced;

            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;

            TMP_Text indexText = CreateText(view.Root, (order + 1).ToString("00"), 14f, new Color(0.80f, 0.75f, 0.63f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(indexText.rectTransform, new Vector2(0f, 0f), new Vector2(0.18f, 1f), new Vector2(8f, 0f), Vector2.zero);

            view.Name = CreateText(view.Root, memberName, 16f, new Color(0.91f, 0.87f, 0.75f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(view.Name.rectTransform, new Vector2(0.18f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), new Vector2(-8f, 0f));

            return view;
        }

        private void MoveSelected(int direction)
        {
            if (selectedIndex < 0 || selectedIndex >= members.Count)
            {
                return;
            }

            int target = selectedIndex + direction;
            if (target < 0 || target >= members.Count)
            {
                return;
            }

            string current = members[selectedIndex];
            members[selectedIndex] = members[target];
            members[target] = current;
            selectedIndex = target;

            RebuildMemberList();
            UpdateSelectionDetails();
            MemberOrderChanged?.Invoke(members);

            if (statusText != null)
            {
                statusText.text = "Reordered " + members[selectedIndex] + " to slot " + (selectedIndex + 1) + ".";
            }
        }

        private void ShiftOption(string[] options, TMP_Text valueText, int direction)
        {
            if (options == null || options.Length == 0 || valueText == null)
            {
                return;
            }

            int index = IndexOfOption(options, valueText.text);
            index += direction;
            if (index < 0)
            {
                index = options.Length - 1;
            }
            else if (index >= options.Length)
            {
                index = 0;
            }

            valueText.text = options[index];
        }

        private void ApplyAssignment()
        {
            if (selectedIndex < 0 || selectedIndex >= members.Count)
            {
                return;
            }

            string member = members[selectedIndex];
            if (!assignmentByMember.TryGetValue(member, out MemberAssignment assignment))
            {
                assignment = new MemberAssignment();
                assignmentByMember[member] = assignment;
            }

            assignment.RoleIndex = IndexOfOption(roleOptions, roleValueText != null ? roleValueText.text : string.Empty);
            assignment.LoadoutIndex = IndexOfOption(loadoutOptions, loadoutValueText != null ? loadoutValueText.text : string.Empty);
            assignment.PositionIndex = IndexOfOption(positionOptions, positionValueText != null ? positionValueText.text : string.Empty);

            if (statusText != null)
            {
                statusText.text = member
                    + " -> " + roleOptions[assignment.RoleIndex]
                    + " / " + loadoutOptions[assignment.LoadoutIndex]
                    + " / " + positionOptions[assignment.PositionIndex];
            }
        }

        private void EmitSquadNameChange()
        {
            if (squadNameInput == null)
            {
                return;
            }

            string trimmed = string.IsNullOrWhiteSpace(squadNameInput.text)
                ? string.Empty
                : squadNameInput.text.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            SquadNameChanged?.Invoke(trimmed);
            if (statusText != null)
            {
                statusText.text = "Renamed squad to " + trimmed + ".";
            }
        }

        private void LoadAssignmentIntoControls()
        {
            if (selectedIndex < 0 || selectedIndex >= members.Count)
            {
                return;
            }

            string member = members[selectedIndex];
            if (!assignmentByMember.TryGetValue(member, out MemberAssignment assignment))
            {
                assignment = new MemberAssignment();
                assignmentByMember[member] = assignment;
            }

            if (roleValueText != null)
            {
                roleValueText.text = roleOptions[Mathf.Clamp(assignment.RoleIndex, 0, roleOptions.Length - 1)];
            }

            if (loadoutValueText != null)
            {
                loadoutValueText.text = loadoutOptions[Mathf.Clamp(assignment.LoadoutIndex, 0, loadoutOptions.Length - 1)];
            }

            if (positionValueText != null)
            {
                positionValueText.text = positionOptions[Mathf.Clamp(assignment.PositionIndex, 0, positionOptions.Length - 1)];
            }
        }

        private void UpdateSelectionDetails()
        {
            if (selectedMemberText == null)
            {
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= members.Count)
            {
                selectedMemberText.text = "Selected: -";
                return;
            }

            selectedMemberText.text = "Selected: " + members[selectedIndex];
        }

        private void UpdateMemberListVisuals()
        {
            for (int i = 0; i < memberViews.Count; i++)
            {
                bool selected = i == selectedIndex;
                if (memberViews[i].Background != null)
                {
                    memberViews[i].Background.color = selected
                        ? new Color(0.46f, 0.33f, 0.17f, 1f)
                        : new Color(0.22f, 0.20f, 0.17f, 0.98f);
                }

                if (memberViews[i].Name != null)
                {
                    memberViews[i].Name.color = selected
                        ? new Color(1f, 0.94f, 0.79f, 1f)
                        : new Color(0.90f, 0.86f, 0.74f, 1f);
                }
            }

            if (moveUpButton != null)
            {
                moveUpButton.interactable = selectedIndex > 0;
            }

            if (moveDownButton != null)
            {
                moveDownButton.interactable = selectedIndex >= 0 && selectedIndex < members.Count - 1;
            }
        }

        private static int IndexOfOption(string[] options, string current)
        {
            if (options == null || options.Length == 0)
            {
                return 0;
            }

            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i], current, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        private TMP_Text CreateText(
            RectTransform parent,
            string value,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect("Text", parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = fontAsset;
            tmp.text = value;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            return image;
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
