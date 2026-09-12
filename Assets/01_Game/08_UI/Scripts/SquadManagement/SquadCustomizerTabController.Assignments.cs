#region

using System;
using TMPro;
using UnityEngine;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class SquadCustomizerTabController
    {
        private static void ShiftOption(string[] options, TMP_Text valueText, int direction)
        {
            if (options == null || options.Length == 0 || valueText == null) return;

            var index = IndexOfOption(options, valueText.text);
            index += direction;
            if (index < 0)
                index = options.Length - 1;
            else if (index >= options.Length) index = 0;

            valueText.text = options[index];
        }

        private void ApplyAssignment()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _members.Count) return;

            var member = _members[_selectedIndex];
            if (!_assignmentByMember.TryGetValue(member, out var assignment))
            {
                assignment = new MemberAssignment { RoleIndex = 0, LoadoutIndex = 0, PositionIndex = 0 };
                _assignmentByMember[member] = assignment;
            }

            assignment.RoleIndex =
                IndexOfOption(_roleOptions, _roleValueText != null ? _roleValueText.text : string.Empty);
            assignment.LoadoutIndex = IndexOfOption(_loadoutOptions,
                _loadoutValueText != null ? _loadoutValueText.text : string.Empty);
            assignment.PositionIndex = IndexOfOption(_positionOptions,
                _positionValueText != null ? _positionValueText.text : string.Empty);

            if (_statusText != null)
            {
                _statusText.text = member
                                   + " -> " + _roleOptions[assignment.RoleIndex]
                                   + " / " + _loadoutOptions[assignment.LoadoutIndex]
                                   + " / " + _positionOptions[assignment.PositionIndex];
            }
        }

        private void EmitSquadNameChange()
        {
            if (_squadNameInput == null) return;

            var trimmed = string.IsNullOrWhiteSpace(_squadNameInput.text)
                ? string.Empty
                : _squadNameInput.text.Trim();

            if (string.IsNullOrEmpty(trimmed)) return;

            SquadNameChanged?.Invoke(trimmed);
            if (_statusText != null) _statusText.text = "Renamed squad to " + trimmed + ".";
        }

        private void LoadAssignmentIntoControls()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _members.Count) return;

            var member = _members[_selectedIndex];
            if (!_assignmentByMember.TryGetValue(member, out var assignment))
            {
                assignment = new MemberAssignment { RoleIndex = 0, LoadoutIndex = 0, PositionIndex = 0 };
                _assignmentByMember[member] = assignment;
            }

            if (_roleValueText != null)
                _roleValueText.text = _roleOptions[Mathf.Clamp(assignment.RoleIndex, 0, _roleOptions.Length - 1)];

            if (_loadoutValueText != null)
            {
                _loadoutValueText.text =
                    _loadoutOptions[Mathf.Clamp(assignment.LoadoutIndex, 0, _loadoutOptions.Length - 1)];
            }

            if (_positionValueText != null)
            {
                _positionValueText.text =
                    _positionOptions[Mathf.Clamp(assignment.PositionIndex, 0, _positionOptions.Length - 1)];
            }
        }

        private static int IndexOfOption(string[] options, string current)
        {
            if (options == null || options.Length == 0) return 0;

            for (var i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i], current, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }
    }
}
