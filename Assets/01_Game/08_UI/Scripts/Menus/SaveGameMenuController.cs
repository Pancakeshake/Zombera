using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zombera.Core;
using Zombera.Systems;
using System.Linq;
using System;
using System.Globalization;

namespace Zombera.UI.Menus
{
    public partial class SaveGameMenuController : MonoBehaviour
    {
        public enum MenuMode
        {
            Save,
            Load
        }

        [Header("Containers")]
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private GameObject panelRoot;

        [Header("Details Panel")]
        [SerializeField] private TMP_Text detailsTitleText;
        [SerializeField] private TMP_Text locationText;
        [SerializeField] private TMP_Text playTimeText;
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text difficultyText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text versionText;
        [SerializeField] private Image detailScreenshotImage;
        [SerializeField] private TMP_Text recentActivityText;

        [Header("Controls")]
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button saveButton;
        [SerializeField] private TMP_Text saveButtonLabel;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button renameButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button closeButton;
        
        public event Action OnHide;
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        private readonly List<SaveSlotItem> _spawnedSlots = new List<SaveSlotItem>();
        private readonly Dictionary<string, Sprite> _screenshotCache = new Dictionary<string, Sprite>();
        private SaveSlotItem _selectedSlot;
        private SaveSystem _saveSystem;
        private SaveSystem _subscribedSaveSystem;
        private ISaveManagerGateway _saveManager;
        private MenuMode _currentMode = MenuMode.Save;

        private void Awake()
        {
            _saveSystem = FindFirstObjectByType<SaveSystem>();
            _saveManager = SaveManagerGateway.ResolveActive();

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backButton != null) backButton.onClick.AddListener(Hide);
            if (saveButton != null) saveButton.onClick.AddListener(HandlePrimaryAction);
            if (deleteButton != null) deleteButton.onClick.AddListener(HandleDelete);
            if (renameButton != null) renameButton.onClick.AddListener(HandleRename);

            EnsureReferences();

            // Do not call Hide() here: if this panel is first activated by a button click,
            // Awake runs during Show() and would immediately close the panel.
        }

        private void OnDestroy()
        {
            if (_subscribedSaveSystem != null)
            {
                _subscribedSaveSystem.SaveListChanged -= HandleSaveListChanged;
                _subscribedSaveSystem = null;
            }
            ClearScreenshotCache();
        }

        private void HandleSaveListChanged()
        {
            RefreshList(_selectedSlot?.SlotId);
        }

            public void Show()
            {
                Show(MenuMode.Save);
            }

            public void Show(MenuMode mode)
            {
                Debug.Log($"[SaveGameMenu] Show mode={mode} on {gameObject.name}");
                EnsureReferences();
                _currentMode = mode;
                ApplyModeVisuals();
                if (panelRoot != null)
                {
                    panelRoot.SetActive(true);
                    panelRoot.transform.SetAsLastSibling();
                    Debug.Log($"[SaveGameMenu] Activated panelRoot: {panelRoot.name}");
                }
                else
                {
                    Debug.LogWarning("[SaveGameMenu] panelRoot is NULL!");
                }
                RefreshList();
            }

            public void ShowLoadMode()
            {
                Debug.Log("[SaveGameMenu] ShowLoadMode()");
                Show(MenuMode.Load);
            }

            public void Hide()
            {
                if (panelRoot != null) panelRoot.SetActive(false);
                OnHide?.Invoke();
            }

            private void RefreshList(string preferredSlotId = null)
            {
                ClearScreenshotCache();
                foreach (var slot in _spawnedSlots) Destroy(slot.gameObject);
                _spawnedSlots.Clear();

                EnsureReferences();
                if (_saveSystem == null) return;

                var activeId = _saveManager != null ? _saveManager.ActiveSlotId : "";
                PopulateSlotsFromMetadata(GetOrderedSlotMetadata(), activeId);
                AppendPlaceholderSlotsForSaveMode();

                if (TrySelectSlotAfterRefresh(preferredSlotId)) return;

                ClearSelectionForEmptyList();
            }

            private List<SaveMetadata> GetOrderedSlotMetadata()
            {
                return _saveSystem.GetAllSlotMetadata()
                    .OrderByDescending(GetSortTimestamp)
                    .ThenByDescending(static metadata => metadata.playTimeSeconds)
                    .ToList();
            }

            private void PopulateSlotsFromMetadata(IReadOnlyList<SaveMetadata> metadatas, string activeId)
            {
                foreach (var metadata in metadatas)
                    CreateSlot(metadata, false, metadata.slotId == activeId);
            }

            private void AppendPlaceholderSlotsForSaveMode()
            {
                if (_currentMode != MenuMode.Save) return;

                for (var i = _spawnedSlots.Count; i < 5; i++)
                    CreateSlot(new SaveMetadata { slotName = "NEW SLOT" }, true, false);
            }

