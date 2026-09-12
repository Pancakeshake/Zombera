#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Adds a <c>TextMeshPro</c> label child with a background quad to the
    ///     street sign prefab so the city-gen placer can set road names and the
    ///     label covers any baked-in sign graphics.
    /// </summary>
    public static class StreetSignLabelTool
    {
        private const string SignPrefabPath = "Assets/02_Shared/Prefabs/Signage/SM_Street_Sign.prefab";
        private const string LabelContainerName = "Label";
        private const string FontPath           = "Fonts & Materials/LiberationSans SDF";

        private const float BgQuadWidth  = 4.2f;
        private const float BgQuadHeight = 1.7f;

        [MenuItem(MenuPaths.WorldSignage + "Add TMP Label to Street Sign Prefab", priority = -1000)]
        private static void AddLabelToStreetSignPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SignPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[StreetSignLabel] Prefab not found at '{SignPrefabPath}'.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(SignPrefabPath);
            if (contents == null)
            {
                Debug.LogError($"[StreetSignLabel] Failed to load prefab contents.");
                return;
            }

            var existing = contents.transform.Find(LabelContainerName);
            if (existing != null)
            {
                var existingTmp = existing.GetComponentInChildren<TextMeshPro>();
                if (existingTmp != null)
                {
                    Debug.Log($"[StreetSignLabel] Label with TMP already exists — skipped.");
                    PrefabUtility.UnloadPrefabContents(contents);
                    return;
                }
            }

            var font = Resources.Load<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError($"[StreetSignLabel] TMP font not found at Resources/{FontPath}.");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            // Remove stale label container
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            // Container
            var container = new GameObject(LabelContainerName);
            container.transform.SetParent(contents.transform, false);
            container.transform.localPosition = new Vector3(0f, 0f, -0.05f);

            // Background quad — covers the built-in sign graphics
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Label_BG";
            bg.transform.SetParent(container.transform, false);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(BgQuadWidth, BgQuadHeight, 1f);
            var bgMat = new Material(Shader.Find("Unlit/Color"));
            bgMat.color = Color.white;
            bgMat.name = "SignLabel_Background";
            bg.GetComponent<MeshRenderer>().sharedMaterial = bgMat;
            // Disable shadow casting / receiving for the quad
            var bgRenderer = bg.GetComponent<MeshRenderer>();
            bgRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bgRenderer.receiveShadows = false;
            Object.DestroyImmediate(bg.GetComponent<Collider>());

            // TMP text in front of background
            var textGo = new GameObject("Label_Text");
            textGo.transform.SetParent(container.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSize = 2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color32(20, 20, 20, 255);
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(BgQuadWidth - 0.4f, BgQuadHeight - 0.4f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.text = "Street Name";

            var saved = PrefabUtility.SaveAsPrefabAsset(contents, SignPrefabPath, out var success);
            PrefabUtility.UnloadPrefabContents(contents);

            if (success && saved != null)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[StreetSignLabel] Added TMP label with background to street sign prefab.");
            }
            else
            {
                Debug.LogError($"[StreetSignLabel] Failed to save prefab.");
            }
        }
    }
}
#endif
