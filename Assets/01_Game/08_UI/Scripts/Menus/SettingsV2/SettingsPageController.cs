using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Zombera.UI.SettingsV2
{
    public class SettingsPageController : MonoBehaviour
    {
        public event Action OnHide;

        [Header("Panels")]
        [SerializeField] private RectTransform _leftPanel;
        [SerializeField] private RectTransform _centerPanel;
        [SerializeField] private RectTransform _rightPanel;
        [SerializeField] private RectTransform _contentRoot;

        [Header("Info Panel")]
        [SerializeField] private TextMeshProUGUI _infoTitle;
        [SerializeField] private TextMeshProUGUI _infoDescription;

        [Header("Controls")]
        [SerializeField] private Button _applyButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _closeButton;

        [Header("Factory")]
        [SerializeField] private SettingsRowFactory _rowFactory;

        private List<SettingItemData> _settingsData = new List<SettingItemData>();
        private bool _hasPendingChanges = false;
        private SettingCategory _currentCategory = SettingCategory.General;
        private bool _awaitingConfirmation = false;

        [Header("Animation")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _mainFrame;

        private Coroutine _animationRoutine;
        private Vector2 _mainFrameDefaultPos;

        public bool IsVisible => gameObject.activeSelf;
        public void Show() 
        {
            gameObject.SetActive(true);
            OpenSettings();
        }
        public void Hide() => CloseSettings();

        private void Awake()
        {
            if (_mainFrame != null) _mainFrameDefaultPos = _mainFrame.anchoredPosition;
            
            InitializeSampleData();
            
            if (_applyButton != null)
            {
                _applyButton.onClick.AddListener(ApplySettings);
                _applyButton.interactable = false;
            }

            if (_backButton != null) _backButton.onClick.AddListener(() => TryCloseOrBack("Back"));
            if (_closeButton != null) _closeButton.onClick.AddListener(() => TryCloseOrBack("Close"));

            WireNavigationButtons();
            }

            public void OpenSettings()
            {
                if (!gameObject.activeInHierarchy)
                {
                    gameObject.SetActive(true);
                }
                if (_animationRoutine != null) StopCoroutine(_animationRoutine);
                _animationRoutine = StartCoroutine(OpenRoutine());
            }

            private IEnumerator OpenRoutine()
            {
            if (_canvasGroup != null) _canvasGroup.alpha = 0;
            if (_mainFrame != null) _mainFrame.anchoredPosition = _mainFrameDefaultPos - new Vector2(0, 12);

            // Start staggered nav fade
            StartCoroutine(StaggeredNavFade());

            float elapsed = 0;
            while (elapsed < 0.22f)
            {
                elapsed += Time.unscaledDeltaTime;
                
                if (_canvasGroup != null)
                {
                    float alphaT = Mathf.Clamp01(elapsed / 0.2f);
                    _canvasGroup.alpha = alphaT;
                }

                if (_mainFrame != null)
                {
                    float slideT = Mathf.Clamp01(elapsed / 0.22f);
                    // Use a simple ease out quad for smoother slide
                    float easedT = 1 - (1 - slideT) * (1 - slideT);
                    _mainFrame.anchoredPosition = Vector2.Lerp(_mainFrameDefaultPos - new Vector2(0, 12), _mainFrameDefaultPos, easedT);
                }

                yield return null;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1;
            if (_mainFrame != null) _mainFrame.anchoredPosition = _mainFrameDefaultPos;
            _animationRoutine = null;
        }

        private IEnumerator StaggeredNavFade()
        {
            if (_leftPanel == null) yield break;
            
            var navButtons = _leftPanel.GetComponentsInChildren<SettingsNavigationButton>(true);
            foreach (var btn in navButtons)
            {
                var cg = btn.GetComponent<CanvasGroup>();
                if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0;
            }

            foreach (var btn in navButtons)
            {
                StartCoroutine(FadeInCG(btn.GetComponent<CanvasGroup>(), 0.15f));
                yield return new WaitForSecondsRealtime(0.02f);
            }
        }

        private IEnumerator FadeInCG(CanvasGroup cg, float duration)
        {
            if (cg == null) yield break;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            cg.alpha = 1;
        }

        private IEnumerator CloseRoutine()
        {
            float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1;
            float elapsed = 0;
            float duration = 0.15f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, elapsed / duration);
                }
                yield return null;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            _animationRoutine = null;
            OnHide?.Invoke();
            }

        public void CloseSettings()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }
            if (_animationRoutine != null) StopCoroutine(_animationRoutine);
            _animationRoutine = StartCoroutine(CloseRoutine());
        }

        private void Start()
        {
            // Initial selection
            SelectNavButton(SettingCategory.General);
            SwitchCategory(SettingCategory.General);
        }

        private void Update()
        {
            HandleResponsiveUI();
        }

        private void HandleResponsiveUI()
        {
            if (_rightPanel != null)
            {
                bool isWide = Screen.width >= 1024;
                if (_rightPanel.gameObject.activeSelf != isWide)
                {
                    _rightPanel.gameObject.SetActive(isWide);
                }
            }
        }

        private void InitializeSampleData()
        {
            _settingsData = new List<SettingItemData>
            {
                new SettingItemData { Title = "Language", Description = "Select your preferred language for text and dialogue.", Category = SettingCategory.General, Type = SettingType.Dropdown, Options = new List<string>{"English", "French", "Spanish", "German", "Japanese"}, DefaultValue = 0 },
                new SettingItemData { Title = "Subtitles", Description = "Enable or disable in-game subtitles for dialogue.", Category = SettingCategory.General, Type = SettingType.Toggle, DefaultValue = 1 },
                new SettingItemData { Title = "Text Size", Description = "Adjust the scale of UI text for better readability.", Category = SettingCategory.Accessibility, Type = SettingType.Slider, MinValue = 0.5f, MaxValue = 2.0f, DefaultValue = 1.0f },
                
                new SettingItemData { Title = "Resolution", Description = "Change the display resolution. Higher values require more GPU power.", Category = SettingCategory.Graphics, Type = SettingType.Dropdown, Options = new List<string>{"1920x1080", "2560x1440", "3840x2160"}, DefaultValue = 0 },
                new SettingItemData { Title = "Texture Quality", Description = "Adjust the detail level of in-game textures.", Category = SettingCategory.Graphics, Type = SettingType.Segmented, Options = new List<string>{"Low", "Med", "High", "Ultra"}, DefaultValue = 2 },
                new SettingItemData { Title = "VSync", Description = "Synchronize frame rate with monitor refresh rate to prevent tearing.", Category = SettingCategory.Graphics, Type = SettingType.Toggle, DefaultValue = 0 },
                
                new SettingItemData { Title = "Master Volume", Description = "Overall game volume level.", Category = SettingCategory.Audio, Type = SettingType.Slider, MinValue = 0, MaxValue = 1, DefaultValue = 0.8f },
                new SettingItemData { Title = "Music Volume", Description = "Volume of background music.", Category = SettingCategory.Audio, Type = SettingType.Slider, MinValue = 0, MaxValue = 1, DefaultValue = 0.5f },
                new SettingItemData { Title = "SFX Volume", Description = "Volume of sound effects like gunfire and footsteps.", Category = SettingCategory.Audio, Type = SettingType.Slider, MinValue = 0, MaxValue = 1, DefaultValue = 0.7f }
            };
        }

        private void WireNavigationButtons()
        {
            if (_leftPanel == null) return;

            var navButtons = _leftPanel.GetComponentsInChildren<SettingsNavigationButton>(true);
            foreach (var btn in navButtons)
            {
                var toggle = btn.GetComponent<Toggle>();
                if (toggle != null)
                {
                    string btnName = btn.gameObject.name.Replace("_Button", "");
                    if (System.Enum.TryParse(btnName, true, out SettingCategory category))
                    {
                        toggle.onValueChanged.AddListener((isOn) => {
                            if (isOn) SwitchCategory(category);
                        });
                    }
                }
            }
        }

        private void SelectNavButton(SettingCategory category)
        {
            if (_leftPanel == null) return;
            var navButtons = _leftPanel.GetComponentsInChildren<SettingsNavigationButton>(true);
            foreach (var btn in navButtons)
            {
                string btnName = btn.gameObject.name.Replace("_Button", "");
                if (System.Enum.TryParse(btnName, true, out SettingCategory cat) && cat == category)
                {
                    var toggle = btn.GetComponent<Toggle>();
                    if (toggle != null) toggle.SetIsOnWithoutNotify(true);
                }
            }
        }

        public void SwitchCategory(SettingCategory category)
        {
            _currentCategory = category;

            // Clear existing rows
            if (_contentRoot != null)
            {
                foreach (Transform child in _contentRoot)
                {
                    Destroy(child.gameObject);
                }
            }

            // Update Info Panel
            if (_infoTitle != null) _infoTitle.text = category.ToString().ToUpper();
            if (_infoDescription != null) 
            {
                _infoDescription.text = GetCategoryDescription(category);
            }

            // Create new rows
            var items = _settingsData.Where(x => x.Category == category).ToList();
            if (_rowFactory != null)
            {
                _rowFactory.CreateSectionHeader(category.ToString().ToUpper(), _contentRoot);
                foreach (var item in items)
                {
                    var rowUI = _rowFactory.CreateRow(item, _contentRoot);
                    if (rowUI != null && rowUI.Control != null)
                    {
                        SubscribeToChanges(rowUI.Control);
                    }
                }
            }
        }

        private string GetCategoryDescription(SettingCategory category)
        {
            return category switch
            {
                SettingCategory.General => "Basic game settings including language and subtitles.",
                SettingCategory.Graphics => "Visual quality and performance settings.",
                SettingCategory.Audio => "Volume levels for music, effects, and master output.",
                SettingCategory.Controls => "Input bindings and sensitivity settings.",
                SettingCategory.Gameplay => "Settings related to game difficulty and mechanics.",
                SettingCategory.Accessibility => "Options to improve game accessibility and readability.",
                _ => "Adjust settings for this category."
            };
        }

        private void SubscribeToChanges(GameObject control)
        {
            var toggle = control.GetComponent<Toggle>();
            if (toggle != null) toggle.onValueChanged.AddListener(_ => SetDirty());

            var slider = control.GetComponentInChildren<Slider>();
            if (slider != null) slider.onValueChanged.AddListener(_ => SetDirty());

            var dropdown = control.GetComponent<TMP_Dropdown>();
            if (dropdown != null) dropdown.onValueChanged.AddListener(_ => SetDirty());
        }

        private void SetDirty()
        {
            if (!_hasPendingChanges)
            {
                _hasPendingChanges = true;
                if (_applyButton != null) _applyButton.interactable = true;
                _awaitingConfirmation = false;
            }
        }

        public void ApplySettings()
        {
            Debug.Log("Settings Applied and Saved");
            _hasPendingChanges = false;
            _awaitingConfirmation = false;
            if (_applyButton != null) _applyButton.interactable = false;
        }

        private void TryCloseOrBack(string action)
        {
            if (_hasPendingChanges && !_awaitingConfirmation)
            {
                Debug.LogWarning($"Unsaved changes! Click '{action}' again to confirm and discard changes.");
                _awaitingConfirmation = true;
                return;
            }

            CloseSettings();
        }


    }
}
