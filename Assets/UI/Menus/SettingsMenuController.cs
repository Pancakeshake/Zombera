using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.Menus
{
    /// <summary>
    /// Controls simple runtime settings for audio and quality.
    /// </summary>
    public sealed class SettingsMenuController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TMP_Dropdown qualityDropdown;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        public bool IsInitialized { get; private set; }
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

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

        private static void ApplyMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }

        private static void ApplyQualityLevel(int qualityLevel)
        {
            int clamped = Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1);
            if (!Application.isPlaying)
            {
                return;
            }

            if (clamped == QualitySettings.GetQualityLevel())
            {
                return;
            }

            QualitySettings.SetQualityLevel(clamped, true);
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