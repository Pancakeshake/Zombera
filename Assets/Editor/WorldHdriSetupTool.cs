#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Zombera.Environment;

namespace Zombera.Editor
{
    /// <summary>
    /// Applies the project HDRI skybox to the World scene and wires DayNightController skybox references.
    /// </summary>
    public static class WorldHdriSetupTool
    {
        private const string WorldScenePath = "Assets/Scenes/World.unity";
        private const string HdriTexturePath = "Assets/HDR/overcast_soil_puresky_2k.hdr";
        private const string WorldHdriSkyboxMaterialPath = "Assets/Art/Skybox_HDRI_World.mat";

        [MenuItem("Tools/Zombera/World/Environment/Apply HDRI Skybox To World")]
        public static void ApplyHdriSkyboxToWorld()
        {
            Texture hdriTexture = AssetDatabase.LoadAssetAtPath<Texture>(HdriTexturePath);
            if (hdriTexture == null)
            {
                EditorUtility.DisplayDialog(
                    "HDRI Not Found",
                    $"Could not load HDRI texture at '{HdriTexturePath}'.",
                    "OK");
                return;
            }

            Shader panoramicSkyboxShader = Shader.Find("Skybox/Panoramic");
            if (panoramicSkyboxShader == null)
            {
                EditorUtility.DisplayDialog(
                    "Skybox Shader Not Found",
                    "Could not find shader 'Skybox/Panoramic'.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Material skyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>(WorldHdriSkyboxMaterialPath);
            bool createdSkyboxMaterial = false;
            if (skyboxMaterial == null)
            {
                skyboxMaterial = new Material(panoramicSkyboxShader)
                {
                    name = "Skybox_HDRI_World"
                };

                AssetDatabase.CreateAsset(skyboxMaterial, WorldHdriSkyboxMaterialPath);
                createdSkyboxMaterial = true;
            }

            bool configuredSkyboxMaterial = ConfigureHdriSkyboxMaterial(skyboxMaterial, panoramicSkyboxShader, hdriTexture);

            Scene worldScene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            DynamicGI.UpdateEnvironment();

            int wiredDayNightControllers = WireDayNightSkyboxReferences(skyboxMaterial);

            EditorUtility.SetDirty(skyboxMaterial);
            EditorSceneManager.MarkSceneDirty(worldScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = "[Zombera] World HDRI skybox setup complete.\n"
                + $"  HDRI: {HdriTexturePath}\n"
                + $"  Skybox material: {WorldHdriSkyboxMaterialPath} (created: {createdSkyboxMaterial})\n"
                + $"  Skybox material updated: {configuredSkyboxMaterial}\n"
                + $"  RenderSettings.skybox assigned: true\n"
                + $"  DayNightController instances wired: {wiredDayNightControllers}";

            Debug.Log(summary, skyboxMaterial);
            EditorUtility.DisplayDialog("World HDRI Setup", summary, "OK");
        }

        [MenuItem("Tools/Zombera/World/Environment/Apply HDRI Skybox To World", true)]
        private static bool ValidateApplyHdriSkyboxToWorld()
        {
            return AssetDatabase.LoadAssetAtPath<Texture>(HdriTexturePath) != null;
        }

        private static bool ConfigureHdriSkyboxMaterial(Material material, Shader shader, Texture hdriTexture)
        {
            bool changed = false;

            if (material.shader != shader)
            {
                material.shader = shader;
                changed = true;
            }

            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != hdriTexture)
            {
                material.SetTexture("_MainTex", hdriTexture);
                changed = true;
            }

            if (material.HasProperty("_Mapping") && !Mathf.Approximately(material.GetFloat("_Mapping"), 0f))
            {
                material.SetFloat("_Mapping", 0f);
                changed = true;
            }

            if (material.HasProperty("_ImageType") && !Mathf.Approximately(material.GetFloat("_ImageType"), 0f))
            {
                material.SetFloat("_ImageType", 0f);
                changed = true;
            }

            if (material.HasProperty("_Exposure") && !Mathf.Approximately(material.GetFloat("_Exposure"), 1f))
            {
                material.SetFloat("_Exposure", 1f);
                changed = true;
            }

            if (material.HasProperty("_Rotation") && !Mathf.Approximately(material.GetFloat("_Rotation"), 0f))
            {
                material.SetFloat("_Rotation", 0f);
                changed = true;
            }

            if (material.HasProperty("_Tint") && material.GetColor("_Tint") != Color.white)
            {
                material.SetColor("_Tint", Color.white);
                changed = true;
            }

            return changed;
        }

        private static int WireDayNightSkyboxReferences(Material skyboxMaterial)
        {
            int wired = 0;

            DayNightController[] dayNightControllers = Object.FindObjectsByType<DayNightController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < dayNightControllers.Length; i++)
            {
                DayNightController controller = dayNightControllers[i];
                if (controller == null)
                {
                    continue;
                }

                SerializedObject serializedController = new SerializedObject(controller);
                SerializedProperty skyboxMaterialProperty = serializedController.FindProperty("skyboxMaterial");
                if (skyboxMaterialProperty == null)
                {
                    continue;
                }

                if (skyboxMaterialProperty.objectReferenceValue == skyboxMaterial)
                {
                    continue;
                }

                skyboxMaterialProperty.objectReferenceValue = skyboxMaterial;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
                wired++;
            }

            return wired;
        }
    }
}
#endif
