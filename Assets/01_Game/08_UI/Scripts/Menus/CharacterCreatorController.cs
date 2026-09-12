#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.UI.Menus.CharacterCreation;
using Zombera.UI.SquadManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif
using Random = UnityEngine.Random;

#endregion

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Zombera.UI.Menus
{
    /// <summary>
    ///     Handles lightweight character creation panel interactions.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class CharacterCreatorController : MonoBehaviour
    {
        [Header("Asset Paths")]
        [SerializeField] private string menuClickSfxAssetPath = "Assets/03_ThirdParty/Universal Sound FX/BUTTONS/BUTTON_Click_Electric_Sander_Crop_02_mono.wav";
        [SerializeField] private string portraitCatalogAssetPath = "Assets/Resources/Zombera/CharacterPortraitCatalog.asset";
        [SerializeField] private string portraitSourceFolderPath = "Assets/Art/Character_Portraits";

        private static Sprite _runtimeSolidSprite;


        [Header("Refs")] [SerializeField] [FormerlySerializedAs("_refs")]
        private CharacterCreatorRefs creatorRefs;

        [Header("Validation")] [SerializeField]
        private string defaultCharacterName = "Survivor";

        [SerializeField] [Min(1)] private int minimumNameLength = 3;
        [SerializeField] [Min(1)] private int maximumNameLength = 16;

        [Header("Input UX")] [SerializeField] private Vector4 nameInputRaycastPadding = new(18f, 10f, 18f, 10f);


        [Header("Presets")] [SerializeField] private List<CharacterAppearancePreset> appearancePresets = new();

        [Header("Portraits")] [SerializeField]
        private CharacterPortraitCatalog portraitCatalog;

        [SerializeField] private bool useRandomPortraitWhenNoSavedSelection = true;

        [Header("Presentation")] [SerializeField]
        private bool applyGameLikePresentation = true;

        [SerializeField] private string creatorHeaderTitle = string.Empty;
        [SerializeField] private string creatorHeaderSubtitle = string.Empty;
        [SerializeField] private string confirmButtonLabel = "CONFIRM";
        [SerializeField] private string closeButtonLabel = "BACK";

        [Header("Audio")] [SerializeField] private AudioClip menuClickSfx;

        [SerializeField] [Range(0f, 1f)] private float menuClickVolume = 0.05f;
        [SerializeField] [Range(0.05f, 1f)] private float sliderInteractionVolumeScale = 0.35f;

        [Header("Preview Quality")] [SerializeField]
        private bool useHighResolutionPreviewTexture = true;

        [SerializeField] [Min(256)] private int highResolutionPreviewWidth = 1536;
        [SerializeField] [Min(256)] private int highResolutionPreviewHeight = 1536;
        [SerializeField] [Range(1, 8)] private int highResolutionPreviewMsaa = 1;
        private RenderTexture _highResolutionPreviewTexture;
        private RenderTexture _originalPreviewTexture;
        private readonly Dictionary<Button, UnityAction> _boundClickHandlers = new();
        private GameObject _runtimeResolvedPreviewAvatar;

        private Camera _previewRenderCamera;
        private AudioSource _uiClickAudioSource;
        private int _selectedPortraitCatalogIndex = -1;
    #if UNITY_EDITOR
        private CharacterPortraitCatalog _editorFallbackPortraitCatalog;
    #endif

        public bool IsInitialized { get; private set; }

        public bool IsVisible =>
            creatorRefs != null && creatorRefs.panelRoot != null && creatorRefs.panelRoot.activeSelf;

        public string SelectedCharacterName { get; private set; }
        public int SelectedAppearancePresetIndex { get; private set; }

        public event Action<string> SelectionConfirmed;
        public event Action<bool> VisibilityChanged;

        public void Initialize()
        {
            if (IsInitialized) return;

            EnsureRefs();
            if (creatorRefs == null) return;

            if (creatorRefs.panelRoot == null) creatorRefs.panelRoot = gameObject;

            AutoResolveReferences();
            EnsureNameInputClickability();
            EnsureDefaults();
            ApplyGameLikePresentation();
            EnsureUiClickAudioSource();

            BindButton(creatorRefs.confirmButton, ConfirmSelection);
            BindButton(creatorRefs.backButton, Hide);
            BindButton(creatorRefs.randomNameButton, ApplyRandomName);
            BindButton(creatorRefs.previousPortraitButton, SelectPreviousPortrait);
            BindButton(creatorRefs.nextPortraitButton, SelectNextPortrait);
            BindButton(creatorRefs.randomPortraitButton, SelectRandomPortrait);
            BindPortraitPreviewCycleFallback();
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
            EnsureRefs();
            if (creatorRefs == null) return;

            if (creatorRefs.panelRoot == null) creatorRefs.panelRoot = gameObject;

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

            if (creatorRefs != null && creatorRefs.customizationController != null)
                creatorRefs.customizationController.EnsureCustomizationInputBindings();
        }

        public void Hide()
        {
            TouchSplitMembersForAnalysis();
            SetPanelVisible(false);
        }

        public void PrepareForSceneTransition()
        {
            TouchSplitMembersForAnalysis();
            var previewAvatar = ResolvePreviewAvatar(includeGlobalFallback: true);
            if (previewAvatar == null) return;

            ClearPreviewAvatarGeneratorReferences();

            // Disable preview avatar before world load to avoid editor cross-scene serialization warnings.
            previewAvatar.SetActive(false);
        }

        public void ConfirmSelection()
        {
            var preset = GetCurrentPreset();

            if (preset == null)
            {
                SetValidationMessage("Add at least one appearance preset before confirming.");
                return;
            }

            var normalizedName =
                NormalizeName(creatorRefs.nameInput != null ? creatorRefs.nameInput.text : SelectedCharacterName);

            if (!TryValidateCharacterName(normalizedName, out var validationMessage))
            {
                SetValidationMessage(validationMessage);

                if (creatorRefs.confirmButton != null) creatorRefs.confirmButton.interactable = false;

                return;
            }

            SelectedCharacterName = normalizedName;
            SelectedAppearancePresetIndex = ResolveSelectedPresetIndex();

            var appearanceRecipe = string.Empty;
            var appearanceProfileJson = CaptureAppearanceProfileJson();
            var flavorText = preset.flavorText;
            var loadoutSummary = preset.BuildLoadoutSummary();

            var selectedPortrait = ResolveSelectedPortraitSprite(out var selectedPortraitId);
            if (selectedPortrait == null)
            {
                selectedPortrait = CharacterSelectionState.SelectedPortraitSprite;
                selectedPortraitId = CharacterSelectionState.SelectedPortraitId;
            }

            CharacterSelectionState.SetPortraitSprite(selectedPortrait, selectedPortraitId);

            CharacterSelectionState.SetSelection(
                SelectedCharacterName,
                SelectedAppearancePresetIndex,
                preset.maxHealth,
                preset.damage,
                preset.moveSpeed,
                preset.stamina,
                preset.carryCapacity,
                flavorText,
                loadoutSummary,
                appearanceRecipe,
                appearanceProfileJson,
                selectedPortraitId);

                // Refresh portrait studio with the new appearance for the squad UI
                if (PortraitStudioManager.Instance != null)
                {
                var previewAvatar = ResolvePreviewAvatar(includeGlobalFallback: true);
                if (previewAvatar != null)
                {
                    var avatar = previewAvatar.GetComponent<UMA.CharacterSystem.DynamicCharacterAvatar>();
                    if (avatar != null)
                    {
                        PortraitStudioManager.Instance.SyncWithPlayer(avatar);
                    }
                }
                }

                SelectionConfirmed?.Invoke(SelectedCharacterName);
            Hide();
        }

        public void SelectPreviousPortrait()
        {
            TouchSplitMembersForAnalysis();
            StepPortraitSelection(-1);
        }

        public void SelectNextPortrait()
        {
            TouchSplitMembersForAnalysis();
            StepPortraitSelection(1);
        }

        public void SelectRandomPortrait()
        {
            var catalog = ResolvePortraitCatalog();
            if (catalog == null || catalog.Count == 0)
            {
                _selectedPortraitCatalogIndex = -1;
                RefreshPreview();
                return;
            }

            EnsurePortraitSelectionInitialized(catalog);

            if (catalog.Count <= 1)
            {
                _selectedPortraitCatalogIndex = 0;
            }
            else
            {
                var nextIndex = Random.Range(0, catalog.Count);
                if (nextIndex == _selectedPortraitCatalogIndex)
                    nextIndex = (nextIndex + 1) % catalog.Count;

                _selectedPortraitCatalogIndex = nextIndex;
            }

            ApplySelectedPortraitToRuntimeState();
            RefreshPreview();
        }


        private static void KeepMutableForSplitAnalysis<T>(ref T value)
        {
            // Intentionally empty. Passing by ref keeps split-host fields visible to analyzers.
        }


        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TouchSplitMembersForAnalysis()
        {
            KeepMutableForSplitAnalysis(ref _uiClickAudioSource);
            var visibilityChanged = VisibilityChanged;
            _ = visibilityChanged;
        }


        private void PlayMenuClickSfx()
        {
            PlayMenuClickSfx(1f);
        }

        private void PlayMenuClickSfx(float volumeScale)
        {
        #if UNITY_EDITOR
            if (menuClickSfx == null && !string.IsNullOrWhiteSpace(menuClickSfxAssetPath))
                menuClickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(menuClickSfxAssetPath);
        #endif
            if (menuClickSfx == null) return;

            EnsureUiClickAudioSource();
            if (_uiClickAudioSource == null) return;

            var scaledVolume = Mathf.Clamp01(menuClickVolume * Mathf.Max(0f, volumeScale));
            if (scaledVolume <= 0f) return;

            _uiClickAudioSource.PlayOneShot(menuClickSfx, scaledVolume);
        }

        private static string BuildLoadoutSummaryText(List<CharacterLoadoutEntry> loadout)
        {
            if (loadout == null || loadout.Count == 0) return string.Empty;

            var summary = string.Empty;
            foreach (var entry in loadout)
            {
                if (string.IsNullOrWhiteSpace(entry.itemName)) continue;

                if (summary.Length > 0) summary += ", ";
                summary += $"{entry.itemName} x{Mathf.Max(1, entry.quantity)}";
            }

            return summary;
        }

        [Serializable]
        private sealed class CharacterAppearancePreset
        {
            public string displayName = "Preset";
            [TextArea(2, 4)] public string flavorText = "A survivor profile.";
            [Min(1f)] public float maxHealth = 100000f;
            [Min(1f)] public float damage = 10f;
            [Min(0.1f)] public float moveSpeed = 4f;
            [Min(1f)] public float stamina = 100f;
            [Min(1f)] public float carryCapacity = 35f;
            public List<CharacterLoadoutEntry> startingLoadout = new();

            public string BuildLoadoutSummary()
            {
                return CharacterCreatorController.BuildLoadoutSummaryText(startingLoadout);
            }

            public void Sanitize(int presetNumber)
            {
                if (string.IsNullOrWhiteSpace(displayName)) displayName = $"Preset {presetNumber}";

                maxHealth = Mathf.Max(1f, maxHealth);
                damage = Mathf.Max(0f, damage);
                moveSpeed = Mathf.Max(0.1f, moveSpeed);
                stamina = Mathf.Max(0f, stamina);
                carryCapacity = Mathf.Max(1f, carryCapacity);

                startingLoadout ??= new List<CharacterLoadoutEntry>();

                for (var index = 0; index < startingLoadout.Count; index++)
                {
                    var entry = startingLoadout[index];
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
                var preset = new CharacterAppearancePreset
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