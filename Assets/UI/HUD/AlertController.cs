using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    /// <summary>
    /// Controls bottom-right alert panel display state and message presentation.
    /// </summary>
    public sealed class AlertController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Image panelBackground;
        [SerializeField] private Image alertIconImage;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI alertTitleText;
        [SerializeField] private TextMeshProUGUI alertBodyText;

        [Header("Severity Colors")]
        [SerializeField] private Color infoColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color dangerColor = Color.red;

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

            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = GetComponent<CanvasGroup>();
            }

            ClearAlert();
            IsInitialized = true;

            // TODO: Add non-blocking alert queue and timed fade logic.
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void ShowAlert(AlertViewData alertData)
        {
            if (alertTitleText != null)
            {
                alertTitleText.text = string.IsNullOrWhiteSpace(alertData.title) ? "Alert" : alertData.title;
            }

            if (alertBodyText != null)
            {
                alertBodyText.text = alertData.message;
            }

            if (alertIconImage != null)
            {
                alertIconImage.sprite = alertData.icon;
                alertIconImage.enabled = alertData.icon != null;
            }

            ApplySeverity(alertData.severity);
            SetCanvasGroup(1f, true);
        }

        public void ClearAlert()
        {
            if (alertTitleText != null)
            {
                alertTitleText.text = string.Empty;
            }

            if (alertBodyText != null)
            {
                alertBodyText.text = string.Empty;
            }

            if (alertIconImage != null)
            {
                alertIconImage.sprite = null;
                alertIconImage.enabled = false;
            }

            ApplySeverity(AlertSeverity.Info);
            SetCanvasGroup(0f, false);
        }

        private void ApplySeverity(AlertSeverity severity)
        {
            if (panelBackground == null)
            {
                return;
            }

            switch (severity)
            {
                case AlertSeverity.Info:
                    panelBackground.color = infoColor;
                    break;
                case AlertSeverity.Warning:
                    panelBackground.color = warningColor;
                    break;
                case AlertSeverity.Danger:
                    panelBackground.color = dangerColor;
                    break;
            }
        }

        private void SetCanvasGroup(float alpha, bool interactable)
        {
            if (panelCanvasGroup == null)
            {
                return;
            }

            panelCanvasGroup.alpha = alpha;
            panelCanvasGroup.interactable = interactable;
            panelCanvasGroup.blocksRaycasts = interactable;
        }
    }

    [Serializable]
    public struct AlertViewData
    {
        public string title;
        public string message;
        public Sprite icon;
        public AlertSeverity severity;
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Danger
    }
}