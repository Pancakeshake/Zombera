using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Inventory.Crafting;
using Zombera.Inventory;
using Zombera.Characters;

namespace Zombera.UI.SquadManagement
{
    public sealed class IngredientListItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Color affordableColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color insufficientColor = new Color(0.8f, 0.3f, 0.3f, 1f);

        public void Setup(CraftingIngredient ingredient, IInventoryHolder inventory)
        {
            if (ingredient.useTag)
            {
                if (nameText) nameText.text = $"[Tag] {ingredient.requiredTag}";
                if (icon) icon.enabled = false;
            }
            else if (ingredient.item != null)
            {
                if (icon)
                {
                    icon.enabled = true;
                    icon.sprite = ingredient.item.inventoryIcon;
                }
                if (nameText) nameText.text = ingredient.item.displayName;
            }

            int owned = 0;
            if (inventory != null)
            {
                if (ingredient.useTag)
                {
                    foreach (var stack in inventory.Items)
                    {
                        if (stack.item != null && stack.item.tags != null && System.Array.Exists(stack.item.tags, t => t == ingredient.requiredTag))
                        {
                            owned += stack.quantity;
                        }
                    }
                }
                else
                {
                    owned = inventory.GetQuantity(ingredient.item);
                }
            }

            if (countText)
            {
                countText.text = $"{owned} / {ingredient.amount}";
                countText.color = owned >= ingredient.amount ? affordableColor : insufficientColor;
            }
        }

        public void SetupPreview(string displayName, int owned, int required)
        {
            if (icon)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (nameText) nameText.text = displayName;

            if (countText)
            {
                countText.text = $"{owned} / {required}";
                countText.color = owned >= required ? affordableColor : insufficientColor;
            }
        }
    }
}
