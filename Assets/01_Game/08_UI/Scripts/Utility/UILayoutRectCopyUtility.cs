using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    internal static class UILayoutRectCopyUtility
    {
        public static void CopyRectLayout(RectTransform source, RectTransform target)
        {
            if (source == null || target == null) return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        public static void CopyLayoutElement(GameObject source, GameObject target)
        {
            if (source == null || target == null) return;

            var sourceElement = source.GetComponent<LayoutElement>();
            if (sourceElement == null)
            {
                var targetElement = target.GetComponent<LayoutElement>();
                if (targetElement != null)
                    Object.DestroyImmediate(targetElement);
                return;
            }

            var layoutElement = target.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = target.AddComponent<LayoutElement>();

            layoutElement.ignoreLayout = sourceElement.ignoreLayout;
            layoutElement.minWidth = sourceElement.minWidth;
            layoutElement.minHeight = sourceElement.minHeight;
            layoutElement.preferredWidth = sourceElement.preferredWidth;
            layoutElement.preferredHeight = sourceElement.preferredHeight;
            layoutElement.flexibleWidth = sourceElement.flexibleWidth;
            layoutElement.flexibleHeight = sourceElement.flexibleHeight;
            layoutElement.layoutPriority = sourceElement.layoutPriority;
        }
    }
}
