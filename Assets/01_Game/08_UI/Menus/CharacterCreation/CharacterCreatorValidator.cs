#region

using UnityEngine;
using UnityEngine.Serialization;

#endregion

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Validates required refs and hierarchy integrity for the character creator panel.
    ///     Run via Context Menu after a build, or call ValidateCreator() in a CI inspection pass.
    /// </summary>
    public sealed class CharacterCreatorValidator : MonoBehaviour
    {
        [SerializeField] [FormerlySerializedAs("_refs")]
        private CharacterCreatorRefs creatorRefs;

        [ContextMenu("Validate Creator")]
        public void ValidateCreator()
        {
            if (creatorRefs == null) creatorRefs = GetComponent<CharacterCreatorRefs>();

            var passed = true;

            passed &= AssertNotNull(creatorRefs, "CharacterCreatorRefs component is missing.");

            if (creatorRefs != null)
            {
                passed &= AssertNotNull(creatorRefs.panelRoot, "Refs.panelRoot is null.");
                passed &= AssertNotNull(creatorRefs.nameInput, "Refs.nameInput is null — name field not wired.");
                passed &= AssertNotNull(creatorRefs.confirmButton,
                    "Refs.confirmButton is null — confirm button not wired.");
                passed &= AssertNotNull(creatorRefs.backButton,
                    "Refs.backButton is null — back/close button not wired.");
                passed &= AssertNotNull(creatorRefs.customizationController,
                    "Refs.customizationController is null — customization panel not wired.");
                passed &= AssertNotNull(creatorRefs.previewDisplay,
                    "Refs.previewDisplay is null — preview RawImage not wired.");

                if (creatorRefs.previewAvatar == null)
                    Debug.LogWarning(
                        "[CharacterCreatorValidator] Refs.previewAvatar is null — runtime will resolve a preview avatar at show-time (local first, then global fallback).",
                        this);
            }

            if (passed) Debug.Log("[CharacterCreatorValidator] All checks passed.", this);
        }

        private static bool AssertNotNull(Object obj, string message)
        {
            if (obj != null) return true;

            Debug.LogError("[CharacterCreatorValidator] " + message);
            return false;
        }
    }
}