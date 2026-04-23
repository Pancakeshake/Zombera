using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.UI.Menus.CharacterCreation;

namespace Zombera.UI.Menus
{
    /// <summary>
    /// Handles lightweight character creation panel interactions.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CharacterCreatorController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Fields")]
        [SerializeField] private TMP_InputField characterNameInput;
        [SerializeField] private TMP_Dropdown appearancePresetDropdown;

        [Header("Preview")]
        [SerializeField] private Image portraitPreviewImage;
        [SerializeField] private RawImage umaPreviewRawImage;
        [SerializeField] private TMP_Text flavorTextLabel;
        [SerializeField] private TMP_Text statsPreviewText;
        [SerializeField] private TMP_Text loadoutPreviewText;
        [SerializeField] private TMP_Text validationMessageText;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button randomNameButton;

        [Header("Validation")]
        [SerializeField] private string defaultCharacterName = "Survivor";
        [SerializeField, Min(1)] private int minimumNameLength = 3;
        [SerializeField, Min(1)] private int maximumNameLength = 16;

        [Header("Input UX")]
        [SerializeField] private Vector4 nameInputRaycastPadding = new Vector4(18f, 10f, 18f, 10f);

        [Header("UMA Preview Avatar")]
        [Tooltip("Optional DynamicCharacterAvatar in the preview scene. Recipe is saved to CharacterSelectionState on confirm.")]
        [SerializeField] private DynamicCharacterAvatar previewAvatar;

        [Header("Customization")]
        [SerializeField] private CharacterCreatorCustomizationController customizationController;

        [Header("Presets")]
        [SerializeField] private List<CharacterAppearancePreset> appearancePresets = new List<CharacterAppearancePreset>();

        [Header("Random Names")]
        [SerializeField] private List<string> randomNames = new List<string>
        {
            "Ash",
            "Mara",
            "Juno",
            "Rook",
            "Quinn",
            "Sable",
            "Nova",
            "Kade"
        };

        [Header("Presentation")]
        [SerializeField] private bool applyGameLikePresentation = true;
        [SerializeField] private string creatorHeaderTitle = string.Empty;
        [SerializeField] private string creatorHeaderSubtitle = string.Empty;
        [SerializeField] private string confirmButtonLabel = "CONFIRM";
        [SerializeField] private string closeButtonLabel = "BACK";
        [SerializeField] private Color inputTint = new Color(0.06f, 0.05f, 0.04f, 0.92f);
        [SerializeField] private Color confirmTint = new Color(0.40f, 0.12f, 0.07f, 0.98f);
        [SerializeField] private Color closeTint = new Color(0.11f, 0.11f, 0.11f, 0.96f);
        [SerializeField] private Color accentTint = new Color(0.82f, 0.64f, 0.36f, 0.98f);
        [SerializeField] private Color textTint = new Color(0.95f, 0.90f, 0.78f, 1f);
        [SerializeField] private Color buttonBorderTint = new Color(0.67f, 0.52f, 0.31f, 0.94f);

        [Header("Preview Quality")]
        [SerializeField] private bool useHighResolutionPreviewTexture = true;
        [SerializeField, Min(256)] private int highResolutionPreviewWidth = 1536;
        [SerializeField, Min(256)] private int highResolutionPreviewHeight = 1536;
        [SerializeField, Range(1, 8)] private int highResolutionPreviewMsaa = 1;

        private static Sprite runtimeSolidSprite;

        public bool IsInitialized { get; private set; }
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
        public string SelectedCharacterName { get; private set; }
        public int SelectedAppearancePresetIndex { get; private set; }

        public event Action<string> SelectionConfirmed;
        public event Action<bool> VisibilityChanged;

        private readonly StringBuilder stringBuilder = new StringBuilder(128);

        private Camera previewRenderCamera;
        private RenderTexture originalPreviewTexture;
        private RenderTexture highResolutionPreviewTexture;

        private void Awake()
        {
            // Deactivate the preview avatar before this frame's default-order Awake() calls run.
            // DynamicCharacterAvatar.Awake() unconditionally sets this.context = UMAContextBase.Instance,
            // which is scene-global and returns World's UMA_GLIB when both scenes are loaded.
            // With StartGuard, InitialStartup() only runs once, so if it fires with the wrong context
            // the avatar can never recover. Deactivating here ensures DCA.Awake() only fires later,
            // inside Show(), after EnsurePreviewAvatarGeneratorBoundToCurrentScene() has pinned the
            // correct scene-local context.
            EarlyDeactivatePreviewAvatar();
            ClearPreviewAvatarGeneratorReferences();
            Initialize();
        }

        private void EarlyDeactivatePreviewAvatar()
        {
            // Try serialized reference first.
            if (previewAvatar != null && previewAvatar.gameObject.scene == gameObject.scene)
            {
                previewAvatar.gameObject.SetActive(false);
                return;
            }

            // Serialized ref was null or stripped (cross-scene) — scan the scene.
            DynamicCharacterAvatar[] candidates = FindObjectsByType<DynamicCharacterAvatar>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (DynamicCharacterAvatar candidate in candidates)
            {
                if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                {
                    candidate.gameObject.SetActive(false);
                }
            }
        }

        private void OnEnable()
        {
            EnsureNameInputBackgroundImageVisibility();
            EnsureNameInputClickability();
            EnsureEditModeNameInputPresentation();
        }

        private void OnValidate()
        {
            EnsureNameInputBackgroundImageVisibility();

            if (previewAvatar == null)
            {
                return;
            }

            if (previewAvatar.gameObject.scene != gameObject.scene)
            {
                previewAvatar = null;
                return;
            }

            if (previewAvatar.context != null && previewAvatar.context.gameObject.scene != gameObject.scene)
            {
                previewAvatar.context = null;
            }

            if (previewAvatar.umaGenerator != null && previewAvatar.umaGenerator.gameObject.scene != gameObject.scene)
            {
                previewAvatar.umaGenerator = null;
            }

            if (previewAvatar.umaData != null &&
                previewAvatar.umaData.umaGenerator != null &&
                previewAvatar.umaData.umaGenerator.gameObject.scene != gameObject.scene)
            {
                previewAvatar.umaData.umaGenerator = null;
            }
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            AutoResolveReferences();
            EnsureNameInputClickability();
            EnsureDefaults();
            ApplyGameLikePresentation();

            BindButton(confirmButton, ConfirmSelection);
            BindButton(closeButton, Hide);
            BindButton(randomNameButton, ApplyRandomName);
            BindInputEvents();

            InitializePresetDropdown();
            ApplySavedSelectionToFields();
            RefreshPreview();
            InitializeCustomizationController();

            Hide();
            IsInitialized = true;
        }

