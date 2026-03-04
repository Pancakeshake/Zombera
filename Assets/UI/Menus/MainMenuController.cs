using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zombera.Core;

namespace Zombera.UI.Menus
{
    /// <summary>
    /// Controls top-level main menu actions and routes into menu subpanels.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject menuRoot;

        [Header("Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button characterCreatorButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private CharacterCreatorController characterCreatorPanel;
        [SerializeField] private SettingsMenuController settingsPanel;

        [Header("Scene")]
        [SerializeField] private string worldSceneName = "World";

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            if (menuRoot == null)
            {
                menuRoot = gameObject;
            }

            BindButton(startGameButton, HandleStartGameRequested);
            BindButton(characterCreatorButton, HandleCharacterCreatorRequested);
            BindButton(settingsButton, HandleSettingsRequested);
            BindButton(quitButton, HandleQuitRequested);

            characterCreatorPanel?.Initialize();
            settingsPanel?.Initialize();

            characterCreatorPanel?.Hide();
            settingsPanel?.Hide();
            Show();

            IsInitialized = true;
        }

        public void Show()
        {
            if (menuRoot != null)
            {
                menuRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }
        }

        private void HandleStartGameRequested()
        {
            if (GameManager.Instance != null)
            {
                if (!GameManager.Instance.IsInitialized)
                {
                    GameManager.Instance.InitializeSystems();
                }

                GameManager.Instance.StartNewGame();
                Hide();
                return;
            }

            if (!string.IsNullOrWhiteSpace(worldSceneName))
            {
                SceneManager.LoadScene(worldSceneName);
            }
        }

        private void HandleCharacterCreatorRequested()
        {
            characterCreatorPanel?.Show();
        }

        private void HandleSettingsRequested()
        {
            settingsPanel?.Show();
        }

        private static void HandleQuitRequested()
        {
            Application.Quit();
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(callback);
        }
    }
}