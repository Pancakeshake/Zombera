#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor.Organizing
{
    /// <summary>
    /// Editor window for auditing and organizing project assets.
    ///
    /// Menu: <c>Tools → Organizing → Asset Auditor &amp; Organizer…</c>
    /// </summary>
    public class AssetAuditorWindow : EditorWindow
    {
        // ── state ───────────────────────────────────────────────────────

        private AssetType _assetType;
        private int _categoryIndex;
        private string _searchFolder = "";
        private string _destinationFolder = "";
        private List<string> _excludedFolders = new();
        private List<ScanResult> _scanResults = new();
        private Vector2 _resultsScrollPos;
        private readonly HashSet<int> _selectedIndices = new();

        // ── column widths ───────────────────────────────────────────────

        private const float SelectColWidth  = 22f;
        private const float NameColWidth    = 180f;
        private const float FolderColWidth  = 340f;
        private const float StatusColWidth  = 110f;

        // ── menu ────────────────────────────────────────────────────────

        [MenuItem(MenuPaths.Assets + "Asset Auditor & Organizer", priority = -500)]
        public static void ShowWindow()
        {
            var window = GetWindow<AssetAuditorWindow>("Asset Auditor & Organizer");
            window.minSize = new Vector2(820, 500);
            window.Show();
        }

        // ── lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            _assetType     = AssetAuditorConfig.GetSelectedAssetType();
            _categoryIndex = CategoryRegistry.IndexOf(AssetAuditorConfig.GetSelectedCategory());
            if (_categoryIndex < 0)
                _categoryIndex = 0;

            _searchFolder      = AssetAuditorConfig.GetSearchFolder();
            _excludedFolders   = new List<string>(AssetAuditorConfig.GetExcludedFolders());
            _destinationFolder = CategoryRegistry.BuildDestinationPath(
                _assetType, CategoryRegistry.Names[_categoryIndex]);
        }

        // ── GUI ─────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(4);
            DrawControls();
            EditorGUILayout.Space(4);
            DrawButtons();
            EditorGUILayout.Space(6);
            DrawResults();
        }

        // ── header ──────────────────────────────────────────────────────

        private static void DrawHeader()
        {
            EditorGUILayout.LabelField("Asset Auditor & Organizer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Scan, review, and move project assets into their correct category folders under Assets/02_Shared/.",
                EditorStyles.wordWrappedMiniLabel);
        }

        // ── controls ────────────────────────────────────────────────────

        private void DrawControls()
        {
            // --- Asset Type ---
            var newType = (AssetType)EditorGUILayout.EnumPopup("Asset Type", _assetType);
            if (newType != _assetType)
            {
                _assetType = newType;
                AssetAuditorConfig.SetSelectedAssetType(_assetType);
                UpdateDestination();
            }

            // --- Category ---
            var newCatIdx = EditorGUILayout.Popup("Category", _categoryIndex, CategoryRegistry.Names);
            if (newCatIdx != _categoryIndex)
            {
                _categoryIndex = newCatIdx;
                AssetAuditorConfig.SetSelectedCategory(CategoryRegistry.Names[_categoryIndex]);
                UpdateDestination();
            }

            // --- Search Folder ---
            EditorGUILayout.BeginHorizontal();
            _searchFolder = EditorGUILayout.TextField("Search Folder", _searchFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var abs = EditorUtility.OpenFolderPanel("Select Search Folder", "Assets/", "");
                if (!string.IsNullOrEmpty(abs))
                {
                    _searchFolder = "Assets" + abs.Replace(Application.dataPath, "").Replace('\\', '/');
                    AssetAuditorConfig.SetSearchFolder(_searchFolder);
                }
            }
            EditorGUILayout.EndHorizontal();

            // --- Destination (read-only) ---
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Destination", _destinationFolder);
            EditorGUI.EndDisabledGroup();

            // --- Excluded Folders ---
            EditorGUILayout.LabelField("Excluded Folders", EditorStyles.boldLabel);
            for (var i = 0; i < _excludedFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(_excludedFolders[i]);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    AssetAuditorConfig.RemoveExcludedFolder(_excludedFolders[i]);
                    _excludedFolders.RemoveAt(i);
                    break; // avoid iterating a modified collection
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Folder", GUILayout.Width(100)))
            {
                var abs = EditorUtility.OpenFolderPanel("Select Folder to Exclude", "Assets/", "");
                if (string.IsNullOrEmpty(abs))
                    return;

                var rel = "Assets" + abs.Replace(Application.dataPath, "").Replace('\\', '/');
                if (rel == "Assets")
                    return; // safety — never exclude the root

                AssetAuditorConfig.AddExcludedFolder(rel);
                _excludedFolders = new List<string>(AssetAuditorConfig.GetExcludedFolders());
            }
        }

        // ── buttons ─────────────────────────────────────────────────────

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Scan", GUILayout.Height(28), GUILayout.Width(80)))
                RunScan();

            var canMove = _assetType.CanMove();

            GUI.enabled = canMove && _selectedIndices.Count > 0;
            if (GUILayout.Button("Move Selected", GUILayout.Height(28)))
                MoveSelected();

            GUI.enabled = canMove && _scanResults.Any(r => r.Status == ScanStatus.NeedsMoving);
            if (GUILayout.Button("Move All", GUILayout.Height(28)))
                MoveAll();

            GUI.enabled = _scanResults.Any(r => r.Status == ScanStatus.DuplicateName);
            if (GUILayout.Button("Delete Duplicates", GUILayout.Height(28)))
                DeleteDuplicates();

            GUI.enabled = true;
            if (GUILayout.Button("Refresh", GUILayout.Height(28)))
                RunScan();

            EditorGUILayout.EndHorizontal();
        }

        // ── results table ───────────────────────────────────────────────

        private void DrawResults()
        {
            if (_scanResults.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Scan' to search for assets.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                $"Results — {_scanResults.Count} total  |  " +
                $"{_scanResults.Count(r => r.Status == ScanStatus.NeedsMoving)} need moving  |  " +
                $"{_scanResults.Count(r => r.Status == ScanStatus.AlreadyCorrect)} correct",
                EditorStyles.boldLabel);

            // column header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(SelectColWidth + 4);
            EditorGUILayout.LabelField("Asset Name",   EditorStyles.boldLabel, GUILayout.Width(NameColWidth));
            EditorGUILayout.LabelField("Current Folder",EditorStyles.boldLabel, GUILayout.Width(FolderColWidth));
            EditorGUILayout.LabelField("Status",        EditorStyles.boldLabel, GUILayout.Width(StatusColWidth));
            EditorGUILayout.EndHorizontal();

            _resultsScrollPos = EditorGUILayout.BeginScrollView(
                _resultsScrollPos, GUILayout.ExpandHeight(true));

            for (var i = 0; i < _scanResults.Count; i++)
                DrawResultRow(i);

            EditorGUILayout.EndScrollView();
        }

        private void DrawResultRow(int index)
        {
            var r    = _scanResults[index];
            var sel  = _selectedIndices.Contains(index);
            var canSelect = r.Status == ScanStatus.NeedsMoving && _assetType.CanMove();

            // row background
            var rowRect = EditorGUILayout.BeginHorizontal();
            var bg = sel
                ? new Color(0.25f, 0.45f, 0.75f, 0.40f)
                : index % 2 == 0
                    ? new Color(0.12f, 0.12f, 0.12f, 0.08f)
                    : Color.clear;

            if (bg != Color.clear)
            {
                EditorGUI.DrawRect(rowRect, bg);
            }

            // selection toggle
            if (canSelect)
            {
                var toggled = EditorGUILayout.Toggle(sel, GUILayout.Width(SelectColWidth));
                if (toggled != sel)
                {
                    if (toggled) _selectedIndices.Add(index);
                    else         _selectedIndices.Remove(index);
                }
            }
            else
            {
                GUILayout.Space(SelectColWidth + 2);
            }

            EditorGUILayout.LabelField(r.AssetName,     GUILayout.Width(NameColWidth));
            EditorGUILayout.LabelField(r.CurrentFolder, GUILayout.Width(FolderColWidth));

            var prevColor = GUI.color;
            GUI.color = GetStatusColor(r.Status);
            EditorGUILayout.LabelField(r.Status.ToString(), GUILayout.Width(StatusColWidth));
            GUI.color = prevColor;

            EditorGUILayout.EndHorizontal();
        }

        private static Color GetStatusColor(ScanStatus status)
        {
            return status switch
            {
                ScanStatus.AlreadyCorrect    => new Color(0.35f, 0.85f, 0.35f),
                ScanStatus.NeedsMoving       => new Color(1.0f,  0.85f, 0.2f),
                ScanStatus.Missing           => new Color(1.0f,  0.3f,  0.3f),
                ScanStatus.DuplicateName     => new Color(1.0f,  0.55f, 0.0f),
                ScanStatus.InvalidDestination => new Color(1.0f, 0.3f,  0.3f),
                _ => Color.white
            };
        }

        // ── actions ─────────────────────────────────────────────────────

        private void UpdateDestination()
        {
            if (_assetType == AssetType.AllTypes)
            {
                var cat = CategoryRegistry.Names[_categoryIndex];
                _destinationFolder = $"Assets/02_Shared/*/{CategoryRegistry.GetFolderName(cat)}";
            }
            else
            {
                _destinationFolder = CategoryRegistry.BuildDestinationPath(
                    _assetType, CategoryRegistry.Names[_categoryIndex]);
            }
        }

        private void RunScan()
        {
            _selectedIndices.Clear();

            try
            {
                _scanResults = AssetAuditorScanner.Scan(
                    _assetType, _searchFolder, _destinationFolder,
                    _excludedFolders.ToArray(),
                    CategoryRegistry.Names[_categoryIndex]);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Asset Auditor] Scan error: {ex}");
                EditorUtility.DisplayDialog("Scan Error", ex.Message, "OK");
            }

            Repaint();
        }

        private void MoveSelected()
        {
            PerformMove(_scanResults
                .Where((r, i) => _selectedIndices.Contains(i))
                .ToList());
        }

        private void MoveAll()
        {
            PerformMove(_scanResults
                .Where(r => r.Status == ScanStatus.NeedsMoving)
                .ToList());
        }

        private void PerformMove(List<ScanResult> assets)
        {
            if (assets.Count == 0)
                return;

            var label = _assetType == AssetType.AllTypes
                ? assets.Count == 1
                    ? $"Move '{assets[0].AssetName}' to '{assets[0].DestinationFolder}'?"
                    : $"Move {assets.Count} assets to their type-specific folders under Assets/02_Shared/?"
                : assets.Count == 1
                    ? $"Move '{assets[0].AssetName}' to '{_destinationFolder}'?"
                    : $"Move {assets.Count} assets to '{_destinationFolder}'?";

            if (!EditorUtility.DisplayDialog("Move Assets", label, "Move", "Cancel"))
                return;

            if (_assetType == AssetType.AllTypes)
            {
                // Ensure each unique destination folder exists
                foreach (var dest in assets.Select(a => a.DestinationFolder).Distinct())
                    EnsureDirectoryExists(dest);
            }
            else
            {
                EnsureDirectoryExists(_destinationFolder);
            }

            var moved  = 0;
            var errors = 0;

            for (var i = 0; i < assets.Count; i++)
            {
                var a = assets[i];

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Asset Auditor — Moving",
                    a.AssetName,
                    (float)i / assets.Count))
                {
                    break;
                }

                var error = AssetDatabase.MoveAsset(a.AssetPath, a.DestinationPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[Asset Auditor] Move failed '{a.AssetPath}': {error}");
                    errors++;
                }
                else
                {
                    moved++;
                    Debug.Log($"[Asset Auditor] Moved → {a.DestinationPath}");
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _selectedIndices.Clear();

            EditorUtility.DisplayDialog("Move Complete",
                $"Moved: {moved}\nErrors: {errors}", "OK");

            RunScan();
        }

        private void DeleteDuplicates()
        {
            var duplicates = _scanResults
                .Where(r => r.Status == ScanStatus.DuplicateName)
                .ToList();

            if (duplicates.Count == 0)
                return;

            if (!EditorUtility.DisplayDialog("Delete Duplicates",
                $"Delete {duplicates.Count} duplicate asset(s)?\n\nThis removes the copies found outside the destination folder.",
                "Delete", "Cancel"))
                return;

            var deleted = 0;
            var errors  = 0;

            for (var i = 0; i < duplicates.Count; i++)
            {
                var d = duplicates[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Asset Auditor — Deleting", d.AssetName, (float)i / duplicates.Count))
                    break;

                if (AssetDatabase.DeleteAsset(d.AssetPath))
                {
                    deleted++;
                    Debug.Log($"[Asset Auditor] Deleted duplicate: {d.AssetPath}");
                }
                else
                {
                    errors++;
                    Debug.LogError($"[Asset Auditor] Failed to delete: {d.AssetPath}");
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Delete Complete",
                $"Deleted: {deleted}\nErrors: {errors}", "OK");

            RunScan();
        }

        private static void EnsureDirectoryExists(string folder)
        {
            if (Directory.Exists(folder))
                return;

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
#endif
