using System;
using System.Collections.Generic;

namespace Zombera.UI.SettingsV2
{
    public enum SettingCategory
    {
        General,
        Graphics,
        Audio,
        Controls,
        Gameplay,
        Accessibility
    }

    public enum SettingType
    {
        Dropdown,
        Toggle,
        Slider,
        Segmented
    }

    [Serializable]
    public class SettingItemData
    {
        public string Title;
        public string Description;
        public SettingCategory Category;
        public SettingType Type;
        public List<string> Options;
        public float MinValue;
        public float MaxValue;
        public float DefaultValue;
    }
}