            private bool TrySelectSlotAfterRefresh(string preferredSlotId)
            {
                if (_spawnedSlots.Count == 0) return false;

                if (!string.IsNullOrWhiteSpace(preferredSlotId))
                {
                    for (var i = 0; i < _spawnedSlots.Count; i++)
                    {
                        var slot = _spawnedSlots[i];
                        if (slot == null || slot.IsNewSlot) continue;
                        if (!string.Equals(slot.SlotId, preferredSlotId, StringComparison.Ordinal)) continue;

                        SelectSlot(slot);
                        return true;
                    }
                }

                SelectSlot(_spawnedSlots[0]);
                return true;
            }

            private void ClearSelectionForEmptyList()
            {
                _selectedSlot = null;
                if (_currentMode != MenuMode.Load) return;

                detailsTitleText.text = "NO SAVE SLOTS";
                locationText.text = "-";
                playTimeText.text = "-";
                dayText.text = "-";
                difficultyText.text = "-";
                progressText.text = "-";
                versionText.text = "-";
                recentActivityText.text = "No save slots available to load.";
                detailScreenshotImage.sprite = null;
                detailScreenshotImage.color = new Color(0, 0, 0, 0.2f);
                saveButton.interactable = false;
                deleteButton.interactable = false;
                renameButton.interactable = false;
            }

            private void CreateSlot(SaveMetadata metadata, bool isNew, bool isCurrent)
            {
                var go = Instantiate(slotPrefab, slotContainer);
                var item = go.GetComponent<SaveSlotItem>();
                item.Setup(metadata, isNew, SelectSlot, isCurrent);

                // Decode and apply screenshot if present (with caching)
                if (!isNew && !string.IsNullOrWhiteSpace(metadata.screenshotBase64))
                {
                    item.SetScreenshot(GetCachedScreenshot(metadata.screenshotBase64, metadata.slotId));
                }

                _spawnedSlots.Add(item);
            }

            private void SelectSlot(SaveSlotItem item)
            {
                if (_selectedSlot != null) _selectedSlot.SetSelected(false);
                _selectedSlot = item;
                _selectedSlot.SetSelected(true);

                UpdateDetails(item.Metadata, item.IsNewSlot);
            }

            private void UpdateDetails(SaveMetadata metadata, bool isNew)
            {
                if (isNew)
                {
                    detailsTitleText.text = _currentMode == MenuMode.Load
                        ? "NO SAVE SELECTED"
                        : "NEW SLOT DETAILS";
                    locationText.text = "-";
                    playTimeText.text = "-";
                    dayText.text = "-";
                    difficultyText.text = "-";
                    progressText.text = "-";
                    versionText.text = "-";
                    recentActivityText.text = "No activity yet.";
                    detailScreenshotImage.sprite = null;
                    detailScreenshotImage.color = new Color(0, 0, 0, 0.2f);

                    saveButton.interactable = _currentMode == MenuMode.Save;
                    deleteButton.interactable = false;
                    renameButton.interactable = false;
                }
                else
                {
                    detailsTitleText.text = $"{metadata.slotName} DETAILS";
                    locationText.text = metadata.locationName;

                    TimeSpan t = TimeSpan.FromSeconds(metadata.playTimeSeconds);
                    playTimeText.text = string.Format("{0}h {1}m", (int)t.TotalHours, t.Minutes);

                    dayText.text = $"Day {metadata.dayNumber}";
                    difficultyText.text = metadata.difficulty;
                    progressText.text = $"{metadata.progressPercent}%";
                    versionText.text = metadata.gameVersion;

                    recentActivityText.text = string.Join("\n", metadata.recentActivity);

                    if (!string.IsNullOrWhiteSpace(metadata.screenshotBase64))
                    {
                        detailScreenshotImage.sprite = GetCachedScreenshot(metadata.screenshotBase64, metadata.slotId);
                        detailScreenshotImage.color = Color.white;
                    }
                    else
                    {
                        detailScreenshotImage.sprite = null;
                        detailScreenshotImage.color = new Color(0, 0, 0, 0.2f);
                    }

                    saveButton.interactable = true;
                    // Allow delete and rename in both modes for existing slots
                    deleteButton.interactable = true;
                    renameButton.interactable = true;
                    }
                    }

            private void HandlePrimaryAction()
            {
                if (_currentMode == MenuMode.Load)
                {
                    HandleLoad();
                    return;
                }

                HandleSave();
            }

            private void HandleLoad()
            {
                if (_selectedSlot == null || _selectedSlot.IsNewSlot) return;

                if (GameManagerGateway.Instance == null)
                {
                    if (statusText != null) statusText.text = "Error: Game manager not found!";
                    return;
                }

                if (statusText != null) statusText.text = "Loading...";
                GameManagerGateway.Instance.LoadGame(_selectedSlot.SlotId);
                Hide();
            }

