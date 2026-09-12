#region

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

#endregion

namespace Zombera.UI
{
#pragma warning disable S101 // HUD is an intentional acronym in this project's naming convention
    public sealed partial class WorldHUDController
    {
        private enum BuildHudCommandType
        {
            None,
            PagePrevious,
            PageNext,
            SetModePlace,
            SetModeDelete,
            SetModeEdit,
            SetModeReplace,
            SelectVisibleItem,
            CancelBuild
        }


        private bool HandleBuildPageHotkeys(Keyboard keyboard)
        {
            if (!_showingBuildItems || !_bottomBarExpanded || ActiveTab != TabId.None) return false;
            if (IsBuildSearchInputFocused()) return false;

            if (keyboard.leftBracketKey.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.PagePrevious);

            if (keyboard.rightBracketKey.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.PageNext);

            return false;
        }


        private bool TryStepBuildItemPage(int directionSign)
        {
            if (directionSign == 0) return false;

            if (_buildFilterDirty)
            {
                RebuildFilteredBuildSourceIndices();
                _buildFilterDirty = false;
            }

            var count = _filteredBuildSourceIndices.Count;
            if (count <= BuildHudItemsPerPage) return false;

            var maxOffset = Mathf.Max(0, count - BuildHudItemsPerPage);
            var step = BuildHudItemsPerPage;
            var newOffset = Mathf.Clamp(_buildItemPageOffset + directionSign * step, 0, maxOffset);
            if (newOffset == _buildItemPageOffset) return false;

            var preferredVisibleIndex = _explicitBuildItemBoxIndex is >= 0 and <= 8
                ? _explicitBuildItemBoxIndex
                : 0;

            BuildLog($"Paging build items: offset {_buildItemPageOffset} -> {newOffset} (count={count})");
            _buildItemPageOffset = newOffset;
            _explicitBuildItemBoxIndex = -2;
            _buildVisualsDirty = true;
            _buildPageIndicatorDirty = true;
            EnsureBuildUiFresh();
            RefreshBottomBuildItemHighlights();

            var visibleCount = Mathf.Min(BuildHudItemsPerPage, count - _buildItemPageOffset);
            if (visibleCount <= 0) return true;

            var clampedVisibleIndex = Mathf.Clamp(preferredVisibleIndex, 0, visibleCount - 1);
            if (!TryActivateBottomBuildItem(clampedVisibleIndex))
                BuildLog($"Page selection activation failed at visibleIndex={clampedVisibleIndex}");

            return true;
        }


        private bool HandleBottomBuildHotkeys(Keyboard keyboard)
        {
            if (keyboard == null || !_showingBuildItems) return false;
            if (IsBuildSearchInputFocused()) return false;

            if (keyboard.digit1Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 0);
            if (keyboard.digit2Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 1);
            if (keyboard.digit3Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 2);
            if (keyboard.digit4Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 3);
            if (keyboard.digit5Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 4);
            if (keyboard.digit6Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 5);
            if (keyboard.digit7Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 6);
            if (keyboard.digit8Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 7);
            if (keyboard.digit9Key.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, 8);
            if (keyboard.escapeKey.wasPressedThisFrame)
                return TryDispatchBuildCommand(BuildHudCommandType.CancelBuild);

            return false;
        }


        private bool IsBuildSearchInputFocused()
        {
            return _buildSearchInput != null && _buildSearchInput.isFocused;
        }


        private void HandleBuildCommandButtonClicked(int commandIndex)
        {
            if (commandIndex < 0 || commandIndex >= BuildCommandLabels.Length) return;
            if (!_showingBuildItems) return;

            var command = ResolveBuildCommandFromButtonIndex(commandIndex);
            var handled = TryDispatchBuildCommand(command);

            BuildLog($"Build command clicked index={commandIndex} label={BuildCommandLabels[commandIndex]} handled={handled}");
            UpdateBuildCommandButtonVisuals();
        }


        private bool TrySetEasyBuildMode(int commandIndex)
        {
            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            if (_easyBuildRadialMenuInputBridge == null) return false;

            return BuildCommandModeHelper.TrySetEasyBuildMode(_easyBuildRadialMenuInputBridge, commandIndex);
        }


