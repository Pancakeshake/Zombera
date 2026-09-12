#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public sealed class MeshyImportPipelineTool : EditorWindow
    {
        private enum DestinationRootSelection
        {
            Weapons,
            Clothing
        }

        private const string MenuPath = "Tools/Assets/Meshy Importer/Open Meshy Importer";

        [SerializeField] private DestinationRootSelection destinationRoot = DestinationRootSelection.Weapons;
        [SerializeField] private int selectedSubfolderIndex;
        [SerializeField] private bool useCustomSubfolder;
        [SerializeField] private string customSubfolder = string.Empty;

        private string[] subfolderOptions = Array.Empty<string>();
        private MeshyImportSummary _lastSummary;

        private static readonly string[] PipelineStageDisplayNames =
        {
            "Scale model import",
            "Extract textures",
            "Extract materials",
            "Delete legacy assets",
            "Rewire materials & fix normals",
            "Normalize and rename",
            "Move to destination"
        };

        private static readonly string[] PipelineStageKeys =
        {
            "scale-model-import",
            "extract-textures",
            "extract-materials",
            "delete-legacy-assets",
            "rewire-materials-and-normals",
            "normalize-and-rename",
            "move-to-destination"
        };

        [MenuItem(MenuPath, priority = -500)]
        private static void OpenWindow()
        {
            var window = GetWindow<MeshyImportPipelineTool>("Meshy Importer");
            window.minSize = new Vector2(560f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSubfolderOptions();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Meshy Import Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var previousRoot = destinationRoot;
            destinationRoot = (DestinationRootSelection)EditorGUILayout.EnumPopup("Destination Root", destinationRoot);
            if (destinationRoot != previousRoot)
                RefreshSubfolderOptions();

            if (subfolderOptions == null || subfolderOptions.Length == 0)
                RefreshSubfolderOptions();

            selectedSubfolderIndex = EditorGUILayout.Popup(
                "Destination Subfolder",
                Mathf.Clamp(selectedSubfolderIndex, 0, Mathf.Max(0, subfolderOptions.Length - 1)),
                subfolderOptions);

            useCustomSubfolder = EditorGUILayout.ToggleLeft("Use custom destination subfolder", useCustomSubfolder);
            if (useCustomSubfolder)
                customSubfolder = EditorGUILayout.TextField("Custom Subfolder", customSubfolder ?? string.Empty);

            var destinationRootPath = GetDestinationRootPath(destinationRoot);
            var selectedSubfolder = ResolveSelectedSubfolder();
            var destinationParent = MeshyImportPipelineRunner.BuildDestinationParentPath(destinationRootPath, selectedSubfolder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Source Root", MeshyImportPipelineRunner.ImportRoot);
            EditorGUILayout.LabelField("Resolved Destination", destinationParent);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Subfolder List", GUILayout.Height(28f)))
                    RefreshSubfolderOptions();

                if (GUILayout.Button("Run Meshy Import Pipeline", GUILayout.Height(28f)))
                    RunPipeline(destinationRootPath, selectedSubfolder);
            }

            if (_lastSummary != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Pipeline Stages", EditorStyles.boldLabel);
                DrawPipelineStages();
            }
        }

        private void DrawPipelineStages()
        {
            if (_lastSummary == null) return;

            for (var i = 0; i < PipelineStageDisplayNames.Length; i++)
            {
                var stageName = PipelineStageDisplayNames[i];
                var stageKey = PipelineStageKeys[i];
                var hasFailures = _lastSummary.StageFailures.TryGetValue(stageKey, out var errors) && errors.Count > 0;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                var icon = hasFailures ? "\u2718" : "\u2714";
                var iconColor = hasFailures ? Color.red : Color.green;

                var originalColor = GUI.color;
                GUI.color = iconColor;
                EditorGUILayout.LabelField(icon, GUILayout.Width(20f));
                GUI.color = originalColor;

                EditorGUILayout.LabelField(stageName, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                if (hasFailures)
                {
                    EditorGUI.indentLevel++;
                    for (var j = 0; j < errors.Count; j++)
                    {
                        var errorStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                        {
                            normal = { textColor = new Color(0.9f, 0.15f, 0.15f) }
                        };
                        EditorGUILayout.LabelField(errors[j], errorStyle);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();

                if (i < PipelineStageDisplayNames.Length - 1)
                    EditorGUILayout.Space(2f);
            }
        }

        private void RunPipeline(string destinationRootPath, string destinationSubfolder)
        {
            if (!EditorUtility.DisplayDialog(
                    "Run Meshy Import Pipeline",
                    "Process all folders under " + MeshyImportPipelineRunner.ImportRoot + " with the configured pipeline?",
                    "Run",
                    "Cancel"))
                return;

            var summary = MeshyImportPipelineRunner.RunImport(destinationRootPath, destinationSubfolder);
            _lastSummary = summary;

            Debug.Log("[MeshyImportPipelineTool] " + summary.ToSummaryText());
            EditorUtility.DisplayDialog("Meshy Import Pipeline", summary.ToSummaryText(), "OK");

            RefreshSubfolderOptions();
        }

        private void RefreshSubfolderOptions()
        {
            var rootPath = GetDestinationRootPath(destinationRoot);
            subfolderOptions = CollectSubfolderOptions(rootPath);
            selectedSubfolderIndex = Mathf.Clamp(selectedSubfolderIndex, 0, Mathf.Max(0, subfolderOptions.Length - 1));
        }

        private string ResolveSelectedSubfolder()
        {
            if (useCustomSubfolder)
                return NormalizeRelativeSubfolder(customSubfolder);

            if (subfolderOptions == null || subfolderOptions.Length == 0)
                return string.Empty;

            var index = Mathf.Clamp(selectedSubfolderIndex, 0, subfolderOptions.Length - 1);
            var option = subfolderOptions[index];
            return string.Equals(option, "<root>", StringComparison.OrdinalIgnoreCase) ? string.Empty : option;
        }

        private static string GetDestinationRootPath(DestinationRootSelection selection)
        {
            return selection == DestinationRootSelection.Weapons
                ? MeshyImportPipelineRunner.WeaponsModelRoot
                : MeshyImportPipelineRunner.ClothingModelRoot;
        }

        private static string[] CollectSubfolderOptions(string rootPath)
        {
            var options = new List<string> { "<root>" };
            if (!AssetDatabase.IsValidFolder(rootPath))
                return options.ToArray();

            CollectSubfoldersRecursive(rootPath, rootPath, options);
            return options.ToArray();
        }

        private static void CollectSubfoldersRecursive(string rootPath, string current, IList<string> options)
        {
            var subfolders = AssetDatabase.GetSubFolders(current);
            if (subfolders == null || subfolders.Length == 0)
                return;

            Array.Sort(subfolders, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < subfolders.Length; i++)
            {
                var child = subfolders[i]?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(child))
                    continue;

                var relative = child[rootPath.Length..].TrimStart('/');
                if (!string.IsNullOrWhiteSpace(relative))
                    options.Add(relative);

                CollectSubfoldersRecursive(rootPath, child, options);
            }
        }

        private static string NormalizeRelativeSubfolder(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var normalized = value.Replace('\\', '/').Trim();
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return normalized.Trim('/');
        }
    }

    internal static partial class MeshyImportPipelineRunner
    {
        internal const string ImportRoot = "Assets/MeshyImports";
        internal const string WeaponsModelRoot = "Assets/Systems/Weapons";
        internal const string ClothingModelRoot = "Assets/Systems/Clothing";

        internal static MeshyImportSummary RunImport(string destinationRoot, string destinationSubfolder)
        {
            var summary = new MeshyImportSummary
            {
                DestinationRoot = MeshyAssetPathUtility.NormalizePath(destinationRoot),
                DestinationSubfolder = destinationSubfolder ?? string.Empty
            };

            if (!AssetDatabase.IsValidFolder(ImportRoot))
            {
                summary.Failures.Add("Source root was not found: " + ImportRoot);
                return summary;
            }

            if (!AssetDatabase.IsValidFolder(destinationRoot))
                MeshyAssetPathUtility.EnsureFolderHierarchy(destinationRoot);

            var destinationParent = BuildDestinationParentPath(destinationRoot, destinationSubfolder);
            MeshyAssetPathUtility.EnsureFolderHierarchy(destinationParent);
            summary.ResolvedDestination = destinationParent;

            var sourceFolders = AssetDatabase.GetSubFolders(ImportRoot);
            summary.SourceFoldersScanned = sourceFolders?.Length ?? 0;

            if (sourceFolders == null || sourceFolders.Length == 0)
                return summary;

            try
            {
                for (var i = 0; i < sourceFolders.Length; i++)
                {
                    var sourceFolder = sourceFolders[i]?.Replace('\\', '/');
                    if (string.IsNullOrWhiteSpace(sourceFolder))
                        continue;

                    EditorUtility.DisplayProgressBar(
                        "Meshy Import Pipeline",
                        "Processing " + sourceFolder + " (" + (i + 1) + "/" + sourceFolders.Length + ")",
                        (i + 1f) / sourceFolders.Length);

                    ProcessSingleImportFolder(sourceFolder, destinationParent, summary);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            CommitAssetDatabaseCheckpoint();
            return summary;
        }

        internal static string BuildDestinationParentPath(string destinationRoot, string destinationSubfolder)
        {
            var normalizedRoot = MeshyAssetPathUtility.NormalizePath(destinationRoot);
            if (string.IsNullOrWhiteSpace(destinationSubfolder))
                return normalizedRoot;

            var relative = destinationSubfolder.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(relative))
                return normalizedRoot;

            if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                if (relative.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                    return relative;

                return normalizedRoot;
            }

            return normalizedRoot.TrimEnd('/') + "/" + relative;
        }
    }
}
#endif
