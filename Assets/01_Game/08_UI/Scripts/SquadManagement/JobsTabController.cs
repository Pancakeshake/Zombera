#region

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Characters.Work;
using Zombera.Systems;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed class JobsTabController : MonoBehaviour
    {
        private sealed class JobRowDefinition
        {
            public WorkJobType? JobType;
            public string Title;
            public string Description;
            public string IconGlyph;
            public bool IsImplemented;
        }

        private static readonly JobRowDefinition[] JobRows =
        {
            new() { JobType = WorkJobType.Looting, Title = "LOOTING", Description = "Search containers and gather loose supplies.", IconGlyph = "L", IsImplemented = true },
            new() { JobType = WorkJobType.Mining, Title = "DIGGING / MINING", Description = "Extract stone, ore, and buried resources.", IconGlyph = "M", IsImplemented = false },
            new() { JobType = WorkJobType.Building, Title = "BUILDING", Description = "Construct and repair structures.", IconGlyph = "B", IsImplemented = true },
            new() { JobType = WorkJobType.Guarding, Title = "GUARDING", Description = "Hold posts and defend assigned areas.", IconGlyph = "G", IsImplemented = false },
            new() { JobType = WorkJobType.Cooking, Title = "COOKING", Description = "Prepare meals at kitchen stations.", IconGlyph = "C", IsImplemented = false },
            new() { JobType = WorkJobType.Crafting, Title = "CRAFTING", Description = "Fabricate gear, parts, and components.", IconGlyph = "F", IsImplemented = true },
            new() { JobType = null, Title = "FARMING", Description = "Tend crops and harvest produce.", IconGlyph = "A", IsImplemented = false },
            new() { JobType = null, Title = "HAULING", Description = "Move items between storage and work sites.", IconGlyph = "H", IsImplemented = false },
            new() { JobType = null, Title = "RESEARCH", Description = "Advance tech at research benches.", IconGlyph = "R", IsImplemented = false },
            new() { JobType = null, Title = "MEDICAL", Description = "Treat wounded squad members.", IconGlyph = "+", IsImplemented = false }
        };

        private static readonly Color PanelDark = new(0.13f, 0.13f, 0.12f, 0.98f);
        private static readonly Color PanelMid = new(0.16f, 0.16f, 0.15f, 0.98f);
        private static readonly Color AccentGreen = new(0.36f, 0.58f, 0.30f, 1f);
        private static readonly Color AccentGreenBright = new(0.42f, 0.72f, 0.34f, 1f);
        internal static readonly Color TextPrimary = new(0.94f, 0.90f, 0.78f, 1f);
        internal static readonly Color TextMuted = new(0.68f, 0.65f, 0.58f, 1f);
        private static readonly Color PriorityInactive = new(0.20f, 0.20f, 0.19f, 0.98f);
        private static readonly Color PriorityActive = new(0.34f, 0.58f, 0.28f, 1f);
        internal static readonly Color SelectedBorder = new(0.38f, 0.68f, 0.32f, 1f);

        private readonly List<SquadMember> _memberBuffer = new();
        private readonly List<JobsTabMemberCardBuilder.MemberCardView> _memberCards = new();

        private WorkManager _workManager;
        private TMP_FontAsset _fontAsset;
        private RectTransform _hostRoot;
        private Sprite _panelSprite;
        private Sprite _slotSprite;
        private int _selectedMemberIndex = -1;
        private TMP_Text _survivorCountText;
        private RectTransform _memberListContent;
        private RectTransform _jobListContent;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            _hostRoot = host;
            _fontAsset = font;
            _panelSprite = panelBackground;
            _slotSprite = slotBackground;

            JobsTabUIPrimitives.ClearChildren(_hostRoot);
            BuildLayout();
            EnsureRuntimeReferences();
            RefreshFromRuntime();
        }

        public void RefreshFromRuntime()
        {
            EnsureRuntimeReferences();
            _workManager?.RefreshRosterFromSquad();
            RebuildMemberList();
            RebuildJobMatrix();
            RefreshSurvivorCount();
        }

        private void OnEnable()
        {
            EnsureRuntimeReferences();
            if (_workManager != null) _workManager.WorkStateChanged += HandleWorkStateChanged;
        }

        private void OnDisable()
        {
            if (_workManager != null) _workManager.WorkStateChanged -= HandleWorkStateChanged;
        }

        private void HandleWorkStateChanged()
        {
            RefreshMemberStatuses();
        }

        private void BuildLayout()
        {
            var body = JobsTabUIPrimitives.CreateRect("Body", _hostRoot);
            JobsTabUIPrimitives.Stretch(body, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 6f;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = true;

            BuildSurvivorColumn(body);
            BuildJobsColumn(body);
            BuildInfoColumn(body);
        }

        private void BuildSurvivorColumn(RectTransform parent)
        {
            var column = JobsTabUIPrimitives.CreatePanelColumn(parent, 230f, PanelMid, _panelSprite);
            var headerRow = JobsTabUIPrimitives.CreateRect("HeaderRow", column);
            JobsTabUIPrimitives.Stretch(headerRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -44f));

            JobsTabUIPrimitives.CreateText(headerRow, "SURVIVORS", 20f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, _fontAsset);
            _survivorCountText = JobsTabUIPrimitives.CreateText(headerRow, "0 / 0", 14f, TextMuted, FontStyles.Normal, TextAlignmentOptions.MidlineRight, _fontAsset);
            JobsTabUIPrimitives.Stretch(_survivorCountText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            JobsTabUIPrimitives.BuildVerticalScroll(column, new Vector2(8f, 8f), new Vector2(-8f, -52f), out _memberListContent);

            var footer = JobsTabUIPrimitives.CreateRect("Footer", column);
            JobsTabUIPrimitives.Stretch(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), new Vector2(-8f, 48f));
            JobsTabUIPrimitives.CreateActionButton(footer, "MANAGE GROUPS", false, AccentGreen * 0.55f, _panelSprite, null);
        }

        private void BuildJobsColumn(RectTransform parent)
        {
            var column = JobsTabUIPrimitives.CreatePanelColumn(parent, 0f, PanelDark, _panelSprite, flexibleWidth: true);

            var headerRow = JobsTabUIPrimitives.CreateRect("HeaderRow", column);
            JobsTabUIPrimitives.Stretch(headerRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -10f), new Vector2(-12f, -44f));

            JobsTabUIPrimitives.CreateText(headerRow, "JOBS", 20f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, _fontAsset);
            JobsTabUIPrimitives.CreateText(headerRow, "PRIORITY (1 = HIGHEST)", 12f, TextMuted, FontStyles.Normal, TextAlignmentOptions.MidlineRight, _fontAsset);

            JobsTabUIPrimitives.BuildVerticalScroll(column, new Vector2(8f, 8f), new Vector2(-8f, -52f), out _jobListContent);
        }

        private void BuildInfoColumn(RectTransform parent)
        {
            var column = JobsTabUIPrimitives.CreatePanelColumn(parent, 250f, PanelMid, _panelSprite);

            var howTitle = JobsTabUIPrimitives.CreateText(column, "HOW IT WORKS", 16f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.TopLeft, _fontAsset);
            JobsTabUIPrimitives.Stretch(howTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -12f), new Vector2(-12f, -40f));

            var howBody = JobsTabUIPrimitives.CreateText(column,
                "Each survivor can be assigned a priority from 1 (highest) to 9 (lowest) for every job type. " +
                "Survivors will work on the highest priority available job. Jobs marked with X are ignored.",
                12f, TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft, _fontAsset);
            howBody.textWrappingMode = TextWrappingModes.Normal;
            JobsTabUIPrimitives.Stretch(howBody.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -44f), new Vector2(-12f, -170f));

            var legendTitle = JobsTabUIPrimitives.CreateText(column, "JOB STATUS", 16f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.TopLeft, _fontAsset);
            JobsTabUIPrimitives.Stretch(legendTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -178f), new Vector2(-12f, -206f));

            BuildLegendRow(column, PriorityActive, "Will do", -214f);
            BuildLegendRow(column, PriorityInactive, "Lower priority", -244f);
            BuildLegendRow(column, new Color(0.18f, 0.18f, 0.17f, 1f), "Will not do (X)", -274f);
            BuildLegendRow(column, new Color(0.14f, 0.14f, 0.13f, 1f), "Not capable (-)", -304f, labelOverride: "-");

            var presetTitle = JobsTabUIPrimitives.CreateText(column, "PRESETS", 14f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.TopLeft, _fontAsset);
            JobsTabUIPrimitives.Stretch(presetTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -336f), new Vector2(-12f, -358f));
            BuildPresetButton(column, "BUILDER", WorkPriorityPreset.Builder, -364f);
            BuildPresetButton(column, "CRAFTER", WorkPriorityPreset.Crafter, -396f);
            BuildPresetButton(column, "SCAVENGER", WorkPriorityPreset.Scavenger, -428f);
            BuildPresetButton(column, "GUARD", WorkPriorityPreset.Guard, -460f);
            BuildPresetButton(column, "BALANCED", WorkPriorityPreset.Balanced, -492f);

            var resetButton = JobsTabUIPrimitives.CreateRect("ResetButton", column);
            JobsTabUIPrimitives.Stretch(resetButton, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 64f), new Vector2(-12f, 108f));
            JobsTabUIPrimitives.CreateActionButton(resetButton, "RESET ALL JOBS", true, new Color(0.22f, 0.22f, 0.20f, 1f), _panelSprite, OnResetSelectedMember);

            var applyButton = JobsTabUIPrimitives.CreateRect("ApplyButton", column);
            JobsTabUIPrimitives.Stretch(applyButton, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 12f), new Vector2(-12f, 56f));
            JobsTabUIPrimitives.CreateActionButton(applyButton, "APPLY TO ALL", true, AccentGreenBright, _panelSprite, OnApplyToAll);
        }

        private void BuildPresetButton(RectTransform parent, string label, WorkPriorityPreset preset, float topOffset)
        {
            var buttonRoot = JobsTabUIPrimitives.CreateRect("Preset_" + label, parent);
            JobsTabUIPrimitives.Stretch(buttonRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, topOffset - 28f), new Vector2(-12f, topOffset));
            JobsTabUIPrimitives.CreateActionButton(buttonRoot, label, true, new Color(0.20f, 0.24f, 0.19f, 1f), _panelSprite, () => ApplyPreset(preset));
        }

        private void ApplyPreset(WorkPriorityPreset preset)
        {
            if (_selectedMemberIndex < 0 || _selectedMemberIndex >= _memberBuffer.Count) return;

            var profile = _workManager?.GetOrCreateProfile(_memberBuffer[_selectedMemberIndex]);
            if (profile == null) return;

            profile.ApplyPreset(preset);
            RebuildJobMatrix();
        }

        private void BuildLegendRow(RectTransform parent, Color swatchColor, string label, float topOffset, string labelOverride = null)
        {
            var row = JobsTabUIPrimitives.CreateRect("Legend_" + label, parent);
            JobsTabUIPrimitives.Stretch(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, topOffset - 24f), new Vector2(-12f, topOffset));

            var swatch = JobsTabUIPrimitives.CreateRect("Swatch", row);
            var swatchRect = swatch;
            swatchRect.anchorMin = new Vector2(0f, 0.5f);
            swatchRect.anchorMax = new Vector2(0f, 0.5f);
            swatchRect.pivot = new Vector2(0f, 0.5f);
            swatchRect.sizeDelta = new Vector2(18f, 18f);
            swatchRect.anchoredPosition = Vector2.zero;
            JobsTabUIPrimitives.AddImage(swatch, swatchColor, _panelSprite).type = Image.Type.Sliced;

            var text = JobsTabUIPrimitives.CreateText(row, labelOverride ?? label, 12f, TextMuted, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, _fontAsset);
            JobsTabUIPrimitives.Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(26f, 0f), Vector2.zero);
        }

        private void RebuildMemberList()
        {
            for (var i = _memberCards.Count - 1; i >= 0; i--)
            {
                if (_memberCards[i].Root != null) Destroy(_memberCards[i].Root.gameObject);
            }

            _memberCards.Clear();
            _memberBuffer.Clear();


            var members = SquadManager.Instance.SquadMembers;
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i] == null) continue;
                _memberBuffer.Add(members[i]);
            }

            if (_selectedMemberIndex < 0 && _memberBuffer.Count > 0) _selectedMemberIndex = 0;
            if (_selectedMemberIndex >= _memberBuffer.Count) _selectedMemberIndex = _memberBuffer.Count - 1;

            for (var i = 0; i < _memberBuffer.Count; i++)
            {
                var index = i;
                var member = _memberBuffer[i];
                var card = JobsTabMemberCardBuilder.BuildMemberCard(
                    _memberListContent, member, i == _selectedMemberIndex,
                    _fontAsset, _panelSprite, _slotSprite,
                    TextPrimary, TextMuted, SelectedBorder, _workManager);
                card.Button.onClick.AddListener(() => SelectMember(index));
                _memberCards.Add(card);
            }

            RefreshSurvivorCount();
        }

        private void RefreshSurvivorCount()
        {
            JobsTabMemberCardBuilder.RefreshSurvivorCount(_survivorCountText, _memberBuffer);
        }

        private void RefreshMemberStatuses()
        {
            JobsTabMemberCardBuilder.RefreshMemberStatuses(_memberCards, _memberBuffer, _workManager);
        }

        private void SelectMember(int index)
        {
            _selectedMemberIndex = index;
            for (var i = 0; i < _memberCards.Count; i++)
                JobsTabMemberCardBuilder.ApplyMemberCardVisual(_memberCards[i], i == _selectedMemberIndex);

            RebuildJobMatrix();
        }

        private void RebuildJobMatrix()
        {
            if (_jobListContent == null) return;
            JobsTabUIPrimitives.ClearChildren(_jobListContent);

            if (_selectedMemberIndex < 0 || _selectedMemberIndex >= _memberBuffer.Count) return;

            var member = _memberBuffer[_selectedMemberIndex];
            var profile = _workManager?.GetOrCreateProfile(member);
            if (profile == null) return;

            for (var i = 0; i < JobRows.Length; i++)
                BuildJobRow(_jobListContent, JobRows[i], member, profile);
        }

        private void BuildJobRow(
            RectTransform parent,
            JobRowDefinition definition,
            SquadMember member,
            WorkAssignmentProfile profile)
        {
            var row = JobsTabUIPrimitives.CreateRect(definition.Title + "Row", parent);
            var rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.minHeight = 72f;

            JobsTabUIPrimitives.AddImage(row, new Color(0.15f, 0.15f, 0.14f, 0.96f), _panelSprite).type = Image.Type.Sliced;

            var iconFrame = JobsTabUIPrimitives.CreateRect("Icon", row);
            var iconRect = iconFrame;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            iconRect.anchoredPosition = new Vector2(10f, 0f);
            JobsTabUIPrimitives.AddImage(iconFrame, new Color(0.22f, 0.22f, 0.20f, 1f), _panelSprite).type = Image.Type.Sliced;
            JobsTabUIPrimitives.CreateText(iconFrame, definition.IconGlyph, 18f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.Center, _fontAsset);

            var textColumn = JobsTabUIPrimitives.CreateRect("TextColumn", row);
            JobsTabUIPrimitives.Stretch(textColumn, new Vector2(0f, 0f), new Vector2(0.42f, 1f), new Vector2(58f, 8f), new Vector2(0f, -8f));

            var title = JobsTabUIPrimitives.CreateText(textColumn, definition.Title, 15f, TextPrimary, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, _fontAsset);
            JobsTabUIPrimitives.Stretch(title.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var desc = JobsTabUIPrimitives.CreateText(textColumn, definition.Description, 11f, TextMuted, FontStyles.Normal,
                TextAlignmentOptions.TopLeft, _fontAsset);
            desc.textWrappingMode = TextWrappingModes.Normal;
            JobsTabUIPrimitives.Stretch(desc.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.55f), Vector2.zero, Vector2.zero);

            var capable = definition.IsImplemented
                          && definition.JobType.HasValue
                          && WorkManager.CanMemberPerformJob(member, definition.JobType.Value);

            var currentPriority = definition.JobType.HasValue
                ? profile.GetPriority(definition.JobType.Value)
                : WorkPriorityLevel.Off;

            var track = JobsTabUIPrimitives.CreateRect("PriorityTrack", row);
            JobsTabUIPrimitives.Stretch(track, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(4f, 10f), new Vector2(-10f, -10f));

            var trackLayout = track.gameObject.AddComponent<HorizontalLayoutGroup>();
            trackLayout.spacing = 3f;
            trackLayout.childAlignment = TextAnchor.MiddleRight;
            trackLayout.childControlWidth = true;
            trackLayout.childControlHeight = true;
            trackLayout.childForceExpandWidth = true;
            trackLayout.childForceExpandHeight = true;

            if (!capable)
            {
                var dash = CreatePrioritySlot(track, "-", false, true, PriorityInactive, null);
                var dashLayout = dash.GetComponent<LayoutElement>();
                dashLayout.flexibleWidth = 9f;
                return;
            }

            for (var priority = 1; priority <= 9; priority++)
            {
                var level = (WorkPriorityLevel)priority;
                var isActive = currentPriority == level;
                var capturedLevel = level;
                var capturedJob = definition.JobType.Value;
                CreatePrioritySlot(
                    track,
                    priority.ToString(),
                    isActive,
                    false,
                    isActive ? PriorityActive : PriorityInactive,
                    () => SetPriority(member, capturedJob, capturedLevel));
            }

            var isOff = currentPriority == WorkPriorityLevel.Off;
            CreatePrioritySlot(track, "X", isOff, false, isOff ? PriorityActive : PriorityInactive,
                () => SetPriority(member, definition.JobType.Value, WorkPriorityLevel.Off));
        }

        private RectTransform CreatePrioritySlot(
            RectTransform parent,
            string label,
            bool active,
            bool disabled,
            Color color,
            System.Action onClick)
        {
            var slot = JobsTabUIPrimitives.CreateRect("Priority_" + label, parent);
            var layout = slot.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 26f;
            layout.preferredWidth = 30f;
            layout.flexibleWidth = 1f;
            layout.minHeight = 30f;

            var image = JobsTabUIPrimitives.AddImage(slot, color, _panelSprite);
            image.type = Image.Type.Sliced;

            if (!disabled && onClick != null)
            {
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => onClick());
            }

            var textColor = active ? Color.white : new Color(0.72f, 0.70f, 0.64f, 1f);
            var text = JobsTabUIPrimitives.CreateText(slot, label, 12f, textColor, FontStyles.Bold, TextAlignmentOptions.Center, _fontAsset);
            JobsTabUIPrimitives.Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.raycastTarget = false;
            return slot;
        }

        private void SetPriority(SquadMember member, WorkJobType jobType, WorkPriorityLevel level)
        {
            var profile = _workManager?.GetOrCreateProfile(member);
            if (profile == null) return;

            profile.SetPriority(jobType, level);
            RebuildJobMatrix();
        }

        private void OnResetSelectedMember()
        {
            if (_selectedMemberIndex < 0 || _selectedMemberIndex >= _memberBuffer.Count) return;
            _workManager?.ResetProfileToDefaults(_memberBuffer[_selectedMemberIndex]);
            RebuildJobMatrix();
        }

        private void OnApplyToAll()
        {
            if (_workManager == null || _selectedMemberIndex < 0 || _selectedMemberIndex >= _memberBuffer.Count) return;

            var template = _workManager.GetOrCreateProfile(_memberBuffer[_selectedMemberIndex]);
            if (template == null) return;

            _workManager.ApplyProfileToAll(template);
            RebuildJobMatrix();
        }

        private void EnsureRuntimeReferences()
        {
            if (_workManager == null)
                _workManager = WorkManager.Instance ?? FindFirstObjectByType<WorkManager>();
        }
    }
}
