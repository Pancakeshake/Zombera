using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Inventory.Crafting;
using Zombera.Characters;
using Zombera.Inventory;
using Zombera.UI;

namespace Zombera.UI.SquadManagement
{
    public sealed partial class CraftingTabController : MonoBehaviour
    {
        [Header("Editor Scene Layout")]
        [SerializeField]
        [Tooltip("Populate category buttons, recipe rows, and ingredient rows in the Scene view for layout work.")]
        private bool populateEditorLayoutPreview = true;

        [SerializeField]
        [Tooltip("Before Play, copy preview row sizes into the source prefabs so runtime Instantiate matches Scene view edits.")]
        private bool autoApplyPreviewLayoutToPrefabsOnPlay = true;

        [Header("Captured Row Layout (drives runtime spawn sizes)")]
        [SerializeField] private CraftingRowLayoutCapture categoryRowLayout;
        [SerializeField] private CraftingRowLayoutCapture recipeRowLayout;
        [SerializeField] private CraftingRowLayoutCapture ingredientRowLayout;

        [SerializeField] [Min(1)]
        private int editorPreviewRecipesPerCategory = 2;

        [SerializeField] [Min(1)]
        private int editorPreviewIngredientCount = 4;

        [SerializeField] private string editorPreviewSelectedRecipeName = "Wooden Spear";
        [SerializeField] private string editorPreviewSelectedCategory = "Weapons";
        [TextArea(2, 4)]
        [SerializeField]
        private string editorPreviewDescription = "A simple sharpened wooden pole for close combat.";
        [SerializeField] private string editorPreviewSkillRequirement = "Required: Engineering 2";
        [SerializeField] private string editorPreviewStationRequirement = "Station: Workbench";
        [SerializeField] [Min(1)]
        private int editorPreviewQuantity = 1;

        [SerializeField] private string[] editorPreviewIngredientNames =
        {
            "Wood Plank", "Rope", "Scrap Metal", "Cloth Rag"
        };

        [SerializeField] private int[] editorPreviewIngredientOwned = { 6, 2, 1, 4 };
        [SerializeField] private int[] editorPreviewIngredientRequired = { 4, 2, 3, 1 };

        [Header("Filters")]
        [SerializeField] private TMP_InputField searchBar;
        [SerializeField] private Transform categoryButtonContainer;
        [SerializeField] private Button categoryButtonPrefab;

        [Header("Recipe List")]
        [SerializeField] private RectTransform recipeListContent;
        [SerializeField] private CraftingRecipeListItem recipeListItemPrefab;

        [Header("Recipe Details")]
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailCategory;
        [SerializeField] private TMP_Text detailDescription;
        [SerializeField] private TMP_Text detailSkillReq;
        [SerializeField] private TMP_Text detailStationReq;
        [SerializeField] private RectTransform ingredientListContent;
        [SerializeField] private IngredientListItem ingredientListItemPrefab;

        [Header("Quantity Controls")]
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button maxButton;

        [Header("Actions")]
        [SerializeField] private Button craftButton;
        [SerializeField] private Button queueButton;

        [Header("Context")]
        [SerializeField] private TMP_Text contextText;

        private CraftingRecipe _selectedRecipe;
        private int _craftQuantity = 1;
        private CraftingCategory? _selectedCategory;
        private IUnit _currentUnit;
        private IInventoryHolder _currentInventory;

        private void Awake()
        {
            if (!Application.isPlaying) return;

            ClearRuntimePreviewArtifacts();
            if (searchBar) searchBar.onValueChanged.AddListener(_ => RefreshRecipeList());
            if (minusButton) minusButton.onClick.AddListener(() => AdjustQuantity(-1));
            if (plusButton) plusButton.onClick.AddListener(() => AdjustQuantity(1));
            if (maxButton) maxButton.onClick.AddListener(SetMaxQuantity);
            if (craftButton) craftButton.onClick.AddListener(CraftInstant);
            if (queueButton) queueButton.onClick.AddListener(QueueCraft);

            InitializeCategoryButtons();
        }

        private void ClearRuntimePreviewArtifacts()
        {
            UILayoutPreviewUtility.ClearPreviewChildren(categoryButtonContainer);
            UILayoutPreviewUtility.ClearPreviewChildren(recipeListContent);
            UILayoutPreviewUtility.ClearPreviewChildren(ingredientListContent);
        }

