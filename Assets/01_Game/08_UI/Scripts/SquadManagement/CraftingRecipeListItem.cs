using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Inventory.Crafting;

namespace Zombera.UI.SquadManagement
{
    public sealed class CraftingRecipeListItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private Button button;

        public void Setup(CraftingRecipe recipe, Action onClick)
        {
            if (icon) icon.sprite = recipe.icon;
            if (nameText) nameText.text = recipe.displayName;
            if (categoryText) categoryText.text = recipe.category.ToString();
            if (button)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        public void SetupPreview(string displayName, string categoryLabel)
        {
            if (icon)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (nameText) nameText.text = displayName;
            if (categoryText) categoryText.text = categoryLabel;

            if (button)
                button.onClick.RemoveAllListeners();
        }
    }
}
