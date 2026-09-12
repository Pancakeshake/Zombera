using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    [Serializable]
    public struct CraftingRowLayoutCapture
    {
        public bool hasCapture;
        public Vector2 sizeDelta;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector3 localScale;
        public bool hasLayoutElement;
        public bool ignoreLayout;
        public float minWidth;
        public float minHeight;
        public float preferredWidth;
        public float preferredHeight;
        public float flexibleWidth;
        public float flexibleHeight;
        public int layoutPriority;

        [Header("Text (TMP)")]
        public bool hasTextCapture;
        public string[] tmpPaths;
        public float[] tmpFontSizes;
        public bool[] tmpAutoSizes;
        public float[] tmpFontSizeMins;
        public float[] tmpFontSizeMaxes;

        public static CraftingRowLayoutCapture FromRectTransform(RectTransform source)
        {
            if (source == null)
                return default;

            var capture = new CraftingRowLayoutCapture
            {
                hasCapture = true,
                sizeDelta = source.sizeDelta,
                anchorMin = source.anchorMin,
                anchorMax = source.anchorMax,
                pivot = source.pivot,
                anchoredPosition = source.anchoredPosition,
                localScale = source.localScale
            };

            CaptureTmpText(source, ref capture);

            var layoutElement = source.GetComponent<LayoutElement>();
            capture.hasLayoutElement = true;
            if (layoutElement != null)
            {
                capture.ignoreLayout = layoutElement.ignoreLayout;
                capture.minWidth = layoutElement.minWidth;
                capture.minHeight = layoutElement.minHeight;
                capture.preferredWidth = layoutElement.preferredWidth;
                capture.preferredHeight = layoutElement.preferredHeight;
                capture.flexibleWidth = layoutElement.flexibleWidth;
                capture.flexibleHeight = layoutElement.flexibleHeight;
                capture.layoutPriority = layoutElement.layoutPriority;
                return capture;
            }

            // Vertical/horizontal layout groups read LayoutElement, not just sizeDelta.
            capture.preferredWidth = Mathf.Max(0f, capture.sizeDelta.x);
            capture.preferredHeight = Mathf.Max(0f, capture.sizeDelta.y);
            return capture;
        }

        public void ApplyTo(RectTransform target)
        {
            if (!hasCapture || target == null) return;

            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.pivot = pivot;
            target.sizeDelta = sizeDelta;
            target.anchoredPosition = anchoredPosition;
            target.localScale = localScale;
            ApplyLayoutElement(target);
            ApplyTmpText(target);
        }

        /// <summary>
        ///     Size + layout-element only. Use for rows parented under Vertical/Horizontal layout groups
        ///     so Scene-view stacking stays correct after play mode.
        /// </summary>
        public void ApplySizeTo(RectTransform target)
        {
            if (!hasCapture || target == null) return;

            target.sizeDelta = sizeDelta;
            target.localScale = localScale;
            ApplyLayoutElement(target);
            ApplyTmpText(target);
        }

        private void ApplyLayoutElement(RectTransform target)
        {
            if (!hasLayoutElement) return;

            var layoutElement = target.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = target.gameObject.AddComponent<LayoutElement>();

            layoutElement.ignoreLayout = ignoreLayout;
            layoutElement.minWidth = minWidth;
            layoutElement.minHeight = minHeight;
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = flexibleWidth;
            layoutElement.flexibleHeight = flexibleHeight;
            layoutElement.layoutPriority = layoutPriority;
        }

        private static void CaptureTmpText(RectTransform source, ref CraftingRowLayoutCapture capture)
        {
            if (source == null) return;

            var texts = source.GetComponentsInChildren<TMP_Text>(true);
            if (texts == null || texts.Length == 0) return;

            var paths = new List<string>(texts.Length);
            var sizes = new List<float>(texts.Length);
            var autoSizes = new List<bool>(texts.Length);
            var minSizes = new List<float>(texts.Length);
            var maxSizes = new List<float>(texts.Length);

            for (var i = 0; i < texts.Length; i++)
            {
                var txt = texts[i];
                if (txt == null) continue;

                paths.Add(ResolveRelativePath(source, txt.transform));
                sizes.Add(txt.fontSize);
                autoSizes.Add(txt.enableAutoSizing);
                minSizes.Add(txt.fontSizeMin);
                maxSizes.Add(txt.fontSizeMax);
            }

            if (paths.Count == 0) return;

            capture.hasTextCapture = true;
            capture.tmpPaths = paths.ToArray();
            capture.tmpFontSizes = sizes.ToArray();
            capture.tmpAutoSizes = autoSizes.ToArray();
            capture.tmpFontSizeMins = minSizes.ToArray();
            capture.tmpFontSizeMaxes = maxSizes.ToArray();
        }

        private void ApplyTmpText(RectTransform target)
        {
            if (!hasTextCapture || target == null) return;
            if (tmpPaths == null || tmpFontSizes == null) return;

            var count = Mathf.Min(tmpPaths.Length, tmpFontSizes.Length);
            for (var i = 0; i < count; i++)
            {
                var path = tmpPaths[i];
                if (string.IsNullOrWhiteSpace(path)) continue;

                var child = target.Find(path);
                if (child == null) continue;

                var txt = child.GetComponent<TMP_Text>();
                if (txt == null) continue;

                txt.enableAutoSizing = tmpAutoSizes != null && i < tmpAutoSizes.Length && tmpAutoSizes[i];
                if (tmpFontSizeMins != null && i < tmpFontSizeMins.Length) txt.fontSizeMin = tmpFontSizeMins[i];
                if (tmpFontSizeMaxes != null && i < tmpFontSizeMaxes.Length) txt.fontSizeMax = tmpFontSizeMaxes[i];
                txt.fontSize = tmpFontSizes[i];
            }
        }

        private static string ResolveRelativePath(Transform root, Transform child)
        {
            if (root == null || child == null) return string.Empty;
            if (root == child) return string.Empty;

            var segments = new List<string>(8);
            var current = child;
            while (current != null && current != root)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