        private void InitializeCategoryButtons()
        {
            if (categoryButtonContainer == null || categoryButtonPrefab == null) return;

            UILayoutPreviewUtility.ClearAllChildren(categoryButtonContainer);

            // Add "ALL" button
            var allBtn = Instantiate(categoryButtonPrefab, categoryButtonContainer);
            ApplyCapturedRowLayout(allBtn.transform as RectTransform, categoryRowLayout);
            allBtn.GetComponentInChildren<TMP_Text>().text = "ALL";
            allBtn.onClick.AddListener(() => SelectCategory(null));

            foreach (CraftingCategory cat in Enum.GetValues(typeof(CraftingCategory)))
            {
                var btn = Instantiate(categoryButtonPrefab, categoryButtonContainer);
                ApplyCapturedRowLayout(btn.transform as RectTransform, categoryRowLayout);
                btn.GetComponentInChildren<TMP_Text>().text = cat.ToString().ToUpper();
                btn.onClick.AddListener(() => SelectCategory(cat));
            }

            if (categoryRowLayout.hasCapture && categoryButtonContainer is RectTransform categoryRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(categoryRect);
        }

        private static void ApplyCapturedRowLayout(RectTransform row, CraftingRowLayoutCapture capture)
        {
            capture.ApplySizeTo(row);
        }

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            // Placeholder implementation to satisfy the original signature if needed.
            // In the prefab-based workflow, we rely on the prefab setup.
        }

        public void SetContextSurvivor(string displayName)
        {
            if (contextText == null) return;
            contextText.text = "Operator: " + (string.IsNullOrWhiteSpace(displayName) ? "-" : displayName);
        }

        public void SetContext(IUnit unit, IInventoryHolder inventory)
        {
            if (_currentInventory != null)
            {
                _currentInventory.OnInventoryChanged -= UpdateDetailsDisplay;
            }

            _currentUnit = unit;
            _currentInventory = inventory;

            if (_currentInventory != null)
            {
                _currentInventory.OnInventoryChanged += UpdateDetailsDisplay;
            }

            RefreshRecipeList();
        }

        private void OnDestroy()
        {
            if (_currentInventory != null)
            {
                _currentInventory.OnInventoryChanged -= UpdateDetailsDisplay;
            }
        }

        public void SelectCategory(CraftingCategory? category)
        {
            _selectedCategory = category;
            RefreshRecipeList();
        }

        public void RefreshRecipeList()
        {
            if (recipeListContent == null || recipeListItemPrefab == null) return;

            // Clear existing
            foreach (Transform child in recipeListContent)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            var allRecipes = CraftingRecipeRegistry.Instance.GetAllRecipes();
            var filtered = allRecipes.AsEnumerable();

            if (_selectedCategory.HasValue)
            {
                filtered = filtered.Where(r => r.category == _selectedCategory.Value);
            }

            if (searchBar != null && !string.IsNullOrWhiteSpace(searchBar.text))
            {
                string query = searchBar.text.ToLower();
                filtered = filtered.Where(r => r.displayName.ToLower().Contains(query));
            }

            foreach (var recipe in filtered)
            {
                var item = Instantiate(recipeListItemPrefab, recipeListContent);
                ApplyCapturedRowLayout(item.transform as RectTransform, recipeRowLayout);
                item.Setup(recipe, () => SelectRecipe(recipe));
            }

            if (_selectedRecipe == null && filtered.Any())
            {
                SelectRecipe(filtered.First());
            }
            else if (!filtered.Any())
            {
                ClearDetails();
            }
        }

        private void SelectRecipe(CraftingRecipe recipe)
        {
            _selectedRecipe = recipe;
            _craftQuantity = 1;
            UpdateDetailsDisplay();
        }

        private void ClearDetails()
        {
            _selectedRecipe = null;
            UpdateDetailsDisplay();
        }

