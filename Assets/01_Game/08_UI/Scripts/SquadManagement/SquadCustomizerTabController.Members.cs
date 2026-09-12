#region

using TMPro;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class SquadCustomizerTabController
    {
        private void RebuildMemberList()
        {
            _memberViews.Clear();
            if (_memberListContent == null) return;

            ClearChildren(_memberListContent);

            for (var i = 0; i < _members.Count; i++)
            {
                var captured = i;
                var view = BuildMemberView(_memberListContent, i, _members[i]);
                view.Button.onClick.AddListener(() => SetSelectedIndex(captured));
                _memberViews.Add(view);
            }

            UpdateMemberListVisuals();
        }

        private MemberView BuildMemberView(RectTransform parent, int order, string memberName)
        {
            var view = new MemberView
            {
                Root = CreateRect("Member_" + order, parent)
            };
            var element = view.Root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 52f;

            view.Background = AddImage(view.Root, new Color(0.20f, 0.19f, 0.17f, 0.98f), _slotSprite);
            view.Background.type = Image.Type.Sliced;

            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;

            var indexText = CreateText(view.Root, (order + 1).ToString("00"), 14f, new Color(0.80f, 0.75f, 0.63f, 1f),
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(indexText.rectTransform, new Vector2(0f, 0f), new Vector2(0.18f, 1f), new Vector2(8f, 0f),
                Vector2.zero);

            view.Name = CreateText(view.Root, memberName, 16f, new Color(0.91f, 0.87f, 0.75f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(view.Name.rectTransform, new Vector2(0.18f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f),
                new Vector2(-8f, 0f));

            return view;
        }

        private void MoveSelected(int direction)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _members.Count) return;

            var target = _selectedIndex + direction;
            if (target < 0 || target >= _members.Count) return;

            (_members[_selectedIndex], _members[target]) = (_members[target], _members[_selectedIndex]);
            _selectedIndex = target;

            RebuildMemberList();
            UpdateSelectionDetails();
            MemberOrderChanged?.Invoke(_members);

            if (_statusText != null)
                _statusText.text = "Reordered " + _members[_selectedIndex] + " to slot " + (_selectedIndex + 1) + ".";
        }

        private void UpdateSelectionDetails()
        {
            if (_selectedMemberText == null) return;

            if (_selectedIndex < 0 || _selectedIndex >= _members.Count)
            {
                _selectedMemberText.text = "Selected: -";
                return;
            }

            _selectedMemberText.text = "Selected: " + _members[_selectedIndex];
        }

        private void UpdateMemberListVisuals()
        {
            for (var i = 0; i < _memberViews.Count; i++)
            {
                var selected = i == _selectedIndex;
                if (_memberViews[i].Background != null)
                {
                    _memberViews[i].Background.color = selected
                        ? new Color(0.46f, 0.33f, 0.17f, 1f)
                        : new Color(0.22f, 0.20f, 0.17f, 0.98f);
                }

                if (_memberViews[i].Name != null)
                {
                    _memberViews[i].Name.color = selected
                        ? new Color(1f, 0.94f, 0.79f, 1f)
                        : new Color(0.90f, 0.86f, 0.74f, 1f);
                }
            }

            if (_moveUpButton != null) _moveUpButton.interactable = _selectedIndex > 0;

            if (_moveDownButton != null)
                _moveDownButton.interactable = _selectedIndex >= 0 && _selectedIndex < _members.Count - 1;
        }
    }
}
