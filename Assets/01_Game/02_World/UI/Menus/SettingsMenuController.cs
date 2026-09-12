#region

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zombera.Systems;

#endregion

namespace Zombera.UI.Menus
{
    /// <summary>
    ///     Controls simple runtime settings for audio and quality.
    /// </summary>
    public sealed class SettingsMenuController : MonoBehaviour
    {
        [Header("Root")] [SerializeField] private GameObject panelRoot;

        [Header("Settings")] [SerializeField] private Slider masterVolumeSlider;

        [SerializeField] private TMP_Dropdown qualityDropdown;

        [Header("Buttons")] [SerializeField] private Button closeButton;

        public event Action OnShow;
        public event Action OnHide;

        [Header("Input Rebinding")] [SerializeField]
        private PlayerInputController playerInputController;

        public bool IsInitialized { get; private set; }
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (IsInitialized) return;

            if (panelRoot == null) panelRoot = gameObject;

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
                masterVolumeSlider.onValueChanged.AddListener(ApplyMasterVolume);
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.SetValueWithoutNotify(QualitySettings.GetQualityLevel());
                qualityDropdown.onValueChanged.AddListener(ApplyQualityLevel);
            }

            BindButton(closeButton, Hide);

            if (playerInputController == null)
                playerInputController = FindFirstObjectByType<PlayerInputController>();

            Hide();
            IsInitialized = true;
        }

        public bool BeginInputRebind(string actionName, int bindingIndex = 0)
        {
            if (playerInputController == null) return false;
            return playerInputController.StartInteractiveRebind(actionName, bindingIndex);
        }

        public void CancelInputRebind()
        {
            playerInputController?.CancelActiveInputRebind();
        }

        public string GetInputBindingDisplayString(string actionName, int bindingIndex = 0)
        {
            if (playerInputController == null) return string.Empty;
            return playerInputController.GetBindingDisplayString(actionName, bindingIndex);
        }

        public void ResetInputBindingsToDefault()
        {
            playerInputController?.ResetInputBindingOverrides();
        }

        public void Show()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            OnShow?.Invoke();
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            OnHide?.Invoke();
        }

        private static void ApplyMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }

        private static void ApplyQualityLevel(int qualityLevel)
        {
            var clamped = Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1);
            if (!Application.isPlaying) return;

            if (clamped == QualitySettings.GetQualityLevel()) return;

            QualitySettings.SetQualityLevel(clamped, true);
        }

        private static void BindButton(Button button, UnityAction callback)
        {
            if (button == null) return;

            button.onClick.AddListener(callback);
        }
    }
}