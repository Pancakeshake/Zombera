using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.Menus
{
    /// <summary>
    /// Handles lightweight character creation panel interactions.
    /// </summary>
    public sealed class CharacterCreatorController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Fields")]
        [SerializeField] private TMP_InputField characterNameInput;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        [Header("Defaults")]
        [SerializeField] private string defaultCharacterName = "Survivor";

        public bool IsInitialized { get; private set; }
        public string SelectedCharacterName { get; private set; }

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

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            BindButton(confirmButton, ConfirmSelection);
            BindButton(closeButton, Hide);

            SelectedCharacterName = string.IsNullOrWhiteSpace(defaultCharacterName)
                ? "Survivor"
                : defaultCharacterName;

            if (characterNameInput != null)
            {
                characterNameInput.text = SelectedCharacterName;
            }

            Hide();
            IsInitialized = true;
        }

        public void Show()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public void ConfirmSelection()
        {
            if (characterNameInput != null && !string.IsNullOrWhiteSpace(characterNameInput.text))
            {
                SelectedCharacterName = characterNameInput.text.Trim();
            }

            // TODO: Persist selected character setup into player spawn/save profile.
            Hide();
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