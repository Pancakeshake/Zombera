using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.Systems;
using Zombera.UI.SettingsV2;

namespace Zombera.UI.Menus
{
    /// <summary>
    ///     Handles the in-game pause menu logic.
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject mainContentRoot;
        [SerializeField] private bool hideMainContentRootWhenSubmenuOpen = true;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button saveLogsButton;
        [SerializeField] private Button quitButton;

        [Header("Sub-Panels")]
        [SerializeField] private SettingsPageController settingsPanel;
        [SerializeField] private SaveGameMenuController loadSavePanel;
        [SerializeField] private SaveGameMenuController saveGameOverlay;

        [Header("Visibility Overrides")]
        [SerializeField] private List<GameObject> hideWhenSubmenuOpen = new();

        private readonly List<VisibilityRestoreEntry> _visibilityRestoreBuffer = new();
        private CursorStateHandle _menuCursorStateHandle;

        private struct VisibilityRestoreEntry
        {
            public GameObject Target;
            public bool WasActive;
        }

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        public void Initialize()
        {
            Debug.Log($"[PauseMenu] Initializing controller on {gameObject.name}");

            // Fallback for missing references
            if (resumeButton == null) resumeButton = FindButton("ResumeButton");
            if (saveButton == null) saveButton = FindButton("SaveButton");
            if (loadButton == null) loadButton = FindButton("LoadButton");
            if (settingsButton == null) settingsButton = FindButton("SettingsButton");
            if (quitButton == null) quitButton = FindButton("QuitButton");

            if (settingsPanel == null) settingsPanel = GetComponentInChildren<SettingsPageController>(true);
            if (loadSavePanel == null) loadSavePanel = GetComponentInChildren<SaveGameMenuController>(true);
            if (saveGameOverlay == null) saveGameOverlay = GetComponentInChildren<SaveGameMenuController>(true);

            WireSubmenuEvents();

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(Resume);
                Debug.Log("[PauseMenu] Wired Resume Button");
            }
            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(Save);
                Debug.Log("[PauseMenu] Wired Save Button");
            }
            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(Load);
                Debug.Log("[PauseMenu] Wired Load Button");
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(ShowSettings);
                Debug.Log("[PauseMenu] Wired Settings Button");
            }
            if (saveLogsButton != null)
            {
                saveLogsButton.onClick.RemoveAllListeners();
                saveLogsButton.onClick.AddListener(SaveLogs);
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(Quit);
                Debug.Log("[PauseMenu] Wired Quit Button");
            }

            // Ensure sub-panels are hidden immediately without animation on initialization
            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
            if (loadSavePanel != null) loadSavePanel.gameObject.SetActive(false);
            if (saveGameOverlay != null) saveGameOverlay.gameObject.SetActive(false);

            Hide();
            }

        private Button FindButton(string name)
        {
            var t = transform.Find(name);
            if (t == null) t = transform.Find("PauseCard/" + name);
            if (t == null) t = transform.Find("PauseCard/Content/" + name); // Try deeper
            return t != null ? t.GetComponent<Button>() : null;
        }

        public void Show()
        {
            if (panelRoot != null) panelRoot.SetActive(true);

            _menuCursorStateHandle.Dispose();
            _menuCursorStateHandle = CursorService.RequestMenuModal();
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (settingsPanel != null) settingsPanel.Hide();
            if (loadSavePanel != null) loadSavePanel.Hide();
            if (saveGameOverlay != null) saveGameOverlay.Hide();
            if (mainContentRoot != null) mainContentRoot.SetActive(true);
            RestoreSubmenuVisibilityOverrides();

            _menuCursorStateHandle.Dispose();

            UiMenuUtility.DeselectCurrent();
        }

        private void OnDestroy()
        {
            _menuCursorStateHandle.Dispose();
        }

        public void Resume()
        {
            UiMenuUtility.DeselectCurrent();
            GameManagerGateway.Instance?.SetGameState(GameState.Playing);
            Hide();
        }

        public void Save()
        {
            Debug.Log("[PauseMenu] Save button clicked.");
            UiMenuUtility.DeselectCurrent();

            if (saveGameOverlay != null)
            {
                Debug.Log("[PauseMenu] Showing saveGameOverlay.");
                SetSubmenuVisibility(true);
                saveGameOverlay.Show();
            }
            else if (loadSavePanel != null)
            {
                Debug.Log("[PauseMenu] saveGameOverlay is null, showing loadSavePanel.");
                SetSubmenuVisibility(true);
                loadSavePanel.Show(SaveGameMenuController.MenuMode.Save);
            }
            else
            {
                Debug.Log("[PauseMenu] No panels found, falling back to direct save.");
                var saveManager = SaveManagerGateway.ResolveActive();
                if (saveManager != null)
                {
                    var slotId = saveManager.ActiveSlotId;
                    if (string.IsNullOrEmpty(slotId)) slotId = "ManualSave";
                    saveManager.SetActiveSlot(slotId);
                    saveManager.SaveGame(slotId);
                    Debug.Log($"[PauseMenu] Game saved to slot: {slotId}");
                }
            }
        }

        public void Load()
        {
            Debug.Log("[PauseMenu] Load button clicked.");
            UiMenuUtility.DeselectCurrent();

            if (loadSavePanel != null)
            {
                Debug.Log("[PauseMenu] Showing loadSavePanel.");
                SetSubmenuVisibility(true);
                loadSavePanel.ShowLoadMode();
            }
            else if (saveGameOverlay != null)
            {
                Debug.Log("[PauseMenu] loadSavePanel is null, showing saveGameOverlay.");
                SetSubmenuVisibility(true);
                saveGameOverlay.ShowLoadMode();
            }
            else
            {
                Debug.LogWarning("[PauseMenu] No load/save panels found!");
            }
        }

        public void SaveLogs()
        {
            UiMenuUtility.DeselectCurrent();
            try
            {
                string logPath = Path.Combine(Application.persistentDataPath, "Player.log");
                if (File.Exists(logPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string destPath = Path.Combine(Application.persistentDataPath, $"Player_{timestamp}.log");
                    File.Copy(logPath, destPath, true);
                    Debug.Log($"[PauseMenu] Logs saved to: {destPath}");
                }
                else
                {
                    Debug.LogWarning("[PauseMenu] Player.log not found in persistentDataPath.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PauseMenu] Failed to save logs: {ex.Message}");
            }
        }

        public void ShowSettings()
        {
            UiMenuUtility.DeselectCurrent();
            if (settingsPanel != null)
            {
                SetSubmenuVisibility(true);
                settingsPanel.Show();
            }
        }

        public void Quit()
        {
            UiMenuUtility.DeselectCurrent();
            GameManagerGateway.Instance?.QuitToMainMenu();
        }

        private void HandleOverlayHidden()
        {
            if (AnySubmenuVisible()) return;

            SetSubmenuVisibility(false);
        }

        private bool AnySubmenuVisible()
        {
            var settingsVisible = settingsPanel != null && settingsPanel.IsVisible;
            var loadSaveVisible = loadSavePanel != null && loadSavePanel.IsVisible;
            var saveOverlayVisible = saveGameOverlay != null && saveGameOverlay.IsVisible;
            return settingsVisible || loadSaveVisible || saveOverlayVisible;
        }

        private void SetSubmenuVisibility(bool isVisible)
        {
            if (mainContentRoot != null && hideMainContentRootWhenSubmenuOpen)
                mainContentRoot.SetActive(!isVisible);

            if (isVisible)
            {
                ApplySubmenuVisibilityOverrides();
                return;
            }

            RestoreSubmenuVisibilityOverrides();
        }

        private void ApplySubmenuVisibilityOverrides()
        {
            if (_visibilityRestoreBuffer.Count > 0) return;

            for (var i = 0; i < hideWhenSubmenuOpen.Count; i++)
            {
                var target = hideWhenSubmenuOpen[i];
                if (target == null) continue;

                var alreadyTracked = false;
                for (var j = 0; j < _visibilityRestoreBuffer.Count; j++)
                {
                    if (_visibilityRestoreBuffer[j].Target != target) continue;

                    alreadyTracked = true;
                    break;
                }

                if (alreadyTracked) continue;

                _visibilityRestoreBuffer.Add(new VisibilityRestoreEntry
                {
                    Target = target,
                    WasActive = target.activeSelf
                });

                target.SetActive(false);
            }
        }

        private void RestoreSubmenuVisibilityOverrides()
        {
            for (var i = 0; i < _visibilityRestoreBuffer.Count; i++)
            {
                var entry = _visibilityRestoreBuffer[i];
                if (entry.Target == null) continue;

                entry.Target.SetActive(entry.WasActive);
            }

            _visibilityRestoreBuffer.Clear();
        }

        private void WireSubmenuEvents()
        {
            if (settingsPanel != null)
            {
                settingsPanel.OnHide -= HandleOverlayHidden;
                settingsPanel.OnHide += HandleOverlayHidden;
            }

            if (loadSavePanel != null)
            {
                loadSavePanel.OnHide -= HandleOverlayHidden;
                loadSavePanel.OnHide += HandleOverlayHidden;
            }

            if (saveGameOverlay != null)
            {
                saveGameOverlay.OnHide -= HandleOverlayHidden;
                saveGameOverlay.OnHide += HandleOverlayHidden;
            }
        }
        }
        }