#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.Menus
{
    /// <summary>
    ///     Persistent full-screen black overlay used to hide scene transitions and initialization.
    /// </summary>
    public sealed class LoadingScreenOverlay : MonoBehaviour
    {
        private const float VisualProgressSpeed = 0.65f;
        private const float BackgroundSwapIntervalSeconds = 5f;
        private const string LoadingImagesAssetFolder = "Assets/Art/Title_LoadingImages";

        private static readonly string[] LoadingImagesResourcesFolders =
        {
            "Title_LoadingImages",
            "Art/Title_LoadingImages"
        };

        /// <summary>Main menu / branding art — must not appear in the loading slideshow rotation.</summary>
        private const string SlideshowExcludedNameToken = "ZomberaTitle";

        private static readonly string[] LoadingImageAssetPaths =
        {
            "Assets/Art/Title_LoadingImages/Zomberaloading1.png",
            "Assets/Art/Title_LoadingImages/Zomberaloading2.png",
            "Assets/Art/Title_LoadingImages/Zomberaloading3.png",
            "Assets/Art/Title_LoadingImages/Zomberaloading4.png",
            "Assets/Art/Title_LoadingImages/Zomberaloading5.png"
        };

        private static LoadingScreenOverlay _instance;
        private static Sprite _solidSprite;

        private Canvas _canvas;
        private float _currentProgress01;
        private string _currentStatus = "Loading...";
        private float _displayedProgress01;
        private RawImage _loadingBackgroundImage;
        private Texture[] _loadingTextures;
        private int _loadingTextureIndex;
        private float _nextBackgroundSwapAt;
        private Text _percentageText;
        private Image _progressFillImage;
        private Text _statusText;
        private bool _audioGateHeld;

        // ReSharper disable once UnusedMember.Global
        public static bool IsVisible => _instance != null && _instance.gameObject.activeInHierarchy;
        // ReSharper disable once UnusedMember.Global
        public static float VisibleProgress01 => _instance != null ? _instance._displayedProgress01 : 0f;
        // ReSharper disable once UnusedMember.Global
        public static string VisibleStatus => _instance != null ? _instance._currentStatus : string.Empty;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_audioGateHeld)
            {
                LoadingAudioGate.Release();
                _audioGateHeld = false;
            }

            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            TryAdvanceBackgroundImage();

            if (Mathf.Approximately(_displayedProgress01, _currentProgress01)) return;

            _displayedProgress01 = Mathf.MoveTowards(_displayedProgress01, _currentProgress01,
                Time.unscaledDeltaTime * VisualProgressSpeed);
            ApplyVisualState();
        }

        public static void Show(string status = null)
        {
            EnsureInstance();

            if (_instance == null) return;

            if (!_instance._audioGateHeld)
            {
                LoadingAudioGate.Acquire();
                _instance._audioGateHeld = true;
            }

            _instance.gameObject.SetActive(true);
            if (_instance._canvas != null) _instance._canvas.enabled = true;

            _instance._displayedProgress01 = _instance._currentProgress01;
            _instance.ResetBackgroundCycle();

            if (!string.IsNullOrWhiteSpace(status)) _instance._currentStatus = status.Trim();

            _instance.ApplyVisualState();
        }

        public static void SetProgress(float progress01, string status = null)
        {
            EnsureInstance();

            if (_instance == null) return;

            _instance._currentProgress01 = Mathf.Clamp01(progress01);

            if (!string.IsNullOrWhiteSpace(status)) _instance._currentStatus = status.Trim();

            _instance.ApplyVisualState();
        }

        public static void Hide()
        {
            if (_instance == null) return;

            if (_instance._audioGateHeld)
            {
                LoadingAudioGate.Release();
                _instance._audioGateHeld = false;
            }

            Destroy(_instance.gameObject);
            _instance = null;
        }

        public static IEnumerator WaitForVisualProgress(float progress01, float timeoutSeconds = 2f)
        {
            EnsureInstance();

            if (_instance == null) yield break;

            var target = Mathf.Clamp01(progress01);
            var timeout = Mathf.Max(0f, timeoutSeconds);
            var elapsed = 0f;

            while (!HasReachedVisualProgressTarget(target))
            {
                if (timeout > 0f && elapsed >= timeout) yield break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static bool HasReachedVisualProgressTarget(float target)
        {
            return _instance == null || _instance._displayedProgress01 + 0.001f >= target;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var root = new GameObject("LoadingScreenOverlay");
            _instance = root.AddComponent<LoadingScreenOverlay>();
            _instance.BuildUi(root);
            DontDestroyOnLoad(root);
        }

        private void BuildUi(GameObject root)
        {
            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = ZomberaCanvasLayer.Loading;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            _loadingTextures = LoadLoadingTextures();
            _loadingTextureIndex = 0;

            var background = new GameObject("LoadingBackground");
            background.transform.SetParent(root.transform, false);

            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            _loadingBackgroundImage = background.AddComponent<RawImage>();
            _loadingBackgroundImage.raycastTarget = true;
            _loadingBackgroundImage.color = Color.white;
            _loadingBackgroundImage.texture = _loadingTextures != null && _loadingTextures.Length > 0
                ? _loadingTextures[0]
                : null;

            var screenTint = new GameObject("ScreenTint");
            screenTint.transform.SetParent(background.transform, false);

            var tintRect = screenTint.AddComponent<RectTransform>();
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.offsetMin = Vector2.zero;
            tintRect.offsetMax = Vector2.zero;

            var tintImage = screenTint.AddComponent<Image>();
            tintImage.color = new Color(0f, 0f, 0f, 0.55f);
            tintImage.raycastTarget = false;

            var progressRoot = new GameObject("LoadingProgressRoot");
            progressRoot.transform.SetParent(background.transform, false);

            var progressRootRect = progressRoot.AddComponent<RectTransform>();
            progressRootRect.anchorMin = new Vector2(0.08f, 0f);
            progressRootRect.anchorMax = new Vector2(0.92f, 0f);
            progressRootRect.pivot = new Vector2(0.5f, 0f);
            progressRootRect.sizeDelta = new Vector2(0f, 116f);
            progressRootRect.anchoredPosition = new Vector2(0f, 26f);

            _statusText = CreateText(progressRoot.transform, "StatusText", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), 26);
            _statusText.alignment = TextAnchor.UpperLeft;
            _statusText.text = _currentStatus;

            _percentageText = CreateText(progressRoot.transform, "PercentageText", new Vector2(0f, 0.52f),
                new Vector2(1f, 0.52f), new Vector2(1f, 1f), 24);
            _percentageText.alignment = TextAnchor.UpperRight;
            _percentageText.text = "0%";

            var barBackground = new GameObject("ProgressBarBackground");
            barBackground.transform.SetParent(progressRoot.transform, false);

            var barBackgroundRect = barBackground.AddComponent<RectTransform>();
            barBackgroundRect.anchorMin = new Vector2(0f, 0f);
            barBackgroundRect.anchorMax = new Vector2(1f, 0f);
            barBackgroundRect.pivot = new Vector2(0.5f, 0f);
            barBackgroundRect.sizeDelta = new Vector2(0f, 28f);
            barBackgroundRect.anchoredPosition = Vector2.zero;

            var barBackgroundImage = barBackground.AddComponent<Image>();
            barBackgroundImage.sprite = GetSolidSprite();
            barBackgroundImage.type = Image.Type.Sliced;
            barBackgroundImage.color = new Color(1f, 1f, 1f, 0.18f);

            var barFill = new GameObject("ProgressBarFill");
            barFill.transform.SetParent(barBackground.transform, false);

            var barFillRect = barFill.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = Vector2.one;
            barFillRect.offsetMin = new Vector2(3f, 3f);
            barFillRect.offsetMax = new Vector2(-3f, -3f);

            _progressFillImage = barFill.AddComponent<Image>();
            _progressFillImage.sprite = GetSolidSprite();
            _progressFillImage.type = Image.Type.Filled;
            _progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            _progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _progressFillImage.fillAmount = 0f;
            _progressFillImage.color = new Color(0.20f, 0.84f, 0.28f, 0.98f);

            ResetBackgroundCycle();
            ApplyVisualState();
        }

        private static Texture[] LoadLoadingTextures()
        {
            var loaded = new List<Texture>();

            AppendResourceTextures(loaded);

#if UNITY_EDITOR
            AppendEditorDiscoveredTextures(loaded);
            if (loaded.Count == 0) AppendEditorFallbackTextures(loaded);
#endif

            if (loaded.Count == 0)
            {
                if (!Application.isEditor)
                    Debug.LogWarning(
                        "[LoadingScreenOverlay] No loading images found in Resources. " +
                        "Expected folders include Resources/Title_LoadingImages. Falling back to black background.");

                loaded.Add(CreateFallbackTexture());
            }

            return loaded.Distinct().ToArray();
        }

        private static void AppendResourceTextures(List<Texture> loaded)
        {
            for (var i = 0; i < LoadingImagesResourcesFolders.Length; i++)
            {
                var folder = LoadingImagesResourcesFolders[i];
                if (string.IsNullOrWhiteSpace(folder)) continue;

                var resourcesLoaded = Resources.LoadAll<Texture2D>(folder);
                if (resourcesLoaded == null || resourcesLoaded.Length == 0) continue;

                loaded.AddRange(resourcesLoaded.Where(texture =>
                    texture != null && !IsSlideshowExcludedTexture(texture)));
            }
        }

#if UNITY_EDITOR
        private static void AppendEditorDiscoveredTextures(List<Texture> loaded)
        {
            var foundGuids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { LoadingImagesAssetFolder });
            if (foundGuids == null || foundGuids.Length == 0) return;

            var discoveredPaths = foundGuids
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path)
                               && path.StartsWith(LoadingImagesAssetFolder)
                               && path.EndsWith(".png")
                               && !IsSlideshowExcludedAssetPath(path))
                .OrderBy(path => path)
                .ToArray();

            foreach (var path in discoveredPaths)
            {
                var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null) loaded.Add(texture);
            }
        }

        private static void AppendEditorFallbackTextures(List<Texture> loaded)
        {
            for (var i = 0; i < LoadingImageAssetPaths.Length; i++)
            {
                var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(LoadingImageAssetPaths[i]);
                if (texture != null) loaded.Add(texture);
            }
        }
