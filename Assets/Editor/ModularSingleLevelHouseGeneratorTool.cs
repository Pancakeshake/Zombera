#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Zombera.Data;
using Zombera.World.City;

namespace Zombera.Editor
{
    /// <summary>
    /// Contract-stable host for modular house generation. Implementation is split across partial files.
    /// </summary>
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        private const string BuildingPrefabsRoot = "Assets/02_Shared/Prefabs/Building";

        public const string DefaultKitFolder = BuildingPrefabsRoot + "/Building_Modular_Parts";
        public const string DefaultOutputFolder = BuildingPrefabsRoot + "/Buildings_Modular_Complete";
        public const string DefaultProxyOutputFolder = "Assets/02_Shared/Proxies/Buildings_Complete";
        public const string DefaultExteriorStairsPrefabPath = BuildingPrefabsRoot + "/Building_Modular_Parts/Front_Stairs.prefab";

        public const int MaxFloors = 20;
        public const int MaxFootprintCells = 32;
        public const int MinFootprintCells = 2;

        private const float CellSize = 3f;
        private const float FoundationTopY = 0.1f;
        private const float FloorSurfaceY = FoundationTopY;

        /// <summary>Top of <c>Building_Floor</c> slab above prefab root (kit mesh ~0–0.1 m local).</summary>
        private const float FloorSlabTopAboveRoot = 0.1f;

        /// <summary>Must match full wall piece height; stacks upper floors on kit snapping interval.</summary>
        private const float WallHeight = 3f;

        /// <summary>Wall piece pivot-to-inner-face offset; shared by walls and gables.</summary>
        private const float WallInset = 0.049f;

        private const string MenuGenerate =
            "Tools/Build/Mod Kits/Building Generator/Generate Modular Building (Prefab)";
        private const string MenuWindow =
            "Tools/Build/Mod Kits/Building Generator/Modular Building Generator...";

        [MenuItem(MenuGenerate, priority = -500)]
        private static void GenerateFromMenu()
        {
            Generate(new GeneratorSettings());
        }

        [MenuItem(MenuWindow, priority = -500)]
        private static void OpenWindow()
        {
            ModularSingleLevelHouseGeneratorWindow.ShowWindow();
        }

        public static string Generate(GeneratorSettings settings)
        {
            TouchSplitConstantsForAnalysis();

            if (!GeneratorSettingsValidator.TryNormalizeForGeneration(settings, out var normalizedSettings,
                    out var validationError))
                return FailGeneration(validationError);

            return GenerateNormalized(normalizedSettings);
        }

        private static void TouchSplitConstantsForAnalysis()
        {
            _ = CellSize;
            _ = FloorSurfaceY;
            _ = FloorSlabTopAboveRoot;
            _ = WallHeight;
            _ = DefaultProxyOutputFolder;
        }

