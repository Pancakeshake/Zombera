using UnityEngine;
using UnityEngine.UI;
using Zombera.Systems.Digging;

namespace Zombera.UI
{
    /// <summary>
    /// Runtime world-space progress bar displayed above the player while digging.
    /// </summary>
    public sealed class DigProgressWorldUI : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private DiggingSystem diggingSystem;
        [SerializeField] private Transform target;
        [SerializeField] private Camera worldCamera;

        [Header("Layout")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Image _fillImage;

        public void SetDiggingSystem(DiggingSystem system)
        {
            diggingSystem = system;
        }

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
        }

        public void SetWorldCamera(Camera camera)
        {
            worldCamera = camera;
        }

        private void Awake()
        {
            if (diggingSystem == null)
            {
                diggingSystem = GetComponent<DiggingSystem>();
            }

            if (target == null)
            {
                target = transform;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            BuildRuntimeUI();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_canvas == null || diggingSystem == null)
            {
                return;
            }

            bool shouldShow = diggingSystem.IsDigging;
            SetVisible(shouldShow);

            if (!shouldShow)
            {
                return;
            }

            if (_fillImage != null)
            {
                _fillImage.fillAmount = diggingSystem.Progress01;
            }

            Transform follow = target != null ? target : transform;
            _canvasRect.position = follow.position + worldOffset;

            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam != null)
            {
                Vector3 forward = _canvasRect.position - cam.transform.position;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    _canvasRect.rotation = Quaternion.LookRotation(forward.normalized, cam.transform.up);
                }
            }
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null)
            {
                _canvas.enabled = visible;
            }
        }

        private void BuildRuntimeUI()
        {
            Transform existing = transform.Find("DigProgressCanvas");
            if (existing != null)
            {
                _canvas = existing.GetComponent<Canvas>();
                _canvasRect = existing as RectTransform;
                _fillImage = existing.Find("Fill")?.GetComponent<Image>();
                return;
            }

            GameObject canvasGo = new GameObject("DigProgressCanvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 200;

            canvasGo.AddComponent<GraphicRaycaster>();

            _canvasRect = canvasGo.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(120f, 16f);
            _canvasRect.localScale = Vector3.one * 0.01f;

            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            RectTransform bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);
            bgImage.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            RectTransform fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.color = new Color(0.62f, 0.45f, 0.20f, 0.95f);
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fillImage.fillAmount = 0f;
            _fillImage.raycastTarget = false;
        }
    }
}