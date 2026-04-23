using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zombera.Characters;

namespace Zombera.UI
{
    /// <summary>
    /// Data holder for one portrait slot in the BottomBar strip.
    /// The editor setup tool wires the sub-image references; SquadPortraitStrip drives them at runtime.
    /// </summary>
    [AddComponentMenu("Zombera/UI/Squad Portrait Slot")]
    public sealed class SquadPortraitSlot : MonoBehaviour
    {
        [SerializeField] public Image portraitImage;
        [SerializeField] public Image hpFill;
        [SerializeField] public Image staminaFill;
        [SerializeField] public TextMeshProUGUI nameLabel;
        [SerializeField] public Image selectOverlay;
        [SerializeField] public Button slotButton;

        /// <summary>Set at runtime by SquadPortraitStrip — not serialized.</summary>
        [System.NonSerialized] public Unit BoundUnit;
    }
}
