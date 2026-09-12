#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Zombera.Editor
{
    /// <summary>
    ///     One-shot: strips ProBuilderMesh from roof kit prefabs via reflection (no assembly dependency).
    ///     Run via Tools → Build → Mod Kits → Strip ProBuilder From Roof Kit.
    /// </summary>
    internal static class StripProBuilderFromRoofKit
    {
        private const string MenuPath = "Tools/Build/Mod Kits/Strip ProBuilder From Roof Kit";

        private static readonly string[] PrefabPaths =
        {
            "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Ridge.prefab",
            "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Panel.prefab",
            "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Gable.prefab",
        };

        [MenuItem(MenuPath, priority = -496)]
        private static void StripAll()
        {
            var stripped = 0;
            foreach (var path in PrefabPaths)
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
                {
                    Debug.LogWarning($"[StripRoofKit] Not found: {path}");
                    continue;
                }

                var content = PrefabUtility.LoadPrefabContents(path);
                if (content == null) continue;

                try
                {
                    var pbm = content.GetComponent<ProBuilderMesh>();
                    if (pbm == null)
                    {
                        Debug.Log($"[StripRoofKit] Already stripped: {path}");
                        continue;
                    }

                    var mf = content.GetComponent<MeshFilter>();
                    if (mf == null) mf = content.AddComponent<MeshFilter>();

                    // Access internal ProBuilderMesh.mesh via reflection
                    var meshProp = typeof(ProBuilderMesh).GetProperty("mesh",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    var mesh = meshProp?.GetValue(pbm) as Mesh;
                    mf.sharedMesh = mesh != null ? Object.Instantiate(mesh) : new Mesh();
                    mf.sharedMesh.name = content.name;

                    Object.DestroyImmediate(pbm, true);
                    PrefabUtility.SaveAsPrefabAsset(content, path);
                    stripped++;
                    Debug.Log($"[StripRoofKit] Stripped: {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(content);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Strip ProBuilder",
                $"Stripped ProBuilder from {stripped} / {PrefabPaths.Length} prefab(s).", "OK");
        }
    }
}
#endif
