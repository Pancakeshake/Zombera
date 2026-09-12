#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace Zombera.Editor.Organizing
{
    /// <summary>
    /// Persists user preferences (excluded folders, last-used settings)
    /// across Unity sessions via <c>EditorPrefs</c>.
    /// </summary>
    public static class AssetAuditorConfig
    {
        private const string ExcludedFoldersKey   = "AssetAuditor.ExcludedFolders";
        private const string SearchFolderKey      = "AssetAuditor.SearchFolder";
        private const string SelectedCategoryKey  = "AssetAuditor.SelectedCategory";
        private const string SelectedAssetTypeKey = "AssetAuditor.SelectedAssetType";
        private const string Separator            = "|";

        private static readonly string[] FallbackExcluded = { "Assets/03_ThirdParty/" };

        // ── Excluded folders ────────────────────────────────────────────

        public static string[] GetExcludedFolders()
        {
            var saved = EditorPrefs.GetString(ExcludedFoldersKey, string.Empty);
            if (string.IsNullOrEmpty(saved))
                return (string[])FallbackExcluded.Clone();

            return saved.Split(Separator[0]);
        }

        public static void SetExcludedFolders(IEnumerable<string> folders)
        {
            EditorPrefs.SetString(ExcludedFoldersKey, string.Join(Separator, folders));
        }

        public static void AddExcludedFolder(string folder)
        {
            var list = new List<string>(GetExcludedFolders());
            if (list.Contains(folder))
                return;

            list.Add(folder);
            SetExcludedFolders(list);
        }

        public static void RemoveExcludedFolder(string folder)
        {
            var list = new List<string>(GetExcludedFolders());
            list.Remove(folder);
            SetExcludedFolders(list);
        }

        // ── Search folder ───────────────────────────────────────────────

        public static string GetSearchFolder()
        {
            return EditorPrefs.GetString(SearchFolderKey, "Assets/");
        }

        public static void SetSearchFolder(string folder)
        {
            EditorPrefs.SetString(SearchFolderKey, folder);
        }

        // ── Category ────────────────────────────────────────────────────

        public static string GetSelectedCategory()
        {
            return EditorPrefs.GetString(SelectedCategoryKey, "Fences");
        }

        public static void SetSelectedCategory(string category)
        {
            EditorPrefs.SetString(SelectedCategoryKey, category);
        }

        // ── Asset type ──────────────────────────────────────────────────

        public static AssetType GetSelectedAssetType()
        {
            return (AssetType)EditorPrefs.GetInt(SelectedAssetTypeKey, (int)AssetType.Prefabs);
        }

        public static void SetSelectedAssetType(AssetType type)
        {
            EditorPrefs.SetInt(SelectedAssetTypeKey, (int)type);
        }
    }
}
#endif
