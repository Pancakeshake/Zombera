#if UNITY_EDITOR
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Inventory.Crafting;
using Zombera.UI;

namespace Zombera.UI.SquadManagement
{
    public sealed partial class CraftingTabController
    {
        private void OnEnable()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext) return;
            ScheduleEditorLayoutPreviewEnsure();
        }

        private void OnValidate()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext) return;
            ScheduleEditorLayoutPreviewEnsure();
        }

        [ContextMenu("Rebuild Editor Layout Preview")]
        private void RebuildEditorLayoutPreviewFromMenu()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext) return;
            RebuildEditorLayoutPreview();
        }

        [ContextMenu("Capture Preview Layout (no prefab push)")]
        private void CapturePreviewLayoutFromMenu()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext) return;
            CaptureLayoutFromPreviews();
            ApplyCapturedLayoutToPreviews();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Apply Preview Layout To Prefabs")]
        private void ApplyPreviewLayoutToPrefabsFromMenu()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext) return;
            ApplyPreviewLayoutToPrefabs(refreshPreviewsAfterApply: true);
        }

        [ContextMenu("Clear Editor Layout Preview")]
        private void ClearEditorLayoutPreviewFromMenu()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext) return;
            ClearEditorPreviewContent();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        internal bool ShouldAutoApplyPreviewLayoutToPrefabsOnPlay => autoApplyPreviewLayoutToPrefabsOnPlay;
        internal bool PopulateEditorLayoutPreview => populateEditorLayoutPreview;

        private void ScheduleEditorLayoutPreviewEnsure()
        {
            if (IsPrefabAssetWithoutPrefabStage()) return;

            UnityEditor.EditorApplication.delayCall -= EnsureEditorLayoutPreviewDeferred;
            UnityEditor.EditorApplication.delayCall += EnsureEditorLayoutPreviewDeferred;
        }

        private bool IsPrefabAssetWithoutPrefabStage()
        {
            if (this == null) return true;

            return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)
                   && UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) == null;
        }

        private void EnsureEditorLayoutPreviewDeferred()
        {
            if (this == null) return;
            EnsureEditorLayoutPreview();
        }

        private void EnsureEditorLayoutPreview()
        {
            if (!populateEditorLayoutPreview)
            {
                ClearEditorPreviewContent();
                return;
            }

            if (HasExactEditorLayoutPreview())
            {
                PopulateEditorDetailPanel();
                return;
            }

            RebuildEditorLayoutPreview();
        }

        private void GetExpectedEditorPreviewCounts(out int expectedCategories, out int expectedRecipes,
            out int expectedIngredients)
        {
            var craftingCategoryCount = Enum.GetValues(typeof(CraftingCategory)).Length;
            expectedCategories = craftingCategoryCount + 1;
            expectedRecipes = craftingCategoryCount * Mathf.Max(1, editorPreviewRecipesPerCategory);
            expectedIngredients = Mathf.Max(1, editorPreviewIngredientCount);
        }

        private bool HasExactEditorLayoutPreview()
        {
            GetExpectedEditorPreviewCounts(out var expectedCategories, out var expectedRecipes,
                out var expectedIngredients);

            var categoryCount = UILayoutPreviewUtility.CountPreviewChildren(categoryButtonContainer, "Category");
            if (categoryCount != expectedCategories) return false;

            var recipeCount = UILayoutPreviewUtility.CountPreviewChildren(recipeListContent, "Recipe");
            if (recipeCount != expectedRecipes) return false;

            var ingredientCount = UILayoutPreviewUtility.CountPreviewChildren(ingredientListContent, "Ingredient");
            if (ingredientCount != expectedIngredients) return false;

            // Also clear stray duplicates that lost preview markers from older runs.
            if (categoryButtonContainer != null && categoryButtonContainer.childCount != expectedCategories)
                return false;

            if (recipeListContent != null && recipeListContent.childCount != expectedRecipes)
                return false;

            if (ingredientListContent != null && ingredientListContent.childCount != expectedIngredients)
                return false;

            return true;
        }

        internal void RebuildEditorLayoutPreview()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext || !populateEditorLayoutPreview)
                return;

            ClearEditorPreviewContent();
            PopulateEditorCategoryButtons();
            PopulateEditorRecipeList();
            PopulateEditorDetailPanel();
            PopulateEditorIngredientList();
            ApplyCapturedLayoutToPreviews();

            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void ClearEditorPreviewContent()
        {
            UILayoutPreviewUtility.ClearAllChildren(categoryButtonContainer);
            UILayoutPreviewUtility.ClearAllChildren(recipeListContent);
            UILayoutPreviewUtility.ClearAllChildren(ingredientListContent);
        }

        private void PopulateEditorCategoryButtons()
        {
            if (categoryButtonContainer == null || categoryButtonPrefab == null) return;

            SpawnEditorCategoryButton("ALL", null);

            foreach (CraftingCategory category in Enum.GetValues(typeof(CraftingCategory)))
                SpawnEditorCategoryButton(category.ToString().ToUpper(), category);
        }

        private void SpawnEditorCategoryButton(string label, CraftingCategory? category)
        {
            var button = UILayoutPreviewUtility.InstantiatePreview(
                categoryButtonPrefab, categoryButtonContainer, label, "Category",
                CraftingTabLayoutTemplate.TemplateKind.Category);

            if (button == null) return;

            var labelText = button.GetComponentInChildren<TMP_Text>(true);
            if (labelText != null)
                labelText.text = label;

            button.interactable = false;
        }

        private void PopulateEditorRecipeList()
        {
            if (recipeListContent == null || recipeListItemPrefab == null) return;

            foreach (CraftingCategory category in Enum.GetValues(typeof(CraftingCategory)))
            {
                var count = Mathf.Max(1, editorPreviewRecipesPerCategory);
                for (var i = 0; i < count; i++)
                {
                    var displayName = $"Preview {category} {i + 1}";
                    var item = UILayoutPreviewUtility.InstantiatePreview(
                        recipeListItemPrefab, recipeListContent, displayName, "Recipe",
                        CraftingTabLayoutTemplate.TemplateKind.Recipe);

                    if (item == null) continue;

                    item.SetupPreview(displayName, category.ToString());
                    if (item.TryGetComponent<Button>(out var button))
                        button.interactable = false;
                }
            }
        }

        private void PopulateEditorDetailPanel()
        {
            if (contextText != null)
                contextText.text = "Operator: Preview Survivor";

            if (detailName != null)
                detailName.text = editorPreviewSelectedRecipeName;

            if (detailCategory != null)
                detailCategory.text = editorPreviewSelectedCategory;

            if (detailDescription != null)
                detailDescription.text = editorPreviewDescription;

            if (detailSkillReq != null)
                detailSkillReq.text = editorPreviewSkillRequirement;

            if (detailStationReq != null)
                detailStationReq.text = editorPreviewStationRequirement;

            if (detailIcon != null)
                detailIcon.enabled = false;

            if (quantityText != null)
                quantityText.text = editorPreviewQuantity.ToString();

            if (craftButton != null)
                craftButton.interactable = false;

            if (queueButton != null)
                queueButton.interactable = false;
        }

        private void PopulateEditorIngredientList()
        {
            if (ingredientListContent == null || ingredientListItemPrefab == null) return;

            var count = Mathf.Max(1, editorPreviewIngredientCount);
            for (var i = 0; i < count; i++)
            {
                var item = UILayoutPreviewUtility.InstantiatePreview(
                    ingredientListItemPrefab, ingredientListContent, $"Ingredient {i + 1}", "Ingredient",
                    CraftingTabLayoutTemplate.TemplateKind.Ingredient);

                if (item == null) continue;

                item.SetupPreview(editorPreviewIngredientNames[i % editorPreviewIngredientNames.Length],
                    editorPreviewIngredientOwned[i % editorPreviewIngredientOwned.Length],
                    editorPreviewIngredientRequired[i % editorPreviewIngredientRequired.Length]);
            }
        }
    }
}
#endif
