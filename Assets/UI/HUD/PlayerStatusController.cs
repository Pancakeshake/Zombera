using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private TextMeshProUGUI moraleValueText;

        [Header("Bars")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Slider moraleSlider;

        public bool IsInitialized { get; private set; }

        private HUDManager hudManager;

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
                stamina01 = 1f,
                morale01 = 1f
            });

            IsInitialized = true;

            // TODO: Bind live player stat feed for health/stamina/morale.
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
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
            SetNormalizedValue(moraleSlider, moraleValueText, statusData.morale01);

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
        [Range(0f, 1f)] public float morale01;
        public Sprite portrait;
    }
}