using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    /// Converts selected materials from Standard (Built-in) to URP/Lit
    /// while preserving color, textures, and metallic/smoothness values.
    ///
    /// Usage:
    ///   1. Select one or more materials in the Project window
    ///   2. Tools → Zombera → Convert Selected Materials to URP
    /// </summary>
    public static class MaterialUrpConverter
    {
        private const string URPLitShader  = "Universal Render Pipeline/Lit";
        private const string DoorPackFolder = "Assets/ThirdParty/Free Wood Door Pack";

        [MenuItem("Tools/Zombera/Convert Free Wood Door Pack to URP")]
        private static void ConvertDoorPack()
        {
            ConvertFolder(DoorPackFolder);
        }

        [MenuItem("Tools/Zombera/Convert Selected Materials to URP")]
        private static void ConvertSelected()
        {
            var materials = Selection.GetFiltered<Material>(SelectionMode.Assets);
            if (materials.Length == 0)
            {
                EditorUtility.DisplayDialog("Convert to URP", "Select one or more materials in the Project window first.", "OK");
                return;
            }
            ConvertMaterials(materials);
        }

        private static void ConvertFolder(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Convert to URP", $"No materials found in:\n{folder}", "OK");
                return;
            }

            var materials = new Material[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));

            ConvertMaterials(materials);
        }

        private static void ConvertMaterials(Material[] materials)
        {
            Shader urpLit = Shader.Find(URPLitShader);
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog("Convert to URP", "Could not find shader: " + URPLitShader, "OK");
                return;
            }

            int converted = 0;
            int skipped   = 0;

            foreach (var mat in materials)
            {
                if (mat.shader == urpLit) { skipped++; continue; }

                // Read Standard properties BEFORE switching shader
                Color   baseColor   = mat.HasProperty("_Color")              ? mat.GetColor("_Color")                 : Color.white;
                Texture baseTex     = mat.HasProperty("_MainTex")            ? mat.GetTexture("_MainTex")             : null;
                Vector2 baseTiling  = mat.HasProperty("_MainTex")            ? mat.GetTextureScale("_MainTex")        : Vector2.one;
                Vector2 baseOffset  = mat.HasProperty("_MainTex")            ? mat.GetTextureOffset("_MainTex")       : Vector2.zero;
                Texture metallicMap = mat.HasProperty("_MetallicGlossMap")   ? mat.GetTexture("_MetallicGlossMap")    : null;
                float   metallic    = mat.HasProperty("_Metallic")           ? mat.GetFloat("_Metallic")              : 0f;
                float   smoothness  = mat.HasProperty("_Glossiness")         ? mat.GetFloat("_Glossiness")            : 0.5f;
                Texture normalMap   = mat.HasProperty("_BumpMap")            ? mat.GetTexture("_BumpMap")             : null;
                float   normalScale = mat.HasProperty("_BumpScale")          ? mat.GetFloat("_BumpScale")             : 1f;
                Texture occlusionMap= mat.HasProperty("_OcclusionMap")       ? mat.GetTexture("_OcclusionMap")        : null;
                Texture emissionMap = mat.HasProperty("_EmissionMap")        ? mat.GetTexture("_EmissionMap")         : null;
                Color   emissionCol = mat.HasProperty("_EmissionColor")      ? mat.GetColor("_EmissionColor")         : Color.black;

                // Switch shader
                mat.shader = urpLit;

                // Remap to URP property names
                mat.SetColor("_BaseColor",    baseColor);

                if (baseTex != null)
                {
                    mat.SetTexture("_BaseMap",  baseTex);
                    mat.SetTextureScale("_BaseMap",  baseTiling);
                    mat.SetTextureOffset("_BaseMap", baseOffset);
                }

                if (metallicMap != null) mat.SetTexture("_MetallicGlossMap", metallicMap);
                mat.SetFloat("_Metallic",    metallic);
                mat.SetFloat("_Smoothness",  smoothness);

                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap",  normalMap);
                    mat.SetFloat("_BumpScale",  normalScale);
                    mat.EnableKeyword("_NORMALMAP");
                }

                if (occlusionMap != null) mat.SetTexture("_OcclusionMap", occlusionMap);

                if (emissionMap != null || emissionCol != Color.black)
                {
                    mat.SetTexture("_EmissionMap",  emissionMap);
                    mat.SetColor("_EmissionColor",  emissionCol);
                    mat.EnableKeyword("_EMISSION");
                }

                EditorUtility.SetDirty(mat);
                converted++;
                Debug.Log($"[URP Convert] {mat.name}: converted, base color={baseColor}, tex={baseTex?.name ?? "none"}");
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Convert to URP",
                $"Done.\nConverted: {converted}\nAlready URP (skipped): {skipped}", "OK");
        }
    }
}
