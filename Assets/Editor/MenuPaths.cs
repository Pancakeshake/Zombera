#if UNITY_EDITOR
// ═══════════════════════════════════════════════════════════════════════════
// Shared menu-path constants for all Zombera Editor tools.
//
// Using these constants instead of raw strings in [MenuItem] attributes:
//   - Prevents typos across files
//   - Makes future renames a single-point change
//   - Documents the menu hierarchy in one place
//
// Usage:
//   [MenuItem(MenuPaths.Assets + "Asset Auditor & Organizer", priority = -500)]
// ═══════════════════════════════════════════════════════════════════════════
namespace Zombera.Editor
{
    public static class MenuPaths
    {
        public const string Root      = "Tools/";

        public const string Assets    = Root + "Assets/";
        public const string World     = Root + "World/";
        public const string Items     = Root + "Items/";
        public const string Scenes    = Root + "Scenes/";
        public const string Build     = Root + "Build/";
        public const string AI        = Root + "AI/";
        public const string Utilities = Root + "Utilities/";
        public const string Settings  = Root + "Settings/";

        // ── Assets sub-paths ────────────────────────────────────────────

        public const string AssetsMeshyImporter = Assets + "Meshy Importer/";

        // ── World sub-paths ─────────────────────────────────────────────

        public const string WorldRoads      = World + "Roads/";
        public const string WorldBuildings  = World + "Buildings/";
        public const string WorldTerrain    = World + "Terrain/";
        public const string WorldZombies    = World + "Zombies/";
        public const string WorldBuildingIcons = World + "Building Icons/";
        public const string WorldCity          = World + "City/";
        public const string WorldFences        = World + "Fences/";
        public const string WorldSignage       = World + "Signage/";

        // ── Build sub-paths ─────────────────────────────────────────────

        public const string BuildModKits = Build + "Mod Kits/";
    }
}
#endif
