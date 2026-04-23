using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;

namespace Zombera.UI
{
    /// <summary>
    /// Controls bottom-left player status panel references and value presentation.
    /// </summary>
    public sealed class PlayerStatusController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;

        [Header("Portrait")]
        [SerializeField] private Image portraitImage;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI conditionText;
        [SerializeField] private TextMeshProUGUI healthValueText;
        [SerializeField] private TextMeshProUGUI staminaValueText;

        [Header("Bars")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider staminaSlider;

        public bool IsInitialized { get; private set; }

        private HUDManager hudManager;
        private UnitHealth boundHealth;
        private UnitStats  boundStats;

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized)
            {
                return;
            }

            hudManager = manager;

            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            SetStatus(new PlayerStatusViewData
            {
                playerName = "Player",
                conditionText = "Stable",
                health01 = 1f,
                stamina01 = 1f
            });

            IsInitialized = true;
        }

        /// <summary>
        /// Subscribes to live health, stamina, and morale events on the given unit.
        /// Safe to call multiple times — unbinds the previous unit first.
        /// </summary>
        public void BindUnit(Unit unit)
        {
            UnbindUnit();

            if (unit == null) return;

            boundHealth = unit.Health;
            boundStats  = unit.Stats;

            if (boundHealth != null)
            {
                boundHealth.Damaged += OnHealthChanged;
                boundHealth.Healed  += OnHealthChanged;
            }

            if (boundStats != null)
            {
                boundStats.StaminaChanged += OnStaminaChanged;
            }

            RefreshAll();
        }

        public void UnbindUnit()
        {
            if (boundHealth != null)
            {
                boundHealth.Damaged -= OnHealthChanged;
                boundHealth.Healed  -= OnHealthChanged;
                boundHealth = null;
            }

            if (boundStats != null)
            {
                boundStats.StaminaChanged -= OnStaminaChanged;
                boundStats = null;
            }
        }

        private void OnDestroy() => UnbindUnit();

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void OnHealthChanged(float _) => RefreshAll();

        private void OnStaminaChanged(float current, float max)
        {
            SetNormalizedValue(staminaSlider, staminaValueText,
                max > 0f ? Mathf.Clamp01(current / max) : 0f);
        }

        private void RefreshAll()
        {
            float health01 = 1f;
            if (boundHealth != null && boundHealth.MaxHealth > 0f)
                health01 = Mathf.Clamp01(boundHealth.CurrentHealth / boundHealth.MaxHealth);
            SetNormalizedValue(healthSlider, healthValueText, health01);

            float stamina01 = 1f;
            if (boundStats != null && boundStats.MaxStamina > 0f)
                stamina01 = Mathf.Clamp01(boundStats.Stamina / boundStats.MaxStamina);
            SetNormalizedValue(staminaSlider, staminaValueText, stamina01);
        }

        public void SetStatus(PlayerStatusViewData statusData)
        {
            if (playerNameText != null)
            {
                playerNameText.text = string.IsNullOrWhiteSpace(statusData.playerName) ? "Player" : statusData.playerName;
            }

            if (conditionText != null)
            {
                conditionText.text = string.IsNullOrWhiteSpace(statusData.conditionText) ? "Stable" : statusData.conditionText;
            }

            SetNormalizedValue(healthSlider, healthValueText, statusData.health01);
            SetNormalizedValue(staminaSlider, staminaValueText, statusData.stamina01);

            if (portraitImage != null)
            {
                portraitImage.sprite = statusData.portrait;
                portraitImage.enabled = statusData.portrait != null;
            }
        }

        private static void SetNormalizedValue(Slider slider, TextMeshProUGUI valueLabel, float value01)
        {
            float clamped = Mathf.Clamp01(value01);

            if (slider != null)
            {
                slider.value = clamped;
            }

            if (valueLabel != null)
            {
                valueLabel.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            }
        }
    }

    [Serializable]
    public struct PlayerStatusViewData
    {
        public string playerName;
        public string conditionText;
        [Range(0f, 1f)] public float health01;
        [Range(0f, 1f)] public float stamina01;
        public Sprite portrait;
    }
}