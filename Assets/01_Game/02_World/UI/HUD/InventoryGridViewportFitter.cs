#region

using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Inventory Grid Viewport Fitter")]
    [DisallowMultipleComponent]
    public sealed class InventoryGridViewportFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] [Min(1)] private int columns = 10;
        [SerializeField] [Min(1)] private int visibleRows = 5;
        [SerializeField] [Min(1f)] private float minCellSize = 8f;

        private int _lastChildCount = -1;
        private Vector2 _lastViewportSize;

        private void Update()
        {
            if (viewport == null || content == null || grid == null) return;

            var viewportSize = viewport.rect.size;
            var childCount = content.childCount;
            if (childCount != _lastChildCount || viewportSize != _lastViewportSize) ApplyLayout();
        }

        private void OnEnable()
        {
            ApplyLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout();
        }

        public void Configure(RectTransform viewportRect, RectTransform contentRect, GridLayoutGroup layoutGroup,
            int columnCount, int visibleRowCount)
        {
            viewport = viewportRect;
            content = contentRect;
            grid = layoutGroup;
            columns = Mathf.Max(1, columnCount);
            visibleRows = Mathf.Max(1, visibleRowCount);
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (viewport == null || content == null || grid == null) return;

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);

            var usableWidth = viewport.rect.width - grid.padding.left - grid.padding.right -
                              grid.spacing.x * (columns - 1);
            var usableHeight = viewport.rect.height - grid.padding.top - grid.padding.bottom -
                               grid.spacing.y * (visibleRows - 1);

            var cellWidth = Mathf.Max(minCellSize, usableWidth / columns);
            var cellHeight = Mathf.Max(minCellSize, usableHeight / visibleRows);
            grid.cellSize = new Vector2(cellWidth, cellHeight);

            var totalRows = Mathf.CeilToInt(content.childCount / (float)columns);
            var totalHeight = grid.padding.top + grid.padding.bottom + totalRows * cellHeight +
                              Mathf.Max(0, totalRows - 1) * grid.spacing.y;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);

            _lastChildCount = content.childCount;
            _lastViewportSize = viewport.rect.size;
        }
    }
}