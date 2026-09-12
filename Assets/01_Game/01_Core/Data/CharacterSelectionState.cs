#region

using UnityEngine;

#endregion

namespace Zombera.Core
{
    /// <summary>
    ///     Runtime selection payload from character creator flow.
    /// </summary>
    public static class CharacterSelectionState
    {
        private const string DefaultCharacterName = "Survivor";
        private const float DefaultMaxHealth = 100f;
        private const float DefaultDamage = 10f;
        private const float DefaultMoveSpeed = 4f;
        private const float DefaultStamina = 100f;
        private const float DefaultCarryCapacity = 35f;
        private const string DefaultLoadoutSummary = "Pistol x1, Ammo x10, Bandage x1, Food x1";
        private const string DefaultFlavorText = "";

        private const string ProfileCharacterNameKey = "zombera.profile.character.name";
        private const string ProfileAppearancePresetKey = "zombera.profile.character.appearancePreset";
        private const string ProfileAppearanceProfileJsonKey = "zombera.profile.character.appearanceProfileJson";
        private const string ProfilePortraitIdKey = "zombera.profile.character.portraitId";

        static CharacterSelectionState()
        {
            ResetRuntimeToDefaults();
            LoadProfileDefaults();
        }

        public static bool HasSelection { get; private set; }
        public static string SelectedCharacterName { get; private set; }
        public static int SelectedAppearancePresetIndex { get; private set; }
        public static float SelectedMaxHealth { get; private set; }
        public static float SelectedDamage { get; private set; }
        public static float SelectedMoveSpeed { get; private set; }
        public static float SelectedStamina { get; private set; }
        public static float SelectedCarryCapacity { get; private set; }
        public static string SelectedFlavorText { get; private set; }

        public static string SelectedLoadoutSummary { get; private set; }

        public static string SelectedAppearanceRecipe { get; private set; }
        public static string SelectedAppearanceProfileJson { get; private set; }
        public static Texture2D SelectedPortraitTexture { get; private set; }

        public static Sprite SelectedPortraitSprite { get; private set; }
        public static string SelectedPortraitId { get; private set; }

        public static void SetSelection(
            string characterName,
            int appearancePresetIndex,
            float maxHealth,
            float damage,
            float moveSpeed,
            float stamina,
            float carryCapacity,
            string flavorText,
            string loadoutSummary,
            string appearanceRecipe = null,
            string appearanceProfileJson = null,
            string portraitId = null)
        {
            SelectedCharacterName = NormalizeName(characterName);
            SelectedAppearancePresetIndex = Mathf.Max(0, appearancePresetIndex);
            SelectedMaxHealth = Mathf.Max(1f, maxHealth);
            SelectedDamage = Mathf.Max(0f, damage);
            SelectedMoveSpeed = Mathf.Max(0.1f, moveSpeed);
            SelectedStamina = Mathf.Max(0f, stamina);
            SelectedCarryCapacity = Mathf.Max(1f, carryCapacity);
            SelectedFlavorText = string.IsNullOrWhiteSpace(flavorText) ? string.Empty : flavorText.Trim();
            SelectedLoadoutSummary = string.IsNullOrWhiteSpace(loadoutSummary) ? string.Empty : loadoutSummary.Trim();
            SelectedAppearanceRecipe = appearanceRecipe ?? string.Empty;
            SelectedAppearanceProfileJson = NormalizeProfileJson(appearanceProfileJson);
            if (portraitId != null)
                SelectedPortraitId = NormalizePortraitId(portraitId);

            HasSelection = true;

            SaveProfileDefaults(
                SelectedCharacterName,
                SelectedAppearancePresetIndex,
                SelectedAppearanceProfileJson,
                SelectedPortraitId);
        }

        public static void GetProfileDefaults(out string characterName, out int appearancePresetIndex)
        {
            GetProfileDefaults(out characterName, out appearancePresetIndex, out _);
        }

        public static void GetProfileDefaults(out string characterName, out int appearancePresetIndex,
            out string appearanceProfileJson)
        {
            GetProfileDefaults(out characterName, out appearancePresetIndex, out appearanceProfileJson,
                out _);
        }

        public static void GetProfileDefaults(out string characterName, out int appearancePresetIndex,
            out string appearanceProfileJson, out string portraitId)
        {
            characterName = NormalizeName(PlayerPrefs.GetString(ProfileCharacterNameKey, SelectedCharacterName));
            appearancePresetIndex = Mathf.Max(0,
                PlayerPrefs.GetInt(ProfileAppearancePresetKey, SelectedAppearancePresetIndex));
            appearanceProfileJson =
                NormalizeProfileJson(PlayerPrefs.GetString(ProfileAppearanceProfileJsonKey,
                    SelectedAppearanceProfileJson));
            portraitId = NormalizePortraitId(PlayerPrefs.GetString(ProfilePortraitIdKey, SelectedPortraitId));
        }

        public static void SetAppearanceProfileJson(string appearanceProfileJson, bool persistProfileDefaults = true)
        {
            SelectedAppearanceProfileJson = NormalizeProfileJson(appearanceProfileJson);

            if (!persistProfileDefaults) return;

            SaveProfileDefaults(
                SelectedCharacterName,
                SelectedAppearancePresetIndex,
                SelectedAppearanceProfileJson,
                SelectedPortraitId);
        }

        public static void ClearRuntimeSelection()
        {
            HasSelection = false;
            ResetRuntimeToDefaults();
            LoadProfileDefaults();
        }

