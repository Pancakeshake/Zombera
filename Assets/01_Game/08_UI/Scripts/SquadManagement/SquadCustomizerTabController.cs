#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class SquadCustomizerTabController : MonoBehaviour
    {
        private readonly Dictionary<string, MemberAssignment> _assignmentByMember = new(StringComparer.Ordinal);

        private readonly string[] _loadoutOptions =
            { "Balanced Kit", "Rifle Kit", "Shotgun Kit", "Support Kit", "Stealth Kit" };

        private readonly List<string> _members = new();
        private readonly List<MemberView> _memberViews = new();

        private readonly string[] _positionOptions =
            { "Frontline", "Flank Left", "Flank Right", "Rear Guard", "Reserve" };

        private readonly string[] _roleOptions = { "Scout", "Medic", "Bruiser", "Technician", "Leader" };
        private TMP_FontAsset _fontAsset;

        private RectTransform _hostRoot;
        private TMP_Text _loadoutValueText;
        private RectTransform _memberListContent;
        private Button _moveDownButton;

        private Button _moveUpButton;
        private Sprite _panelSprite;
        private TMP_Text _positionValueText;
        private TMP_Text _roleValueText;
        private int _selectedIndex = -1;
        private TMP_Text _selectedMemberText;
        private Sprite _slotSprite;

        private TMP_InputField _squadNameInput;
        private TMP_Text _statusText;

        public event Action<string> SquadNameChanged;
        public event Action<IReadOnlyList<string>> MemberOrderChanged;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            _hostRoot = host;
            _fontAsset = font;
            _panelSprite = panelBackground;
            _slotSprite = slotBackground;

            ClearChildren(_hostRoot);

            var header = CreateRect("Header", _hostRoot);
            Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-6f, -76f));
            AddImage(header, new Color(0.20f, 0.20f, 0.18f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            var title = CreateText(header, "SQUAD CUSTOMISER", 24f, new Color(0.95f, 0.91f, 0.78f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.32f, 1f), new Vector2(10f, 0f),
                Vector2.zero);

            var nameRow = CreateRect("NameRow", header);
            Stretch(nameRow, new Vector2(0.34f, 0f), new Vector2(1f, 1f), new Vector2(0f, 12f),
                new Vector2(-10f, -12f));
            BuildSquadNameRow(nameRow);

            var body = CreateRect("Body", _hostRoot);
            Stretch(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -82f));
            AddImage(body, new Color(0.14f, 0.14f, 0.13f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            BuildBody(body);
        }

        public void SetSquadName(string squadName)
        {
            if (_squadNameInput == null) return;

            _squadNameInput.text = string.IsNullOrWhiteSpace(squadName) ? string.Empty : squadName.Trim();
        }

        public void SetMembers(IReadOnlyList<string> orderedNames)
        {
            _members.Clear();
            _assignmentByMember.Clear();

            if (orderedNames != null)
            {
                for (var i = 0; i < orderedNames.Count; i++)
                {
                    var memberName = string.IsNullOrWhiteSpace(orderedNames[i])
                        ? "Survivor " + (i + 1)
                        : orderedNames[i].Trim();

                    _members.Add(memberName);
                    _assignmentByMember[memberName] = new MemberAssignment
                        { RoleIndex = 0, LoadoutIndex = 0, PositionIndex = 0 };
                }
            }

            RebuildMemberList();

            if (_members.Count > 0)
                SetSelectedIndex(0);
            else
            {
                _selectedIndex = -1;
                UpdateSelectionDetails();
            }
        }

        public void SetSelectedIndex(int index)
        {
            if (_members.Count == 0)
            {
                _selectedIndex = -1;
                UpdateSelectionDetails();
                return;
            }

            _selectedIndex = Mathf.Clamp(index, 0, _members.Count - 1);
            UpdateMemberListVisuals();
            LoadAssignmentIntoControls();
            UpdateSelectionDetails();
        }

        private sealed class MemberView
        {
            public Image Background;
            public Button Button;
            public TMP_Text Name;
            public RectTransform Root;
        }

        private sealed class MemberAssignment
        {
            public int LoadoutIndex;
            public int PositionIndex;
            public int RoleIndex;
        }
    }
}
