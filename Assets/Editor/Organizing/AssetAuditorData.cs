#if UNITY_EDITOR
namespace Zombera.Editor.Organizing
{
    /// <summary>
    /// Asset types the auditor can target.
    /// </summary>
    public enum AssetType
    {
        Prefabs,
        Materials,
        Meshes,
        Proxies,
        AllTypes
    }

    /// <summary>
    /// Classification of a single asset during a scan.
    /// </summary>
    public enum ScanStatus
    {
        /// <summary>Asset is already in the destination folder.</summary>
        AlreadyCorrect,

        /// <summary>Asset is in the wrong folder and can be moved.</summary>
        NeedsMoving,

        /// <summary>Source file or meta missing on disk.</summary>
        Missing,

        /// <summary>An asset with the same name already exists at the destination.</summary>
        DuplicateName,

        /// <summary>Destination folder is not under Assets/.</summary>
        InvalidDestination
    }

    /// <summary>
    /// Immutable scan result for a single asset.
    /// </summary>
    public readonly struct ScanResult
    {
        public readonly string AssetPath;
        public readonly string AssetName;
        public readonly string CurrentFolder;
        public readonly string DestinationFolder;
        public readonly ScanStatus Status;

        public ScanResult(
            string assetPath,
            string assetName,
            string currentFolder,
            string destinationFolder,
            ScanStatus status)
        {
            AssetPath = assetPath;
            AssetName = assetName;
            CurrentFolder = currentFolder;
            DestinationFolder = destinationFolder;
            Status = status;
        }

        public string DestinationPath => $"{DestinationFolder}/{AssetName}{System.IO.Path.GetExtension(AssetPath)}";
    }

    /// <summary>
    /// Lightweight category descriptor used by the registry.
    /// </summary>
    public readonly struct CategoryDefinition
    {
        public readonly string Name;
        public readonly string FolderName;

        public CategoryDefinition(string name, string folderName)
        {
            Name = name;
            FolderName = folderName;
        }
    }

    /// <summary>
    /// Convenience helpers for <see cref="AssetType"/>.
    /// </summary>
    public static class AssetTypeExtensions
    {
        /// <summary>Returns the subfolder name under Assets/02_Shared/.</summary>
        public static string ToSubfolder(this AssetType type)
        {
            return type switch
            {
                AssetType.Prefabs => "Prefabs",
                AssetType.Materials => "Materials",
                AssetType.Meshes => "Meshes",
                AssetType.Proxies => "Proxies",
                AssetType.AllTypes => "",
                _ => "Prefabs"
            };
        }

        /// <summary>Returns the AssetDatabase.FindAssets filter string.</summary>
        public static string ToSearchFilter(this AssetType type)
        {
            return type switch
            {
                AssetType.Prefabs => "t:Prefab",
                AssetType.Materials => "t:Material",
                AssetType.Meshes => "t:Mesh t:Model",
                AssetType.Proxies => "t:Prefab",
                AssetType.AllTypes => "t:Prefab t:Material t:Mesh t:Model",
                _ => "t:Prefab"
            };
        }

        /// <summary>True when moving is allowed for this asset type.</summary>
        public static bool CanMove(this AssetType type)
        {
            return true;
        }
    }
}
#endif
