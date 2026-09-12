using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI;

namespace Zombera.Editor
{
    public static partial class WorldHudSetupTool
    {
        private static GameObject BuildSquadPanel(Transform parent)
        {
            var panel = MakePanel(parent, "SquadPanel");
            BuildPanelHeader(panel.transform, "SQUAD OVERVIEW");

            const float squadFontScale = 2f;

            var squadHeaderRT = panel.transform.Find("PanelHeader") as RectTransform;
            if (squadHeaderRT != null) squadHeaderRT.sizeDelta = new Vector2(0f, 84f);

            var squadHeaderTitle = panel.transform.Find("PanelHeader/Title")?.GetComponent<TextMeshProUGUI>();
            if (squadHeaderTitle != null) squadHeaderTitle.fontSize = 24f * squadFontScale;

            var body = MakeImage("Body", panel.transform, new Color(0.06f, 0.06f, 0.08f, 1f));
            var bodyRT = RT(body);
            FillParent(bodyRT);
            bodyRT.offsetMin = new Vector2(6f, 6f);
            bodyRT.offsetMax = new Vector2(-6f, -88f);

            var rosterBG = MakeImage("RosterBG", body.transform, new Color(0.08f, 0.09f, 0.11f, 1f));
            var rosterBGRT = RT(rosterBG);
            rosterBGRT.anchorMin = new Vector2(0f, 1f);
            rosterBGRT.anchorMax = new Vector2(1f, 1f);
            rosterBGRT.pivot = new Vector2(0.5f, 1f);
            rosterBGRT.offsetMin = new Vector2(0f, -176f);
            rosterBGRT.offsetMax = Vector2.zero;

            var rosterHeader = MakeRect("RosterHeader", rosterBG.transform);
            var rosterHeaderRT = RT(rosterHeader);
            rosterHeaderRT.anchorMin = new Vector2(0f, 1f);
            rosterHeaderRT.anchorMax = new Vector2(1f, 1f);
            rosterHeaderRT.pivot = new Vector2(0.5f, 1f);
            rosterHeaderRT.offsetMin = new Vector2(8f, -40f);
            rosterHeaderRT.offsetMax = new Vector2(-8f, 0f);
            var rosterHeaderHLG = rosterHeader.AddComponent<HorizontalLayoutGroup>();
            rosterHeaderHLG.spacing = 8f;
            rosterHeaderHLG.childAlignment = TextAnchor.MiddleLeft;
            rosterHeaderHLG.childControlWidth = true;
            rosterHeaderHLG.childControlHeight = true;
            rosterHeaderHLG.childForceExpandWidth = false;
            rosterHeaderHLG.childForceExpandHeight = false;
            rosterHeaderHLG.padding = new RectOffset(0, 0, 0, 0);

            var rosterLabelGO = MakeRect("RosterLabel", rosterHeader.transform);
            var rosterLabelLE = rosterLabelGO.AddComponent<LayoutElement>();
            rosterLabelLE.flexibleWidth = 0f;

            var rosterLabel = rosterLabelGO.AddComponent<TextMeshProUGUI>();
            rosterLabel.text = "ROSTER";
            rosterLabel.fontSize = 16f * squadFontScale;
            rosterLabel.fontStyle = FontStyles.Bold;
            rosterLabel.color = C_TextDim;
            rosterLabel.alignment = TextAlignmentOptions.MidlineLeft;
            rosterLabel.raycastTarget = false;

            var rosterSquadPageTabs = BuildRosterHeaderSquadTabs(rosterHeader.transform, squadFontScale);

            var rosterStripRoot = MakeRect("RosterStrip", rosterBG.transform);
            var rosterStripRT = RT(rosterStripRoot);
            rosterStripRT.anchorMin = new Vector2(0f, 0f);
            rosterStripRT.anchorMax = new Vector2(1f, 1f);
            rosterStripRT.offsetMin = new Vector2(8f, 8f);
            rosterStripRT.offsetMax = new Vector2(-8f, -48f);
            var rosterHLG = rosterStripRoot.AddComponent<HorizontalLayoutGroup>();
            rosterHLG.spacing = 8f;
            rosterHLG.childAlignment = TextAnchor.MiddleLeft;
            rosterHLG.childControlWidth = true;
            rosterHLG.childControlHeight = true;
            rosterHLG.childForceExpandWidth = false;
            rosterHLG.childForceExpandHeight = false;
            rosterHLG.padding = new RectOffset(0, 0, 0, 0);

            for (var i = 0; i < 6; i++)
                BuildInventoryMemberSlot(rosterStripRoot.transform, i, i == 0, 120f, "Loading...", true,
                    13f * squadFontScale);

            var rosterStrip = rosterStripRoot.AddComponent<SquadPortraitStrip>();
            var rosterStripSO = new SerializedObject(rosterStrip);
            rosterStripSO.FindProperty("enableSquadTabs").boolValue = true;
            rosterStripSO.FindProperty("autoEnableTabsForBottomStrip").boolValue = false;
            rosterStripSO.FindProperty("squadTabCount").intValue = 4;
            rosterStripSO.FindProperty("slotsPerSquadTab").intValue = 5;
            rosterStripSO.FindProperty("squadTabActiveColor").colorValue = C_Accent;
            rosterStripSO.FindProperty("squadTabInactiveColor").colorValue = C_Btn;
            var rosterTabButtonsProperty = rosterStripSO.FindProperty("squadTabButtons");
            rosterTabButtonsProperty.arraySize = rosterSquadPageTabs.Length;
            for (var i = 0; i < rosterSquadPageTabs.Length; i++)
                rosterTabButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = rosterSquadPageTabs[i];
            rosterStripSO.ApplyModifiedPropertiesWithoutUndo();

            var details = MakeImage("DetailsArea", body.transform, new Color(0.05f, 0.06f, 0.08f, 1f));
            var detailsRT = RT(details);
            detailsRT.anchorMin = new Vector2(0f, 0f);
            detailsRT.anchorMax = new Vector2(1f, 1f);
            detailsRT.offsetMin = new Vector2(0f, 112f);
            detailsRT.offsetMax = new Vector2(0f, -184f);

            var summary = MakeImage("SummaryCard", details.transform, new Color(0.08f, 0.09f, 0.12f, 1f));
            var summaryRT = RT(summary);
            summaryRT.anchorMin = new Vector2(0f, 0f);
            summaryRT.anchorMax = new Vector2(0.34f, 1f);
            summaryRT.offsetMin = new Vector2(0f, 0f);
            summaryRT.offsetMax = new Vector2(-4f, 0f);

            var selectedName = MakeRect("SelectedName", summary.transform).AddComponent<TextMeshProUGUI>();
            var selectedNameRT = RT(selectedName.gameObject);
            selectedNameRT.anchorMin = new Vector2(0f, 1f);
            selectedNameRT.anchorMax = new Vector2(1f, 1f);
            selectedNameRT.pivot = new Vector2(0.5f, 1f);
            selectedNameRT.offsetMin = new Vector2(8f, -64f);
            selectedNameRT.offsetMax = new Vector2(-8f, -8f);
            selectedName.text = "No Unit Selected";
            selectedName.fontSize = 24f * squadFontScale;
            selectedName.fontStyle = FontStyles.Bold;
            selectedName.color = C_Text;
            selectedName.alignment = TextAlignmentOptions.MidlineLeft;
            selectedName.raycastTarget = false;

            var roleText = MakeRect("RoleText", summary.transform).AddComponent<TextMeshProUGUI>();
            var roleTextRT = RT(roleText.gameObject);
            roleTextRT.anchorMin = new Vector2(0f, 1f);
            roleTextRT.anchorMax = new Vector2(1f, 1f);
            roleTextRT.pivot = new Vector2(0.5f, 1f);
            roleTextRT.offsetMin = new Vector2(8f, -118f);
            roleTextRT.offsetMax = new Vector2(-8f, -70f);
            roleText.text = "Role: -";
            roleText.fontSize = 17f * squadFontScale;
            roleText.color = C_TextDim;
            roleText.alignment = TextAlignmentOptions.MidlineLeft;
            roleText.raycastTarget = false;

            var healthText = MakeRect("HealthText", summary.transform).AddComponent<TextMeshProUGUI>();
            var healthTextRT = RT(healthText.gameObject);
            healthTextRT.anchorMin = new Vector2(0f, 1f);
            healthTextRT.anchorMax = new Vector2(1f, 1f);
            healthTextRT.pivot = new Vector2(0.5f, 1f);
            healthTextRT.offsetMin = new Vector2(8f, -170f);
            healthTextRT.offsetMax = new Vector2(-8f, -122f);
            healthText.text = "Health: -";
            healthText.fontSize = 17f * squadFontScale;
            healthText.color = C_Text;
            healthText.alignment = TextAlignmentOptions.MidlineLeft;
            healthText.raycastTarget = false;

            var staminaText = MakeRect("StaminaText", summary.transform).AddComponent<TextMeshProUGUI>();
            var staminaTextRT = RT(staminaText.gameObject);
            staminaTextRT.anchorMin = new Vector2(0f, 1f);
            staminaTextRT.anchorMax = new Vector2(1f, 1f);
            staminaTextRT.pivot = new Vector2(0.5f, 1f);
            staminaTextRT.offsetMin = new Vector2(8f, -222f);
            staminaTextRT.offsetMax = new Vector2(-8f, -174f);
            staminaText.text = "Stamina: -";
            staminaText.fontSize = 17f * squadFontScale;
            staminaText.color = C_Text;
            staminaText.alignment = TextAlignmentOptions.MidlineLeft;
            staminaText.raycastTarget = false;

            var skills = MakeImage("SkillsCard", details.transform, new Color(0.07f, 0.08f, 0.11f, 1f));
            var skillsRT = RT(skills);
            skillsRT.anchorMin = new Vector2(0.34f, 0f);
            skillsRT.anchorMax = Vector2.one;
            skillsRT.offsetMin = new Vector2(4f, 0f);
            skillsRT.offsetMax = Vector2.zero;

            var skillsHeader = MakeRect("SkillsHeader", skills.transform).AddComponent<TextMeshProUGUI>();
            var skillsHeaderRT = RT(skillsHeader.gameObject);
            skillsHeaderRT.anchorMin = new Vector2(0f, 1f);
            skillsHeaderRT.anchorMax = new Vector2(1f, 1f);
            skillsHeaderRT.pivot = new Vector2(0.5f, 1f);
            skillsHeaderRT.offsetMin = new Vector2(8f, -44f);
            skillsHeaderRT.offsetMax = new Vector2(-8f, 0f);
            skillsHeader.text = "SKILLS";
            skillsHeader.fontSize = 16f * squadFontScale;
            skillsHeader.fontStyle = FontStyles.Bold;
            skillsHeader.color = C_TextDim;
            skillsHeader.alignment = TextAlignmentOptions.MidlineLeft;
            skillsHeader.raycastTarget = false;

            var statsGrid = MakeRect("StatsGrid", skills.transform);
            var statsGridRT = RT(statsGrid);
            statsGridRT.anchorMin = new Vector2(0f, 0f);
            statsGridRT.anchorMax = new Vector2(1f, 1f);
            statsGridRT.offsetMin = new Vector2(8f, 8f);
            statsGridRT.offsetMax = new Vector2(-8f, -52f);
            var statsGLG = statsGrid.AddComponent<GridLayoutGroup>();
            statsGLG.cellSize = new Vector2(252f, 62f);
            statsGLG.spacing = new Vector2(10f, 10f);
            statsGLG.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            statsGLG.constraintCount = 2;
            statsGLG.childAlignment = TextAnchor.UpperLeft;

            TextMeshProUGUI strengthValue =
                BuildStatChip(statsGrid.transform, "Strength", squadFontScale, out var strengthButton);
            TextMeshProUGUI shootingValue =
                BuildStatChip(statsGrid.transform, "Shooting", squadFontScale, out var shootingButton);
            TextMeshProUGUI meleeValue =
                BuildStatChip(statsGrid.transform, "Melee", squadFontScale, out var meleeButton);
            TextMeshProUGUI medicalValue =
                BuildStatChip(statsGrid.transform, "Medical", squadFontScale, out var medicalButton);
            TextMeshProUGUI engineeringValue = BuildStatChip(statsGrid.transform, "Engineering", squadFontScale,
                out var engineeringButton);
            TextMeshProUGUI toughnessValue =
                BuildStatChip(statsGrid.transform, "Toughness", squadFontScale, out var toughnessButton);
            TextMeshProUGUI constitutionValue = BuildStatChip(statsGrid.transform, "Constitution", squadFontScale,
                out var constitutionButton);
            TextMeshProUGUI agilityValue =
                BuildStatChip(statsGrid.transform, "Agility", squadFontScale, out var agilityButton);
            TextMeshProUGUI enduranceValue =
                BuildStatChip(statsGrid.transform, "Endurance", squadFontScale, out var enduranceButton);
            TextMeshProUGUI scavengingValue = BuildStatChip(statsGrid.transform, "Scavenging", squadFontScale,
                out var scavengingButton);
            TextMeshProUGUI stealthValue =
                BuildStatChip(statsGrid.transform, "Stealth", squadFontScale, out var stealthButton);

            var skillInfoModal = MakeImage("SkillInfoModal", skills.transform, new Color(0.03f, 0.04f, 0.06f, 0.98f));
            var skillInfoModalRT = RT(skillInfoModal);
            skillInfoModalRT.anchorMin = new Vector2(0.56f, 0.10f);
            skillInfoModalRT.anchorMax = new Vector2(0.99f, 0.90f);
            skillInfoModalRT.offsetMin = Vector2.zero;
            skillInfoModalRT.offsetMax = Vector2.zero;
            var modalOutline = skillInfoModal.AddComponent<Outline>();
            modalOutline.effectColor = new Color(0.30f, 0.35f, 0.40f, 0.95f);
            modalOutline.effectDistance = new Vector2(1f, -1f);

            var modalTitle = MakeRect("Title", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var modalTitleRT = RT(modalTitle.gameObject);
            modalTitleRT.anchorMin = new Vector2(0f, 1f);
            modalTitleRT.anchorMax = new Vector2(1f, 1f);
            modalTitleRT.pivot = new Vector2(0.5f, 1f);
            modalTitleRT.offsetMin = new Vector2(10f, -48f);
            modalTitleRT.offsetMax = new Vector2(-54f, -8f);
            modalTitle.text = "SKILL DETAILS";
            modalTitle.fontSize = 12f * squadFontScale;
            modalTitle.fontStyle = FontStyles.Bold;
            modalTitle.color = C_Text;
            modalTitle.alignment = TextAlignmentOptions.MidlineLeft;
            modalTitle.raycastTarget = false;

            var modalCloseGO = MakeImage("CloseButton", skillInfoModal.transform, C_Btn);
            var modalCloseRT = RT(modalCloseGO);
            modalCloseRT.anchorMin = new Vector2(1f, 1f);
            modalCloseRT.anchorMax = new Vector2(1f, 1f);
            modalCloseRT.pivot = new Vector2(1f, 1f);
            modalCloseRT.anchoredPosition = new Vector2(-8f, -8f);
            modalCloseRT.sizeDelta = new Vector2(40f, 36f);
            var modalCloseButton = modalCloseGO.AddComponent<Button>();
            modalCloseButton.targetGraphic = modalCloseGO.GetComponent<Image>();
            var modalCloseLabel = MakeRect("Label", modalCloseGO.transform).AddComponent<TextMeshProUGUI>();
            FillParent(RT(modalCloseLabel.gameObject));
            modalCloseLabel.text = "X";
            modalCloseLabel.fontSize = 10f * squadFontScale;
            modalCloseLabel.fontStyle = FontStyles.Bold;
            modalCloseLabel.color = C_Text;
            modalCloseLabel.alignment = TextAlignmentOptions.Center;
            modalCloseLabel.raycastTarget = false;

            var modalLevel = MakeRect("Level", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var modalLevelRT = RT(modalLevel.gameObject);
            modalLevelRT.anchorMin = new Vector2(0f, 1f);
            modalLevelRT.anchorMax = new Vector2(1f, 1f);
            modalLevelRT.pivot = new Vector2(0.5f, 1f);
            modalLevelRT.offsetMin = new Vector2(10f, -88f);
            modalLevelRT.offsetMax = new Vector2(-10f, -52f);
            modalLevel.text = "Level: -";
            modalLevel.fontSize = 9f * squadFontScale;
            modalLevel.fontStyle = FontStyles.Bold;
            modalLevel.color = C_Text;
            modalLevel.alignment = TextAlignmentOptions.MidlineLeft;
            modalLevel.raycastTarget = false;

            var modalXp = MakeRect("Xp", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var modalXpRT = RT(modalXp.gameObject);
            modalXpRT.anchorMin = new Vector2(0f, 1f);
            modalXpRT.anchorMax = new Vector2(1f, 1f);
            modalXpRT.pivot = new Vector2(0.5f, 1f);
            modalXpRT.offsetMin = new Vector2(10f, -126f);
            modalXpRT.offsetMax = new Vector2(-10f, -90f);
            modalXp.text = "XP: -";
            modalXp.fontSize = 8f * squadFontScale;
            modalXp.color = C_TextDim;
            modalXp.alignment = TextAlignmentOptions.MidlineLeft;
            modalXp.raycastTarget = false;

            var howToHeader = MakeRect("HowToHeader", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var howToHeaderRT = RT(howToHeader.gameObject);
            howToHeaderRT.anchorMin = new Vector2(0f, 1f);
            howToHeaderRT.anchorMax = new Vector2(1f, 1f);
            howToHeaderRT.pivot = new Vector2(0.5f, 1f);
            howToHeaderRT.offsetMin = new Vector2(10f, -166f);
            howToHeaderRT.offsetMax = new Vector2(-10f, -132f);
            howToHeader.text = "HOW TO LEVEL";
            howToHeader.fontSize = 8f * squadFontScale;
            howToHeader.fontStyle = FontStyles.Bold;
            howToHeader.color = C_TextDim;
            howToHeader.alignment = TextAlignmentOptions.MidlineLeft;
            howToHeader.raycastTarget = false;

            var howToBody = MakeRect("HowToBody", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var howToBodyRT = RT(howToBody.gameObject);
            howToBodyRT.anchorMin = new Vector2(0f, 1f);
            howToBodyRT.anchorMax = new Vector2(1f, 1f);
            howToBodyRT.pivot = new Vector2(0.5f, 1f);
            howToBodyRT.offsetMin = new Vector2(10f, -258f);
            howToBodyRT.offsetMax = new Vector2(-10f, -168f);
            howToBody.text = "Select a skill to view progression details.";
            howToBody.fontSize = 8f * squadFontScale;
            howToBody.color = C_Text;
            howToBody.alignment = TextAlignmentOptions.TopLeft;
            howToBody.textWrappingMode = TextWrappingModes.Normal;
            howToBody.overflowMode = TextOverflowModes.Truncate;
            howToBody.raycastTarget = false;

            var effectsHeader = MakeRect("EffectsHeader", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var effectsHeaderRT = RT(effectsHeader.gameObject);
            effectsHeaderRT.anchorMin = new Vector2(0f, 1f);
            effectsHeaderRT.anchorMax = new Vector2(1f, 1f);
            effectsHeaderRT.pivot = new Vector2(0.5f, 1f);
            effectsHeaderRT.offsetMin = new Vector2(10f, -294f);
            effectsHeaderRT.offsetMax = new Vector2(-10f, -262f);
            effectsHeader.text = "EFFECTS";
            effectsHeader.fontSize = 8f * squadFontScale;
            effectsHeader.fontStyle = FontStyles.Bold;
            effectsHeader.color = C_TextDim;
            effectsHeader.alignment = TextAlignmentOptions.MidlineLeft;
            effectsHeader.raycastTarget = false;

            var effectsBody = MakeRect("EffectsBody", skillInfoModal.transform).AddComponent<TextMeshProUGUI>();
            var effectsBodyRT = RT(effectsBody.gameObject);
            effectsBodyRT.anchorMin = new Vector2(0f, 0f);
            effectsBodyRT.anchorMax = new Vector2(1f, 1f);
            effectsBodyRT.offsetMin = new Vector2(10f, 10f);
            effectsBodyRT.offsetMax = new Vector2(-10f, -298f);
            effectsBody.text = "Click any skill tile to inspect what it changes in gameplay.";
            effectsBody.fontSize = 8f * squadFontScale;
            effectsBody.color = C_Text;
            effectsBody.alignment = TextAlignmentOptions.TopLeft;
            effectsBody.textWrappingMode = TextWrappingModes.Normal;
            effectsBody.overflowMode = TextOverflowModes.Truncate;
            effectsBody.raycastTarget = false;

            skillInfoModal.SetActive(false);

            var bottom = MakeImage("BottomActions", body.transform, new Color(0.07f, 0.08f, 0.10f, 1f));
            var bottomRT = RT(bottom);
            bottomRT.anchorMin = Vector2.zero;
            bottomRT.anchorMax = new Vector2(1f, 0f);
            bottomRT.pivot = new Vector2(0.5f, 0f);
            bottomRT.anchoredPosition = Vector2.zero;
            bottomRT.sizeDelta = new Vector2(0f, 118f);
            var bottomHLG = bottom.AddComponent<HorizontalLayoutGroup>();
            bottomHLG.spacing = 6f;
            bottomHLG.childAlignment = TextAnchor.MiddleCenter;
            bottomHLG.childControlWidth = true;
            bottomHLG.childControlHeight = true;
            bottomHLG.childForceExpandWidth = true;
            bottomHLG.childForceExpandHeight = true;
            bottomHLG.padding = new RectOffset(10, 10, 10, 10);

            foreach (var action in new[] { "RALLY", "DEFEND", "SCOUT", "ASSIST" })
            {
                var actionGO = MakeImage($"Action_{action}", bottom.transform, C_Btn);
                actionGO.AddComponent<Button>().targetGraphic = actionGO.GetComponent<Image>();
                var actionTMP = MakeRect("Label", actionGO.transform).AddComponent<TextMeshProUGUI>();
                FillParent(RT(actionTMP.gameObject));
                actionTMP.text = action;
                actionTMP.fontSize = 16f * squadFontScale;
                actionTMP.fontStyle = FontStyles.Bold;
                actionTMP.color = C_Text;
                actionTMP.alignment = TextAlignmentOptions.Center;
                actionTMP.raycastTarget = false;
            }

            var squadStatsCtrl = panel.AddComponent<SquadTabUnitStatsController>();
            var squadStatsSO = new SerializedObject(squadStatsCtrl);
            squadStatsSO.FindProperty("rosterStrip").objectReferenceValue = rosterStrip;
            squadStatsSO.FindProperty("selectedNameText").objectReferenceValue = selectedName;
            squadStatsSO.FindProperty("roleText").objectReferenceValue = roleText;
            squadStatsSO.FindProperty("healthText").objectReferenceValue = healthText;
            squadStatsSO.FindProperty("staminaText").objectReferenceValue = staminaText;
            squadStatsSO.FindProperty("strengthValueText").objectReferenceValue = strengthValue;
            squadStatsSO.FindProperty("shootingValueText").objectReferenceValue = shootingValue;
            squadStatsSO.FindProperty("meleeValueText").objectReferenceValue = meleeValue;
            squadStatsSO.FindProperty("medicalValueText").objectReferenceValue = medicalValue;
            squadStatsSO.FindProperty("engineeringValueText").objectReferenceValue = engineeringValue;
            squadStatsSO.FindProperty("toughnessValueText").objectReferenceValue = toughnessValue;
            squadStatsSO.FindProperty("constitutionValueText").objectReferenceValue = constitutionValue;
            squadStatsSO.FindProperty("agilityValueText").objectReferenceValue = agilityValue;
            squadStatsSO.FindProperty("enduranceValueText").objectReferenceValue = enduranceValue;
            squadStatsSO.FindProperty("scavengingValueText").objectReferenceValue = scavengingValue;
            squadStatsSO.FindProperty("stealthValueText").objectReferenceValue = stealthValue;
            squadStatsSO.FindProperty("strengthButton").objectReferenceValue = strengthButton;
            squadStatsSO.FindProperty("shootingButton").objectReferenceValue = shootingButton;
            squadStatsSO.FindProperty("meleeButton").objectReferenceValue = meleeButton;
            squadStatsSO.FindProperty("medicalButton").objectReferenceValue = medicalButton;
            squadStatsSO.FindProperty("engineeringButton").objectReferenceValue = engineeringButton;
            squadStatsSO.FindProperty("toughnessButton").objectReferenceValue = toughnessButton;
            squadStatsSO.FindProperty("constitutionButton").objectReferenceValue = constitutionButton;
            squadStatsSO.FindProperty("agilityButton").objectReferenceValue = agilityButton;
            squadStatsSO.FindProperty("enduranceButton").objectReferenceValue = enduranceButton;
            squadStatsSO.FindProperty("scavengingButton").objectReferenceValue = scavengingButton;
            squadStatsSO.FindProperty("stealthButton").objectReferenceValue = stealthButton;
            squadStatsSO.FindProperty("skillInfoModalRoot").objectReferenceValue = skillInfoModal;
            squadStatsSO.FindProperty("skillInfoCloseButton").objectReferenceValue = modalCloseButton;
            squadStatsSO.FindProperty("skillInfoTitleText").objectReferenceValue = modalTitle;
            squadStatsSO.FindProperty("skillInfoLevelText").objectReferenceValue = modalLevel;
            squadStatsSO.FindProperty("skillInfoXpText").objectReferenceValue = modalXp;
            squadStatsSO.FindProperty("skillInfoHowToLevelText").objectReferenceValue = howToBody;
            squadStatsSO.FindProperty("skillInfoEffectsText").objectReferenceValue = effectsBody;
            squadStatsSO.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static TextMeshProUGUI BuildStatChip(Transform parent, string label, float fontScale, out Button button)
        {
            var chip = MakeImage($"Stat_{label}", parent, new Color(0.10f, 0.11f, 0.14f, 0.98f));
            var chipOutline = chip.AddComponent<Outline>();
            chipOutline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.95f);
            chipOutline.effectDistance = new Vector2(1f, -1f);

            button = chip.AddComponent<Button>();
            button.targetGraphic = chip.GetComponent<Image>();

            var title = MakeRect("Title", chip.transform).AddComponent<TextMeshProUGUI>();
            var titleRT = RT(title.gameObject);
            titleRT.anchorMin = new Vector2(0f, 0.55f);
            titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = new Vector2(8f, 0f);
            titleRT.offsetMax = new Vector2(-8f, -2f);
            title.text = label.ToUpperInvariant();
            title.fontSize = 7f * fontScale;
            title.fontStyle = FontStyles.Bold;
            title.color = C_TextDim;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Truncate;
            title.raycastTarget = false;

            var value = MakeRect("Value", chip.transform).AddComponent<TextMeshProUGUI>();
            var valueRT = RT(value.gameObject);
            valueRT.anchorMin = new Vector2(0f, 0f);
            valueRT.anchorMax = new Vector2(1f, 0.55f);
            valueRT.offsetMin = new Vector2(8f, 2f);
            valueRT.offsetMax = new Vector2(-8f, 0f);
            value.text = "0";
            value.fontSize = 10f * fontScale;
            value.fontStyle = FontStyles.Bold;
            value.color = C_Text;
            value.alignment = TextAlignmentOptions.MidlineLeft;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Truncate;
            value.raycastTarget = false;

            return value;
        }
    }
}
