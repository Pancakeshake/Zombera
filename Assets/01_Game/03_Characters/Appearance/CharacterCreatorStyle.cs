#region

using UnityEngine;

#endregion

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Centralized visual tokens for the character creator panel.
    ///     No inline magic numbers should exist in any CharacterCreator controller or builder.
    /// </summary>
    public static class CharacterCreatorStyle
    {
        // ReSharper disable UnusedMember.Global
        // ──────────────── Typography ────────────────

        public const float HeaderFontSize = 70f;
        public const float SubtitleFontSize = 23f;
        public const float PrimaryButtonFontSize = 36f;
        public const float SecondaryButtonFontSize = 34f;

        // ──────────────── Layout ────────────────

        public const float HeaderBarHeight = 24f;
        public const float NameInputHeight = 56f;
        public const float PrimaryButtonMinWidth = 300f;
        public const float PrimaryButtonMinHeight = 74f;
        public const float SecondaryButtonMinWidth = 280f;
        public const float SecondaryButtonMinHeight = 70f;

        // ──────────────── Labels / Defaults ────────────────

        public const string DefaultCharacterName = "Survivor";
        public const string HeaderDefaultTitle = "CREATE SURVIVOR";
        public const string ConfirmButtonLabel = "CONFIRM";
        public const string CloseButtonLabel = "BACK";

        public const string RandomButtonLabel = "RANDOM";
        // ──────────────── Panel / Input Colors ────────────────

        public static readonly Color InputTint = new(0.06f, 0.05f, 0.04f, 0.92f);
        public static readonly Color ConfirmTint = new(0.40f, 0.12f, 0.07f, 0.98f);
        public static readonly Color CloseTint = new(0.11f, 0.11f, 0.11f, 0.96f);
        public static readonly Color AccentTint = new(0.82f, 0.64f, 0.36f, 0.98f);
        public static readonly Color TextTint = new(0.95f, 0.90f, 0.78f, 1f);
        public static readonly Color ButtonBorderTint = new(0.67f, 0.52f, 0.31f, 0.94f);
        public static readonly Color ValidationErrorTint = new(0.92f, 0.46f, 0.40f, 1f);
        public static readonly Color RandomButtonTint = new(0.24f, 0.24f, 0.24f, 0.95f);

        // ──────────────── Customization Panel Colors ────────────────

        public static readonly Color CustomizationPanelTint = new(0.07f, 0.07f, 0.07f, 0.88f);
        public static readonly Color TabNormalTint = new(0.16f, 0.16f, 0.16f, 0.95f);
        public static readonly Color TabActiveTint = new(0.40f, 0.12f, 0.07f, 0.98f);

        // ──────────────── Tone Gradient Endpoints ────────────────

        public static readonly Color SkinToneDark = new(0.27f, 0.18f, 0.12f, 1f);
        public static readonly Color SkinToneLight = new(0.98f, 0.86f, 0.78f, 1f);
        public static readonly Color SkinToneBlack = SkinToneDark;
        public static readonly Color SkinToneAsian = new(0.93f, 0.78f, 0.64f, 1f);
        public static readonly Color SkinToneMexican = new(0.74f, 0.54f, 0.38f, 1f);
        public static readonly Color SkinToneCaucasian = SkinToneLight;

        public static readonly Color[] PresetSkinTones =
        {
            SkinToneBlack,
            SkinToneAsian,
            SkinToneMexican,
            SkinToneCaucasian
        };
        public static readonly Color HairToneDark = new(0.11f, 0.08f, 0.05f, 1f);
        public static readonly Color HairToneLight = new(0.79f, 0.66f, 0.46f, 1f);
        public static readonly Color EyeToneDark = new(0.16f, 0.24f, 0.30f, 1f);
        public static readonly Color EyeToneLight = new(0.57f, 0.78f, 0.95f, 1f);
        // ReSharper restore UnusedMember.Global
    }
}