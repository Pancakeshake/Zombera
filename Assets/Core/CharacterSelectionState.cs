using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    /// Runtime selection payload from character creator flow.
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
        public static string SelectedUmaRecipe { get; private set; }
        public static string SelectedAppearanceProfileJson { get; private set; }
        public static Texture2D SelectedPortraitTexture { get; private set; }
        public static Sprite SelectedPortraitSprite { get; private set; }

        static CharacterSelectionState()
        {
            ResetRuntimeToDefaults();
            LoadProfileDefaults();
        }

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
            string umaRecipe = null,
            string appearanceProfileJson = null)
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
            SelectedUmaRecipe = umaRecipe ?? string.Empty;
            SelectedAppearanceProfileJson = NormalizeProfileJson(appearanceProfileJson);
            HasSelection = true;

            SaveProfileDefaults(SelectedCharacterName, SelectedAppearancePresetIndex, SelectedAppearanceProfileJson);
        }

        public static void GetProfileDefaults(out string characterName, out int appearancePresetIndex)
        {
            GetProfileDefaults(out characterName, out appearancePresetIndex, out _);
        }

        public static void GetProfileDefaults(out string characterName, out int appearancePresetIndex, out string appearanceProfileJson)
        {
            characterName = NormalizeName(PlayerPrefs.GetString(ProfileCharacterNameKey, SelectedCharacterName));
            appearancePresetIndex = Mathf.Max(0, PlayerPrefs.GetInt(ProfileAppearancePresetKey, SelectedAppearancePresetIndex));
            appearanceProfileJson = NormalizeProfileJson(PlayerPrefs.GetString(ProfileAppearanceProfileJsonKey, SelectedAppearanceProfileJson));
        }

        public static void SetAppearanceProfileJson(string appearanceProfileJson, bool persistProfileDefaults = true)
        {
            SelectedAppearanceProfileJson = NormalizeProfileJson(appearanceProfileJson);

            if (!persistProfileDefaults)
            {
                return;
            }

            SaveProfileDefaults(SelectedCharacterName, SelectedAppearancePresetIndex, SelectedAppearanceProfileJson);
        }

        public static void ClearRuntimeSelection()
        {
            HasSelection = false;
            ResetRuntimeToDefaults();
            LoadProfileDefaults();
        }

        public static void SetPortraitTexture(Texture2D portraitTexture)
        {
            SelectedPortraitTexture = portraitTexture;

            if (portraitTexture == null)
            {
                SelectedPortraitSprite = null;
                return;
            }

            SelectedPortraitSprite = Sprite.Create(
                portraitTexture,
                new Rect(0f, 0f, portraitTexture.width, portraitTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        public static void SetPortraitSprite(Sprite portraitSprite)
        {
            SelectedPortraitSprite = portraitSprite;
            SelectedPortraitTexture = portraitSprite != null ? portraitSprite.texture : null;
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
            SelectedUmaRecipe = string.Empty;
            SelectedAppearanceProfileJson = string.Empty;
            SelectedPortraitTexture = null;
            SelectedPortraitSprite = null;
        }

        private static void LoadProfileDefaults()
        {
            if (PlayerPrefs.HasKey(ProfileCharacterNameKey))
            {
                SelectedCharacterName = NormalizeName(PlayerPrefs.GetString(ProfileCharacterNameKey, DefaultCharacterName));
            }

            if (PlayerPrefs.HasKey(ProfileAppearancePresetKey))
            {
                SelectedAppearancePresetIndex = Mathf.Max(0, PlayerPrefs.GetInt(ProfileAppearancePresetKey, 0));
            }

            if (PlayerPrefs.HasKey(ProfileAppearanceProfileJsonKey))
            {
                SelectedAppearanceProfileJson = NormalizeProfileJson(PlayerPrefs.GetString(ProfileAppearanceProfileJsonKey, string.Empty));
            }
        }

        private static void SaveProfileDefaults(string characterName, int appearancePresetIndex, string appearanceProfileJson)
        {
            PlayerPrefs.SetString(ProfileCharacterNameKey, NormalizeName(characterName));
            PlayerPrefs.SetInt(ProfileAppearancePresetKey, Mathf.Max(0, appearancePresetIndex));

            string normalizedProfileJson = NormalizeProfileJson(appearanceProfileJson);
            if (string.IsNullOrEmpty(normalizedProfileJson))
            {
                PlayerPrefs.DeleteKey(ProfileAppearanceProfileJsonKey);
            }
            else
            {
                PlayerPrefs.SetString(ProfileAppearanceProfileJsonKey, normalizedProfileJson);
            }

            PlayerPrefs.Save();
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
    }
}