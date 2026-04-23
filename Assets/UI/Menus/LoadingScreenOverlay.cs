using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI;

namespace Zombera.UI.Menus
{
    /// <summary>
    /// Persistent full-screen black overlay used to hide scene transitions and initialization.
    /// </summary>
    public sealed class LoadingScreenOverlay : MonoBehaviour
    {
        private const float VisualProgressSpeed = 0.65f;

        private static LoadingScreenOverlay instance;
        private static Sprite solidSprite;

        private Canvas canvas;
        private Image progressFillImage;
        private Text percentageText;
        private Text statusText;
        private float currentProgress01;
        private float displayedProgress01;
        private string currentStatus = "Loading...";

        public static bool IsVisible => instance != null && instance.gameObject.activeInHierarchy;
        public static float VisibleProgress01 => instance != null ? instance.displayedProgress01 : 0f;

        public static void Show(string status = null)
        {
            EnsureInstance();

            if (instance == null)
            {
                return;
            }

            instance.gameObject.SetActive(true);
            if (instance.canvas != null)
            {
                instance.canvas.enabled = true;
            }

            instance.displayedProgress01 = instance.currentProgress01;

            if (!string.IsNullOrWhiteSpace(status))
            {
                instance.currentStatus = status.Trim();
            }

            instance.ApplyVisualState();
        }

        public static void SetProgress(float progress01, string status = null)
        {
            EnsureInstance();

            if (instance == null)
            {
                return;
            }

            instance.currentProgress01 = Mathf.Clamp01(progress01);

            if (!string.IsNullOrWhiteSpace(status))
            {
                instance.currentStatus = status.Trim();
            }

            instance.ApplyVisualState();
        }

        public static void Hide()
        {
            if (instance == null)
            {
                return;
            }

            Destroy(instance.gameObject);
            instance = null;
        }

        public static IEnumerator WaitForVisualProgress(float progress01, float timeoutSeconds = 2f)
        {
            EnsureInstance();

            if (instance == null)
            {
                yield break;
            }

            float target = Mathf.Clamp01(progress01);
            float timeout = Mathf.Max(0f, timeoutSeconds);
            float elapsed = 0f;

            while (instance != null && instance.displayedProgress01 + 0.001f < target)
            {
                if (timeout > 0f && elapsed >= timeout)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            GameObject root = new GameObject("LoadingScreenOverlay");
            instance = root.AddComponent<LoadingScreenOverlay>();
            instance.BuildUi(root);
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void BuildUi(GameObject root)
        {
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ZomberaCanvasLayer.Loading;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            GameObject blackPanel = new GameObject("BlackPanel");
            blackPanel.transform.SetParent(root.transform, false);

            RectTransform panelRect = blackPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image image = blackPanel.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            GameObject progressRoot = new GameObject("LoadingProgressRoot");
            progressRoot.transform.SetParent(blackPanel.transform, false);

            RectTransform progressRootRect = progressRoot.AddComponent<RectTransform>();
            progressRootRect.anchorMin = new Vector2(0.08f, 0f);
            progressRootRect.anchorMax = new Vector2(0.92f, 0f);
            progressRootRect.pivot = new Vector2(0.5f, 0f);
            progressRootRect.sizeDelta = new Vector2(0f, 116f);
            progressRootRect.anchoredPosition = new Vector2(0f, 26f);

            statusText = CreateText(progressRoot.transform, "StatusText", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), 26);
            statusText.alignment = TextAnchor.UpperLeft;
            statusText.text = currentStatus;

            percentageText = CreateText(progressRoot.transform, "PercentageText", new Vector2(0f, 0.52f), new Vector2(1f, 0.52f), new Vector2(1f, 1f), 24);
            percentageText.alignment = TextAnchor.UpperRight;
            percentageText.text = "0%";

            GameObject barBackground = new GameObject("ProgressBarBackground");
            barBackground.transform.SetParent(progressRoot.transform, false);

            RectTransform barBackgroundRect = barBackground.AddComponent<RectTransform>();
            barBackgroundRect.anchorMin = new Vector2(0f, 0f);
            barBackgroundRect.anchorMax = new Vector2(1f, 0f);
            barBackgroundRect.pivot = new Vector2(0.5f, 0f);
            barBackgroundRect.sizeDelta = new Vector2(0f, 28f);
            barBackgroundRect.anchoredPosition = Vector2.zero;

            Image barBackgroundImage = barBackground.AddComponent<Image>();
            barBackgroundImage.sprite = GetSolidSprite();
            barBackgroundImage.type = Image.Type.Sliced;
            barBackgroundImage.color = new Color(1f, 1f, 1f, 0.18f);

            GameObject barFill = new GameObject("ProgressBarFill");
            barFill.transform.SetParent(barBackground.transform, false);

            RectTransform barFillRect = barFill.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = Vector2.one;
            barFillRect.offsetMin = new Vector2(3f, 3f);
            barFillRect.offsetMax = new Vector2(-3f, -3f);

            progressFillImage = barFill.AddComponent<Image>();
            progressFillImage.sprite = GetSolidSprite();
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFillImage.fillAmount = 0f;
            progressFillImage.color = new Color(0.20f, 0.84f, 0.28f, 0.98f);

            ApplyVisualState();
        }

        private void Update()
        {
            if (Mathf.Approximately(displayedProgress01, currentProgress01))
            {
                return;
            }

            displayedProgress01 = Mathf.MoveTowards(displayedProgress01, currentProgress01, Time.unscaledDeltaTime * VisualProgressSpeed);
            ApplyVisualState();
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            int fontSize)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(0f, 32f);
            rect.anchoredPosition = Vector2.zero;

            Text text = textObject.AddComponent<Text>();
            text.font = ResolveDefaultFont();
            text.fontSize = fontSize;
            text.color = new Color(1f, 1f, 1f, 0.92f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void ApplyVisualState()
        {
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = displayedProgress01;
            }

            int percent = Mathf.RoundToInt(displayedProgress01 * 100f);

            if (percentageText != null)
            {
                percentageText.text = percent + "%";
            }

            if (statusText != null)
            {
                statusText.text = string.IsNullOrWhiteSpace(currentStatus) ? "Loading..." : currentStatus;
            }
        }

        private static Font ResolveDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static Sprite GetSolidSprite()
        {
            if (solidSprite != null)
            {
                return solidSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;

            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            solidSprite.hideFlags = HideFlags.HideAndDontSave;
            return solidSprite;
        }
    }
}