            private void HandleSave()
            {
                EnsureReferences();
                LogSaveDiagnostics();

                if (!TryValidateSaveRequest(out var validationError))
                {
                    if (!string.IsNullOrEmpty(validationError) && statusText != null)
                        statusText.text = validationError;
                    return;
                }

                var id = ResolveSaveSlotId(_selectedSlot);
                if (statusText != null) statusText.text = "Saving...";

                _saveManager.SetActiveSlot(id);

                if (_saveManager.SaveGame(id))
                {
                    if (statusText != null) statusText.text = "Save successful!";
                    RefreshList(id);
                }
                else if (statusText != null)
                {
                    statusText.text = "Save failed!";
                }
            }

            private void LogSaveDiagnostics()
            {
                Debug.Log($"[SaveGameMenu] HandleSave - Selected Slot: {(_selectedSlot != null ? _selectedSlot.SlotId : "null")}, " +
                          $"IsNew: {(_selectedSlot != null && _selectedSlot.IsNewSlot)}, " +
                          $"SaveManager: {(_saveManager != null)}, " +
                          $"SaveSystemInit: {(_saveSystem != null && _saveSystem.IsInitialized)}");
            }

            private bool TryValidateSaveRequest(out string validationError)
            {
                validationError = null;

                if (_saveManager == null || _saveSystem == null)
                {
                    validationError = "Error: Save system not found!";
                    return false;
                }

                if (_selectedSlot == null) return false;

                if (!_selectedSlot.IsNewSlot && string.IsNullOrEmpty(_selectedSlot.SlotId))
                {
                    validationError = "Error: Invalid slot ID!";
                    return false;
                }

                return true;
            }

            private static string ResolveSaveSlotId(SaveSlotItem slot)
            {
                if (!slot.IsNewSlot) return slot.SlotId;

                var id = $"Save_{DateTime.Now:yyyyMMdd_HHmm}";
                slot.Metadata.slotId = id;
                return id;
            }

            private void ApplyModeVisuals()
            {
                if (saveButtonLabel == null && saveButton != null)
                    saveButtonLabel = saveButton.GetComponentInChildren<TMP_Text>(true);

                if (saveButtonLabel != null)
                    saveButtonLabel.text = _currentMode == MenuMode.Load ? "LOAD GAME" : "SAVE GAME";

                if (headerText != null)
                    headerText.text = _currentMode == MenuMode.Load ? "LOAD GAME" : "SAVE GAME";

                if (subtitleText != null)
                    subtitleText.text = _currentMode == MenuMode.Load ? "Choose a slot to load your progress." : "Choose a slot to save your progress.";
            }

            private void HandleDelete()
            {
                if (_selectedSlot == null || _selectedSlot.IsNewSlot || _saveSystem == null) return;

                InvalidateScreenshotCacheForSlot(_selectedSlot.SlotId);
                _saveSystem.DeleteSave(_selectedSlot.SlotId);
                RefreshList();
            }

            private void HandleRename()
            {
                if (_selectedSlot == null || _selectedSlot.IsNewSlot || _saveSystem == null) return;

                InvalidateScreenshotCacheForSlot(_selectedSlot.SlotId);

                // In a full implementation, we'd show an input field.
                // For now, we'll just append a suffix for demonstration or use a placeholder.
                string newName = _selectedSlot.Metadata.slotName + " (RENAMED)";
                _saveSystem.RenameSave(_selectedSlot.SlotId, newName);
                RefreshList();
            }

        private void EnsureReferences()
        {
            if (_saveSystem == null) _saveSystem = FindFirstObjectByType<SaveSystem>();

            _saveManager = SaveManagerGateway.ResolveActive();

            // Manage event subscription if the SaveSystem instance changed or was found
            if (_saveSystem != _subscribedSaveSystem)
            {
                if (_subscribedSaveSystem != null)
                {
                    _subscribedSaveSystem.SaveListChanged -= HandleSaveListChanged;
                }

                _subscribedSaveSystem = _saveSystem;

                if (_subscribedSaveSystem != null)
                {
                    _subscribedSaveSystem.SaveListChanged += HandleSaveListChanged;
                }
            }

            if (_saveManager != null && !_saveManager.IsInitialized)
            {
                _saveManager.Initialize();
            }

            if (_saveSystem != null && !_saveSystem.IsInitialized)
            {
                _saveSystem.Initialize();
            }
        }

        private static DateTime GetSortTimestamp(SaveMetadata metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.timestamp)) return DateTime.MinValue;

            if (DateTime.TryParse(
                    metadata.timestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsed))
                return parsed;

            return DateTime.MinValue;
        }
    }
}