#endif

        private static bool IsSlideshowExcludedTexture(Texture texture)
        {
            if (texture == null) return true;

            var name = texture.name;
            return !string.IsNullOrEmpty(name) &&
                   name.IndexOf(SlideshowExcludedNameToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSlideshowExcludedAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return true;

            return assetPath.IndexOf(SlideshowExcludedNameToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture CreateFallbackTexture()
        {
            var fallback = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            fallback.SetPixel(0, 0, Color.black);
            fallback.Apply(false, true);
            fallback.hideFlags = HideFlags.HideAndDontSave;
            return fallback;
        }

        private void ResetBackgroundCycle()
        {
            _nextBackgroundSwapAt = Time.unscaledTime + BackgroundSwapIntervalSeconds;

            if (_loadingTextures == null || _loadingTextures.Length == 0 || _loadingBackgroundImage == null)
                return;

            _loadingTextureIndex = UnityEngine.Random.Range(0, _loadingTextures.Length);
            _loadingBackgroundImage.texture = _loadingTextures[_loadingTextureIndex];
        }

        private void TryAdvanceBackgroundImage()
        {
            if (_loadingBackgroundImage == null || _loadingTextures == null || _loadingTextures.Length <= 1)
                return;

            if (Time.unscaledTime < _nextBackgroundSwapAt) return;

            _loadingTextureIndex = (_loadingTextureIndex + 1) % _loadingTextures.Length;
            _loadingBackgroundImage.texture = _loadingTextures[_loadingTextureIndex];
            _nextBackgroundSwapAt = Time.unscaledTime + BackgroundSwapIntervalSeconds;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            int fontSize)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);

            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(0f, 32f);
            rect.anchoredPosition = Vector2.zero;

            var text = textObject.AddComponent<Text>();
            text.font = ResolveDefaultFont();
            text.fontSize = fontSize;
            text.color = new Color(1f, 1f, 1f, 0.92f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void ApplyVisualState()
        {
            if (_progressFillImage != null) _progressFillImage.fillAmount = _displayedProgress01;

            var percent = Mathf.RoundToInt(_displayedProgress01 * 100f);

            if (_percentageText != null) _percentageText.text = percent + "%";

            if (_statusText != null)
                _statusText.text = string.IsNullOrWhiteSpace(_currentStatus) ? "Loading..." : _currentStatus;
        }

        private static Font ResolveDefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return font;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;

            _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _solidSprite.hideFlags = HideFlags.HideAndDontSave;
            return _solidSprite;
        }
    }
}