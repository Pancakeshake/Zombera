using UnityEngine;
using TMPro;

namespace Zombera.UI.SettingsV2
{
    public class SettingsRowFactory : MonoBehaviour
    {
        [Header("Containers")]
        [SerializeField] private RectTransform _contentRoot;

        [Header("Row Templates")]
        [SerializeField] private GameObject _sectionHeaderTemplate;
        [SerializeField] private GameObject _settingsRowTemplate;

        [Header("Control Templates")]
        [SerializeField] private GameObject _dropdownTemplate;
        [SerializeField] private GameObject _toggleTemplate;
        [SerializeField] private GameObject _sliderTemplate;
        [SerializeField] private GameObject _segmentedTemplate;

        public SettingsRowUI CreateRow(SettingItemData data, Transform parent)
        {
            if (_settingsRowTemplate == null) return null;

            GameObject rowObj = Instantiate(_settingsRowTemplate, parent);
            rowObj.SetActive(true);
            
            SettingsRowUI rowUI = rowObj.GetComponent<SettingsRowUI>();
            if (rowUI == null) rowUI = rowObj.AddComponent<SettingsRowUI>();
            
            rowUI.SetData(data);

            GameObject controlTemplate = GetControlTemplate(data.Type);
            if (controlTemplate != null && rowUI.ControlContainer != null)
            {
                GameObject controlObj = Instantiate(controlTemplate, rowUI.ControlContainer);
                controlObj.SetActive(true);
                rowUI.Control = controlObj;
            }

            return rowUI;
        }

        public void CreateSectionHeader(string title, Transform parent)
        {
            if (_sectionHeaderTemplate == null) return;

            GameObject headerObj = Instantiate(_sectionHeaderTemplate, parent);
            headerObj.SetActive(true);
            
            TextMeshProUGUI label = headerObj.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = title;
        }

        private GameObject GetControlTemplate(SettingType type)
        {
            return type switch
            {
                SettingType.Dropdown => _dropdownTemplate,
                SettingType.Toggle => _toggleTemplate,
                SettingType.Slider => _sliderTemplate,
                SettingType.Segmented => _segmentedTemplate,
                _ => null
            };
        }
    }
}
