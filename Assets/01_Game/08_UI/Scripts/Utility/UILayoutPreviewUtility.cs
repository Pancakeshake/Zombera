using UnityEngine;

namespace Zombera.UI
{
    public static class UILayoutPreviewUtility
    {
        public const string ChildNamePrefix = "[Preview] ";

        public static bool IsEditorLayoutContext => !Application.isPlaying;

        public static void ClearPreviewChildren(Transform container)
        {
            if (container == null) return;

            for (var i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (!IsPreviewObject(child)) continue;

                DestroyObject(child.gameObject);
            }
        }

        /// <summary>
        ///     Clears every child under a dedicated dynamic-spawn root (category rows, recipe list, etc.).
        /// </summary>
        public static void ClearAllChildren(Transform container)
        {
            if (container == null) return;

            for (var i = container.childCount - 1; i >= 0; i--)
                DestroyObject(container.GetChild(i).gameObject);
        }

        public static bool IsPreviewObject(Transform transform)
        {
            if (transform == null) return false;
            if (transform.GetComponent<UILayoutPreviewMarker>() != null) return true;
            return transform.name.StartsWith(ChildNamePrefix, System.StringComparison.Ordinal);
        }

        public static T InstantiatePreview<T>(
            T prefab,
            Transform parent,
            string label,
            string group = "Default",
            CraftingTabLayoutTemplate.TemplateKind? templateKind = null)
            where T : Component
        {
            if (prefab == null || parent == null) return null;

#if UNITY_EDITOR
            T instance;
            if (!Application.isPlaying)
            {
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(parent))
                {
                    instance = Object.Instantiate(prefab, parent);
                }
                else
                {
                    var instantiated = UnityEditor.PrefabUtility.InstantiatePrefab(prefab.gameObject, parent) as GameObject;
                    instance = instantiated != null ? instantiated.GetComponent<T>() : null;
                    if (instance == null)
                        instance = Object.Instantiate(prefab, parent);
                }
            }
            else
            {
                instance = Object.Instantiate(prefab, parent);
            }
#else
            var instance = Object.Instantiate(prefab, parent);
#endif
            if (instance == null) return null;

            instance.name = ChildNamePrefix + label;
            var marker = instance.gameObject.GetComponent<UILayoutPreviewMarker>();
            if (marker == null)
                marker = instance.gameObject.AddComponent<UILayoutPreviewMarker>();

            marker.Configure(group);

            if (templateKind.HasValue)
            {
                var template = instance.gameObject.GetComponent<CraftingTabLayoutTemplate>();
                if (template == null)
                    template = instance.gameObject.AddComponent<CraftingTabLayoutTemplate>();

                template.Configure(templateKind.Value);
            }

            return instance;
        }

        public static Transform FindFirstLayoutTemplate(Transform container, CraftingTabLayoutTemplate.TemplateKind kind)
        {
            return FindBestLayoutTemplate(container, kind);
        }

        /// <summary>
        ///     Picks the preview row with the largest rect area so scene edits on any row are captured.
        /// </summary>
        public static RectTransform FindBestLayoutTemplate(Transform container, CraftingTabLayoutTemplate.TemplateKind kind)
        {
            if (container == null) return null;

            RectTransform best = null;
            var bestArea = 0f;

            for (var i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (!IsPreviewObject(child)) continue;

                var template = child.GetComponent<CraftingTabLayoutTemplate>();
                if (template == null || template.Kind != kind) continue;

                var rect = child as RectTransform;
                if (rect == null) continue;

                var area = Mathf.Abs(rect.sizeDelta.x * rect.sizeDelta.y);
                if (area >= bestArea)
                {
                    bestArea = area;
                    best = rect;
                }
            }

            return best;
        }

        public static int CountPreviewChildren(Transform container, string group = null)
        {
            if (container == null) return 0;

            var count = 0;
            for (var i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (!IsPreviewObject(child)) continue;

                if (!string.IsNullOrEmpty(group))
                {
                    var marker = child.GetComponent<UILayoutPreviewMarker>();
                    if (marker == null || !string.Equals(marker.PreviewGroup, group, System.StringComparison.Ordinal))
                        continue;
                }

                count++;
            }

            return count;
        }

        public static void DestroyObject(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
