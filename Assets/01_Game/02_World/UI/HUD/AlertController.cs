#region

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI
{
    /// <summary>
    ///     Controls bottom-right alert panel display state and message presentation.
    /// </summary>
    public sealed class AlertController : MonoBehaviour
    {
        [Header("Root")] [SerializeField] private RectTransform panelRoot;

        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Image panelBackground;
        [SerializeField] private Image alertIconImage;

        [Header("Text")] [SerializeField] private TextMeshProUGUI alertTitleText;

        [SerializeField] private TextMeshProUGUI alertBodyText;

        [Header("Severity Colors")] [SerializeField]
        private Color infoColor = Color.white;

        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color dangerColor = Color.red;

        private readonly Queue<AlertViewData> _alertQueue = new();

        public bool IsInitialized { get; private set; }

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized) return;
            _ = manager;

            if (panelRoot == null) panelRoot = transform as RectTransform;

            if (panelCanvasGroup == null) panelCanvasGroup = GetComponent<CanvasGroup>();

            ClearAlert();
            IsInitialized = true;
            StartCoroutine(AlertQueueRoutine());
        }

        private IEnumerator AlertQueueRoutine()
        {
            const float displayDuration = 3f;
            const float fadeOutDuration = 0.4f;

            while (isActiveAndEnabled)
            {
                if (!CanDisplayNextAlert())
                {
                    yield return null;
                    continue;
                }

                ShowNextAlert();
                yield return new WaitForSeconds(displayDuration);
                yield return FadeOutAlert(fadeOutDuration);
                ResetAlertToHidden();
            }
        }

        private bool CanDisplayNextAlert()
        {
            return _alertQueue.Count > 0 && (panelCanvasGroup == null || panelCanvasGroup.alpha < 0.05f);
        }

        private void ShowNextAlert()
        {
            var next = _alertQueue.Dequeue();
            ApplyAlert(next);

            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOutAlert(float fadeOutDuration)
        {
            var elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;

                if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f - elapsed / fadeOutDuration;

                yield return null;
            }
        }

        private void ResetAlertToHidden()
        {
            ClearAlert();

            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        [ContextMenu("HUD/Show Test Alert")]
        private void ShowTestAlert()
        {
            ShowAlert(new AlertViewData
            {
                title = "Test Alert",
                message = "This is a debug alert.",
                icon = null,
                severity = AlertSeverity.Info
            });
        }

        // ReSharper disable once UnusedMember.Global
        public void ShowAlert(AlertViewData alertData)
        {
            _alertQueue.Enqueue(alertData);
        }

        private void ApplyAlert(AlertViewData alertData)
        {
            if (alertTitleText != null)
                alertTitleText.text = string.IsNullOrWhiteSpace(alertData.title) ? "Alert" : alertData.title;

            if (alertBodyText != null) alertBodyText.text = alertData.message;

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
            if (alertTitleText != null) alertTitleText.text = string.Empty;

            if (alertBodyText != null) alertBodyText.text = string.Empty;

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
            if (panelBackground == null) return;

            panelBackground.color = severity switch
            {
                AlertSeverity.Info => infoColor,
                AlertSeverity.Warning => warningColor,
                AlertSeverity.Danger => dangerColor,
                _ => infoColor
            };
        }

        private void SetCanvasGroup(float alpha, bool interactable)
        {
            if (panelCanvasGroup == null) return;

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