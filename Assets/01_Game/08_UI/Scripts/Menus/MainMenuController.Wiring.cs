#region

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.Menus
{
    public sealed partial class MainMenuController
    {
        private void EnsureButtonsBound()
        {
            AutoResolveReferences();
            BindButton(continueButton, HandleContinueRequested);
            BindButton(startGameButton, HandleStartGameRequested);
            BindButton(loadGameButton, HandleLoadGameRequested);
            BindButton(settingsButton, HandleSettingsRequested);
            BindButton(quitButton, HandleQuitRequested);
            RefreshLoadGameButtonState();
        }

        private void BindButton(Button button, UnityAction callback)
        {
            if (button == null) return;

            if (_boundClickHandlers.TryGetValue(button, out var existingHandler))
                button.onClick.RemoveListener(existingHandler);

            button.onClick.RemoveListener(callback);

            UnityAction wrappedHandler = () =>
            {
                PlayMenuClickSfx();
                UiMenuUtility.DeselectCurrent();
                callback?.Invoke();
            };

            _boundClickHandlers[button] = wrappedHandler;
            button.onClick.AddListener(wrappedHandler);
        }

        private void AutoResolveReferences()
        {
            if (menuRoot == null) menuRoot = gameObject;

            ResolveButtons();
            ResolvePanels();
            ResolveTitleGraphic();
        }

        private void ResolveButtons()
        {
            ResolveButtonByNames(ref continueButton, "ContinueButton", "Continue");
            ResolveButtonByNames(ref startGameButton, "StartGame");
            ResolveButtonByNames(ref loadGameButton, "LoadGameButton", "LoadGame");
            ResolveButtonByNames(ref settingsButton, "SettingsButton", "Settings");
            ResolveButtonByNames(ref quitButton, "QuitButton", "Quit");
        }

        private void ResolvePanels()
        {
            if (characterCreatorPanel == null)
                characterCreatorPanel = FindPanelController<CharacterCreatorController>("CharacterCreatorPanel");

            if (settingsPanel == null) settingsPanel = FindPanelController<Zombera.UI.SettingsV2.SettingsPageController>("SettingsPanel");

            if (loadSavePanel == null) loadSavePanel = FindPanelController<SaveGameMenuController>("LoadSavePanel");
        }

        private void ResolveTitleGraphic()
        {
            if (mainTitleImage != null || menuRoot == null) return;

            var titleTransform = FindTransformByName(menuRoot.transform, "MainTitleImage");

            if (titleTransform == null) titleTransform = FindTransformByName(menuRoot.transform, "TitleImage");

            if (titleTransform == null)
            {
                var parentCanvas = menuRoot.GetComponentInParent<Canvas>();

                if (parentCanvas != null)
                {
                    titleTransform = FindTransformByName(parentCanvas.transform, "MainTitleImage");

                    if (titleTransform == null)
                        titleTransform = FindTransformByName(parentCanvas.transform, "TitleImage");

                    if (titleTransform == null)
                        titleTransform = FindTransformByName(parentCanvas.transform, "Screen Space Overlay");

                    if (titleTransform == null)
                    {
                        var canvasGraphic = parentCanvas.GetComponent<Graphic>();

                        if (canvasGraphic != null) mainTitleImage = canvasGraphic;
                    }
                }
            }

            if (mainTitleImage != null || titleTransform == null) return;

            mainTitleImage = titleTransform.GetComponent<Graphic>();

            if (mainTitleImage == null) mainTitleImage = titleTransform.GetComponentInChildren<Graphic>(true);
        }

        private void ResolveButtonByNames(ref Button button, params string[] buttonNames)
        {
            if (button != null || buttonNames == null) return;

            for (var index = 0; index < buttonNames.Length; index++)
            {
                button = FindButtonByName(buttonNames[index]);
                if (button != null) return;
            }
        }

        private Button FindButtonByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return null;

            if (menuRoot != null)
            {
                var buttons = menuRoot.GetComponentsInChildren<Button>(true);

                foreach (var button in buttons)
                {
                    if (button != null && string.Equals(button.name, objectName, StringComparison.OrdinalIgnoreCase))
                        return button;
                }
            }

            var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var button in allButtons)
            {
                if (button != null && string.Equals(button.name, objectName, StringComparison.OrdinalIgnoreCase))
                    return button;
            }

            return null;
        }

        private TPanel FindPanelController<TPanel>(string panelObjectName) where TPanel : MonoBehaviour
        {
            TPanel panelController;

            if (menuRoot != null)
            {
                panelController = menuRoot.GetComponentInChildren<TPanel>(true);

                if (panelController != null) return panelController;

                if (!string.IsNullOrWhiteSpace(panelObjectName))
                {
                    var panelTransform = FindTransformByName(menuRoot.transform, panelObjectName);

                    if (panelTransform != null)
                    {
                        panelController = panelTransform.GetComponent<TPanel>();

                        if (panelController != null) return panelController;

                        panelController = panelTransform.gameObject.AddComponent<TPanel>();
                        Debug.LogWarning(
                            $"[MainMenuController] Auto-added {typeof(TPanel).Name} to '{panelTransform.name}'.",
                            panelTransform.gameObject);

                        return panelController;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(panelObjectName))
                return FindFirstObjectByType<TPanel>(FindObjectsInactive.Include);

            var panelObject = GameObject.Find(panelObjectName);
            if (panelObject == null) return FindFirstObjectByType<TPanel>(FindObjectsInactive.Include);

            panelController = panelObject.GetComponent<TPanel>();
            if (panelController != null) return panelController;

            panelController = panelObject.AddComponent<TPanel>();
            Debug.LogWarning(
                $"[MainMenuController] Auto-added {typeof(TPanel).Name} to '{panelObject.name}'.",
                panelObject);

            return panelController;
        }

        private static Transform FindTransformByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName)) return null;

            if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase)) return root;

            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                var found = FindTransformByName(root.GetChild(childIndex), objectName);

                if (found != null) return found;
            }

            return null;
        }
    }
}
