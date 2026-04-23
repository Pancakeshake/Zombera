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
            StartCoroutine(AlertQueueRoutine());
        }

        private readonly System.Collections.Generic.Queue<AlertViewData> alertQueue
            = new System.Collections.Generic.Queue<AlertViewData>();

        /// <summary>Enqueues an alert. If no alert is showing it plays immediately.</summary>
        public void EnqueueAlert(AlertViewData alertData, float displaySeconds = 3f)
        {
            alertQueue.Enqueue(alertData);
            _ = displaySeconds; // Consumed inside the coroutine.
        }

        private System.Collections.IEnumerator AlertQueueRoutine()
        {
            float displayDuration = 3f;
            float fadeOutDuration = 0.4f;

            while (true)
            {
                if (alertQueue.Count > 0 && (panelCanvasGroup == null || panelCanvasGroup.alpha < 0.05f))
                {
                    AlertViewData next = alertQueue.Dequeue();
                    ShowAlert(next);

                    if (panelCanvasGroup != null)
                    {
                        panelCanvasGroup.alpha = 1f;
                    }

                    yield return new UnityEngine.WaitForSeconds(displayDuration);

                    // Fade out.
                    float elapsed = 0f;

                    while (elapsed < fadeOutDuration)
                    {
                        elapsed += Time.deltaTime;

                        if (panelCanvasGroup != null)
                        {
                            panelCanvasGroup.alpha = 1f - (elapsed / fadeOutDuration);
                        }

                        yield return null;
                    }

                    ClearAlert();

                    if (panelCanvasGroup != null)
                    {
                        panelCanvasGroup.alpha = 0f;
                    }
                }
                else
                {
                    yield return null;
                }
            }
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