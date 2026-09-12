#region

using UnityEngine;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Lightweight drag-selection box renderer in screen space.
    /// </summary>
    public sealed class SelectionBoxUI : MonoBehaviour
    {
        [SerializeField] private Color fillColor = new(0.16f, 0.56f, 0.86f, 0.18f);
        [SerializeField] private Color borderColor = new(0.31f, 0.79f, 1f, 0.95f);
        [SerializeField] [Min(1f)] private float borderThickness = 2f;

        private bool _isVisible;
        private Rect _screenRect;

        public void ShowScreenRect(Rect screenRect)
        {
            _screenRect = NormalizeRect(screenRect);
            _isVisible = _screenRect.width > 0.5f && _screenRect.height > 0.5f;
        }

        public void Hide()
        {
            _isVisible = false;
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            var guiRect = ConvertScreenRectToGuiRect(_screenRect);
            DrawRect(guiRect, fillColor);

            var thickness = Mathf.Max(1f, borderThickness);
            DrawRect(new Rect(guiRect.xMin, guiRect.yMin, guiRect.width, thickness), borderColor);
            DrawRect(new Rect(guiRect.xMin, guiRect.yMax - thickness, guiRect.width, thickness), borderColor);
            DrawRect(new Rect(guiRect.xMin, guiRect.yMin, thickness, guiRect.height), borderColor);
            DrawRect(new Rect(guiRect.xMax - thickness, guiRect.yMin, thickness, guiRect.height), borderColor);
        }

        private static Rect NormalizeRect(Rect rect)
        {
            var minX = Mathf.Min(rect.xMin, rect.xMax);
            var minY = Mathf.Min(rect.yMin, rect.yMax);
            var maxX = Mathf.Max(rect.xMin, rect.xMax);
            var maxY = Mathf.Max(rect.yMin, rect.yMax);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Rect ConvertScreenRectToGuiRect(Rect screenRect)
        {
            var y = Screen.height - screenRect.yMax;
            return new Rect(screenRect.xMin, y, screenRect.width, screenRect.height);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
