using TMPro;
using UnityEditor;
using UnityEngine;
using Zombera.UI;

namespace Zombera.Editor
{
    public static partial class WorldHudSetupTool
    {
        private static bool TryInstantiateWorldHudCanvasPrefab(out GameObject instance)
        {
            instance = null;

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WorldHudCanvasPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning(
                    $"[WorldHUDSetupTool] Could not load prefab at '{WorldHudCanvasPrefabPath}'. Falling back to legacy code builder.");
                return false;
            }

            instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (instance == null)
            {
                Debug.LogWarning(
                    "[WorldHUDSetupTool] Prefab instantiation failed. Falling back to legacy code builder.");
                return false;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Build World HUD");

            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            var canvas = instance.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = ZomberaCanvasLayer.Hud;

            return true;
        }

        private static void ForceTruncateOverflowOnHudText(Transform root)
        {
            if (root == null) return;

            var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text == null || text.overflowMode != TextOverflowModes.Ellipsis) continue;
                text.overflowMode = TextOverflowModes.Truncate;
            }
        }
    }
}
