#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;

namespace Zombera.Editor.Organizing
{
    /// <summary>
    /// Central registry of supported categories.
    /// Add new entries here; the UI and path builder pick them up automatically.
    /// </summary>
    public static class CategoryRegistry
    {
        private static readonly CategoryDefinition[] BuiltIn =
        {
            new("Fences",    "Fences"),
            new("Buildings", "Buildings"),
            new("Decals",    "Decals"),
            new("Props",     "Props"),
            new("Vegetation","Vegetation"),
            new("Roads",     "Roads"),
            new("Signs",     "Signage"),
            new("Rocks",     "Rocks"),
            new("Trees",     "Trees"),
            new("Furniture", "Furniture"),
            new("NPCs",      "NPCs"),
            new("Weapons",   "Weapons"),
            new("Misc",      "Misc"),
        };

        /// <summary>Ordered list of all category definitions.</summary>
        public static IReadOnlyList<CategoryDefinition> All => BuiltIn;

        /// <summary>Populated once so dropdowns don't allocate every frame.</summary>
        public static readonly string[] Names = BuiltIn.Select(c => c.Name).ToArray();

        /// <summary>
        /// Returns the filesystem folder name for a category.
        /// Falls back to the display name when the definition is not found.
        /// </summary>
        public static string GetFolderName(string categoryName)
        {
            var def = BuiltIn.FirstOrDefault(c => c.Name == categoryName);
            return def.Name != null ? def.FolderName : categoryName;
        }

        /// <summary>
        /// Builds the full destination path for a given asset type + category.
        /// Example: Prefabs + Fences → Assets/02_Shared/Prefabs/Fences
        /// </summary>
        public static string BuildDestinationPath(AssetType assetType, string categoryName)
        {
            var folderName = GetFolderName(categoryName);
            return $"Assets/02_Shared/{assetType.ToSubfolder()}/{folderName}";
        }

        /// <summary>Index of a category name or -1 when not found.</summary>
        public static int IndexOf(string categoryName)
        {
            return System.Array.IndexOf(Names, categoryName);
        }
    }
}
#endif
