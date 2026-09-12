using UnityEngine;
using TMPro;

namespace Zombera.UI.SettingsV2
{
    public class SettingsRowUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private RectTransform _controlContainer;

        public TextMeshProUGUI Label => _label;
        public RectTransform ControlContainer => _controlContainer;
        
        // We can store the created control here
        public GameObject Control { get; set; }
        public SettingItemData Data { get; set; }

        public void SetData(SettingItemData data)
        {
            Data = data;
            if (_label != null) _label.text = data.Title;
        }
    }
}
