using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

namespace Zombera.UI.Effects
{
    /// <summary>
    /// Adds a pulsating flash effect to a UI element when hovered.
    /// Works best with an overlay image to avoid conflicting with Button ColorTint transitions.
    /// </summary>
    public class ButtonHoverFlash : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Targeting")]
        [SerializeField] private Image overlayImage;
        
        [Header("Flash Settings")]
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashSpeed = 4f;
        [SerializeField] [Range(0f, 1f)] private float maxAlpha = 0.4f;

        private Coroutine _flashCoroutine;
        private bool _isHovered;

        private void Awake()
        {
            if (overlayImage == null)
            {
                // Try to find a child named "FlashOverlay"
                var child = transform.Find("FlashOverlay");
                if (child != null) overlayImage = child.GetComponent<Image>();
            }

            if (overlayImage != null)
            {
                overlayImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
                overlayImage.raycastTarget = false;
            }
        }

        private void OnDisable()
        {
            _isHovered = false;
            StopFlash();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            if (_flashCoroutine == null && gameObject.activeInHierarchy)
            {
                _flashCoroutine = StartCoroutine(FlashRoutine());
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
        }

        private IEnumerator FlashRoutine()
        {
            float intensity = 0f;
            while (_isHovered || intensity > 0.01f)
            {
                // Transition intensity based on hover state
                float targetIntensity = _isHovered ? 1f : 0f;
                intensity = Mathf.MoveTowards(intensity, targetIntensity, Time.unscaledDeltaTime * 5f);

                if (overlayImage != null)
                {
                    // Pulsate alpha
                    float pulse = (Mathf.Sin(Time.unscaledTime * flashSpeed) * 0.5f + 0.5f);
                    float alpha = pulse * maxAlpha * intensity;
                    
                    var color = flashColor;
                    color.a = alpha;
                    overlayImage.color = color;
                }

                yield return null;
            }

            StopFlash();
        }

        private void StopFlash()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (overlayImage != null)
            {
                overlayImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            }
        }
    }
}
