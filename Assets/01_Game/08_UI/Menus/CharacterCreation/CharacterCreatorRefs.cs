#region

using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Strongly-typed UI reference container for the character creator panel.
    ///     Assign all fields in the Inspector. The controller only talks to this —
    ///     no deep Find() calls at runtime.
    /// </summary>
    public sealed class CharacterCreatorRefs : MonoBehaviour
    {
        [Header("Root")] [FormerlySerializedAs("PanelRoot")]
        public GameObject panelRoot;

        [Header("Name / Preset")] [FormerlySerializedAs("NameInput")]
        public TMP_InputField nameInput;

        [FormerlySerializedAs("PresetDropdown")]
        public TMP_Dropdown presetDropdown;

        [Header("Preview")] [FormerlySerializedAs("PortraitPreview")]
        public Image portraitPreview;

        [FormerlySerializedAs("UmaPreviewDisplay")]
        public RawImage previewDisplay;

        [Header("Text")] [FormerlySerializedAs("ValidationMessage")]
        public TMP_Text validationMessage;

        [Header("Buttons")] [FormerlySerializedAs("ConfirmButton")]
        public Button confirmButton;

        [FormerlySerializedAs("BackButton")] public Button backButton;

        [FormerlySerializedAs("RandomNameButton")]
        public Button randomNameButton;

        [FormerlySerializedAs("PreviousPortraitButton")]
        public Button previousPortraitButton;

        [FormerlySerializedAs("NextPortraitButton")]
        public Button nextPortraitButton;

        [FormerlySerializedAs("RandomPortraitButton")]
        public Button randomPortraitButton;

        [Header("Preview Avatar")] [FormerlySerializedAs("PreviewAvatar")]
        public GameObject previewAvatar;

        [Header("Customization")] [FormerlySerializedAs("CustomizationController")]
        public CharacterCreatorCustomizationController customizationController;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (previewAvatar != null && previewAvatar.scene != gameObject.scene)
                previewAvatar = null;

            if (customizationController != null)
                customizationController.SetPreviewAvatar(previewAvatar);
        }
#endif
    }
}