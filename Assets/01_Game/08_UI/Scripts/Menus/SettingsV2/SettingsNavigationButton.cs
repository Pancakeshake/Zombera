using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Zombera.UI.SettingsV2
{
    [RequireComponent(typeof(Toggle))]
    public class SettingsNavigationButton : MonoBehaviour
    {
        [SerializeField] private GameObject _selectionRoot;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Color _selectedColor = new Color(0.835f, 0.706f, 0.357f); // #D5B45B
        [SerializeField] private Color _defaultColor = Color.white;

        private Toggle _toggle;

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
            _toggle.onValueChanged.AddListener(OnToggleChanged);
            UpdateVisuals(_toggle.isOn);
        }

        private void OnToggleChanged(bool isOn)
        {
            UpdateVisuals(isOn);
        }

        private void UpdateVisuals(bool isOn)
        {
            if (_selectionRoot != null)
                _selectionRoot.SetActive(isOn);

            if (_label != null)
                _label.color = isOn ? _selectedColor : _defaultColor;
            
            // If selected, we might want to hide the hover background or dim it
            if (_backgroundImage != null)
            {
                var color = _backgroundImage.color;
                color.a = isOn ? 0.2f : 0.05f;
                _backgroundImage.color = color;
            }
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (_toggle == null) _toggle = GetComponent<Toggle>();
            if (_toggle != null) UpdateVisuals(_toggle.isOn);
        }
        #endif
    }
}