        private bool HandleBottomSquadHotkeys(Keyboard keyboard)
        {
            if (keyboard == null || _showingBuildItems) return false;
            if (portraitStrip == null || !portraitStrip.isActiveAndEnabled || !portraitStrip.gameObject.activeInHierarchy)
                return false;

            if (keyboard.digit1Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(0);
            if (keyboard.digit2Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(1);
            if (keyboard.digit3Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(2);
            if (keyboard.digit4Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(3);
            if (keyboard.digit5Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(4);
            if (keyboard.digit6Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(5);
            if (keyboard.digit7Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(6);
            if (keyboard.digit8Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(7);
            if (keyboard.digit9Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(8);
            if (keyboard.digit0Key.wasPressedThisFrame)
                return portraitStrip.TrySelectVisibleSlotByIndex(9);

            return false;
        }


        private void HandleBottomBuildItemClicked(int itemIndex)
        {
            if (itemIndex < 0) return;

            BuildLog($"Build tile clicked: visibleIndex={itemIndex}");
            _ = TryDispatchBuildCommand(BuildHudCommandType.SelectVisibleItem, itemIndex);
        }


        private bool TryActivateBottomBuildItem(int itemIndex)
        {
            var activated = false;
            BuildLog($"Activate build tile requested: visibleIndex={itemIndex}");

            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);

            if (itemIndex == 9)
            {
                if (_easyBuildRadialMenuInputBridge != null)
                    activated = _easyBuildRadialMenuInputBridge.TryCancelBuildUi();

                // Ensure legacy ghost is also cleared even if EasyBuild handles the cancel
                if (EnsureLegacyBuildPlacementController())
                {
                    _legacyBuildPlacementController.ExitBuildMode();
                    activated = true;
                }

                if (!activated) return false;

                _explicitBuildItemBoxIndex = -1;
                _showingBuildItems = IsBuildModeActive();
                BuildLog("Build cancel triggered from tile 9 / Esc");
                ApplyBottomSquadsExpandedVisualState();
                UpdateLayoutForTabState(ActiveTab != TabId.None);
                RefreshBottomBuildItemHighlights();
                return true;
            }

            var sourceItemIndex = ResolveBuildSourceItemIndex(itemIndex);
            if (sourceItemIndex < 0)
            {
                BuildLog($"Activate build tile aborted: no source item for visibleIndex={itemIndex}");
                return false;
            }

            BuildLog($"Resolved build tile: visibleIndex={itemIndex} -> sourceIndex={sourceItemIndex}", true);

            if (_easyBuildRadialMenuInputBridge != null)
                activated = _easyBuildRadialMenuInputBridge.TryActivateHudItem(sourceItemIndex);

            if (!activated && EnsureLegacyBuildPlacementController())
                activated = _legacyBuildPlacementController.TryActivateHudItem(sourceItemIndex);

            if (!activated)
            {
                BuildLog($"Activate build tile failed: sourceIndex={sourceItemIndex}");
                return false;
            }

            _explicitBuildItemBoxIndex = itemIndex;
            _showingBuildItems = IsBuildModeActive();
            BuildLog($"Build tile activated: visibleIndex={itemIndex}, sourceIndex={sourceItemIndex}");
            ApplyBottomSquadsExpandedVisualState();
            UpdateLayoutForTabState(ActiveTab != TabId.None);
            RefreshBottomBuildItemHighlights();
            return true;
        }


        private static BuildHudCommandType ResolveBuildCommandFromButtonIndex(int commandIndex)
        {
            return commandIndex switch
            {
                0 => BuildHudCommandType.PagePrevious,
                1 => BuildHudCommandType.PageNext,
                2 => BuildHudCommandType.SetModePlace,
                3 => BuildHudCommandType.SetModeDelete,
                4 => BuildHudCommandType.SetModeEdit,
                5 => BuildHudCommandType.SetModeReplace,
                _ => BuildHudCommandType.None
            };
        }


        private bool TryDispatchBuildCommand(BuildHudCommandType command, int argument = -1)
        {
            return command switch
            {
                BuildHudCommandType.PagePrevious => TryStepBuildItemPage(-1),
                BuildHudCommandType.PageNext => TryStepBuildItemPage(1),
                BuildHudCommandType.SetModePlace => TrySetEasyBuildMode(2),
                BuildHudCommandType.SetModeDelete => TrySetEasyBuildMode(3),
                BuildHudCommandType.SetModeEdit => TrySetEasyBuildMode(4),
                BuildHudCommandType.SetModeReplace => TrySetEasyBuildMode(5),
                BuildHudCommandType.SelectVisibleItem => TryActivateBottomBuildItem(argument),
                BuildHudCommandType.CancelBuild => TryActivateBottomBuildItem(9),
                _ => false
            };
        }


        private static class BuildCommandModeHelper
        {
            internal static bool TrySetEasyBuildMode(EasyBuildRadialMenuInputBridge bridge, int commandIndex)
            {
                if (bridge == null) return false;

                return commandIndex switch
                {
                    2 => bridge.TrySetPlacementMode(),
                    3 => bridge.TrySetDestructionMode(),
                    4 => bridge.TrySetEditionMode(),
                    5 => bridge.TrySetReplacementMode(),
                    _ => false
                };
            }


            internal static void UpdateBuildCommandButtonVisuals(
                IReadOnlyList<BuildCommandButtonView> commandButtons,
                int activeModeCommandIndex)
            {
                if (commandButtons == null) return;

                for (var i = 0; i < commandButtons.Count; i++)
                    ApplyCommandButtonColors(commandButtons[i], activeModeCommandIndex);
            }

            private static void ApplyCommandButtonColors(BuildCommandButtonView command, int activeModeCommandIndex)
            {
                if (command == null || command.Button == null) return;

                var isActive = command.Index >= 2 && command.Index == activeModeCommandIndex;

                if (command.Background != null)
                    command.Background.color = isActive
                        ? new Color(0.20f, 0.50f, 0.34f, 0.98f)
                        : command.DefaultBackgroundColor;

                if (command.Label != null)
                    command.Label.color = isActive
                        ? new Color(0.95f, 0.98f, 0.96f, 1f)
                        : new Color(0.93f, 0.96f, 0.98f, 0.98f);
            }


            internal static int ResolveActiveBuildModeCommandIndex(EasyBuildRadialMenuInputBridge bridge)
            {
                if (bridge == null) return -1;

                var mode = bridge.GetCurrentControllerModeName();
                if (string.IsNullOrWhiteSpace(mode)) return -1;

                return ResolveModeIndex(mode);
            }

            private static int ResolveModeIndex(string mode)
            {
                if (MatchesToken(mode, "place")) return 2;
                if (MatchesToken(mode, "destroy", "demol", "remov")) return 3;
                if (MatchesToken(mode, "edit", "adjust", "select")) return 4;
                if (MatchesToken(mode, "replac")) return 5;
                return -1;
            }

            private static bool MatchesToken(string mode, params string[] tokens)
            {
                for (var i = 0; i < tokens.Length; i++)
                    if (mode.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                return false;
            }
        }
    }
}
