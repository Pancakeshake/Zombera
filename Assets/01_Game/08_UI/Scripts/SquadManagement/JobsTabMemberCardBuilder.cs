using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Characters.Work;
using Zombera.Systems;

namespace Zombera.UI.SquadManagement
{
    /// <summary>
    ///     Static member card construction for the jobs tab.
    ///     Extracted from <see cref="JobsTabController"/>.
    /// </summary>
    internal static class JobsTabMemberCardBuilder
    {
        internal sealed class MemberCardView
        {
            public RectTransform Root;
            public Image Background;
            public Button Button;
            public RectTransform Border;
            public Outline BorderOutline;
            public Image PortraitImage;
            public TMP_Text NameText;
            public TMP_Text StatusText;
            public TMP_Text PortraitInitial;
        }

        public static void RefreshSurvivorCount(TMP_Text counterText, List<SquadMember> memberBuffer)
        {
            if (counterText == null) return;

            var alive = 0;
            for (var i = 0; i < memberBuffer.Count; i++)
            {
                if (memberBuffer[i] != null && memberBuffer[i].IsAvailableForOrders()) alive++;
            }

            counterText.text = alive + " / " + memberBuffer.Count;
        }

        public static void RefreshMemberStatuses(List<MemberCardView> cards, List<SquadMember> memberBuffer, WorkManager workManager)
        {
            for (var i = 0; i < cards.Count && i < memberBuffer.Count; i++)
            {
                var member = memberBuffer[i];
                if (cards[i].StatusText != null)
                    cards[i].StatusText.text = ResolveMemberStatus(member, workManager);
            }
        }

        public static MemberCardView BuildMemberCard(
            RectTransform parent,
            SquadMember member,
            bool selected,
            TMP_FontAsset font,
            Sprite panelSprite,
            Sprite slotSprite,
            Color textPrimary,
            Color textMuted,
            Color selectedBorder,
            WorkManager workManager)
        {
            var view = new MemberCardView { Root = JobsTabUIPrimitives.CreateRect("Member_" + member.MemberId, parent) };
            var element = view.Root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 88f;

            view.Background = JobsTabUIPrimitives.AddImage(view.Root, new Color(0.18f, 0.18f, 0.17f, 0.97f), slotSprite);
            view.Background.type = Image.Type.Sliced;
            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;

            view.Border = JobsTabUIPrimitives.CreateRect("Border", view.Root);
            JobsTabUIPrimitives.Stretch(view.Border, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var borderImage = JobsTabUIPrimitives.AddImage(view.Border, Color.clear, null);
            borderImage.raycastTarget = false;
            view.BorderOutline = view.Border.gameObject.AddComponent<Outline>();
            view.BorderOutline.effectColor = selectedBorder;
            view.BorderOutline.effectDistance = new Vector2(2f, -2f);

            var portraitFrame = JobsTabUIPrimitives.CreateRect("PortraitFrame", view.Root);
            JobsTabUIPrimitives.Stretch(portraitFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 8f), new Vector2(72f, -8f));
            JobsTabUIPrimitives.AddImage(portraitFrame, new Color(0.24f, 0.23f, 0.20f, 1f), panelSprite).type = Image.Type.Sliced;

            var portrait = JobsTabUIPrimitives.CreateRect("Portrait", portraitFrame);
            JobsTabUIPrimitives.Stretch(portrait, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var displayName = member.Unit != null ? member.Unit.name : member.name;
            view.PortraitImage = JobsTabUIPrimitives.AddImage(portrait, new Color(0.24f, 0.28f, 0.24f, 1f), null);
            view.PortraitImage.type = Image.Type.Simple;
            view.PortraitImage.preserveAspect = true;
            view.PortraitInitial = JobsTabUIPrimitives.CreateText(portrait, GetInitial(displayName), 22f, textPrimary, FontStyles.Bold,
                TextAlignmentOptions.Center, font);
            JobsTabUIPrimitives.Stretch(view.PortraitInitial.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ApplyMemberPortrait(view, member);

            var details = JobsTabUIPrimitives.CreateRect("Details", view.Root);
            JobsTabUIPrimitives.Stretch(details, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(78f, 8f), new Vector2(-8f, -8f));

            view.NameText = JobsTabUIPrimitives.CreateText(details, displayName.ToUpperInvariant(), 16f, textPrimary, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, font);
            JobsTabUIPrimitives.Stretch(view.NameText.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            view.StatusText = JobsTabUIPrimitives.CreateText(details, ResolveMemberStatus(member, workManager), 12f, textMuted, FontStyles.Normal,
                TextAlignmentOptions.BottomLeft, font);
            JobsTabUIPrimitives.Stretch(view.StatusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);

            ApplyMemberCardVisual(view, selected);
            return view;
        }

        public static void ApplyMemberCardVisual(MemberCardView view, bool selected)
        {
            if (view.BorderOutline != null) view.BorderOutline.enabled = selected;
            if (view.Background != null)
                view.Background.color = selected
                    ? new Color(0.22f, 0.26f, 0.20f, 0.98f)
                    : new Color(0.18f, 0.18f, 0.17f, 0.97f);
        }

        private static void ApplyMemberPortrait(MemberCardView view, SquadMember member)
        {
            if (view.PortraitImage == null) return;

            var unit = member?.Unit;
            if (unit == null)
            {
                view.PortraitImage.sprite = null;
                if (view.PortraitInitial != null) view.PortraitInitial.gameObject.SetActive(true);
                return;
            }

            var studio = PortraitStudioManager.Instance ?? Object.FindFirstObjectByType<PortraitStudioManager>();
            if (studio == null)
            {
                if (view.PortraitInitial != null) view.PortraitInitial.gameObject.SetActive(true);
                return;
            }

            var unitKey = string.IsNullOrWhiteSpace(unit.UnitId) ? unit.GetInstanceID().ToString() : unit.UnitId;
            if (studio.TryGetCachedPortrait(unitKey, out var cachedSprite) && cachedSprite != null)
            {
                view.PortraitImage.sprite = cachedSprite;
                view.PortraitImage.color = Color.white;
                if (view.PortraitInitial != null) view.PortraitInitial.gameObject.SetActive(false);
                return;
            }

            studio.RefreshPortraitFromUnit(unit);
            if (view.PortraitInitial != null) view.PortraitInitial.gameObject.SetActive(true);
        }

        public static string ResolveMemberStatus(SquadMember member, WorkManager workManager)
        {
            if (member == null) return "Unknown";

            if (workManager != null
                && workManager.TryGetActiveTask(member.MemberId, out var task)
                && task.IsValid)
                return FormatJobLabel(task.jobType);

            return member.CurrentState switch
            {
                SquadUnitState.Moving => "Moving",
                SquadUnitState.Attacking => "Attacking",
                SquadUnitState.Following => "Following",
                SquadUnitState.Dead => "Dead",
                _ => "Idle"
            };
        }

        private static string FormatJobLabel(WorkJobType jobType)
        {
            return jobType switch
            {
                WorkJobType.Looting => "Looting",
                WorkJobType.Mining => "Digging",
                WorkJobType.Building => "Building",
                WorkJobType.Guarding => "Guarding",
                WorkJobType.Cooking => "Cooking",
                WorkJobType.Crafting => "Crafting",
                _ => jobType.ToString()
            };
        }

        private static string GetInitial(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "?";
            return displayName.Trim()[0].ToString().ToUpperInvariant();
        }
    }
}