        public void BakeRuntimePresentationToScene()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            AutoResolveReferences();
            EnsureDefaults();
            ApplyGameLikePresentation();
            InitializePresetDropdown();
            ApplySavedSelectionToFields();
            RefreshPreview();
            InitializeCustomizationController();
        }

        [ContextMenu("Bake Runtime Presentation To Scene")]
        private void BakeRuntimePresentationFromContextMenu()
        {
            BakeRuntimePresentationToScene();
        }

        private void OnDestroy()
        {
            ClearPreviewAvatarGeneratorReferences();

            if (characterNameInput != null)
            {
                characterNameInput.onValueChanged.RemoveListener(HandleCharacterNameChanged);
            }

            if (appearancePresetDropdown != null)
            {
                appearancePresetDropdown.onValueChanged.RemoveListener(HandleAppearancePresetChanged);
            }

            RestorePreviewTextureForSelector();
        }

        private void OnDisable()
        {
            // Clear generator links so preview avatars never retain references to generators from other scenes.
            ClearPreviewAvatarGeneratorReferences();
        }

        public void Show()
        {
            EnsurePreviewAvatarGeneratorBoundToCurrentScene();
            SetPreviewAvatarVisible(true);
            // Awake() on DynamicCharacterAvatar overwrites context with the global singleton
            // which may point to another scene. Re-pin immediately after activation.
            EnsurePreviewAvatarGeneratorBoundToCurrentScene();
            ApplySavedSelectionToFields();
            RestoreSavedAppearanceOnPreview();
            EnsureNameInputClickability();
            RefreshPreview();
            SetPanelVisible(true);
        }

        public void Hide()
        {
            SetPanelVisible(false);
        }

        public void PrepareForSceneTransition()
        {
            if (previewAvatar == null || previewAvatar.gameObject.scene != gameObject.scene)
            {
                return;
            }

            ClearPreviewAvatarGeneratorReferences();

            // Disable preview avatar before world load to avoid editor cross-scene serialization warnings.
            previewAvatar.gameObject.SetActive(false);
        }

        public void ConfirmSelection()
        {
            CharacterAppearancePreset preset = GetCurrentPreset();

            if (preset == null)
            {
                SetValidationMessage("Add at least one appearance preset before confirming.");
                return;
            }

            string normalizedName = NormalizeName(characterNameInput != null ? characterNameInput.text : SelectedCharacterName);

            if (!TryValidateCharacterName(normalizedName, out string validationMessage))
            {
                SetValidationMessage(validationMessage);

                if (confirmButton != null)
                {
                    confirmButton.interactable = false;
                }

                return;
            }

            SelectedCharacterName = normalizedName;
            SelectedAppearancePresetIndex = ResolveSelectedPresetIndex();

            string umaRecipe = previewAvatar != null ? previewAvatar.GetCurrentRecipe() : string.Empty;
            string appearanceProfileJson = CaptureAppearanceProfileJson();

            CharacterSelectionState.SetSelection(
                SelectedCharacterName,
                SelectedAppearancePresetIndex,
                preset.maxHealth,
                preset.damage,
                preset.moveSpeed,
                preset.stamina,
                preset.carryCapacity,
                preset.flavorText,
                BuildLoadoutPreviewText(preset),
                umaRecipe,
                appearanceProfileJson);

            Texture2D capturedPortrait = CapturePortraitTextureFromPreview();
            if (capturedPortrait != null)
            {
                CharacterSelectionState.SetPortraitTexture(capturedPortrait);
            }
            else
            {
                CharacterSelectionState.SetPortraitSprite(preset.portrait);
            }

            SelectionConfirmed?.Invoke(SelectedCharacterName);
            Hide();
        }

        private string CaptureAppearanceProfileJson()
        {
            if (customizationController != null)
            {
                customizationController.SetPreviewAvatar(previewAvatar);
                string profileJson = customizationController.CaptureProfileJson();

                if (!string.IsNullOrWhiteSpace(profileJson))
                {
                    return profileJson;
                }
            }

            if (previewAvatar == null)
            {
                return CharacterSelectionState.SelectedAppearanceProfileJson;
            }

            UmaAppearanceOperationReport report = UmaAppearanceService.TryCaptureProfile(previewAvatar, out CharacterAppearanceProfile profile);
            if (!report.Success)
            {
                Debug.LogWarning("[CharacterCreatorController] Failed to capture appearance profile. " + report.ToMultilineString(), this);
                return CharacterSelectionState.SelectedAppearanceProfileJson;
            }

            if (report.HasWarnings)
            {
                Debug.Log("[CharacterCreatorController] Appearance capture warnings: " + report.ToMultilineString(), this);
            }

            return CharacterAppearanceProfile.Serialize(profile);
        }

        private void RestoreSavedAppearanceOnPreview()
        {
            if (customizationController != null)
            {
                customizationController.SetPreviewAvatar(previewAvatar);
                customizationController.ApplySavedProfile();
                return;
            }

            if (previewAvatar == null)
            {
                return;
            }

            string appearanceProfileJson = CharacterSelectionState.HasSelection
                ? CharacterSelectionState.SelectedAppearanceProfileJson
                : string.Empty;

            if (string.IsNullOrWhiteSpace(appearanceProfileJson))
            {
                CharacterSelectionState.GetProfileDefaults(out _, out _, out appearanceProfileJson);
            }

            if (string.IsNullOrWhiteSpace(appearanceProfileJson))
            {
                return;
            }

            CharacterAppearanceProfile profile = CharacterAppearanceProfile.Deserialize(appearanceProfileJson);
            UmaAppearanceOperationReport report = UmaAppearanceService.TryApplyProfile(previewAvatar, profile, true);

            if (!report.Success)
            {
                Debug.LogWarning("[CharacterCreatorController] Failed to restore appearance profile on preview avatar. " + report.ToMultilineString(), this);
                return;
            }

            if (report.HasWarnings)
            {
                Debug.Log("[CharacterCreatorController] Appearance restore warnings: " + report.ToMultilineString(), this);
            }
        }

        private Texture2D CapturePortraitTextureFromPreview()
        {
            if (umaPreviewRawImage == null || umaPreviewRawImage.texture == null)
            {
                return null;
            }

            Texture previewTexture = umaPreviewRawImage.texture;

            Texture2D readableTexture;
            if (previewTexture is RenderTexture previewRenderTexture)
            {
                readableTexture = ReadRenderTexture(previewRenderTexture);
            }
            else if (previewTexture is Texture2D previewTexture2D)
            {
                readableTexture = CopyTexture2D(previewTexture2D);
            }
            else
            {
                return null;
            }

            if (readableTexture == null)
            {
                return null;
            }

            return CropFaceSquare(readableTexture);
        }

        private static Texture2D ReadRenderTexture(RenderTexture source)
        {
            if (source == null)
            {
                return null;
            }

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = source;

            Texture2D output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            output.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
            output.Apply(false, false);

            RenderTexture.active = previousActive;
            return output;
        }

        private static Texture2D CopyTexture2D(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            RenderTexture temp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, temp);

            Texture2D output = ReadRenderTexture(temp);
            RenderTexture.ReleaseTemporary(temp);
            return output;
        }

        private static Texture2D CropFaceSquare(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            int baseSize = Mathf.Min(source.width, source.height);
            int cropSize = Mathf.Clamp(Mathf.RoundToInt(baseSize * 0.58f), 32, baseSize);
            if (cropSize <= 0)
            {
                return null;
            }

            int centerX = source.width / 2;
            int centerY = Mathf.RoundToInt(source.height * 0.68f);
            int cropX = Mathf.Clamp(centerX - (cropSize / 2), 0, Mathf.Max(0, source.width - cropSize));
            int cropY = Mathf.Clamp(centerY - (cropSize / 2), 0, Mathf.Max(0, source.height - cropSize));

            Color[] pixels = source.GetPixels(cropX, cropY, cropSize, cropSize);
            Texture2D cropped = new Texture2D(cropSize, cropSize, TextureFormat.RGBA32, false);
            cropped.SetPixels(pixels);
            cropped.Apply(false, false);
            return cropped;
        }

        private void ApplyRandomName()
        {
            if (characterNameInput == null || randomNames == null || randomNames.Count == 0)
            {
                return;
            }

            int randomIndex = UnityEngine.Random.Range(0, randomNames.Count);
            string randomName = NormalizeName(randomNames[randomIndex]);
            characterNameInput.SetTextWithoutNotify(randomName);
            RefreshPreview();
        }

        private void SetPanelVisible(bool isVisible)
        {
            if (isVisible)
            {
                EnsurePreviewAvatarGeneratorBoundToCurrentScene();
                ApplyHighResolutionPreviewTextureForSelector();
            }
            else
            {
                RestorePreviewTextureForSelector();
                ClearPreviewAvatarGeneratorReferences();
            }

            SetPreviewAvatarVisible(isVisible);

            bool wasVisible = IsVisible;

            if (panelRoot != null)
            {
                panelRoot.SetActive(isVisible);
            }

            if (wasVisible != isVisible)
            {
                VisibilityChanged?.Invoke(isVisible);
            }
        }

        private void SetPreviewAvatarVisible(bool isVisible)
        {
            if (previewAvatar == null)
            {
                return;
            }

            if (previewAvatar.gameObject.scene != gameObject.scene)
            {
                return;
            }

            if (previewAvatar.gameObject.activeSelf != isVisible)
            {
                previewAvatar.gameObject.SetActive(isVisible);
            }
        }

        private void EnsurePreviewAvatarGeneratorBoundToCurrentScene()
        {
            if (previewAvatar == null || previewAvatar.gameObject.scene != gameObject.scene)
            {
                return;
            }

            // Pin context to this scene so the avatar doesn't pick up UMAContextBase.Instance
            // from another additively-loaded scene (e.g. the World scene in the editor).
            UMAContextBase localContext = FindUmaContextInCurrentScene();
            previewAvatar.context = localContext;

            UMAGeneratorBase generator = FindUmaGeneratorInCurrentScene();
            if (generator == null)
            {
                previewAvatar.umaGenerator = null;
                if (previewAvatar.umaData != null)
                {
                    previewAvatar.umaData.umaGenerator = null;
                }
                return;
            }

            previewAvatar.umaGenerator = generator;

            if (previewAvatar.umaData != null)
            {
                previewAvatar.umaData.umaGenerator = generator;
            }
        }

        private void ClearPreviewAvatarGeneratorReferences()
        {
            if (previewAvatar == null || previewAvatar.gameObject.scene != gameObject.scene)
            {
                return;
            }

            previewAvatar.context = null;
            previewAvatar.umaGenerator = null;

            if (previewAvatar.umaData != null)
            {
                previewAvatar.umaData.umaGenerator = null;
            }
        }

        private UMAContextBase FindUmaContextInCurrentScene()
        {
            UMAContextBase[] contexts = FindObjectsByType<UMAContextBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            UMAContextBase fallback = null;

            for (int index = 0; index < contexts.Length; index++)
            {
                UMAContextBase candidate = contexts[index];

                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }

                if (candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private UMAGeneratorBase FindUmaGeneratorInCurrentScene()
        {
            UMAGeneratorBase[] generators = FindObjectsByType<UMAGeneratorBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            UMAGeneratorBase fallback = null;

            for (int index = 0; index < generators.Length; index++)
            {
                UMAGeneratorBase candidate = generators[index];

                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }

                if (candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private void BindInputEvents()
        {
            if (characterNameInput != null)
            {
                characterNameInput.characterLimit = Mathf.Max(minimumNameLength, maximumNameLength);
                characterNameInput.onValueChanged.AddListener(HandleCharacterNameChanged);
            }

            if (appearancePresetDropdown != null)
            {
                appearancePresetDropdown.onValueChanged.AddListener(HandleAppearancePresetChanged);
            }
        }

        private void HandleCharacterNameChanged(string _)
        {
            RefreshPreview();
        }

        private void HandleAppearancePresetChanged(int _)
        {
            RefreshPreview();
        }

        private void ApplySavedSelectionToFields()
        {
            CharacterSelectionState.GetProfileDefaults(out _, out int savedPresetIndex);

            if (CharacterSelectionState.HasSelection)
            {
                savedPresetIndex = CharacterSelectionState.SelectedAppearancePresetIndex;
            }

            // Keep preset selection, but force the user to enter a fresh name each time.
            SelectedCharacterName = string.Empty;
            SelectedAppearancePresetIndex = ClampPresetIndex(savedPresetIndex);

            if (characterNameInput != null)
            {
                characterNameInput.SetTextWithoutNotify(string.Empty);
            }

            if (appearancePresetDropdown != null && appearancePresetDropdown.options.Count > 0)
            {
                appearancePresetDropdown.SetValueWithoutNotify(SelectedAppearancePresetIndex);
            }
        }

        private void RefreshPreview()
        {
            CharacterAppearancePreset preset = GetCurrentPreset();
            string normalizedName = NormalizeName(characterNameInput != null ? characterNameInput.text : SelectedCharacterName);

            bool isNameValid = TryValidateCharacterName(normalizedName, out string validationMessage);

            if (confirmButton != null)
            {
                confirmButton.interactable = isNameValid && preset != null;
            }

            SetValidationMessage(validationMessage);

            if (portraitPreviewImage != null)
            {
                portraitPreviewImage.sprite = preset != null ? preset.portrait : null;
                portraitPreviewImage.enabled = portraitPreviewImage.sprite != null;
            }

            if (flavorTextLabel != null)
            {
                flavorTextLabel.text = preset != null ? preset.flavorText : string.Empty;
            }

            if (statsPreviewText != null)
            {
                statsPreviewText.text = BuildStatsPreviewText(preset);
            }

            if (loadoutPreviewText != null)
            {
                loadoutPreviewText.text = BuildLoadoutPreviewText(preset);
            }
        }

        private void SetValidationMessage(string message)
        {
            if (validationMessageText == null)
            {
                return;
            }

            validationMessageText.text = string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message;
        }

        private string BuildStatsPreviewText(CharacterAppearancePreset preset)
        {
            if (preset == null)
            {
                return "HP: --  DMG: --  SPD: --\nSTA: --  CARRY: --";
            }

            stringBuilder.Clear();
            stringBuilder.Append("HP: ").Append(Mathf.RoundToInt(preset.maxHealth));
            stringBuilder.Append("  DMG: ").Append(Mathf.RoundToInt(preset.damage));
            stringBuilder.Append("  SPD: ").Append(preset.moveSpeed.ToString("0.0"));
            stringBuilder.Append('\n');
            stringBuilder.Append("STA: ").Append(Mathf.RoundToInt(preset.stamina));
            stringBuilder.Append("  CARRY: ").Append(Mathf.RoundToInt(preset.carryCapacity));
            return stringBuilder.ToString();
        }

        private string BuildLoadoutPreviewText(CharacterAppearancePreset preset)
        {
            if (preset == null || preset.startingLoadout == null || preset.startingLoadout.Count == 0)
            {
                return "Starting Loadout: None";
            }

            stringBuilder.Clear();
            stringBuilder.Append("Starting Loadout:");

            bool hasAnyEntry = false;

            for (int index = 0; index < preset.startingLoadout.Count; index++)
            {
                CharacterLoadoutEntry entry = preset.startingLoadout[index];

                if (string.IsNullOrWhiteSpace(entry.itemName) || entry.quantity <= 0)
                {
                    continue;
                }

                hasAnyEntry = true;
                stringBuilder.Append('\n');
                stringBuilder.Append(entry.itemName.Trim());
                stringBuilder.Append(" x").Append(entry.quantity);
            }

            if (!hasAnyEntry)
            {
                return "Starting Loadout: None";
            }

            return stringBuilder.ToString();
        }

        private bool TryValidateCharacterName(string candidateName, out string validationMessage)
        {
            string trimmed = string.IsNullOrWhiteSpace(candidateName)
                ? string.Empty
                : candidateName.Trim();

            if (trimmed.Length == 0)
            {
                validationMessage = "Name is required.";
                return false;
            }

            if (trimmed.Length < minimumNameLength)
            {
                validationMessage = $"Name must be at least {minimumNameLength} characters.";
                return false;
            }

            if (trimmed.Length > maximumNameLength)
            {
                validationMessage = $"Name must be {maximumNameLength} characters or fewer.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        private void InitializePresetDropdown()
        {
            if (appearancePresetDropdown == null)
            {
                return;
            }

            appearancePresetDropdown.ClearOptions();

            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(appearancePresets.Count);

            for (int index = 0; index < appearancePresets.Count; index++)
            {
                CharacterAppearancePreset preset = appearancePresets[index];
                string displayName = preset != null && !string.IsNullOrWhiteSpace(preset.displayName)
                    ? preset.displayName.Trim()
                    : $"Preset {index + 1}";

                options.Add(new TMP_Dropdown.OptionData(displayName));
            }

            appearancePresetDropdown.AddOptions(options);
            appearancePresetDropdown.SetValueWithoutNotify(ClampPresetIndex(SelectedAppearancePresetIndex));
        }

        private CharacterAppearancePreset GetCurrentPreset()
        {
            if (appearancePresets == null || appearancePresets.Count == 0)
            {
                return null;
            }

            int selectedIndex = ResolveSelectedPresetIndex();

            if (selectedIndex < 0 || selectedIndex >= appearancePresets.Count)
            {
                return null;
            }

            return appearancePresets[selectedIndex];
        }

        private int ResolveSelectedPresetIndex()
        {
            if (appearancePresets == null || appearancePresets.Count == 0)
            {
                return 0;
            }

            int dropdownIndex = appearancePresetDropdown != null
                ? appearancePresetDropdown.value
                : SelectedAppearancePresetIndex;

            return ClampPresetIndex(dropdownIndex);
        }

        private int ClampPresetIndex(int index)
        {
            if (appearancePresets == null || appearancePresets.Count == 0)
            {
                return 0;
            }

            return Mathf.Clamp(index, 0, appearancePresets.Count - 1);
        }

        private void EnsureDefaults()
        {
            minimumNameLength = Mathf.Max(1, minimumNameLength);
            maximumNameLength = Mathf.Max(minimumNameLength, maximumNameLength);

            if (string.IsNullOrWhiteSpace(defaultCharacterName))
            {
                defaultCharacterName = "Survivor";
            }

            if (appearancePresets == null)
            {
                appearancePresets = new List<CharacterAppearancePreset>();
            }

            if (appearancePresets.Count == 0)
            {
                appearancePresets.Add(CharacterAppearancePreset.Create(
                    "Drifter",
                    "A steady survivor who can adapt to most situations.",
                    100000f,
                    10f,
                    4f,
                    100f,
                    35f,
                    new CharacterLoadoutEntry("Pistol", 1),
                    new CharacterLoadoutEntry("9mm Ammo", 10),
                    new CharacterLoadoutEntry("Bandage", 1),
                    new CharacterLoadoutEntry("Food", 1)));

                appearancePresets.Add(CharacterAppearancePreset.Create(
                    "Runner",
                    "Moves quickly and keeps stamina high while carrying less.",
                    100000f,
                    9f,
                    4.8f,
                    120f,
                    28f,
                    new CharacterLoadoutEntry("Knife", 1),
                    new CharacterLoadoutEntry("Bandage", 2),
                    new CharacterLoadoutEntry("Food", 1)));

                appearancePresets.Add(CharacterAppearancePreset.Create(
                    "Bruiser",
                    "Hits harder and carries more, but moves slower.",
                    100000f,
                    13f,
                    3.5f,
                    90f,
                    42f,
                    new CharacterLoadoutEntry("Bat", 1),
                    new CharacterLoadoutEntry("Pistol", 1),
                    new CharacterLoadoutEntry("9mm Ammo", 8)));
            }

            for (int index = 0; index < appearancePresets.Count; index++)
            {
                CharacterAppearancePreset preset = appearancePresets[index];
                if (preset == null)
                {
                    continue;
                }

                preset.Sanitize(index + 1);
                preset.maxHealth = 100000f;
            }

            if (randomNames == null || randomNames.Count == 0)
            {
                randomNames = new List<string>
                {
                    "Ash",
                    "Mara",
                    "Juno",
                    "Rook",
                    "Quinn",
                    "Sable",
                    "Nova",
                    "Kade"
                };
            }
        }

        private string NormalizeName(string candidateName)
        {
            string trimmed = string.IsNullOrWhiteSpace(candidateName)
                ? string.Empty
                : candidateName.Trim();

            if (trimmed.Length > maximumNameLength)
            {
                trimmed = trimmed.Substring(0, maximumNameLength);
            }

            return trimmed;
        }

        private void ApplyHighResolutionPreviewTextureForSelector()
        {
            if (!useHighResolutionPreviewTexture)
            {
                return;
            }

            if (!TryResolvePreviewRenderTarget(out Camera camera, out RenderTexture currentTexture))
            {
                return;
            }

            if (camera == null)
            {
                return;
            }

            if (originalPreviewTexture == null)
            {
                originalPreviewTexture = currentTexture != null
                    ? currentTexture
                    : camera.targetTexture;
            }

            int targetWidth = Mathf.Max(256, highResolutionPreviewWidth);
            int targetHeight = Mathf.Max(256, highResolutionPreviewHeight);

            bool needsRecreate = highResolutionPreviewTexture == null ||
                                 highResolutionPreviewTexture.width != targetWidth ||
                                 highResolutionPreviewTexture.height != targetHeight;

            if (needsRecreate)
            {
                ReleaseHighResolutionPreviewTexture();
                highResolutionPreviewTexture = CreateHighResolutionPreviewTexture(targetWidth, targetHeight, originalPreviewTexture);
            }

            if (highResolutionPreviewTexture == null)
            {
                return;
            }

            previewRenderCamera = camera;
            previewRenderCamera.targetTexture = highResolutionPreviewTexture;

            if (umaPreviewRawImage != null)
            {
                umaPreviewRawImage.texture = highResolutionPreviewTexture;
            }
        }

        private void RestorePreviewTextureForSelector()
        {
            if (previewRenderCamera != null)
            {
                previewRenderCamera.targetTexture = originalPreviewTexture;
            }

            if (umaPreviewRawImage != null && originalPreviewTexture != null)
            {
                umaPreviewRawImage.texture = originalPreviewTexture;
            }

            ReleaseHighResolutionPreviewTexture();
            previewRenderCamera = null;
            originalPreviewTexture = null;
        }

        private void ReleaseHighResolutionPreviewTexture()
        {
            if (highResolutionPreviewTexture == null)
            {
                return;
            }

            if (highResolutionPreviewTexture.IsCreated())
            {
                highResolutionPreviewTexture.Release();
            }

            if (Application.isPlaying)
            {
                Destroy(highResolutionPreviewTexture);
            }
            else
            {
                DestroyImmediate(highResolutionPreviewTexture);
            }

            highResolutionPreviewTexture = null;
        }

        private RenderTexture CreateHighResolutionPreviewTexture(int width, int height, RenderTexture sourceTexture)
        {
            RenderTextureDescriptor descriptor;

            if (sourceTexture != null)
            {
                descriptor = sourceTexture.descriptor;
            }
            else
            {
                descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24);
            }

            descriptor.width = width;
            descriptor.height = height;
            descriptor.msaaSamples = Mathf.Clamp(highResolutionPreviewMsaa, 1, 8);
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;

            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = "RT_UMA_Preview_HighResRuntime",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            texture.Create();
            return texture;
        }

        private bool TryResolvePreviewRenderTarget(out Camera camera, out RenderTexture texture)
        {
            camera = null;
            texture = umaPreviewRawImage != null ? umaPreviewRawImage.texture as RenderTexture : null;

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (texture != null && candidate.targetTexture == texture)
                {
                    camera = candidate;
                    return true;
                }
            }

            Camera fallback = null;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (candidate.targetTexture == null)
                {
                    continue;
                }

                string lowerName = candidate.name.ToLowerInvariant();
                if (lowerName.Contains("preview") || lowerName.Contains("uma"))
                {
                    camera = candidate;
                    texture = candidate.targetTexture;
                    return true;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }
            }

            if (fallback == null)
            {
                return false;
            }

            camera = fallback;
            texture = fallback.targetTexture;
            return true;
        }

        private void ApplyGameLikePresentation()
        {
            if (!applyGameLikePresentation || panelRoot == null)
            {
                return;
            }

            RectTransform panelRect = panelRoot.transform as RectTransform;

            if (panelRect == null)
            {
                return;
            }

            RemoveDecorativeLayers(panelRect);

            RectTransform headerRect = FindOrCreateRectChild(panelRect, "CreatorHeaderContainer");
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, 24f);
            headerRect.anchoredPosition = new Vector2(0f, -2f);

            EnsureHeaderText(headerRect);
            LayoutActionButtons();
            StyleNameInput(applyLayout: true);
            StyleButton(confirmButton, confirmButtonLabel, confirmTint, true);
            StyleButton(closeButton, closeButtonLabel, closeTint, false);

            if (randomNameButton != null)
            {
                StyleButton(randomNameButton, "RANDOM", new Color(0.24f, 0.24f, 0.24f, 0.95f), false);
            }

            if (flavorTextLabel != null)
            {
                flavorTextLabel.color = textTint;
                flavorTextLabel.fontStyle = FontStyles.Italic;
            }

            if (statsPreviewText != null)
            {
                statsPreviewText.color = Color.Lerp(textTint, accentTint, 0.25f);
            }

            if (loadoutPreviewText != null)
            {
                loadoutPreviewText.color = Color.Lerp(textTint, accentTint, 0.20f);
            }

            if (validationMessageText != null)
            {
                validationMessageText.color = new Color(0.92f, 0.46f, 0.40f, 1f);
            }
        }

        private static void RemoveDecorativeLayers(RectTransform panelRect)
        {
            RemoveDecorativeLayer(panelRect, "CreatorOverlay");
            RemoveDecorativeLayer(panelRect, "CreatorTopStrip");
            RemoveDecorativeLayer(panelRect, "CreatorBottomStrip");
            RemoveDecorativeLayer(panelRect, "CreatorFrameTop");
            RemoveDecorativeLayer(panelRect, "CreatorFrameBottom");
            RemoveDecorativeLayer(panelRect, "CreatorFrameLeft");
            RemoveDecorativeLayer(panelRect, "CreatorFrameRight");
            RemoveDecorativeLayer(panelRect, "CreatorFrameTopAccent");
            RemoveDecorativeLayer(panelRect, "CreatorFrameBottomAccent");
            RemoveDecorativeLayer(panelRect, "CreatorVignetteTop");
            RemoveDecorativeLayer(panelRect, "CreatorVignetteBottom");
            RemoveDecorativeLayer(panelRect, "CreatorVignetteLeft");
            RemoveDecorativeLayer(panelRect, "CreatorVignetteRight");
            RemoveDecorativeLayer(panelRect, "CreatorSubtitlePlate");
        }

        private static void RemoveDecorativeLayer(RectTransform panelRect, string objectName)
        {
            Transform layer = panelRect.Find(objectName);
            if (layer == null)
            {
                return;
            }

            layer.gameObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(layer.gameObject);
                return;
            }

            DestroyImmediate(layer.gameObject);
        }

        private void LayoutActionButtons()
        {
            PlaceActionButton(confirmButton, true);
            PlaceActionButton(closeButton, false);

            RectTransform randomRect = randomNameButton != null ? randomNameButton.transform as RectTransform : null;
            if (!ParentUsesLayout(randomRect) && randomRect != null)
            {
                randomRect.anchorMin = new Vector2(0.79f, 0.72f);
                randomRect.anchorMax = new Vector2(0.94f, 0.80f);
                randomRect.offsetMin = Vector2.zero;
                randomRect.offsetMax = Vector2.zero;
            }
        }

        private static void PlaceActionButton(Button button, bool placeLeft)
        {
            RectTransform rect = button != null ? button.transform as RectTransform : null;

            if (ParentUsesLayout(rect) || rect == null)
            {
                return;
            }

            rect.anchorMin = placeLeft ? new Vector2(0.16f, 0.04f) : new Vector2(0.52f, 0.04f);
            rect.anchorMax = placeLeft ? new Vector2(0.46f, 0.12f) : new Vector2(0.82f, 0.12f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool ParentUsesLayout(RectTransform rect)
        {
            if (rect == null || rect.parent == null)
            {
                return false;
            }

            return rect.parent.GetComponent<LayoutGroup>() != null;
        }

        private void EnsureHeaderText(RectTransform headerRect)
        {
            TMP_Text title = FindOrCreateText(headerRect, "CreatorHeaderTitle", 70f, FontStyles.Bold);
            title.gameObject.SetActive(false);

            TMP_Text subtitle = FindOrCreateText(headerRect, "CreatorHeaderSubtitle", 23f, FontStyles.Bold);
            subtitle.gameObject.SetActive(false);
        }

        private Image ResolveNameInputImage()
        {
            if (characterNameInput == null)
            {
                characterNameInput = GetComponentInChildren<TMP_InputField>(true);
            }

            if (characterNameInput == null)
            {
                return null;
            }

            Image inputImage = characterNameInput.targetGraphic as Image;
            if (inputImage == null)
            {
                inputImage = characterNameInput.GetComponent<Image>();
            }

            return inputImage;
        }

        private void EnsureNameInputBackgroundImageVisibility()
        {
            Image inputImage = ResolveNameInputImage();
            if (inputImage == null)
            {
                return;
            }

            bool hasCustomBackgroundSprite = inputImage.sprite != null && inputImage.sprite != runtimeSolidSprite;
            if (!hasCustomBackgroundSprite)
            {
                if (!applyGameLikePresentation)
                {
                    return;
                }

                inputImage.sprite = GetSolidSprite();
                inputImage.type = Image.Type.Sliced;
                inputImage.color = inputTint;
                return;
            }

            inputImage.type = Image.Type.Simple;
            inputImage.material = null;

            if (inputImage.color.maxColorComponent < 0.25f)
            {
                inputImage.color = Color.white;
            }
        }

        private void StyleNameInput(bool applyLayout)
        {
            if (characterNameInput == null)
            {
                return;
            }

            EnsureNameInputClickability();

            Image inputImage = ResolveNameInputImage();

            if (inputImage != null)
            {
                EnsureNameInputBackgroundImageVisibility();

                Outline outline = GetOrAddComponent<Outline>(inputImage.gameObject);
                outline.effectColor = new Color(buttonBorderTint.r, buttonBorderTint.g, buttonBorderTint.b, 0.60f);
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;

                Shadow shadow = GetOrAddComponent<Shadow>(inputImage.gameObject);
                shadow.effectColor = new Color(0f, 0f, 0f, 0.52f);
                shadow.effectDistance = new Vector2(0f, -4f);
                shadow.useGraphicAlpha = true;
            }

            if (characterNameInput.textComponent != null)
            {
                characterNameInput.textComponent.color = textTint;
                characterNameInput.textComponent.fontStyle = FontStyles.Bold;
                characterNameInput.textComponent.characterSpacing = 1.6f;
                characterNameInput.textComponent.fontSize = Mathf.Max(28f, characterNameInput.textComponent.fontSize);
            }

            if (characterNameInput.placeholder is TMP_Text placeholder)
            {
                placeholder.color = new Color(textTint.r, textTint.g, textTint.b, 0.45f);
                placeholder.fontStyle = FontStyles.Italic;
            }

            RectTransform inputRect = characterNameInput.transform as RectTransform;
            if (!applyLayout || inputRect == null)
            {
                return;
            }

            if (inputRect != null)
            {
                if (!ParentUsesLayout(inputRect))
                {
                    inputRect.anchorMin = new Vector2(0.23f, 0.75f);
                    inputRect.anchorMax = new Vector2(0.77f, 0.75f);
                    inputRect.sizeDelta = new Vector2(0f, 56f);
                    inputRect.anchoredPosition = Vector2.zero;
                }
                else
                {
                    inputRect.sizeDelta = new Vector2(Mathf.Max(460f, inputRect.sizeDelta.x), Mathf.Max(56f, inputRect.sizeDelta.y));
                }
            }
        }

        private void EnsureNameInputClickability()
        {
            Image inputImage = ResolveNameInputImage();
            if (inputImage != null)
            {
                inputImage.raycastTarget = true;
                inputImage.raycastPadding = new Vector4(
                    Mathf.Max(0f, nameInputRaycastPadding.x),
                    Mathf.Max(0f, nameInputRaycastPadding.y),
                    Mathf.Max(0f, nameInputRaycastPadding.z),
                    Mathf.Max(0f, nameInputRaycastPadding.w));
            }

            if (umaPreviewRawImage != null)
            {
                umaPreviewRawImage.raycastTarget = false;
            }
        }

        private void EnsureEditModeNameInputPresentation()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (!applyGameLikePresentation)
            {
                return;
            }

            if (characterNameInput == null)
            {
                characterNameInput = GetComponentInChildren<TMP_InputField>(true);
            }

            if (characterNameInput == null)
            {
                return;
            }

            StyleNameInput(applyLayout: false);
        }

        private void StyleButton(Button button, string labelText, Color baseColor, bool emphasizePrimary)
        {
            if (button == null)
            {
                return;
            }

            Image buttonImage = button.targetGraphic as Image;
            if (buttonImage == null)
            {
                buttonImage = button.GetComponent<Image>();
            }

            if (buttonImage != null)
            {
                buttonImage.sprite = GetSolidSprite();
                buttonImage.type = Image.Type.Sliced;
                buttonImage.color = baseColor;

                Outline border = GetOrAddComponent<Outline>(buttonImage.gameObject);
                border.effectColor = new Color(buttonBorderTint.r, buttonBorderTint.g, buttonBorderTint.b, emphasizePrimary ? 0.76f : 0.58f);
                border.effectDistance = emphasizePrimary ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
                border.useGraphicAlpha = true;

                Shadow shadow = GetOrAddComponent<Shadow>(buttonImage.gameObject);
                shadow.effectColor = new Color(0f, 0f, 0f, emphasizePrimary ? 0.68f : 0.54f);
                shadow.effectDistance = emphasizePrimary ? new Vector2(0f, -6f) : new Vector2(0f, -4f);
                shadow.useGraphicAlpha = true;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, accentTint, 0.22f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.28f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            RectTransform buttonRect = button.transform as RectTransform;
            if (buttonRect != null && ParentUsesLayout(buttonRect))
            {
                float minWidth = emphasizePrimary ? 300f : 280f;
                float minHeight = emphasizePrimary ? 74f : 70f;
                buttonRect.sizeDelta = new Vector2(Mathf.Max(minWidth, buttonRect.sizeDelta.x), Mathf.Max(minHeight, buttonRect.sizeDelta.y));
            }

            TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                if (!string.IsNullOrWhiteSpace(labelText))
                {
                    tmpLabel.text = labelText.ToUpperInvariant();
                }

                tmpLabel.color = textTint;
                tmpLabel.fontStyle = FontStyles.Bold;
                tmpLabel.characterSpacing = emphasizePrimary ? 2.6f : 2.0f;
                tmpLabel.fontSize = Mathf.Max(emphasizePrimary ? 36f : 34f, tmpLabel.fontSize);
                tmpLabel.alignment = TextAlignmentOptions.Center;
                return;
            }

            UnityEngine.UI.Text legacyLabel = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (legacyLabel != null)
            {
                if (!string.IsNullOrWhiteSpace(labelText))
                {
                    legacyLabel.text = labelText.ToUpperInvariant();
                }

                legacyLabel.color = textTint;
                legacyLabel.fontStyle = FontStyle.Bold;
                legacyLabel.fontSize = Mathf.Max(emphasizePrimary ? 30 : 26, legacyLabel.fontSize);
                legacyLabel.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static TMP_Text FindOrCreateText(RectTransform parent, string objectName, float fontSize, FontStyles fontStyle)
        {
            Transform existing = parent.Find(objectName);
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;

            if (text == null)
            {
                GameObject textObject = new GameObject(objectName);
                textObject.transform.SetParent(parent, false);
                text = textObject.AddComponent<TextMeshProUGUI>();
            }

            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            return text;
        }

        private static RectTransform FindOrCreateRectChild(RectTransform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);

            if (existing is RectTransform existingRect)
            {
                return existingRect;
            }

            GameObject child = new GameObject(objectName, typeof(RectTransform));
            RectTransform childRect = child.GetComponent<RectTransform>();
            childRect.SetParent(parent, false);
            return childRect;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();

            if (component != null)
            {
                return component;
            }

            return target.AddComponent<T>();
        }

        private static Sprite GetSolidSprite()
        {
            if (runtimeSolidSprite != null)
            {
                return runtimeSolidSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;

            runtimeSolidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            runtimeSolidSprite.hideFlags = HideFlags.HideAndDontSave;
            return runtimeSolidSprite;
        }

        private void InitializeCustomizationController()
        {
            if (customizationController == null)
            {
                return;
            }

            customizationController.SetPreviewAvatar(previewAvatar);
            customizationController.Initialize(previewAvatar);
        }

        private void AutoResolveReferences()
        {
            if (previewAvatar == null || previewAvatar.gameObject.scene != gameObject.scene)
            {
                previewAvatar = FindPreviewAvatarInCurrentScene();
            }

            if (characterNameInput == null)
            {
                characterNameInput = GetComponentInChildren<TMP_InputField>(true);
            }

            if (appearancePresetDropdown == null)
            {
                appearancePresetDropdown = GetComponentInChildren<TMP_Dropdown>(true);
            }

            if (portraitPreviewImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);

                for (int index = 0; index < images.Length; index++)
                {
                    Image image = images[index];

                    if (image == null)
                    {
                        continue;
                    }

                    string lowerName = image.name.ToLowerInvariant();

                    if (lowerName.Contains("portrait") || lowerName.Contains("preview"))
                    {
                        portraitPreviewImage = image;
                        break;
                    }
                }
            }

            if (umaPreviewRawImage == null)
            {
                RawImage[] rawImages = GetComponentsInChildren<RawImage>(true);

                for (int index = 0; index < rawImages.Length; index++)
                {
                    RawImage raw = rawImages[index];

                    if (raw == null)
                    {
                        continue;
                    }

                    string lowerName = raw.name.ToLowerInvariant();

                    if (lowerName.Contains("uma") || lowerName.Contains("character") || lowerName.Contains("display"))
                    {
                        umaPreviewRawImage = raw;
                        break;
                    }
                }
            }

            if (flavorTextLabel == null || statsPreviewText == null || loadoutPreviewText == null || validationMessageText == null)
            {
                TMP_Text[] textLabels = GetComponentsInChildren<TMP_Text>(true);

                for (int index = 0; index < textLabels.Length; index++)
                {
                    TMP_Text label = textLabels[index];

                    if (label == null)
                    {
                        continue;
                    }

                    string lowerName = label.name.ToLowerInvariant();

                    if (flavorTextLabel == null && (lowerName.Contains("flavor") || lowerName.Contains("tooltip")))
                    {
                        flavorTextLabel = label;
                        continue;
                    }

                    if (statsPreviewText == null && lowerName.Contains("stats"))
                    {
                        statsPreviewText = label;
                        continue;
                    }

                    if (loadoutPreviewText == null && lowerName.Contains("loadout"))
                    {
                        loadoutPreviewText = label;
                        continue;
                    }

                    if (validationMessageText == null && (lowerName.Contains("validation") || lowerName.Contains("error")))
                    {
                        validationMessageText = label;
                    }
                }
            }

            if (confirmButton == null || closeButton == null || randomNameButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);

                for (int index = 0; index < buttons.Length; index++)
                {
                    Button button = buttons[index];

                    if (button == null)
                    {
                        continue;
                    }

                    string lowerName = button.name.ToLowerInvariant();

                    if (confirmButton == null && lowerName.Contains("confirm"))
                    {
                        confirmButton = button;
                        continue;
                    }

                    if (closeButton == null && (lowerName.Contains("close") || lowerName.Contains("back")))
                    {
                        closeButton = button;
                        continue;
                    }

                    if (randomNameButton == null && lowerName.Contains("random"))
                    {
                        randomNameButton = button;
                    }
                }

                if (confirmButton == null && buttons.Length > 0)
                {
                    confirmButton = buttons[0];
                }

                if (closeButton == null && buttons.Length > 1)
                {
                    closeButton = buttons[1];
                }

                if (randomNameButton == null && buttons.Length > 2)
                {
                    randomNameButton = buttons[2];
                }
            }

            if (customizationController == null)
            {
                customizationController = GetComponent<CharacterCreatorCustomizationController>();
            }

            if (customizationController == null)
            {
                customizationController = gameObject.AddComponent<CharacterCreatorCustomizationController>();
            }

            customizationController.SetPreviewAvatar(previewAvatar);
        }

        private DynamicCharacterAvatar FindPreviewAvatarInCurrentScene()
        {
            DynamicCharacterAvatar[] avatars = FindObjectsByType<DynamicCharacterAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            DynamicCharacterAvatar fallback = null;

            for (int index = 0; index < avatars.Length; index++)
            {
                DynamicCharacterAvatar avatar = avatars[index];

                if (avatar == null || avatar.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = avatar;
                }

                string lowerName = avatar.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("preview"))
                {
                    return avatar;
                }
            }

            return fallback;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(callback);
        }

        [Serializable]
        private sealed class CharacterAppearancePreset
        {
            public string displayName = "Preset";
            [TextArea(2, 4)] public string flavorText = "A survivor profile.";
            public Sprite portrait;
            [Min(1f)] public float maxHealth = 100000f;
            [Min(1f)] public float damage = 10f;
            [Min(0.1f)] public float moveSpeed = 4f;
            [Min(1f)] public float stamina = 100f;
            [Min(1f)] public float carryCapacity = 35f;
            public List<CharacterLoadoutEntry> startingLoadout = new List<CharacterLoadoutEntry>();

            public void Sanitize(int presetNumber)
            {
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = $"Preset {presetNumber}";
                }

                maxHealth = Mathf.Max(1f, maxHealth);
                damage = Mathf.Max(0f, damage);
                moveSpeed = Mathf.Max(0.1f, moveSpeed);
                stamina = Mathf.Max(0f, stamina);
                carryCapacity = Mathf.Max(1f, carryCapacity);

                if (startingLoadout == null)
                {
                    startingLoadout = new List<CharacterLoadoutEntry>();
                }

                for (int index = 0; index < startingLoadout.Count; index++)
                {
                    CharacterLoadoutEntry entry = startingLoadout[index];
                    entry.quantity = Mathf.Max(1, entry.quantity);
                    startingLoadout[index] = entry;
                }
            }

            public static CharacterAppearancePreset Create(
                string presetName,
                string presetFlavorText,
                float presetHealth,
                float presetDamage,
                float presetMoveSpeed,
                float presetStamina,
                float presetCarryCapacity,
                params CharacterLoadoutEntry[] presetLoadout)
            {
                CharacterAppearancePreset preset = new CharacterAppearancePreset
                {
                    displayName = presetName,
                    flavorText = presetFlavorText,
                    maxHealth = presetHealth,
                    damage = presetDamage,
                    moveSpeed = presetMoveSpeed,
                    stamina = presetStamina,
                    carryCapacity = presetCarryCapacity,
                    startingLoadout = presetLoadout != null
                        ? new List<CharacterLoadoutEntry>(presetLoadout)
                        : new List<CharacterLoadoutEntry>()
                };

                preset.Sanitize(1);
                return preset;
            }
        }

        [Serializable]
        private struct CharacterLoadoutEntry
        {
            public string itemName;
            [Min(1)] public int quantity;

            public CharacterLoadoutEntry(string name, int amount)
            {
                itemName = name;
                quantity = Mathf.Max(1, amount);
            }
        }
    }
}