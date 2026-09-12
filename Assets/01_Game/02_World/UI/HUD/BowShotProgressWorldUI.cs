#region

using UnityEngine;
using UnityEngine.UI;
using Zombera.Systems;

#endregion

namespace Zombera.UI
{
    /// <summary>
    ///     Runtime world-space progress bar displayed above the player while charging a bow shot.
    /// </summary>
    public sealed class BowShotProgressWorldUI : MonoBehaviour
    {
        private static readonly Vector2 FillInset = new(2f, 2f);

        [Header("Binding")]
        [SerializeField] private PlayerInputController bowController;

        [SerializeField] private Transform target;
        [SerializeField] private Camera worldCamera;

        [Header("Layout")] [SerializeField] private Vector3 worldOffset = new(0f, 2.35f, 0f);

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Image _fillImage;
        private RectTransform _fillRect;

        private void Awake()
        {
            if (bowController == null) bowController = GetComponent<PlayerInputController>();
            if (bowController == null) bowController = GetComponentInParent<PlayerInputController>();

            if (target == null) target = transform;

            if (worldCamera == null) worldCamera = Zombera.Core.CameraRegistry.Main;

            BuildRuntimeUI();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_canvas == null || bowController == null) return;

            var shouldShow = bowController.IsBowShotCharging;
            SetVisible(shouldShow);

            if (!shouldShow) return;

            if (_fillImage != null) SetFillProgress(bowController.BowShotChargeProgress01);

            var follow = target != null ? target : transform;
            _canvasRect.position = follow.position + worldOffset;

            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;

            var forward = _canvasRect.position - cam.transform.position;
            if (forward.sqrMagnitude > 0.0001f)
                _canvasRect.rotation = Quaternion.LookRotation(forward.normalized, cam.transform.up);
        }

        public void SetBowController(PlayerInputController controller)
        {
            bowController = controller;
        }

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
        }

        public void SetWorldCamera(Camera targetCamera)
        {
            worldCamera = targetCamera;
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        private void BuildRuntimeUI()
        {
            var existing = transform.Find("BowShotProgressCanvas");
            if (existing != null)
            {
                _canvas = existing.GetComponent<Canvas>();
                _canvasRect = existing as RectTransform;
                EnsureFillImage(existing);
                return;
            }

            var canvasGo = new GameObject("BowShotProgressCanvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 205;

            canvasGo.AddComponent<GraphicRaycaster>();

            _canvasRect = canvasGo.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(132f, 18f);
            _canvasRect.localScale = Vector3.one * 0.01f;

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.6f);
            bgImage.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            _fillRect = fillRect;
            ConfigureFillRect(fillRect);

            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.color = new Color(0.92f, 0.70f, 0.22f, 0.95f);
            ConfigureFillImage(_fillImage);
        }

        private void EnsureFillImage(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                _fillImage = null;
                return;
            }

            var fill = canvasTransform.Find("Fill");
            if (fill == null)
            {
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(canvasTransform, false);
                fill = fillGo.transform;
            }

            var fillRect = fill as RectTransform;
            if (fillRect == null) fillRect = fill.gameObject.AddComponent<RectTransform>();
            _fillRect = fillRect;
            ConfigureFillRect(fillRect);

            _fillImage = fill.GetComponent<Image>();
            if (_fillImage == null) _fillImage = fill.gameObject.AddComponent<Image>();

            ConfigureFillImage(_fillImage);
        }

        private static void ConfigureFillImage(Image fillImage)
        {
            if (fillImage == null) return;

            fillImage.color = new Color(0.92f, 0.70f, 0.22f, 0.95f);
            fillImage.type = Image.Type.Simple;
            fillImage.raycastTarget = false;
        }

        private static void ConfigureFillRect(RectTransform fillRect)
        {
            if (fillRect == null) return;

            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = FillInset;
            fillRect.offsetMax = -FillInset;
            fillRect.localScale = Vector3.one;
        }

        private void SetFillProgress(float progress01)
        {
            if (_fillRect == null) return;

            var clamped = Mathf.Clamp01(progress01);
            var scale = _fillRect.localScale;
            scale.x = clamped;
            scale.y = 1f;
            scale.z = 1f;
            _fillRect.localScale = scale;
        }
    }
}