        private void UpdateDetailsDisplay()
        {
            if (_selectedRecipe == null)
            {
                if (detailName) detailName.text = "Select a recipe";
                if (detailIcon) detailIcon.enabled = false;
                if (detailCategory) detailCategory.text = string.Empty;
                if (detailDescription) detailDescription.text = string.Empty;
                if (detailSkillReq) detailSkillReq.text = string.Empty;
                if (detailStationReq) detailStationReq.text = string.Empty;
                UpdateIngredientList();
                UpdateQuantityDisplay();
                UpdateCraftButtonState();
                return;
            }

            if (detailIcon)
            {
                detailIcon.enabled = _selectedRecipe.icon != null;
                detailIcon.sprite = _selectedRecipe.icon;
            }
            if (detailName) detailName.text = _selectedRecipe.displayName;
            if (detailCategory) detailCategory.text = _selectedRecipe.category.ToString();
            if (detailDescription) detailDescription.text = _selectedRecipe.description;
            
            if (detailSkillReq) 
            {
                detailSkillReq.text = string.IsNullOrEmpty(_selectedRecipe.requiredSkillType) 
                    ? "Required Skill: None" 
                    : $"Required: {_selectedRecipe.requiredSkillType} {_selectedRecipe.requiredSkillLevel}";
            }

            if (detailStationReq)
            {
                detailStationReq.text = _selectedRecipe.requiredStationType == CraftingStationType.None 
                    ? "Station: None" 
                    : $"Station: {_selectedRecipe.requiredStationType}";
            }

            UpdateIngredientList();
            UpdateQuantityDisplay();
            UpdateCraftButtonState();
        }

        private void UpdateIngredientList()
        {
            if (ingredientListContent == null || ingredientListItemPrefab == null) return;

            foreach (Transform child in ingredientListContent)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            if (_selectedRecipe == null) return;

            foreach (var ingredient in _selectedRecipe.ingredients)
            {
                var item = Instantiate(ingredientListItemPrefab, ingredientListContent);
                ApplyCapturedRowLayout(item.transform as RectTransform, ingredientRowLayout);
                item.Setup(ingredient, _currentInventory);
            }
        }

        private void AdjustQuantity(int delta)
        {
            if (_selectedRecipe == null) return;
            _craftQuantity = Mathf.Max(1, _craftQuantity + delta);
            if (_selectedRecipe.canBatch && _selectedRecipe.maxBatchSize > 0)
            {
                _craftQuantity = Mathf.Min(_craftQuantity, _selectedRecipe.maxBatchSize);
            }
            UpdateQuantityDisplay();
            UpdateCraftButtonState();
        }

        private void SetMaxQuantity()
        {
            if (_selectedRecipe == null) return;
            _craftQuantity = CalculateMaxAffordable(_selectedRecipe);
            UpdateQuantityDisplay();
            UpdateCraftButtonState();
        }

        private int CalculateMaxAffordable(CraftingRecipe recipe)
        {
            if (recipe == null || _currentInventory == null) return 1;
            int max = int.MaxValue;
            foreach (var ing in recipe.ingredients)
            {
                int owned = 0;
                if (ing.useTag)
                {
                    foreach (var stack in _currentInventory.Items)
                    {
                        if (stack.item != null && stack.item.tags != null && System.Array.Exists(stack.item.tags, t => t == ing.requiredTag))
                        {
                            owned += stack.quantity;
                        }
                    }
                }
                else
                {
                    owned = _currentInventory.GetQuantity(ing.item);
                }
                
                if (ing.amount > 0)
                    max = Mathf.Min(max, owned / ing.amount);
            }
            
            if (recipe.canBatch && recipe.maxBatchSize > 0)
                max = Mathf.Min(max, recipe.maxBatchSize);

            return Mathf.Max(1, max);
        }

        private void UpdateQuantityDisplay()
        {
            if (quantityText) quantityText.text = _craftQuantity.ToString();
        }

        private void UpdateCraftButtonState()
        {
            bool canCraft = _selectedRecipe != null && 
                CraftingService.Instance.CanCraftNow(_selectedRecipe, _currentUnit, _currentInventory);
            
            if (craftButton) craftButton.interactable = canCraft;
            if (queueButton) queueButton.interactable = canCraft;
        }

        private void CraftInstant()
        {
            if (_selectedRecipe == null) return;
            if (CraftingService.Instance.CraftInstant(_selectedRecipe, _currentInventory, _craftQuantity))
            {
                UpdateDetailsDisplay();
            }
        }

        private void QueueCraft()
        {
            if (_selectedRecipe == null) return;
            CraftingService.Instance.QueueCraft(_selectedRecipe, _currentUnit, _currentInventory, _craftQuantity);
            UpdateDetailsDisplay();
        }
    }
}