        public static void SetPortraitSprite(Sprite portraitSprite, string portraitId = null,
            bool persistProfileDefaults = false)
        {
            SelectedPortraitSprite = portraitSprite;
            SelectedPortraitTexture = portraitSprite != null ? portraitSprite.texture : null;

            var normalizedPortraitId = NormalizePortraitId(portraitId);
            if (portraitSprite != null && string.IsNullOrEmpty(normalizedPortraitId))
                normalizedPortraitId = ResolvePortraitIdFromCatalog(portraitSprite);

            SelectedPortraitId = normalizedPortraitId;

            if (!persistProfileDefaults) return;

            SaveProfileDefaults(
                SelectedCharacterName,
                SelectedAppearancePresetIndex,
                SelectedAppearanceProfileJson,
                SelectedPortraitId);
        }

        public static bool TrySetPortraitById(string portraitId, bool persistProfileDefaults = false)
        {
            var normalizedPortraitId = NormalizePortraitId(portraitId);
            if (string.IsNullOrEmpty(normalizedPortraitId))
            {
                SetPortraitSprite(null, string.Empty, persistProfileDefaults);
                return false;
            }

            var catalog = CharacterPortraitCatalog.LoadDefault();
            if (catalog == null || !catalog.TryGetSpriteById(normalizedPortraitId, out var sprite) || sprite == null)
                return false;

            SetPortraitSprite(sprite, normalizedPortraitId, persistProfileDefaults);
            return true;
        }

        private static void ResetRuntimeToDefaults()
        {
            SelectedCharacterName = DefaultCharacterName;
            SelectedAppearancePresetIndex = 0;
            SelectedMaxHealth = DefaultMaxHealth;
            SelectedDamage = DefaultDamage;
            SelectedMoveSpeed = DefaultMoveSpeed;
            SelectedStamina = DefaultStamina;
            SelectedCarryCapacity = DefaultCarryCapacity;
            SelectedFlavorText = DefaultFlavorText;
            SelectedLoadoutSummary = DefaultLoadoutSummary;
            SelectedAppearanceRecipe = string.Empty;
            SelectedAppearanceProfileJson = string.Empty;
            SelectedPortraitTexture = null;
            SelectedPortraitSprite = null;
            SelectedPortraitId = string.Empty;
        }

        private static void LoadProfileDefaults()
        {
            if (PlayerPrefs.HasKey(ProfileCharacterNameKey))
                SelectedCharacterName =
                    NormalizeName(PlayerPrefs.GetString(ProfileCharacterNameKey, DefaultCharacterName));

            if (PlayerPrefs.HasKey(ProfileAppearancePresetKey))
                SelectedAppearancePresetIndex = Mathf.Max(0, PlayerPrefs.GetInt(ProfileAppearancePresetKey, 0));

            if (PlayerPrefs.HasKey(ProfileAppearanceProfileJsonKey))
                SelectedAppearanceProfileJson =
                    NormalizeProfileJson(PlayerPrefs.GetString(ProfileAppearanceProfileJsonKey, string.Empty));

            // Legacy migration: if no profile JSON but we have a recipe, migrate it.
            if (string.IsNullOrWhiteSpace(SelectedAppearanceProfileJson) && !string.IsNullOrWhiteSpace(SelectedAppearanceRecipe))
            {
                SelectedAppearanceProfileJson = Zombera.UI.Menus.CharacterCreation.AppearanceMigrationUtility.MigrateLegacyUmaToProfile(SelectedAppearanceRecipe);
            }

            SelectedPortraitId = NormalizePortraitId(PlayerPrefs.GetString(ProfilePortraitIdKey, string.Empty));
            if (string.IsNullOrEmpty(SelectedPortraitId)) return;

            if (!TrySetPortraitById(SelectedPortraitId))
            {
                SelectedPortraitTexture = null;
                SelectedPortraitSprite = null;
            }
        }

        private static void SaveProfileDefaults(string characterName, int appearancePresetIndex,
            string appearanceProfileJson, string portraitId)
        {
            PlayerPrefs.SetString(ProfileCharacterNameKey, NormalizeName(characterName));
            PlayerPrefs.SetInt(ProfileAppearancePresetKey, Mathf.Max(0, appearancePresetIndex));

            var normalizedProfileJson = NormalizeProfileJson(appearanceProfileJson);
            if (string.IsNullOrEmpty(normalizedProfileJson))
                PlayerPrefs.DeleteKey(ProfileAppearanceProfileJsonKey);
            else
                PlayerPrefs.SetString(ProfileAppearanceProfileJsonKey, normalizedProfileJson);

            var normalizedPortraitId = NormalizePortraitId(portraitId);
            if (string.IsNullOrEmpty(normalizedPortraitId))
                PlayerPrefs.DeleteKey(ProfilePortraitIdKey);
            else
                PlayerPrefs.SetString(ProfilePortraitIdKey, normalizedPortraitId);

            PlayerPrefs.Save();
        }

        private static string ResolvePortraitIdFromCatalog(Sprite portraitSprite)
        {
            if (portraitSprite == null) return string.Empty;

            var catalog = CharacterPortraitCatalog.LoadDefault();
            if (catalog == null) return string.Empty;

            var index = catalog.IndexOfSprite(portraitSprite);
            return index >= 0
                ? NormalizePortraitId(catalog.GetPortraitId(index))
                : string.Empty;
        }

        private static string NormalizeName(string characterName)
        {
            return string.IsNullOrWhiteSpace(characterName)
                ? DefaultCharacterName
                : characterName.Trim();
        }

        private static string NormalizeProfileJson(string appearanceProfileJson)
        {
            return string.IsNullOrWhiteSpace(appearanceProfileJson)
                ? string.Empty
                : appearanceProfileJson.Trim();
        }

        private static string NormalizePortraitId(string portraitId)
        {
            return string.IsNullOrWhiteSpace(portraitId)
                ? string.Empty
                : portraitId.Trim();
        }
    }
}