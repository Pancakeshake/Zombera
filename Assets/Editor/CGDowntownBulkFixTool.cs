#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public sealed class CGDowntownBulkFixTool : EditorWindow
    {
        private const string MenuPath = "Tools/Assets/CG Downtown/Bulk Material Fix";

        [SerializeField] private float defaultSmoothness = 0.15f;
        [SerializeField] private float bumpScale = 1.4f;
        [SerializeField] private bool fixTextureImporters = true;
        [SerializeField] private bool createOrUpdateMaterials = true;
        [SerializeField] private bool remapFbxMaterials = true;
        [SerializeField] private bool forceSmoothness;
        [SerializeField] private bool setFbxNormalsToCalculate;

        private string _lastSummary = string.Empty;

        [MenuItem(MenuPath, priority = -490)]
        private static void OpenWindow()
        {
            var window = GetWindow<CGDowntownBulkFixTool>("CG Downtown Fix");
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        [MenuItem("Tools/Assets/CG Downtown/Run Bulk Material Fix Now", priority = -489)]
        private static void RunNowMenu()
        {
            var summary = CGDowntownBulkFixRunner.Run(CGDowntownBulkFixRunner.DefaultOptions());
            Debug.Log("[CGDowntownBulkFix] " + summary.Message);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("CG Downtown / CGAxis Bulk Fix", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Applies the known-good recipe from your fixed road materials:\n" +
                "• URP Lit + basecolor/diffuse + normal_opengl\n" +
                "• Metallic = 0, no MetallicGlossMap (roughness ≠ smoothness)\n" +
                "• Clamp Smoothness when > 0.5 (CGAxis often imports ~1.19)\n" +
                "• Remap FBX materials by name to Materials/",
                MessageType.Info);

            EditorGUILayout.Space();
            defaultSmoothness = EditorGUILayout.Slider("Default Smoothness", defaultSmoothness, 0f, 1f);
            bumpScale = EditorGUILayout.Slider("Bump Scale", bumpScale, 0f, 3f);
            fixTextureImporters = EditorGUILayout.ToggleLeft("Fix texture importers (normals/linear)", fixTextureImporters);
            createOrUpdateMaterials = EditorGUILayout.ToggleLeft("Create/update materials from maps", createOrUpdateMaterials);
            remapFbxMaterials = EditorGUILayout.ToggleLeft("Remap FBX materials by name", remapFbxMaterials);
            forceSmoothness = EditorGUILayout.ToggleLeft("Force smoothness on already-tuned mats", forceSmoothness);
            setFbxNormalsToCalculate = EditorGUILayout.ToggleLeft(
                "Set FBX Normals = Calculate (optional; your fixed road still uses Import)",
                setFbxNormalsToCalculate);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!createOrUpdateMaterials && !remapFbxMaterials && !fixTextureImporters && !setFbxNormalsToCalculate))
            {
                if (GUILayout.Button("Run Bulk Fix", GUILayout.Height(32f)))
                    RunFromWindow();
            }

            if (!string.IsNullOrEmpty(_lastSummary))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_lastSummary, MessageType.None);
            }
        }

        private void RunFromWindow()
        {
            var options = new CGDowntownBulkFixRunner.Options
            {
                DefaultSmoothness = defaultSmoothness,
                BumpScale = bumpScale,
                FixTextureImporters = fixTextureImporters,
                CreateOrUpdateMaterials = createOrUpdateMaterials,
                RemapFbxMaterials = remapFbxMaterials,
                ForceSmoothness = forceSmoothness,
                SetFbxNormalsToCalculate = setFbxNormalsToCalculate
            };

            try
            {
                var summary = CGDowntownBulkFixRunner.Run(options);
                _lastSummary = summary.Message;
                Debug.Log("[CGDowntownBulkFix] " + summary.Message);
            }
            catch (System.Exception ex)
            {
                _lastSummary = ex.Message;
                Debug.LogException(ex);
            }
        }
    }
}
#endif
