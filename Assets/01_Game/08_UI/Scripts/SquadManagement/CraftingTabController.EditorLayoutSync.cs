#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.UI;

namespace Zombera.UI.SquadManagement
{
    public sealed partial class CraftingTabController
    {
        internal void ApplyPreviewLayoutToPrefabs(bool refreshPreviewsAfterApply)
        {
            CaptureLayoutFromPreviews();
            ApplyCapturedLayoutToPreviews();

            var changed = false;
            changed |= TryPushCapturedLayoutToPrefab(
                categoryRowLayout,
                categoryButtonPrefab != null ? categoryButtonPrefab.gameObject : null,
                "category");

            changed |= TryPushCapturedLayoutToPrefab(
                recipeRowLayout,
                recipeListItemPrefab != null ? recipeListItemPrefab.gameObject : null,
                "recipe");

            changed |= TryPushCapturedLayoutToPrefab(
                ingredientRowLayout,
                ingredientListItemPrefab != null ? ingredientListItemPrefab.gameObject : null,
                "ingredient");

            EditorUtility.SetDirty(this);

            if (!changed) return;

            AssetDatabase.SaveAssets();

            if (refreshPreviewsAfterApply)
            {
                ApplyCapturedLayoutToPreviews();
            }
        }

        internal void CaptureLayoutFromPreviews()
        {
            categoryRowLayout = CaptureRowLayout(
                categoryButtonContainer,
                CraftingTabLayoutTemplate.TemplateKind.Category,
                categoryRowLayout);

            recipeRowLayout = CaptureRowLayout(
                recipeListContent,
                CraftingTabLayoutTemplate.TemplateKind.Recipe,
                recipeRowLayout);

            ingredientRowLayout = CaptureRowLayout(
                ingredientListContent,
                CraftingTabLayoutTemplate.TemplateKind.Ingredient,
                ingredientRowLayout);
        }

        private static CraftingRowLayoutCapture CaptureRowLayout(
            Transform container,
            CraftingTabLayoutTemplate.TemplateKind kind,
            CraftingRowLayoutCapture fallback)
        {
            var source = UILayoutPreviewUtility.FindBestLayoutTemplate(container, kind);
            return source != null ? CraftingRowLayoutCapture.FromRectTransform(source) : fallback;
        }

        internal void ApplyCapturedLayoutToPreviews()
        {
            ApplyCaptureToPreviewRows(categoryButtonContainer, CraftingTabLayoutTemplate.TemplateKind.Category,
                categoryRowLayout);
            ApplyCaptureToPreviewRows(recipeListContent, CraftingTabLayoutTemplate.TemplateKind.Recipe, recipeRowLayout);
            ApplyCaptureToPreviewRows(ingredientListContent, CraftingTabLayoutTemplate.TemplateKind.Ingredient,
                ingredientRowLayout);

            RebuildPreviewContainerLayouts();
        }

        private void RebuildPreviewContainerLayouts()
        {
            RebuildLayoutContainer(categoryButtonContainer);
            RebuildLayoutContainer(recipeListContent);
            RebuildLayoutContainer(ingredientListContent);
        }

        private static void RebuildLayoutContainer(Transform container)
        {
            if (container is not RectTransform rect) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private static void ApplyCaptureToPreviewRows(
            Transform container,
            CraftingTabLayoutTemplate.TemplateKind kind,
            CraftingRowLayoutCapture capture)
        {
            if (container == null || !capture.hasCapture) return;

            for (var i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (!UILayoutPreviewUtility.IsPreviewObject(child)) continue;

                var template = child.GetComponent<CraftingTabLayoutTemplate>();
                if (template == null || template.Kind != kind) continue;

                capture.ApplySizeTo(child as RectTransform);
            }
        }

        private static bool TryPushCapturedLayoutToPrefab(
            CraftingRowLayoutCapture capture,
            GameObject prefabReference,
            string label)
        {
            if (!capture.hasCapture || prefabReference == null) return false;

            var prefabAsset = CraftingTabLayoutPrefabSync.ResolvePrefabAssetRoot(prefabReference);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[CraftingTab] Could not resolve {label} prefab asset for layout push.", prefabReference);
                return false;
            }

            CraftingTabLayoutPrefabSync.ApplyCaptureToPrefabAsset(capture, prefabAsset);
            return true;
        }
    }

    internal static class CraftingTabLayoutPrefabSync
    {
        public static GameObject ResolvePrefabAssetRoot(GameObject reference)
        {
            if (reference == null) return null;

            var path = AssetDatabase.GetAssetPath(reference);
            if (!string.IsNullOrWhiteSpace(path))
                return reference;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(reference);
            if (source != null)
                return source;

            return PrefabUtility.GetCorrespondingObjectFromOriginalSource(reference);
        }

        public static void ApplyCaptureToPrefabAsset(CraftingRowLayoutCapture capture, GameObject prefabAssetRoot)
        {
            if (!capture.hasCapture || prefabAssetRoot == null) return;

            var path = AssetDatabase.GetAssetPath(prefabAssetRoot);
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning($"[CraftingTab] Could not resolve prefab asset path for '{prefabAssetRoot.name}'.",
                    prefabAssetRoot);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target = root.transform as RectTransform;
                if (target == null) return;

                capture.ApplyTo(target);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ApplyAllOpenCraftingTabs(bool refreshPreviewsAfterApply)
        {
            var tabs = Object.FindObjectsByType<CraftingTabController>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < tabs.Length; i++)
                tabs[i].ApplyPreviewLayoutToPrefabs(refreshPreviewsAfterApply);
        }
    }

    [InitializeOnLoad]
    internal static class CraftingTabLayoutPlayModeBridge
    {
        static CraftingTabLayoutPlayModeBridge()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ApplyBeforePlayMode();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
                RestorePreviewsAfterPlayMode();
        }

        private static void ApplyBeforePlayMode()
        {
            var tabs = Object.FindObjectsByType<CraftingTabController>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < tabs.Length; i++)
            {
                if (!tabs[i].ShouldAutoApplyPreviewLayoutToPrefabsOnPlay) continue;
                tabs[i].ApplyPreviewLayoutToPrefabs(refreshPreviewsAfterApply: false);
            }
        }

        private static void RestorePreviewsAfterPlayMode()
        {
            var tabs = Object.FindObjectsByType<CraftingTabController>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < tabs.Length; i++)
                tabs[i].ApplyCapturedLayoutToPreviews();
        }
    }
}
#endif
