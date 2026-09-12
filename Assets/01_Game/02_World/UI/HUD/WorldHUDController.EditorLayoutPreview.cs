#if UNITY_EDITOR
using UnityEngine;

namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {
        private void OnValidate()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext || !showEditorLayoutPreviews) return;
            EnsureEditorLayoutPreviewVisibility();
        }

        private void TryRefreshEditorLayoutPreviewOnEnable()
        {
            if (!UILayoutPreviewUtility.IsEditorLayoutContext || !showEditorLayoutPreviews) return;
            EnsureEditorLayoutPreviewVisibility();
        }

        private void EnsureEditorLayoutPreviewVisibility()
        {
            if (this == null || !UILayoutPreviewUtility.IsEditorLayoutContext || !showEditorLayoutPreviews)
                return;

            if (editorPreviewExpandBottomBar)
                _bottomBarExpanded = true;

            if (editorPreviewShowCraftingPanel && craftingPanel != null)
                craftingPanel.SetActive(true);

            if (editorPreviewShowBuildItemsStrip)
                ApplyEditorBuildStripPreview();
            else
                ApplyEditorSquadStripPreview();
        }

        private void ApplyEditorBuildStripPreview()
        {
            _showingBuildItems = true;
            TryResolveExistingBuildItemStrips();
            if (_bottomBuildItemsRoot == null)
                EnsureBottomBuildItemsStrip();

            SetAllBuildItemStripsActive(true);

            if (portraitStrip != null)
                portraitStrip.gameObject.SetActive(false);
        }

        private void ApplyEditorSquadStripPreview()
        {
            _showingBuildItems = false;
            SetAllBuildItemStripsActive(false);

            if (portraitStrip != null)
                portraitStrip.gameObject.SetActive(editorPreviewExpandBottomBar);
        }

    }
}
#endif
