#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zombera.Systems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#endregion

namespace Zombera.UI
{
    /// <summary>
    ///     Draws a brief green pulse at mouse click positions so click locations are visible during gameplay.
    ///     Auto-installs itself at runtime and persists across scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MouseClickPulseOverlay : MonoBehaviour
    {
        private const string RootObjectName = "MouseClickPulseOverlay";

        private static MouseClickPulseOverlay _instance;
        private static Sprite _pulseSprite;

        [Header("Pulse")]
        [SerializeField] private bool showLeftClickPulse = true;
        [SerializeField] private bool showRightClickPulse = true;
        [SerializeField] private Color pulseColor = new(0.23f, 0.95f, 0.36f, 0.85f);
        [SerializeField] [Min(0.05f)] private float pulseLifetimeSeconds = 0.28f;
        [SerializeField] [Min(4f)] private float pulseStartSize = 16f;
        [SerializeField] [Min(8f)] private float pulseEndSize = 84f;

        private readonly List<PulseView> _activePulses = new(8);

        private RectTransform _canvasRect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstallOnLoad()
        {
            EnsureInstalled();
        }

        private static void EnsureInstalled()
        {
            if (!Application.isPlaying) return;

            if (_instance != null) return;

            var existing = FindFirstObjectByType<MouseClickPulseOverlay>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var root = new GameObject(RootObjectName);
            DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ZomberaCanvasLayer.Overlays;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            root.AddComponent<GraphicRaycaster>();
            _instance = root.AddComponent<MouseClickPulseOverlay>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _canvasRect = transform as RectTransform;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_canvasRect == null) _canvasRect = transform as RectTransform;

            if (showLeftClickPulse && WasLeftClickPressedThisFrame(out var leftPosition))
                SpawnPulseAtScreenPosition(leftPosition);

            if (showRightClickPulse && WasRightClickPressedThisFrame(out var rightPosition))
                SpawnPulseAtScreenPosition(rightPosition);

            TickPulses();
        }

        private bool WasLeftClickPressedThisFrame(out Vector2 screenPosition)
        {
            return TryReadMouseClickState(leftClick: true, out screenPosition);
        }

        private bool WasRightClickPressedThisFrame(out Vector2 screenPosition)
        {
            return TryReadMouseClickState(leftClick: false, out screenPosition);
        }

        private static bool TryReadMouseClickState(bool leftClick, out Vector2 screenPosition)
        {
            screenPosition = default;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                var pressed = leftClick
                    ? Mouse.current.leftButton.wasPressedThisFrame
                    : Mouse.current.rightButton.wasPressedThisFrame;

                if (!pressed) return false;

                return CursorService.TryGetGameplayPointerScreenPosition(out screenPosition);
            }
#endif

            var buttonIndex = leftClick ? 0 : 1;
            if (!Input.GetMouseButtonDown(buttonIndex)) return false;

            return CursorService.TryGetGameplayPointerScreenPosition(out screenPosition);
        }

        private void SpawnPulseAtScreenPosition(Vector2 screenPosition)
        {
            if (_canvasRect == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, null,
                    out var localPoint))
                return;

            var pulseRoot = new GameObject("ClickPulse", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pulseRoot.transform.SetParent(_canvasRect, false);

            var rect = pulseRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = localPoint;
            rect.sizeDelta = new Vector2(pulseStartSize, pulseStartSize);

            var image = pulseRoot.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = GetPulseSprite();
            image.type = Image.Type.Simple;
            image.color = pulseColor;

            _activePulses.Add(new PulseView
            {
                Root = rect,
                Image = image,
                Age = 0f,
                Lifetime = Mathf.Max(0.05f, pulseLifetimeSeconds),
                StartSize = Mathf.Max(4f, pulseStartSize),
                EndSize = Mathf.Max(pulseStartSize + 1f, pulseEndSize),
                BaseColor = pulseColor
            });
        }

        private void TickPulses()
        {
            if (_activePulses.Count == 0) return;

            for (var i = _activePulses.Count - 1; i >= 0; i--)
            {
                var pulse = _activePulses[i];
                if (pulse == null || pulse.Root == null || pulse.Image == null)
                {
                    _activePulses.RemoveAt(i);
                    continue;
                }

                pulse.Age += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(pulse.Age / pulse.Lifetime);

                var size = Mathf.Lerp(pulse.StartSize, pulse.EndSize, t);
                pulse.Root.sizeDelta = new Vector2(size, size);

                var color = pulse.BaseColor;
                color.a *= 1f - t;
                pulse.Image.color = color;

                if (t < 0.999f) continue;

                Destroy(pulse.Root.gameObject);
                _activePulses.RemoveAt(i);
            }
        }

        private static Sprite GetPulseSprite()
        {
            if (_pulseSprite != null) return _pulseSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / radius;
                    var dy = (y - center) / radius;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(1f - dist);
                    alpha *= alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, true);
            _pulseSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _pulseSprite.hideFlags = HideFlags.HideAndDontSave;
            return _pulseSprite;
        }

        private sealed class PulseView
        {
            public float Age;
            public Color BaseColor;
            public float EndSize;
            public Image Image;
            public float Lifetime;
            public RectTransform Root;
            public float StartSize;
        }
    }
}