        private static string FailGeneration(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = "Generation failed due to invalid configuration.";

            Debug.LogError($"[ModularSingleLevelHouseGeneratorTool] {message}");
            EditorUtility.DisplayDialog("Modular House Generator", message, "OK");
            return null;
        }
    }

    [Serializable]
    public sealed class GeneratorSettings
    {
        [Header("Kit & Output")]
        [Tooltip("Optional. When assigned, kit paths and override prefabs are read from this SO instead of the inline fields below.")]
        public BuildingKitConfig KitConfig;

        public string KitFolder = ModularSingleLevelHouseGeneratorTool.DefaultKitFolder;

        public string OutputFolder = ModularSingleLevelHouseGeneratorTool.DefaultOutputFolder;

        [Tooltip("Drag the stair prefab here for multi-story buildings. Leave empty to use Building_Stair.prefab from the kit folder.")]
        public GameObject StairPrefab;

        [Tooltip("Optional upper-floor slab prefab. Empty = reuse Building_Floor.prefab from the kit for levels 1+.")]
        public GameObject UpperFloorPrefab;

        [Tooltip("Drag the exterior stair prefab placed outside each ground-floor door. Empty = none.")]
        public GameObject ExteriorStairsPrefab;

        [Tooltip("Optional roof prefab. Leave empty to use Roof.prefab from the kit folder (or Building_Floor as flat cap).")]
        public GameObject RoofPrefab;

        [Header("Door Overrides")]
        [Tooltip("Drag a door prefab for perimeter (exterior) doors. Leave empty to use the kit's Doorway.prefab.")]
        public GameObject ExteriorDoorPrefab;

        [Tooltip("Drag a door prefab for interior room-to-room doors. Leave empty to use the kit's Doorway.prefab.")]
        public GameObject InteriorDoorPrefab;

        [Tooltip("Scale adjustment for override door prefabs. For FreeWoodDoorPack: (0.904, 0.88, 1.0).")]
        public Vector3 DoorScale = new(0.904f, 0.88f, 1f);

        // ── Legacy path fields (hidden, retained for serialization compatibility) ──

        [HideInInspector] public string StairPrefabFileName = "Building_Stair.prefab";
        [HideInInspector] public string UpperFloorPrefabFileName = string.Empty;
        [HideInInspector] public string ExteriorStairsPrefabPath = ModularSingleLevelHouseGeneratorTool.DefaultExteriorStairsPrefabPath;
        [HideInInspector] public string RoofPrefabPath = string.Empty;
        [HideInInspector] public string ExteriorDoorSourcePath = string.Empty;
        [HideInInspector] public string InteriorDoorSourcePath = string.Empty;

        // ── Building size ──

        /// <summary>When enabled, manual footprint/floor/room sliders override template defaults.</summary>
        public bool UseManualGeneration;

        /// <summary>Stories including ground; 1–<see cref="ModularSingleLevelHouseGeneratorTool.MaxFloors"/>. Each story adds one <c>Building_Wall</c> ring and one floor slab ring (vertical pitch matches wall height).</summary>
        public int FloorCount = 1;

        /// <summary>How many doorways to place on the ground floor perimeter (0..perimeter segments).</summary>
        public int GroundDoorCount = 1;

        /// <summary>Legacy fixed room count. Used only when both <see cref="MinRoomsPerFloor"/> and <see cref="MaxRoomsPerFloor"/> are zero.</summary>
        public int RoomCount;

        /// <summary>Minimum random room count generated on each floor. 0 = open plan on floors that roll zero.</summary>
        public int MinRoomsPerFloor;

        /// <summary>Maximum random room count generated on each floor.</summary>
        public int MaxRoomsPerFloor;

        public int MinCells = 2;
        public int MaxCells = 4;

        /// <summary>Footprint width in cells (3m each). When &gt; 0, fixes width; otherwise random in [<see cref="MinCells"/>, <see cref="MaxCells"/>].</summary>
        public int FixedWidthCells;

        /// <summary>Footprint depth in cells (3m each). When &gt; 0, fixes depth; otherwise random in [<see cref="MinCells"/>, <see cref="MaxCells"/>].</summary>
        public int FixedDepthCells;

        public bool UseFixedRandomSeed;
        public int RandomSeed = 12345;

        /// <summary>Chance (0–1) to swap a non-door wall for Building_Window when present in the kit.</summary>
        public float WindowChance = 0.25f;

        /// <summary>When enabled, upper floors shrink in footprint (width/depth cells) for a tapered skyscraper look.</summary>
        public bool SkyscraperMode;

        /// <summary>0-based floor index where footprint shrinking begins. Floors below this stay full size.</summary>
        public int SkyscraperShrinkStartFloor = 10;

        /// <summary>How many cells to remove per axis per additional floor above the shrink threshold.</summary>
        public int SkyscraperShrinkStep = 1;

        /// <summary>When enabled, the generator skips roof assembly, ceiling tiles, parapet walls, and guttering — leaving the top floor open.</summary>
        public bool SkipRoofAndCeiling;

        /// <summary>The zone / district type for prefab naming and skin-table lookup.</summary>
        public CityDistrictType BuildingCategoryOverride = CityDistrictType.Residential;

        /// <summary>
        ///     Optional reference to the Building Archetype Library SO. When assigned, the generator
        ///     uses lot-type–specific templates (room counts, adjacency rules, sizing) instead of the
        ///     legacy MinRoomsPerFloor / MaxRoomsPerFloor sliders.
        /// </summary>
        public BuildingArchetypeLibrary ArchetypeLibrary;

        /// <summary>Index into the ArchetypeLibrary template list (per current lot type).</summary>
        public int SelectedTemplateIndex;

        /// <summary>Per-room-type behavioural settings (open-plan eligibility, door blocking, adjacency).</summary>
        public RoomSettings RoomSettings;

        /// <summary>
        ///     Optional reference to the Building Skin Table SO. When assigned, the generator
        ///     automatically applies a zone-appropriate skin to the finished prefab after assembly.
        /// </summary>
        public BuildingSkinTable SkinTable;

        [Header("Proxy Output")]
        [Tooltip("When enabled, a game-ready proxy prefab is baked beside each generated building.")]
        public bool GenerateProxyPrefab = true;

        [Tooltip("Root folder for proxy prefabs. A district subfolder is created automatically.")]
        public string ProxyOutputFolder = ModularSingleLevelHouseGeneratorTool.DefaultProxyOutputFolder;
    }

    /// <summary>GM-Stairs pivot offsets — hardcoded to the tuned values for GM-Stairs.prefab.</summary>
    internal static class ExteriorStairsConstants
    {
        /// <summary>Metres outward from wall face.</summary>
        public const float OutwardOffset = 1.0f;

        /// <summary>Lateral shift along wall face (GM-Stairs pivot is 0.5m off-centre).</summary>
        public const float LateralOffset = -0.5f;

        /// <summary>World Y (GM-Stairs pivot sits 0.3m above ground).</summary>
        public const float YOffset = -0.3f;
    }

    public sealed class ModularSingleLevelHouseGeneratorWindow : EditorWindow
    {
        private GeneratorSettings _settings = new();
        private int _bulkGenerateCount = 1;

        // ── Scriptable Objects section ───────────────────────────────
        private enum DatabaseType
        {
            RoomSettings,
            BuildingSkins,
            ArchetypeLibrary,
            InteriorPropConfig,
            BuildingKitConfig,
            RoomFloorSkinConfig,
        }

        private DatabaseType _selectedDatabase = DatabaseType.RoomSettings;
        private bool _showScriptableObjects;

        // Cached asset references for inline editing
        private RoomSettings _dataRoomSettings;
        private BuildingSkinTable _dataBuildingSkins;
        private BuildingArchetypeLibrary _dataArchetypeLibrary;
        private InteriorPropConfig _dataInteriorPropConfig;
        private BuildingKitConfig _dataBuildingKitConfig;
        private Zombera.Data.RoomFloorSkinConfig _dataRoomFloorSkinConfig;

        private Vector2 _dataScroll;

        // Foldout state — kit and manual generation are collapsed by default
        private bool _showLotType = true;
        private bool _showKit;
        private bool _showManualGeneration;
        private bool _showOverrides;
        private bool _showRandomisation = true;

        // Template cache for dropdown population
        private ResidentialHouseTemplate[] _currentTemplates = System.Array.Empty<ResidentialHouseTemplate>();
        private string[] _templateNames = System.Array.Empty<string>();
        private CityDistrictType _lastLotType;
        private BuildingArchetypeLibrary _lastLibrary;
        private double _lastTemplateSyncTime;

        private void OnEnable()
        {
            AutoLoadDefaults();
        }

        private void OnFocus()
        {
            // Rescan the project so newly-created ResidentialHouseTemplate SOs
            // appear in the dropdown without needing to reopen the window.
            if (_settings.ArchetypeLibrary != null)
            {
                _settings.ArchetypeLibrary.SyncFromProject();
                _lastTemplateSyncTime = EditorApplication.timeSinceStartup;
            }
        }

        private void AutoLoadDefaults()
        {
            // Auto-assign known SOs if not already set.
            if (_settings.RoomSettings == null)
                _settings.RoomSettings = LoadDefault<RoomSettings>(
                    "Assets/02_Shared/ScriptableObjects/Buildings/Rooms/RoomSettings.asset");

            if (_settings.ArchetypeLibrary == null)
                _settings.ArchetypeLibrary = LoadDefault<BuildingArchetypeLibrary>(
                    "Assets/02_Shared/ScriptableObjects/Buildings/Building_Types/BuildingArchetypeLibrary.asset");

            if (_settings.SkinTable == null)
                _settings.SkinTable = LoadDefault<BuildingSkinTable>(
                    "Assets/02_Shared/ScriptableObjects/Buildings/Skins/BuildingSkinTable.asset");

            if (_settings.KitConfig == null)
                _settings.KitConfig = FindOrCreateKitConfig();

            // Patch any empty KitConfig fields with defaults from the SO definition.
            PatchKitConfigDefaults(_settings.KitConfig);

            // Sync Browse cached refs to match generator settings.
            _dataRoomSettings ??= _settings.RoomSettings;
            _dataArchetypeLibrary ??= _settings.ArchetypeLibrary;
            _dataBuildingSkins ??= _settings.SkinTable;

            if (_dataInteriorPropConfig == null)
                _dataInteriorPropConfig = LoadDefault<InteriorPropConfig>(
                    "Assets/02_Shared/ScriptableObjects/Buildings/Props/InteriorPropConfig.asset");

            // Auto-wire InteriorPropConfig to RoomSettings if not already assigned.
            if (_settings.RoomSettings != null && _settings.RoomSettings.PropConfig == null
                && _dataInteriorPropConfig != null)
            {
                _settings.RoomSettings.PropConfig = _dataInteriorPropConfig;
                EditorUtility.SetDirty(_settings.RoomSettings);
            }
        }

        private static void PatchKitConfigDefaults(BuildingKitConfig kit)
        {
            if (kit == null) return;
            var changed = false;

            if (string.IsNullOrWhiteSpace(kit.fuseBoxSourceFolder))
            {
                kit.fuseBoxSourceFolder = "Assets/02_Shared/Prefabs/Props/FuseBoxes";
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(kit.exteriorDoorSourceFolder))
            {
                kit.exteriorDoorSourceFolder = "Assets/03_ThirdParty/Free Wood Door Pack/Prefab/Wood";
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(kit.interiorDoorSourceFolder))
            {
                kit.interiorDoorSourceFolder = "Assets/03_ThirdParty/Free Wood Door Pack/Prefab/Wood";
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(kit);
                AssetDatabase.SaveAssets();
            }
        }

        private static T LoadDefault<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                Debug.Log($"[Modular Building] Auto-loaded default: {path}");
            return asset;
        }

        private static BuildingKitConfig FindOrCreateKitConfig()
        {
            // 1. Try the known default path.
            const string defaultPath = "Assets/02_Shared/ScriptableObjects/Buildings/BuildingKitConfig.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BuildingKitConfig>(defaultPath);
            if (existing != null)
            {
                Debug.Log($"[Modular Building] Auto-loaded Kit Config: {defaultPath}");
                return existing;
            }

            // 2. Search project-wide.
            var guids = AssetDatabase.FindAssets("t:BuildingKitConfig");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                existing = AssetDatabase.LoadAssetAtPath<BuildingKitConfig>(path);
                if (existing != null)
                {
                    Debug.Log($"[Modular Building] Auto-loaded Kit Config: {path}");
                    return existing;
                }
            }

            // 3. Create default.
            var folder = System.IO.Path.GetDirectoryName(defaultPath);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parent = System.IO.Path.GetDirectoryName(folder);
                var name = System.IO.Path.GetFileName(folder);
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets", "02_Shared");
                AssetDatabase.CreateFolder(parent, name);
            }

            var config = ScriptableObject.CreateInstance<BuildingKitConfig>();
            AssetDatabase.CreateAsset(config, defaultPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Modular Building] Created default Kit Config: {defaultPath}");
            return config;
        }

        public static void ShowWindow()
        {
            var window = GetWindow<ModularSingleLevelHouseGeneratorWindow>();
            window.titleContent = new GUIContent("Modular Building");
            window.minSize = new Vector2(440f, 420f);
        }

        private void OnGUI()
        {
            GeneratorSettingsValidator.NormalizeForEditor(ref _settings);

            EditorGUILayout.LabelField(
                "Modular Building Generator Master Tool",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Assemble buildings from kit parts on a 3m grid. Assign Scriptable Objects below to configure room layouts, skins, props, and kit paths — then generate a prefab.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4f);

            DrawScriptableObjectsSection();

            EditorGUILayout.Space(2f);
            DrawLotTypeSection();
            DrawManualGenerationSection();
            DrawOverridesSection();

            EditorGUILayout.Space(4f);
            DrawPreviewAndGenerate();
        }

        // ── Scriptable Objects ───────────────────────────────────

        private static readonly string[] DatabaseTypeNames = { "Room Settings", "Building Skins", "Archetype Library", "Interior Prop Config", "Building Kit Config", "Room Floor Skins" };

        private void DrawScriptableObjectsSection()
        {
            _showScriptableObjects = EditorGUILayout.Foldout(_showScriptableObjects, "Scriptable Objects", true);
            if (!_showScriptableObjects)
                return;

            EditorGUI.indentLevel++;

            // ── Unconnected SO warning ──

            var missing = new System.Collections.Generic.List<string>();
            if (_settings.RoomSettings == null) missing.Add("Room Settings");
            if (_settings.ArchetypeLibrary == null) missing.Add("Archetype Library");
            if (_settings.SkinTable == null) missing.Add("Skin Table");
            if (_settings.KitConfig == null) missing.Add("Building Kit Config");

            if (missing.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Unconnected: " + string.Join(", ", missing),
                    MessageType.Error);
                EditorGUILayout.Space(2f);
            }

            // ── Active assignments summary ──

            if (_settings.RoomSettings != null || _settings.ArchetypeLibrary != null)
            {
                EditorGUILayout.Space(2f);
                if (_settings.RoomSettings != null)
                    EditorGUILayout.LabelField(
                        $"Room Settings: {_settings.RoomSettings.name}",
                        EditorStyles.miniLabel);
                if (_settings.ArchetypeLibrary != null)
                    EditorGUILayout.LabelField(
                        $"Archetype Library: {_settings.ArchetypeLibrary.name}",
                        EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4f);

            // ── Browse & assign ──

            _selectedDatabase = (DatabaseType)EditorGUILayout.Popup(
                "Browse",
                (int)_selectedDatabase,
                DatabaseTypeNames);

            EditorGUILayout.Space(2f);

            switch (_selectedDatabase)
            {
                case DatabaseType.RoomSettings:
                    DrawRoomSettingsAsset();
                    break;
                case DatabaseType.BuildingSkins:
                    DrawBuildingSkinsAsset();
                    break;
                case DatabaseType.ArchetypeLibrary:
                    DrawArchetypeLibraryAsset();
                    break;
                case DatabaseType.InteriorPropConfig:
                    DrawInteriorPropConfigAsset();
                    break;
                case DatabaseType.BuildingKitConfig:
                    DrawBuildingKitConfigAsset();
                    break;
                case DatabaseType.RoomFloorSkinConfig:
                    DrawRoomFloorSkinConfigAsset();
                    break;
            }

            EditorGUI.indentLevel--;
        }

        // ── Individual asset drawers ──────────────────────────────

        private void DrawRoomSettingsAsset()
        {
            var prev = _dataRoomSettings;
            _dataRoomSettings = (RoomSettings)EditorGUILayout.ObjectField(
                "Asset", _dataRoomSettings, typeof(RoomSettings), false);

            if (_dataRoomSettings != prev)
                _settings.RoomSettings = _dataRoomSettings;

            if (_dataRoomSettings == null)
                return;

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Room Type Configs", EditorStyles.boldLabel);

            if (_dataRoomSettings.RoomTypes == null)
                _dataRoomSettings.RoomTypes = System.Array.Empty<RoomTypeConfig>();

            _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll, GUILayout.MaxHeight(400f));

            for (var i = 0; i < _dataRoomSettings.RoomTypes.Length; i++)
            {
                var cfg = _dataRoomSettings.RoomTypes[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"{cfg.Type}", EditorStyles.boldLabel);

                cfg.AllowOpenPlan = EditorGUILayout.Toggle("Allow Open Plan", cfg.AllowOpenPlan);
                cfg.BlockFrontDoor = EditorGUILayout.Toggle("Block Front Door", cfg.BlockFrontDoor);
                cfg.FloorPercentageMin = EditorGUILayout.IntSlider("Floor % Min", cfg.FloorPercentageMin, 0, 100);
                cfg.FloorPercentageMax = EditorGUILayout.IntSlider("Floor % Max", cfg.FloorPercentageMax, 0, 100);

                _dataRoomSettings.RoomTypes[i] = cfg;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Adjacency Rules", EditorStyles.boldLabel);

            if (_dataRoomSettings.AdjacencyRules != null)
            {
                for (var i = 0; i < _dataRoomSettings.AdjacencyRules.Length; i++)
                {
                    var rule = _dataRoomSettings.AdjacencyRules[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{rule.RoomA} ↔ {rule.RoomB}",
                        GUILayout.Width(160f));
                    rule.Adjacency = (AdjacencyType)EditorGUILayout.EnumPopup(rule.Adjacency, GUILayout.Width(100f));
                    _dataRoomSettings.AdjacencyRules[i] = rule;
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_dataRoomSettings);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawBuildingSkinsAsset()
        {
            var prev = _dataBuildingSkins;
            _dataBuildingSkins = (BuildingSkinTable)EditorGUILayout.ObjectField(
                "Asset", _dataBuildingSkins, typeof(BuildingSkinTable), false);

            if (_dataBuildingSkins != prev)
                _settings.SkinTable = _dataBuildingSkins;

            if (_dataBuildingSkins == null)
                return;

            EditorGUI.BeginChangeCheck();

            _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll, GUILayout.MaxHeight(400f));

            EditorGUILayout.LabelField("Fallback", EditorStyles.boldLabel);
            if (_dataBuildingSkins.fallback != null)
            {
                _dataBuildingSkins.fallback.name = EditorGUILayout.TextField("Name", _dataBuildingSkins.fallback.name);
                _dataBuildingSkins.fallback.weight = EditorGUILayout.FloatField("Weight", _dataBuildingSkins.fallback.weight);
            }
            else
            {
                EditorGUILayout.LabelField("No fallback set.", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Zones", EditorStyles.boldLabel);
            if (_dataBuildingSkins.zones != null)
            {
                for (var zi = 0; zi < _dataBuildingSkins.zones.Count; zi++)
                {
                    var zone = _dataBuildingSkins.zones[zi];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    zone.zoneName = EditorGUILayout.TextField("Name", zone.zoneName);

                    if (zone.defaultSkins != null)
                    {
                        EditorGUILayout.LabelField($"Default Skins ({zone.defaultSkins.Count})", EditorStyles.miniLabel);
                        for (var si = 0; si < zone.defaultSkins.Count; si++)
                        {
                            var skin = zone.defaultSkins[si];
                            EditorGUILayout.BeginHorizontal();
                            skin.name = EditorGUILayout.TextField(skin.name, GUILayout.Width(140f));
                            skin.weight = EditorGUILayout.FloatField(skin.weight, GUILayout.Width(50f));
                            zone.defaultSkins[si] = skin;
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(1f);
                }
            }

            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_dataBuildingSkins);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawArchetypeLibraryAsset()
        {
            var prev = _dataArchetypeLibrary;
            _dataArchetypeLibrary = (BuildingArchetypeLibrary)EditorGUILayout.ObjectField(
                "Asset", _dataArchetypeLibrary, typeof(BuildingArchetypeLibrary), false);

            if (_dataArchetypeLibrary != prev)
            {
                _settings.ArchetypeLibrary = _dataArchetypeLibrary;
                _settings.SelectedTemplateIndex = 0;
            }

            if (_dataArchetypeLibrary == null)
                return;

            EditorGUI.BeginChangeCheck();

            _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll, GUILayout.MaxHeight(400f));

            foreach (var zone in ModularBuildingCategoryResolver.AllCategoryOptions)
            {
                var templates = _dataArchetypeLibrary.GetTemplatesForLotType(zone);
                if (templates == null || templates.Length == 0)
                    continue;

                EditorGUILayout.LabelField($"{zone} Templates", EditorStyles.boldLabel);
                EditorGUILayout.Space(2f);

                for (var i = 0; i < templates.Length; i++)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    templates[i] =
                        (ResidentialHouseTemplate)EditorGUILayout.ObjectField(
                            $"#{i}", templates[i],
                            typeof(ResidentialHouseTemplate), false);

                    var t = templates[i];
                    if (t != null)
                    {
                        EditorGUILayout.LabelField(
                            $"  Rooms: {t.EstimatedMinCells} cells | Floors: {t.PreferredFloorCount} | W {t.MinFootprintWidth}–{t.MaxFootprintWidth} × D {ResolveTemplateMinDepth(t)}–{ResolveTemplateMaxDepth(t)}",
                            EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(1f);
                }

                EditorGUILayout.Space(4f);
            }

            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_dataArchetypeLibrary);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawInteriorPropConfigAsset()
        {
            _dataInteriorPropConfig = (InteriorPropConfig)EditorGUILayout.ObjectField(
                "Asset", _dataInteriorPropConfig, typeof(InteriorPropConfig), false);

            if (_dataInteriorPropConfig == null)
                return;

            EditorGUI.BeginChangeCheck();

            _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll, GUILayout.MaxHeight(400f));

            if (_dataInteriorPropConfig.roomEntries != null)
            {
                EditorGUILayout.LabelField("Room Entries", EditorStyles.boldLabel);
                EditorGUILayout.Space(2f);

                for (var i = 0; i < _dataInteriorPropConfig.roomEntries.Length; i++)
                {
                    var entry = _dataInteriorPropConfig.roomEntries[i];
                    var propCount = entry.props?.Length ?? 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"{entry.roomType}", EditorStyles.boldLabel);

                    entry.spawnAttempts = EditorGUILayout.IntField("Spawn Attempts", entry.spawnAttempts);
                    EditorGUILayout.LabelField(
                        $"  {propCount} prop(s)",
                        EditorStyles.miniLabel);

                    _dataInteriorPropConfig.roomEntries[i] = entry;
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(1f);
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Fallback", EditorStyles.boldLabel);
            _dataInteriorPropConfig.fallbackSpawnAttempts = EditorGUILayout.IntField(
                "Spawn Attempts", _dataInteriorPropConfig.fallbackSpawnAttempts);
            if (_dataInteriorPropConfig.fallbackProps != null)
                EditorGUILayout.LabelField(
                    $"  {_dataInteriorPropConfig.fallbackProps.Length} prop(s)",
                    EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_dataInteriorPropConfig);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawBuildingKitConfigAsset()
        {
            var prev = _dataBuildingKitConfig;
            _dataBuildingKitConfig = (BuildingKitConfig)EditorGUILayout.ObjectField(
                "Asset", _dataBuildingKitConfig, typeof(BuildingKitConfig), false);

            if (_dataBuildingKitConfig != prev)
                _settings.KitConfig = _dataBuildingKitConfig;

            if (_dataBuildingKitConfig == null)
                return;

            EditorGUI.BeginChangeCheck();

            EditorGUI.indentLevel++;
            _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll, GUILayout.MaxHeight(550f));

            _dataBuildingKitConfig.kitFolder = EditorGUILayout.TextField("Kit Folder", _dataBuildingKitConfig.kitFolder);
            _dataBuildingKitConfig.outputFolder = EditorGUILayout.TextField("Output Folder", _dataBuildingKitConfig.outputFolder);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Core Kit Parts", EditorStyles.boldLabel);
            _dataBuildingKitConfig.foundationPrefab = Pf("Foundation", _dataBuildingKitConfig.foundationPrefab);
            _dataBuildingKitConfig.floorPrefab = Pf("Floor", _dataBuildingKitConfig.floorPrefab);
            _dataBuildingKitConfig.wallPrefab = Pf("Wall", _dataBuildingKitConfig.wallPrefab);
            _dataBuildingKitConfig.doorwayPrefab = Pf("Doorway", _dataBuildingKitConfig.doorwayPrefab);
            _dataBuildingKitConfig.windowPrefab = Pf("Window", _dataBuildingKitConfig.windowPrefab);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Roof Parts", EditorStyles.boldLabel);
            _dataBuildingKitConfig.roofRidgePrefab = Pf("Roof Ridge", _dataBuildingKitConfig.roofRidgePrefab);
            _dataBuildingKitConfig.roofPanelPrefab = Pf("Roof Panel", _dataBuildingKitConfig.roofPanelPrefab);
            _dataBuildingKitConfig.roofGablePrefab = Pf("Roof Gable", _dataBuildingKitConfig.roofGablePrefab);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Window Variants", EditorStyles.boldLabel);
            _dataBuildingKitConfig.windowClosedPrefab = Pf("Window Closed", _dataBuildingKitConfig.windowClosedPrefab);
            _dataBuildingKitConfig.windowMouldingPrefab = Pf("Window Moulding", _dataBuildingKitConfig.windowMouldingPrefab);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Guttering", EditorStyles.boldLabel);
            _dataBuildingKitConfig.gutter3mPrefab = Pf("Gutter 3m", _dataBuildingKitConfig.gutter3mPrefab);
            _dataBuildingKitConfig.gutterBracketsPrefab = Pf("Gutter Brackets", _dataBuildingKitConfig.gutterBracketsPrefab);
            _dataBuildingKitConfig.downpipePrefab = Pf("Downpipe 3m", _dataBuildingKitConfig.downpipePrefab);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Overrides", EditorStyles.boldLabel);
            _dataBuildingKitConfig.stairPrefab = Pf("Stair", _dataBuildingKitConfig.stairPrefab);
            _dataBuildingKitConfig.roofPrefab = Pf("Roof", _dataBuildingKitConfig.roofPrefab);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Exterior Props", EditorStyles.boldLabel);
            _dataBuildingKitConfig.fuseBoxSourceFolder = EditorGUILayout.TextField("FuseBox Folder", _dataBuildingKitConfig.fuseBoxSourceFolder);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Door", EditorStyles.boldLabel);
            _dataBuildingKitConfig.exteriorDoorSourceFolder = EditorGUILayout.TextField("Exterior Door Folder", _dataBuildingKitConfig.exteriorDoorSourceFolder);
            _dataBuildingKitConfig.interiorDoorSourceFolder = EditorGUILayout.TextField("Interior Door Folder", _dataBuildingKitConfig.interiorDoorSourceFolder);
            _dataBuildingKitConfig.doorScale = EditorGUILayout.Vector3Field("Door Scale", _dataBuildingKitConfig.doorScale);

            EditorGUILayout.EndScrollView();
            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_dataBuildingKitConfig);
                AssetDatabase.SaveAssets();
            }
        }

        private static GameObject Pf(string label, GameObject current) =>
            (GameObject)EditorGUILayout.ObjectField(label, current, typeof(GameObject), false);

        private void DrawRoomFloorSkinConfigAsset()
        {
            _dataRoomFloorSkinConfig = (Zombera.Data.RoomFloorSkinConfig)EditorGUILayout.ObjectField(
                "Asset", _dataRoomFloorSkinConfig, typeof(Zombera.Data.RoomFloorSkinConfig), false);

            if (_dataRoomFloorSkinConfig == null)
                return;

            EditorGUI.BeginChangeCheck();

            // BuildingSkinTable — the floor material catalog source.
            _dataRoomFloorSkinConfig.buildingSkinTable = (Zombera.Data.BuildingSkinTable)EditorGUILayout.ObjectField(
                "Building Skin Table", _dataRoomFloorSkinConfig.buildingSkinTable,
                typeof(Zombera.Data.BuildingSkinTable), false);

            if (_dataRoomFloorSkinConfig.roomSettings == null && _settings.RoomSettings != null)
                _dataRoomFloorSkinConfig.roomSettings = _settings.RoomSettings;

            _dataRoomFloorSkinConfig.roomSettings = EditorGUILayout.ObjectField(
                "Room Settings", _dataRoomFloorSkinConfig.roomSettings, typeof(RoomSettings), false);

            var validTypeNames = CollectValidRoomTypeNames(_dataRoomFloorSkinConfig.roomSettings as RoomSettings);

            // Collect floor material pool from the attached BuildingSkinTable.
            var floorMaterialPool = CollectFloorMaterialsFromTable(_dataRoomFloorSkinConfig.buildingSkinTable);

            if (validTypeNames.Count == 0)
            {
                EditorGUILayout.HelpBox("Assign a RoomSettings asset above to see available room types.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Room Entries", EditorStyles.boldLabel);
                _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll, GUILayout.MaxHeight(400f));

                var entryList = SyncRoomFloorEntriesByName(_dataRoomFloorSkinConfig, validTypeNames);
                DrawRoomFloorEntryRows(entryList, validTypeNames, floorMaterialPool);
                _dataRoomFloorSkinConfig.entries = entryList.ToArray();

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Fallback", EditorStyles.boldLabel);
            DrawWeightedMaterialList(_dataRoomFloorSkinConfig.fallback, floorMaterialPool);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_dataRoomFloorSkinConfig);
                AssetDatabase.SaveAssets();
            }
        }

        private static System.Collections.Generic.List<string> CollectValidRoomTypeNames(RoomSettings rs)
        {
            var result = new System.Collections.Generic.List<string>();
            if (rs == null || rs.RoomTypes == null) return result;
            foreach (var rt in rs.RoomTypes)
                result.Add(rt.Type.ToString());
            return result;
        }

        /// <summary>
        ///     Collects all distinct floor materials from a <see cref="BuildingSkinTable"/> —
        ///     across all zones, conditions, and the fallback skin set.
        ///     Returns null when no table is assigned (no filter applied).
        /// </summary>
        private static System.Collections.Generic.HashSet<Material> CollectFloorMaterialsFromTable(
            Zombera.Data.BuildingSkinTable table)
        {
            if (table == null)
                return null;

            var pool = new System.Collections.Generic.HashSet<Material>();
            CollectFloorMaterialsFromSkinSet(table.fallback, pool);

            if (table.zones != null)
                foreach (var zone in table.zones)
                    CollectFloorMaterialsFromZone(zone, pool);

            return pool.Count > 0 ? pool : null;
        }

        private static void CollectFloorMaterialsFromSkinSet(
            Zombera.Data.SkinSet ss,
            System.Collections.Generic.HashSet<Material> pool)
        {
            if (ss?.floorMaterials == null) return;
            foreach (var wm in ss.floorMaterials)
                if (wm.material != null)
                    pool.Add(wm.material);
        }

        private static void CollectFloorMaterialsFromZone(
            Zombera.Data.ZoneSkinSet zone,
            System.Collections.Generic.HashSet<Material> pool)
        {
            if (zone.defaultSkins != null)
                foreach (var ss in zone.defaultSkins)
                    CollectFloorMaterialsFromSkinSet(ss, pool);

            if (zone.conditionOverrides != null)
                foreach (var cond in zone.conditionOverrides)
                    if (cond.skins != null)
                        foreach (var ss in cond.skins)
                            CollectFloorMaterialsFromSkinSet(ss, pool);
        }

        private static System.Collections.Generic.List<Zombera.Data.RoomFloorEntry> SyncRoomFloorEntriesByName(
            Zombera.Data.RoomFloorSkinConfig config,
            System.Collections.Generic.List<string> validTypeNames)
        {
            var entries = config.entries ?? System.Array.Empty<Zombera.Data.RoomFloorEntry>();
            var entryList = new System.Collections.Generic.List<Zombera.Data.RoomFloorEntry>(entries);
            entryList.RemoveAll(e => !validTypeNames.Contains(e.roomTypeName));
            foreach (var typeName in validTypeNames)
            {
                if (!entryList.Exists(e => string.Equals(e.roomTypeName, typeName, System.StringComparison.OrdinalIgnoreCase)))
                    entryList.Add(new Zombera.Data.RoomFloorEntry { roomTypeName = typeName });
            }
            return entryList;
        }

        private static void DrawRoomFloorEntryRows(
            System.Collections.Generic.List<Zombera.Data.RoomFloorEntry> entryList,
            System.Collections.Generic.List<string> validTypeNames,
            System.Collections.Generic.HashSet<Material> floorMaterialPool)
        {
            for (var i = 0; i < entryList.Count; i++)
            {
                var entry = entryList[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Dropdown to select room type by name.
                var selectedIndex = validTypeNames.FindIndex(
                    n => string.Equals(n, entry.roomTypeName, System.StringComparison.OrdinalIgnoreCase));
                if (selectedIndex < 0) selectedIndex = 0;
                selectedIndex = EditorGUILayout.Popup("Room Type", selectedIndex, validTypeNames.ToArray());
                entry.roomTypeName = validTypeNames[selectedIndex];

                EditorGUILayout.LabelField($"  {(entry.materials?.Length ?? 0)} material(s)", EditorStyles.miniLabel);
                DrawWeightedMaterialList(entry.materials, floorMaterialPool);
                entryList[i] = entry;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(1f);
            }
        }

        private static void DrawWeightedMaterialList(
            Zombera.Data.WeightedMaterial[] list,
            System.Collections.Generic.HashSet<Material> materialPool = null)
        {
            if (list == null) return;
            for (var i = 0; i < list.Length; i++)
            {
                var wm = list[i];
                EditorGUILayout.BeginHorizontal();

                if (materialPool != null)
                {
                    // Constrain to materials from the BuildingSkinTable floor pool.
                    wm.material = DrawFilteredMaterialField(wm.material, materialPool);
                }
                else
                {
                    wm.material = (Material)EditorGUILayout.ObjectField(
                        wm.material, typeof(Material), false, GUILayout.Width(180f));
                }

                wm.weight = EditorGUILayout.FloatField(wm.weight, GUILayout.Width(50f));
                list[i] = wm;
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        ///     Draws a material object field filtered to only accept materials
        ///     present in <paramref name="pool"/>. Shows a dropdown of available
        ///     materials or the standard object picker with a help message.
        /// </summary>
        private static Material DrawFilteredMaterialField(
            Material current,
            System.Collections.Generic.HashSet<Material> pool)
        {
            if (pool == null || pool.Count == 0)
                return (Material)EditorGUILayout.ObjectField(
                    current, typeof(Material), false, GUILayout.Width(180f));

            // Build sorted list for stable display.
            var sorted = new System.Collections.Generic.List<Material>(pool);
            sorted.Sort((a, b) => string.CompareOrdinal(
                a != null ? a.name : "",
                b != null ? b.name : ""));

            var names = new string[sorted.Count + 1];
            names[0] = "(None)";
            for (var j = 0; j < sorted.Count; j++)
                names[j + 1] = sorted[j] != null ? sorted[j].name : "(Missing)";

            var selectedIndex = IndexOfMaterial(sorted, current);
            var newIndex = EditorGUILayout.Popup(selectedIndex, names, GUILayout.Width(180f));
            return newIndex > 0 ? sorted[newIndex - 1] : null;
        }

        private static int IndexOfMaterial(
            System.Collections.Generic.List<Material> sorted,
            Material current)
        {
            if (current == null) return 0;
            for (var j = 0; j < sorted.Count; j++)
                if (sorted[j] == current)
                    return j + 1;
            return 0;
        }

        // ── Lot Type ──────────────────────────────────────────────

        private void DrawLotTypeSection()
        {
            _showLotType = EditorGUILayout.Foldout(_showLotType, "Lot Type", true);
            if (!_showLotType)
                return;

            EditorGUI.indentLevel++;

            var previousZone = _settings.BuildingCategoryOverride;
            _settings.BuildingCategoryOverride = DrawCategoryPopup(
                "Zone",
                _settings.BuildingCategoryOverride);
            if (_settings.BuildingCategoryOverride != previousZone)
            {
                // Reset template selection when switching zones so the new
                // zone's first template is selected instead of a stale index.
                _settings.SelectedTemplateIndex = 0;
            }

            EditorGUILayout.Space(2f);

            // Template dropdown when library is assigned
            if (_settings.ArchetypeLibrary != null)
            {
                var effectiveLotType = ResolveEffectiveLotType();

                // Auto-sync every 2s so newly-created template SOs appear without
                // needing to reopen the window. OnFocus also triggers an immediate sync.
                var now = EditorApplication.timeSinceStartup;
                if (now - _lastTemplateSyncTime > 2.0)
                {
                    _settings.ArchetypeLibrary.SyncFromProject();
                    _lastTemplateSyncTime = now;
                }
                RefreshTemplateCache(effectiveLotType);

                if (_currentTemplates.Length > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    _settings.SelectedTemplateIndex = EditorGUILayout.Popup(
                        new GUIContent("House Template",
                            "Select the residential house archetype to generate."),
                        _settings.SelectedTemplateIndex,
                        _templateNames);

                    _settings.SelectedTemplateIndex = Mathf.Clamp(
                        _settings.SelectedTemplateIndex, 0, _currentTemplates.Length - 1);

                    if (GUILayout.Button("↻", GUILayout.Width(28f), GUILayout.Height(18f)))
                    {
                        _settings.ArchetypeLibrary.SyncFromProject();
                        RefreshTemplateCache(effectiveLotType);
                    }
                    EditorGUILayout.EndHorizontal();

                    DrawActiveTemplateInfo();
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"No templates found for {effectiveLotType}. Create ResidentialHouseTemplate assets " +
                        $"via Assets → Create → Zombera → Building → Residential House Template. " +
                        $"They will be auto-detected here.",
                        MessageType.Warning);
                }
            }

            EditorGUI.indentLevel--;
        }

        private CityDistrictType ResolveEffectiveLotType()
        {
            return _settings.BuildingCategoryOverride;
        }

        private void RefreshTemplateCache(CityDistrictType lotType)
        {
            // Always refresh — the cache guard was preventing newly-added
            // templates from appearing until the zone was toggled or the
            // window reopened. GetTemplatesForLotType is cheap (array ref).
            _lastLibrary = _settings.ArchetypeLibrary;
            _lastLotType = lotType;

            _currentTemplates = _settings.ArchetypeLibrary.GetTemplatesForLotType(lotType);
            _templateNames = new string[_currentTemplates.Length];
            for (var i = 0; i < _currentTemplates.Length; i++)
            {
                var name = _currentTemplates[i].DisplayName;
                _templateNames[i] = string.IsNullOrWhiteSpace(name)
                    ? $"Unnamed Template {i}"
                    : name;
            }
        }

        private void DrawActiveTemplateInfo()
        {
            if (_settings.SelectedTemplateIndex < 0
                || _settings.SelectedTemplateIndex >= _currentTemplates.Length)
                return;

            var template = _currentTemplates[_settings.SelectedTemplateIndex];
            var sb = new System.Text.StringBuilder();
            sb.Append("Required rooms: ").Append(template.TotalMinRooms);
            sb.Append(" | Est. cells: ").Append(template.EstimatedMinCells);
            sb.Append(" | Floors: ").Append(template.PreferredFloorCount);

            EditorGUILayout.HelpBox(sb.ToString(), MessageType.Info);
        }

        // ── Kit & Output ──────────────────────────────────────────

        private void DrawKitSection()
        {
            _showKit = EditorGUILayout.Foldout(_showKit, "Kit & Output", true);
            if (!_showKit)
                return;

            EditorGUI.indentLevel++;

            var hasKitConfig = _settings.KitConfig != null;

            if (hasKitConfig)
            {
                _settings.KitConfig = (BuildingKitConfig)EditorGUILayout.ObjectField(
                    new GUIContent("Kit Config"),
                    _settings.KitConfig,
                    typeof(BuildingKitConfig),
                    false);

                if (_settings.KitConfig != null)
                {
                    EditorGUILayout.HelpBox(
                        $"Kit: {_settings.KitConfig.kitFolder}\nOutput: {_settings.KitConfig.outputFolder}",
                        MessageType.Info);
                }
            }

            using (new EditorGUI.DisabledScope(hasKitConfig))
            {
                _settings.KitFolder = EditorGUILayout.TextField("Kit folder", _settings.KitFolder);
                _settings.OutputFolder = EditorGUILayout.TextField("Output folder", _settings.OutputFolder);

                EditorGUILayout.Space(4f);

                _settings.StairPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Stair prefab",
                        "Drag the stair prefab here for multi-story buildings. Empty = use Building_Stair.prefab from kit folder."),
                    _settings.StairPrefab,
                    typeof(GameObject),
                    false);

                _settings.UpperFloorPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Upper floor prefab",
                        "Optional upper-floor slab. Empty = reuse Building_Floor from kit for levels 1+."),
                    _settings.UpperFloorPrefab,
                    typeof(GameObject),
                    false);

                _settings.ExteriorStairsPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Exterior stairs prefab",
                        "Stair piece placed outside each ground-floor door. Empty = none."),
                    _settings.ExteriorStairsPrefab,
                    typeof(GameObject),
                    false);

                _settings.RoofPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Roof prefab",
                        "Optional roof. Empty = use Roof.prefab from kit (or Building_Floor as flat cap)."),
                    _settings.RoofPrefab,
                    typeof(GameObject),
                    false);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Door Overrides", EditorStyles.boldLabel);

                _settings.ExteriorDoorPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Exterior door prefab",
                        "Drag a door prefab for perimeter doors. Empty = use kit Doorway.prefab."),
                    _settings.ExteriorDoorPrefab,
                    typeof(GameObject),
                    false);

                _settings.InteriorDoorPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Interior door prefab",
                        "Drag a door prefab for room-to-room doors. Empty = use kit Doorway.prefab."),
                    _settings.InteriorDoorPrefab,
                    typeof(GameObject),
                    false);

                _settings.DoorScale = EditorGUILayout.Vector3Field(
                    new GUIContent("Door scale",
                        "Scale applied to all override door prefabs. FreeWoodDoorPack: (0.904, 0.88, 1)."),
                    _settings.DoorScale);
            }

            EditorGUI.indentLevel--;
        }

        // ── Manual Generation ──────────────────────────────────────

        private void DrawManualGenerationSection()
        {
            _showManualGeneration = EditorGUILayout.Foldout(_showManualGeneration, "Manual Generation", true);
            if (!_showManualGeneration)
                return;

            EditorGUI.indentLevel++;

            _settings.UseManualGeneration = EditorGUILayout.ToggleLeft(
                new GUIContent("Enable manual generation",
                    "When checked, manual sliders override template defaults for footprint, floors, rooms, and doors."),
                _settings.UseManualGeneration);

            var activeTemplate = GetActiveTemplate();
            var useTemplate = activeTemplate != null;

            using (new EditorGUI.DisabledScope(!_settings.UseManualGeneration))
            {
                _settings.FloorCount = EditorGUILayout.IntSlider(
                    new GUIContent("Floors (height)",
                        $"Stories, 1–{ModularSingleLevelHouseGeneratorTool.MaxFloors}."),
                    _settings.FloorCount,
                    1,
                    ModularSingleLevelHouseGeneratorTool.MaxFloors);

                EditorGUILayout.Space(2f);
                _settings.SkyscraperMode = EditorGUILayout.ToggleLeft(
                    new GUIContent("Skyscraper Mode",
                        "Tapered tower: upper floors shrink in footprint."),
                    _settings.SkyscraperMode);
                if (_settings.SkyscraperMode)
                    DrawSkyscraperOptions();

                EditorGUILayout.Space(2f);
                _settings.GroundDoorCount = EditorGUILayout.IntSlider(
                    new GUIContent("Ground doors", "Doorway segments on the ground floor perimeter."),
                    _settings.GroundDoorCount,
                    0,
                    12);

                // Room count — disabled when template is active
                using (new EditorGUI.DisabledScope(useTemplate))
                {
                    _settings.MinRoomsPerFloor = EditorGUILayout.IntSlider(
                        new GUIContent("Rooms min / floor",
                            useTemplate
                                ? "Controlled by template"
                                : "Random minimum room count per floor. 0 allows open floors."),
                        _settings.MinRoomsPerFloor,
                        0,
                        12);

                    _settings.MaxRoomsPerFloor = EditorGUILayout.IntSlider(
                        new GUIContent("Rooms max / floor",
                            useTemplate
                                ? "Controlled by template"
                                : "Random maximum room count per floor."),
                        _settings.MaxRoomsPerFloor,
                        0,
                        12);
                }

                if (useTemplate)
                {
                    EditorGUILayout.HelpBox(
                        "Room counts are controlled by the selected house template. Disable the Archetype Library to use manual sliders.",
                        MessageType.None);
                }

                // Width / Depth — apply template bounds if active
                DrawFootprintDimensions(activeTemplate);

                EditorGUILayout.Space(8f);

                // ── Randomisation (child of manual generation) ──
                DrawRandomisationSection(activeTemplate);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawSkyscraperOptions()
        {
            EditorGUI.indentLevel++;
            _settings.SkyscraperShrinkStartFloor = EditorGUILayout.IntSlider(
                new GUIContent("Shrink Start Floor",
                    "0-based floor index where footprint shrinking begins."),
                _settings.SkyscraperShrinkStartFloor,
                3,
                Mathf.Max(3, _settings.FloorCount - 1));
            _settings.SkyscraperShrinkStep = EditorGUILayout.IntSlider(
                new GUIContent("Shrink Step",
                    "Cells removed per axis per additional floor above the threshold."),
                _settings.SkyscraperShrinkStep,
                1,
                4);

            if (_settings.SkyscraperShrinkStartFloor < _settings.FloorCount)
            {
                var previewTopSteps = _settings.FloorCount - _settings.SkyscraperShrinkStartFloor;
                var baseW = _settings.FixedWidthCells > 0 ? _settings.FixedWidthCells : _settings.MaxCells;
                var baseD = _settings.FixedDepthCells > 0 ? _settings.FixedDepthCells : _settings.MaxCells;
                const int skyscraperMin = 3;
                var topW = Mathf.Max(skyscraperMin,
                    baseW - previewTopSteps * _settings.SkyscraperShrinkStep);
                var topD = Mathf.Max(skyscraperMin,
                    baseD - previewTopSteps * _settings.SkyscraperShrinkStep);
                EditorGUILayout.HelpBox(
                    $"Top floor preview: {topW}×{topD} cells ({topW * 3}m × {topD * 3}m).",
                    MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawFootprintDimensions(ResidentialHouseTemplate template)
        {
            var minSide = ModularSingleLevelHouseGeneratorTool.MinFootprintCells;
            var maxSide = ModularSingleLevelHouseGeneratorTool.MaxFootprintCells;
            var minDepth = minSide;
            var maxDepth = maxSide;

            if (template != null)
            {
                minSide = Mathf.Max(minSide, template.MinFootprintWidth);
                maxSide = template.MaxFootprintWidth > 0
                    ? Mathf.Min(maxSide, template.MaxFootprintWidth)
                    : maxSide;
                if (maxSide < minSide) maxSide = minSide;

                minDepth = Mathf.Max(minDepth, ResolveTemplateMinDepth(template));
                var templateMaxDepth = ResolveTemplateMaxDepth(template);
                maxDepth = templateMaxDepth > 0 ? Mathf.Min(maxDepth, templateMaxDepth) : maxDepth;
                if (maxDepth < minDepth) maxDepth = minDepth;
            }

            _settings.FixedWidthCells = EditorGUILayout.IntSlider(
                new GUIContent("Width (cells)",
                    $"Footprint width; {minSide}–{maxSide}. Set 0 for random."),
                _settings.FixedWidthCells,
                0,
                maxSide);

            _settings.FixedDepthCells = EditorGUILayout.IntSlider(
                new GUIContent("Depth (cells)",
                    $"Footprint depth; {minDepth}–{maxDepth}. Set 0 for random."),
                _settings.FixedDepthCells,
                0,
                maxDepth);
        }

        private static int ResolveTemplateMinDepth(ResidentialHouseTemplate template)
        {
            if (template == null)
                return ModularSingleLevelHouseGeneratorTool.MinFootprintCells;

            return template.MinFootprintDepth > 0
                ? template.MinFootprintDepth
                : template.MinFootprintWidth;
        }

        private static int ResolveTemplateMaxDepth(ResidentialHouseTemplate template)
        {
            if (template == null)
                return ModularSingleLevelHouseGeneratorTool.MaxFootprintCells;

            return template.MaxFootprintDepth > 0
                ? template.MaxFootprintDepth
                : template.MaxFootprintWidth;
        }

        // ── Randomisation (child of Manual Generation) ────────────

        private void DrawRandomisationSection(ResidentialHouseTemplate activeTemplate)
        {
            _showRandomisation = EditorGUILayout.Foldout(_showRandomisation, "Randomisation", true);
            if (!_showRandomisation)
                return;

            EditorGUI.indentLevel++;

            var useTemplate = activeTemplate != null;

            if (useTemplate)
            {
                EditorGUILayout.HelpBox(
                    "Randomisation ranges are driven by the archetype template. " +
                    "Disable the Archetype Library to use manual ranges.",
                    MessageType.None);
            }

            // Footprint range — archetype-driven when template active
            using (new EditorGUI.DisabledScope(useTemplate))
            {
                EditorGUILayout.LabelField("Random footprint (when width or depth is 0)", EditorStyles.boldLabel);

                _settings.MinCells = EditorGUILayout.IntSlider(
                    new GUIContent("Min cells (W/D)",
                        useTemplate
                            ? $"Template sets min: {activeTemplate.MinFootprintWidth}"
                            : "Minimum random footprint cells per axis."),
                    useTemplate ? activeTemplate.MinFootprintWidth : _settings.MinCells,
                    ModularSingleLevelHouseGeneratorTool.MinFootprintCells,
                    ModularSingleLevelHouseGeneratorTool.MaxFootprintCells);

                var maxFootprint = activeTemplate.MaxFootprintWidth > 0
                    ? activeTemplate.MaxFootprintWidth
                    : ModularSingleLevelHouseGeneratorTool.MaxFootprintCells;

                _settings.MaxCells = EditorGUILayout.IntSlider(
                    new GUIContent("Max cells (W/D)",
                        useTemplate
                            ? $"Template sets max: {maxFootprint}"
                            : "Maximum random footprint cells per axis."),
                    useTemplate ? maxFootprint : _settings.MaxCells,
                    ModularSingleLevelHouseGeneratorTool.MinFootprintCells,
                    ModularSingleLevelHouseGeneratorTool.MaxFootprintCells);

                _settings.WindowChance = EditorGUILayout.Slider(
                    new GUIContent("Window chance",
                        "Chance (0–1) to swap a non-door wall for Window."),
                    _settings.WindowChance, 0f, 1f);
            }

            // Seed — always user-controllable
            _settings.UseFixedRandomSeed = EditorGUILayout.ToggleLeft(
                new GUIContent("Fixed random seed", "Same seed reproduces layout & name collision retries."),
                _settings.UseFixedRandomSeed);
            using (new EditorGUI.DisabledScope(!_settings.UseFixedRandomSeed))
                _settings.RandomSeed = EditorGUILayout.IntField("Seed", _settings.RandomSeed);

            EditorGUI.indentLevel--;
        }

        // ── Overrides ────────────────────────────────────────────

        private void DrawOverridesSection()
        {
            _showOverrides = EditorGUILayout.Foldout(_showOverrides, "Overrides", true);
            if (!_showOverrides)
                return;

            EditorGUI.indentLevel++;

            _settings.SkipRoofAndCeiling = EditorGUILayout.ToggleLeft(
                new GUIContent("Skip roof assembly",
                    "When checked, the generator skips exterior roof assembly, parapet walls, and guttering. Ceilings are still placed to close rooms."),
                _settings.SkipRoofAndCeiling);

            EditorGUI.indentLevel--;
        }

        // ── Preview & Generate ────────────────────────────────────

        private void DrawPreviewAndGenerate()
        {
            GeneratorSettingsValidator.NormalizeForEditor(ref _settings);

            var activeTemplate = GetActiveTemplate();
            var previewCategory = activeTemplate != null
                ? activeTemplate.LotType
                : _settings.BuildingCategoryOverride;

            var previewLabel = activeTemplate != null
                ? $"Prefab category (template): {previewCategory} | Template: {activeTemplate.DisplayName}"
                : $"Prefab category: {previewCategory}";

            EditorGUILayout.HelpBox(previewLabel, MessageType.None);

            EditorGUILayout.Space(4f);

            // Bulk generation: run the generate flow N times with the same settings.
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Bulk count", GUILayout.Width(72f));
            _bulkGenerateCount = Mathf.Clamp(
                EditorGUILayout.IntField(_bulkGenerateCount, GUILayout.Width(56f)), 1, 100);
            EditorGUILayout.LabelField("Generate runs this many times", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            var generateLabel = _bulkGenerateCount > 1
                ? $"Generate {_bulkGenerateCount} prefabs"
                : "Generate prefab";
            if (!GUILayout.Button(generateLabel, GUILayout.Height(32f)))
                return;

            RunBulkGenerate(_bulkGenerateCount);
        }

        private void RunBulkGenerate(int count)
        {
            var generated = 0;
            for (var i = 0; i < count; i++)
            {
                if (count > 1)
                    EditorUtility.DisplayProgressBar(
                        "Modular Building Generator",
                        $"Generating building {i + 1}/{count}...",
                        (float)i / count);

                var path = ModularSingleLevelHouseGeneratorTool.Generate(_settings);
                if (string.IsNullOrWhiteSpace(path))
                {
                    Debug.LogWarning(
                        $"[ModularSingleLevelHouseGeneratorTool] Bulk generation stopped — run {i + 1} failed.");
                    break;
                }

                generated++;
            }

            EditorUtility.ClearProgressBar();
            Debug.Log(
                $"[ModularSingleLevelHouseGeneratorTool] Bulk generation complete: {generated}/{count} prefab(s) created.");
        }

        // ── Helpers ───────────────────────────────────────────────

        private ResidentialHouseTemplate GetActiveTemplate()
        {
            if (_settings.ArchetypeLibrary == null
                || _currentTemplates.Length == 0
                || _settings.SelectedTemplateIndex < 0
                || _settings.SelectedTemplateIndex >= _currentTemplates.Length)
                return null;

            return _currentTemplates[_settings.SelectedTemplateIndex];
        }

        private static CityDistrictType DrawCategoryPopup(string label, CityDistrictType current)
        {
            var options = ModularBuildingCategoryResolver.AllCategoryOptions;
            var labels = new string[options.Length];
            var selectedIndex = 0;
            for (var i = 0; i < options.Length; i++)
            {
                labels[i] = options[i].ToString();
                if (options[i] == current)
                    selectedIndex = i;
            }

            selectedIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
            return options[Mathf.Clamp(selectedIndex, 0, options.Length - 1)];
        }
    }
}
#endif
