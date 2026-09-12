using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace Zombera.UI.Menus
{
    public class PauseMenuButtonHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private TextMeshProUGUI targetText;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = Color.red;

        private void Awake()
        {
            if (targetText == null)
                targetText = GetComponentInChildren<TextMeshProUGUI>();
        }

        private void OnDisable()
        {
            SetHighlight(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetHighlight(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetHighlight(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHighlight(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject)
            {
                SetHighlight(false);
            }
        }

        private void SetHighlight(bool active)
        {
            if (highlightObject != null)
                highlightObject.SetActive(active);

            if (targetText != null)
                targetText.color = active ? hoverColor : normalColor;
        }
    }
}
