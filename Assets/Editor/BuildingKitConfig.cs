#if UNITY_EDITOR
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     ScriptableObject holding all building kit paths and prefab references
    ///     used by the modular building generator. Every kit part can be overridden
    ///     via a direct prefab reference; when left empty, the generator falls back
    ///     to the hardcoded filename convention inside the kit folder.
    ///
    ///     Create via Assets → Create → Zombera → Building → Building Kit Config.
    ///     Save the asset under <c>Assets/02_Shared/ScriptableObjects/Buildings/</c>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/Building/Building Kit Config",
        fileName = "BuildingKitConfig",
        order = 102)]
    public sealed class BuildingKitConfig : ScriptableObject
    {
        [Header("Paths")]
        [Tooltip("Folder containing kit prefabs. Prefab references below override filename lookups.")]
        public string kitFolder = ModularSingleLevelHouseGeneratorTool.DefaultKitFolder;

        [Tooltip("Output folder where generated prefabs are saved.")]
        public string outputFolder = ModularSingleLevelHouseGeneratorTool.DefaultOutputFolder;

        [Header("Core Kit Parts")]
        [Tooltip("Foundation slab prefab. Empty = Foundation.prefab in kit folder.")]
        public GameObject foundationPrefab;

        [Tooltip("Floor slab prefab. Empty = Floor.prefab in kit folder.")]
        public GameObject floorPrefab;

        [Tooltip("Wall piece prefab. Empty = Wall.prefab in kit folder.")]
        public GameObject wallPrefab;

        [Tooltip("Doorway piece prefab (used when no door override is set). Empty = Doorway.prefab in kit folder.")]
        public GameObject doorwayPrefab;

        [Tooltip("Window piece prefab. Empty = Window.prefab in kit folder.")]
        public GameObject windowPrefab;

        [Header("Shop Front Parts")]
        [Tooltip("Shop-glass storefront piece (full window, no end caps). Empty = ShopGlass_Full.prefab in kit folder.")]
        public GameObject shopGlassFullPrefab;

        [Tooltip("Shop-glass storefront piece with a left end cap. Empty = ShopGlass_CapLeft.prefab in kit folder.")]
        public GameObject shopGlassCapLeftPrefab;

        [Tooltip("Shop-glass storefront piece with a right end cap. Empty = ShopGlass_CapRight.prefab in kit folder.")]
        public GameObject shopGlassCapRightPrefab;

        [Tooltip("Shop-glass storefront piece with end caps on both sides. Empty = ShopGlass_CapBoth.prefab in kit folder.")]
        public GameObject shopGlassCapBothPrefab;

        [Header("Roof Parts")]
        [Tooltip("Roof ridge prefab. Empty = Roof_Ridge.prefab in kit folder.")]
        public GameObject roofRidgePrefab;

        [Tooltip("Roof panel prefab. Empty = Roof_Panel.prefab in kit folder.")]
        public GameObject roofPanelPrefab;

        [Tooltip("Roof gable prefab. Empty = Roof_Gable.prefab in kit folder.")]
        public GameObject roofGablePrefab;

        [Header("Window Variants")]
        [Tooltip("Closed window prefab. Empty = Window_Modular_Closed.prefab in kit folder.")]
        public GameObject windowClosedPrefab;

        [Tooltip("Window moulding prefab. Empty = Window_Moulding.prefab in kit folder.")]
        public GameObject windowMouldingPrefab;

        [Header("Guttering")]
        [Tooltip("Gutter 3m prefab. Empty = Gutter_3m.prefab in kit folder.")]
        public GameObject gutter3mPrefab;

        [Tooltip("Gutter brackets prefab. Empty = Gutter_Brackets_Fused.prefab in kit folder.")]
        public GameObject gutterBracketsPrefab;

        [Tooltip("Downpipe 3m prefab. Empty = Downpipe_3m.prefab in kit folder.")]
        public GameObject downpipePrefab;

        [Header("Overrides")]
        [Tooltip("Stair prefab for multi-story buildings. Empty = use Building_Stair.prefab from kit folder.")]
        public GameObject stairPrefab;

        [Tooltip("Roof prefab. Empty = use Roof.prefab from kit folder (or Building_Floor as flat cap).")]
        public GameObject roofPrefab;

        [Header("Exterior Props")]
        [Tooltip("Folder to pick a fusebox from. One is placed per house on an end wall. Empty = none.")]
        public string fuseBoxSourceFolder = "Assets/02_Shared/Prefabs/Props/FuseBoxes";

        [Header("Door Overrides")]
        [Tooltip("Folder to pick exterior door prefabs from. Scanned recursively. Empty = use kit Doorway.prefab.")]
        public string exteriorDoorSourceFolder = "Assets/03_ThirdParty/Free Wood Door Pack/Prefab/Wood";

        [Tooltip("Folder to pick interior door prefabs from. Empty = use exterior door source or kit Doorway.prefab.")]
        public string interiorDoorSourceFolder = "Assets/03_ThirdParty/Free Wood Door Pack/Prefab/Wood";

        [Tooltip("Scale adjustment for override door prefabs. FreeWoodDoorPack: (0.904, 0.88, 1.0).")]
        public Vector3 doorScale = new(0.904f, 0.88f, 1f);

        // ── Resolve helpers ──────────────────────────────────────────

        /// <summary>Returns the asset path for a prefab, or null.</summary>
        internal static string PrefabPath(GameObject prefab) =>
#if UNITY_EDITOR
            prefab != null ? UnityEditor.AssetDatabase.GetAssetPath(prefab) : null;
#else
            null;
#endif

        /// <summary>
        ///     Resolves a kit part path: prefab reference first, then hardcoded filename in kit folder.
        /// </summary>
        internal string ResolveKitPartPath(string kitFolder, GameObject prefabRef, string hardcodedFileName)
        {
            if (prefabRef != null)
                return PrefabPath(prefabRef);

            return ModularSingleLevelHouseGeneratorTool.CombinePrefabPath(kitFolder, hardcodedFileName);
        }
    }
}
#endif
