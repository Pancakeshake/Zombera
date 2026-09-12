using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.Systems;
using TMPro;

namespace Zombera.UI.Menus
{
    /// <summary>
    ///     Controls the Load Save panel, populating it with available save slots.
    /// </summary>
    [System.Obsolete("Use SaveGameMenuController instead for improved robustness and features.")]
    public sealed class LoadSaveMenuController : MonoBehaviour
    {
        public enum MenuMode { Load, Save }

        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;

        [Header("Container")]
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private GameObject slotButtonPrefab;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        public event Action OnShow;
        public event Action OnHide;

        private MenuMode _currentMode = MenuMode.Load;

        public bool IsInitialized { get; private set; }
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        private SaveSystem _saveSystem;
        private SaveSystem _subscribedSaveSystem;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            EnsureReferences();
        }

        private void OnDisable()
        {
            if (_subscribedSaveSystem != null)
            {
                _subscribedSaveSystem.SaveListChanged -= PopulateSlots;
                _subscribedSaveSystem = null;
            }
        }

        private void OnDestroy()
        {
            if (_subscribedSaveSystem != null)
            {
                _subscribedSaveSystem.SaveListChanged -= PopulateSlots;
                _subscribedSaveSystem = null;
            }
        }

        public void Initialize()
        {
            Debug.LogWarning("[LoadSaveMenuController] This menu controller is deprecated. Please migrate to SaveGameMenuController.");

            if (IsInitialized) return;

            EnsureReferences();

            if (panelRoot == null) panelRoot = gameObject;
            
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            Hide();
            IsInitialized = true;
        }

        private void EnsureReferences()
        {
            if (_saveSystem == null)
                _saveSystem = FindFirstObjectByType<SaveSystem>();

            if (_saveSystem == _subscribedSaveSystem) return;

            if (_subscribedSaveSystem != null)
                _subscribedSaveSystem.SaveListChanged -= PopulateSlots;

            _subscribedSaveSystem = _saveSystem;

            if (_subscribedSaveSystem != null)
            {
                _subscribedSaveSystem.SaveListChanged -= PopulateSlots;
                _subscribedSaveSystem.SaveListChanged += PopulateSlots;
            }
        }

        public void Show()
        {
            Show(MenuMode.Load);
        }

        public void Show(MenuMode mode)
        {
            if (titleText != null) titleText.text = mode == MenuMode.Load ? "LOAD GAME" : "SAVE GAME";
            _currentMode = mode;
            if (panelRoot != null) panelRoot.SetActive(true);
            PopulateSlots();
            
            // Fix highlight issue
            UiMenuUtility.DeselectCurrent();
            OnShow?.Invoke();
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            UiMenuUtility.DeselectCurrent();
            OnHide?.Invoke();
        }

        private void PopulateSlots()
        {
            if (slotContainer == null) return;

            // Clear existing slots
            foreach (Transform child in slotContainer)
            {
                Destroy(child.gameObject);
            }

            var saveSystem = FindFirstObjectByType<SaveSystem>();
            if (saveSystem == null)
            {
                Debug.LogWarning("[LoadSaveMenuController] SaveSystem not found in scene.");
                return;
            }

            // If in Save mode, add "NEW SAVE" button first
            if (_currentMode == MenuMode.Save)
            {
                CreateSlotButton("NEW SAVE", CreateNewSave);
            }

            var slots = saveSystem.GetAvailableSlotIds();
            if (slots != null && slots.Count > 0)
            {
                foreach (var slotId in slots)
                {
                    if (string.IsNullOrWhiteSpace(slotId)) continue;
                    CreateSlotButton(slotId.ToUpper(), () => HandleSlotSelected(slotId));
                }
            }
        }

        private void CreateSlotButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = Instantiate(slotButtonPrefab, slotContainer);
            go.name = $"Slot_{label}";
            
            var button = go.GetComponent<Button>();
            var text = go.GetComponentInChildren<TMP_Text>();
            
            if (text != null) text.text = label;
            if (button != null)
            {
                button.onClick.AddListener(() => {
                    UiMenuUtility.DeselectCurrent();
                    onClick.Invoke();
                });
            }
        }

        private void CreateNewSave()
        {
            string newSlotId = "Save_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm");
            var saveManager = SaveManagerGateway.ResolveActive();
            if (saveManager != null)
            {
                Debug.Log($"[LoadSaveMenuController] Creating new save: {newSlotId}");
                saveManager.SetActiveSlot(newSlotId);
                saveManager.SaveGame(newSlotId);
            }
            else
            {
                Debug.LogError("[LoadSaveMenuController] SaveManager not found. Cannot save.");
            }
            Hide();
        }

        private void HandleSlotSelected(string slotId)
        {
            if (_currentMode == MenuMode.Load)
            {
                if (GameManagerGateway.Instance != null)
                {
                    Debug.Log($"[LoadSaveMenuController] Loading slot: {slotId}");
                    GameManagerGateway.Instance.LoadGame(slotId);
                }
                else
                {
                    Debug.LogError("[LoadSaveMenuController] GameManager.Instance is null. Cannot load game.");
                }
            }
            else // Save Mode (Overwrite)
            {
                var saveManager = SaveManagerGateway.ResolveActive();
                if (saveManager != null)
                {
                    Debug.Log($"[LoadSaveMenuController] Overwriting slot: {slotId}");
                    saveManager.SetActiveSlot(slotId);
                    saveManager.SaveGame(slotId);
                }
                else
                {
                    Debug.LogError("[LoadSaveMenuController] SaveManager not found. Cannot save.");
                }
            }
            Hide();
        }
    }
}