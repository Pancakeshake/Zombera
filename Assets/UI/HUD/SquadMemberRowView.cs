using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    /// <summary>
    /// View component for a single squad member row in the squad HUD panel.
    /// Attach to the squadMemberEntryPrefab and wire the UI references in the inspector.
    /// </summary>
    public sealed class SquadMemberRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Image selectionHighlight;
        [SerializeField] private Image downedIndicator;

        public void Bind(SquadMemberViewData data)
        {
            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(data.displayName) ? data.unitId : data.displayName;
            }

            if (healthBar != null)
            {
                healthBar.value = data.health01;
            }

            if (selectionHighlight != null)
            {
                selectionHighlight.enabled = data.isSelected;
            }

            if (downedIndicator != null)
            {
                downedIndicator.enabled = data.isDowned;
            }
        }
    }
}
