#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        /// <summary>Folder holding the flat/parapet roof configs used for commercial buildings.</summary>
        private const string CommercialRoofTypesFolder = "Assets/02_Shared/ScriptableObjects/Buildings/RoofTypes/Flat";

        private static string GenerateNormalized(GeneratorSettings normalizedSettings)
        {
            using var randomScope = new UnityRandomStateScope();

            var context = CreateGenerationContext(normalizedSettings);
            if (!TryResolvePrefabDependencies(context, out var dependencyError))
                return FailGeneration(dependencyError);

            EnsureFolderExists(context.OutputFolder);

            if (!TryComputeGenerationPlan(context, out var planningError))
                return FailGeneration(planningError);

            var assetPath = BuildHierarchyAndSavePrefab(context);
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            // Apply skin from the BuildingSkinTable if one is assigned.
            TryApplySkinFromTable(context, assetPath);

            // Bake a game-ready proxy prefab beside the generated building.
            if (context.Settings.GenerateProxyPrefab)
                TryBuildProxyForBuilding(context, assetPath);

            return assetPath;
        }

        /// <summary>
        ///     If <see cref="GeneratorSettings.SkinTable"/> is assigned, picks a zone-appropriate
        ///     <see cref="SkinSet"/> and applies it to the freshly-generated prefab.
        /// </summary>
        private static void TryApplySkinFromTable(GenerationContext context, string prefabPath)
        {
            var table = context.Settings?.SkinTable;
            if (table == null || string.IsNullOrWhiteSpace(prefabPath))
            {
                if (table == null && !string.IsNullOrWhiteSpace(prefabPath))
                    Debug.Log(
                        "[ModularSingleLevelHouseGeneratorTool] No SkinTable assigned — prefab will use default kit materials. " +
                        "Assign a BuildingSkinTable in the generator's Lot Type section to auto-apply materials.");
                return;
            }

            var zoneName = context.BuildingCategory.ToString();
            Debug.Log(
                $"[ModularSingleLevelHouseGeneratorTool] Looking for skin zone '{zoneName}' in table '{table.name}'...");

            // Diagnostic: list available zones in the table.
            if (table.zones.Count > 0)
            {
                var zoneList = string.Join(", ", table.zones.ConvertAll(z => $"'{z.zoneName}' ({z.defaultSkins.Count} skins)"));
                Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Table zones: {zoneList}");
            }
            else
            {
                Debug.LogWarning(
                    "[ModularSingleLevelHouseGeneratorTool] SkinTable has NO zone entries defined. Add zones like 'Residential', 'Commercial', etc.");
            }

            var skinSet = table.PickSkin(zoneName);
            if (skinSet == null)
            {
                Debug.LogWarning(
                    $"[ModularSingleLevelHouseGeneratorTool] No skin found for zone '{zoneName}' in SkinTable. " +
                    $"{(table.fallback != null ? $"Fallback '{table.fallback.name}' available but zone match failed." : "No fallback configured either.")} " +
                    "Prefab left with kit materials.");
                return;
            }

            Debug.Log(
                $"[ModularSingleLevelHouseGeneratorTool] Picked skin set '{skinSet.name}' (weight: {skinSet.weight:F1}) for zone '{zoneName}'.");

            var skin = skinSet.BuildBuildingSkin(context.Random.ToSystemRandom());
            Debug.Log(
                $"[ModularSingleLevelHouseGeneratorTool] Built skin '{skin.Name}': " +
                $"WallExterior={(skin.WallExterior != null ? skin.WallExterior.name : "NULL")}, " +
                $"Default={(skin.Default != null ? skin.Default.name : "NULL")}");

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                Debug.Log(
                    $"[ModularSingleLevelHouseGeneratorTool] Loaded prefab '{prefabPath}': found {renderers.Length} renderer(s).");

                var changed = BuildingSkinReskinTool.ReskinRoot(root, skin);

                // Per-room floor materials (if configured on the table).
                // This is the sole authority for floor materials — SkinSet no longer sets Floor.
                if (table.roomFloorSkinConfig != null)
                {
                    changed |= ApplyRoomFloorMaterials(root, context, table.roomFloorSkinConfig);
                    changed |= ApplyRoomInteriorWallMaterials(root, context, table.roomFloorSkinConfig);
                    changed |= ApplyRoomCeilingMaterials(root, context, table.roomFloorSkinConfig);
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log(
                        $"[ModularSingleLevelHouseGeneratorTool] Applied skin '{skin.Name}' (zone: {zoneName}) to '{prefabPath}'.");
                }
                else
                {
                    Debug.LogWarning(
                        $"[ModularSingleLevelHouseGeneratorTool] ReskinRoot returned false — no renderers matched or materials unchanged. " +
                        $"Check that kit prefabs have Renderer components and that SkinSet materials are assigned.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GenerationContext CreateGenerationContext(GeneratorSettings normalizedSettings)
        {
            var randomSeed = ResolveRandomSeed(normalizedSettings);
            var random = new GenerationRandom(randomSeed);
            var activeTemplate = ResolveActiveTemplate(normalizedSettings);
            var floorCount = ResolveFloorCount(normalizedSettings, activeTemplate);

            var context = new GenerationContext
            {
                Settings = normalizedSettings,
                Random = random,
                KitFolder = ResolveKitFolder(normalizedSettings),
                OutputFolder = ResolveOutputFolder(normalizedSettings),
                ActiveTemplate = activeTemplate
            };

            var (width, depth) = ResolveFootprintDimensions(normalizedSettings, activeTemplate, random);
            context.Plan.Width = width;
            context.Plan.Depth = depth;
            context.Plan.FloorCount = floorCount;

            // Commercial buildings keep flat/parapet roofs only.
            context.BuildingCategory = activeTemplate != null
                ? activeTemplate.LotType
                : normalizedSettings.BuildingCategoryOverride;
            context.ActiveRoofType = ResolveRoofType(activeTemplate, random, context.BuildingCategory);

            SetupSkyscraperContext(normalizedSettings, context, width, depth, floorCount);

            return context;
        }

        private static string ResolveKitFolder(GeneratorSettings settings)
        {
            var kitConfig = settings.KitConfig;
            var folder = kitConfig != null && !string.IsNullOrWhiteSpace(kitConfig.kitFolder)
                ? kitConfig.kitFolder : settings.KitFolder;
            return string.IsNullOrWhiteSpace(folder) ? DefaultKitFolder : folder;
        }

        private static string ResolveOutputFolder(GeneratorSettings settings)
        {
            var kitConfig = settings.KitConfig;
            var folder = kitConfig != null && !string.IsNullOrWhiteSpace(kitConfig.outputFolder)
                ? kitConfig.outputFolder : settings.OutputFolder;
            return string.IsNullOrWhiteSpace(folder) ? DefaultOutputFolder : folder;
        }

        private static int ResolveFloorCount(GeneratorSettings settings, ResidentialHouseTemplate template)
        {
            // Manual override always wins when enabled.
            if (settings.UseManualGeneration)
                return Mathf.Clamp(settings.FloorCount, 1, MaxFloors);

            // Template-driven: use the template's preferred floor count.
            if (template != null && template.PreferredFloorCount > 0)
                return Mathf.Clamp(template.PreferredFloorCount, 1, MaxFloors);

            return Mathf.Clamp(settings.FloorCount, 1, MaxFloors);
        }

        private static (int width, int depth) ResolveFootprintDimensions(
            GeneratorSettings settings, ResidentialHouseTemplate template, GenerationRandom random)
        {
            int minW, maxW, minD, maxD;

            if (template != null)
            {
                minW = Mathf.Max(MinFootprintCells, template.MinFootprintWidth);
                maxW = ClampFootprintAxis(template.MaxFootprintWidth, minW);

                var templateMinD = template.MinFootprintDepth > 0
                    ? template.MinFootprintDepth
                    : template.MinFootprintWidth;
                var templateMaxD = template.MaxFootprintDepth > 0
                    ? template.MaxFootprintDepth
                    : template.MaxFootprintWidth;
                minD = Mathf.Max(MinFootprintCells, templateMinD);
                maxD = ClampFootprintAxis(templateMaxD, minD);
            }
            else
            {
                minW = Mathf.Clamp(settings.MinCells, MinFootprintCells, MaxFootprintCells);
                maxW = Mathf.Clamp(settings.MaxCells, MinFootprintCells, MaxFootprintCells);
                if (maxW < minW) maxW = minW;
                minD = minW;
                maxD = maxW;
            }

            // Resolve each axis independently — FixedWidthCells / FixedDepthCells pin their axis.
            var width = ResolveAxisCells(settings.FixedWidthCells, minW, maxW, random);
            var depth = ResolveAxisCells(settings.FixedDepthCells, minD, maxD, random);
            return (width, depth);
        }

        private static int ClampFootprintAxis(int maxValue, int minValue)
        {
            var clamped = maxValue > 0
                ? Mathf.Min(MaxFootprintCells, maxValue)
                : MaxFootprintCells;
            return clamped < minValue ? minValue : clamped;
        }

        private static void SetupSkyscraperContext(
            GeneratorSettings settings, GenerationContext context,
            int width, int depth, int floorCount)
        {
            if (!settings.SkyscraperMode || settings.SkyscraperShrinkStartFloor >= floorCount)
            {
                context.Plan.MinFootprintWidth = width;
                context.Plan.MinFootprintDepth = depth;
                context.Plan.EffectiveStairCount = floorCount - 1;
                context.Plan.NeedsStairs = floorCount > 1;
                return;
            }

            context.Plan.SkyscraperMode = true;
            context.Plan.FloorWidths = new int[floorCount];
            context.Plan.FloorDepths = new int[floorCount];
            context.Plan.FloorOffsets = new Vector3[floorCount];

            for (var level = 0; level < floorCount; level++)
            {
                int floorW, floorD;
                if (level < settings.SkyscraperShrinkStartFloor)
                {
                    floorW = width;
                    floorD = depth;
                }
                else
                {
                    var steps = level - settings.SkyscraperShrinkStartFloor + 1;
                    const int skyscraperMinCells = 3;
                    floorW = Mathf.Max(skyscraperMinCells, width - steps * settings.SkyscraperShrinkStep);
                    floorD = Mathf.Max(skyscraperMinCells, depth - steps * settings.SkyscraperShrinkStep);
                }

                context.Plan.FloorWidths[level] = floorW;
                context.Plan.FloorDepths[level] = floorD;
                context.Plan.FloorOffsets[level] = new Vector3(
                    (width - floorW) * CellSize * 0.5f,
                    0f,
                    (depth - floorD) * CellSize * 0.5f);
            }

            context.Plan.MinFootprintWidth = context.Plan.FloorWidths[floorCount - 1];
            context.Plan.MinFootprintDepth = context.Plan.FloorDepths[floorCount - 1];
            context.Plan.EffectiveStairCount = floorCount - 1;
            context.Plan.NeedsStairs = context.Plan.EffectiveStairCount > 0;
        }

        private static int ResolveRandomSeed(GeneratorSettings settings)
        {
            if (settings.UseFixedRandomSeed)
                return settings.RandomSeed;

            return unchecked(System.Environment.TickCount ^ (int)DateTime.UtcNow.Ticks);
        }

        private static bool TryComputeGenerationPlan(GenerationContext context, out string errorMessage)
        {
            context.Plan.StairTransitions = Array.Empty<StairTransition>();
            context.Plan.SkipUpperLandingFloors.Clear();

            if (context.Plan.NeedsStairs)
            {
                // Plan stairs within the minimum footprint so every stair cell fits on every floor,
                // including the smallest shrunken floors at the top. Cells are reused (stacked)
                // when the grid is smaller than the transition count.
                var stairPlanW = context.Plan.MinFootprintWidth;
                var stairPlanD = context.Plan.MinFootprintDepth;

                if (!TryPlanStairLocations(stairPlanW, stairPlanD, context.Plan.EffectiveStairCount,
                        context.Random, out var stairTransitions))
                {
                    var need = context.Plan.EffectiveStairCount;
                    errorMessage =
                        $"Could not place {need} stairs in {stairPlanW}x{stairPlanD} grid. Try a larger base footprint or reduce Shrink Step so the minimum floor has more cells.";
                    return false;
                }

                context.Plan.StairTransitions = stairTransitions;
            }

            context.Plan.SkipUpperLandingFloors = BuildStairUpperLandingSkipSet(
                context.Plan.StairTransitions,
                context.Plan);

            // Place rooms FIRST so we can tell which perimeter cells face blocking rooms
            PlanFloorRooms(context);

            // Resolve the district category BEFORE wall planning — commercial buildings
            // swap their ground-floor storefront for shop glass (front side only).
            context.BuildingCategory = context.ActiveTemplate != null
                ? context.ActiveTemplate.LotType
                : context.Settings.BuildingCategoryOverride;

            // Pick front doors only from perimeter segments facing non-blocking room types
            // (e.g. not bedrooms). Bedrooms can still have exterior walls — just not doors.
            var segmentsPerLevel = 2 * context.Plan.Width + 2 * context.Plan.Depth;
            var doorCount = Mathf.Clamp(context.Settings.GroundDoorCount, 0, segmentsPerLevel);

            context.Plan.CornerStorefront = context.BuildingCategory == CityDistrictType.Commercial
                                            && context.ActiveTemplate != null
                                            && context.ActiveTemplate.CornerStorefront;
            context.Plan.CornerSideIsLeft = context.Random.Value01() < 0.5f;

            if (IsCommercialSnapType(context))
            {
                if (context.Plan.CornerStorefront)
                {
                    // Corner shops pin their single door to the corner-adjacent
                    // storefront segment so the L-shaped glass reads as one entrance.
                    context.Plan.GroundDoorIndices = doorCount > 0
                        ? new HashSet<int> { context.Plan.CornerSideIsLeft ? 0 : context.Plan.Width - 1 }
                        : s_EmptyIntSet;
                }
                else
                {
                    // Snapping commercial buildings keep their ground door on the storefront
                    // row (segments 0..Width-1) so the shop-glass run always composes on
                    // the street side around the door — never on a side or back wall.
                    var storefrontSegments = new HashSet<int>();
                    var storefrontCount = Mathf.Min(context.Plan.Width, segmentsPerLevel);
                    for (var i = 0; i < storefrontCount; i++)
                        storefrontSegments.Add(i);
                    context.Plan.GroundDoorIndices = PickUniqueIndicesFromSet(
                        storefrontSegments, Mathf.Min(storefrontCount, doorCount), context.Random);
                }
            }
            else
            {
                context.Plan.GroundDoorIndices = PickNonBlockingDoorIndices(
                    doorCount, segmentsPerLevel, context.Plan, context.Settings.RoomSettings, context.Random);
            }

            Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Placing {context.Plan.GroundDoorIndices.Count} ground door(s) " +
                      $"(requested {doorCount}, segments={segmentsPerLevel})");

            PlanPerimeterWalls(context);

            // Footprint subfolder groups buildings by grid size:
            //   <OutputFolder>/<District>/<Width>x<Depth>/<District>_N.prefab
            var footprintSubFolder = $"{context.Plan.Width}x{context.Plan.Depth}";
            var categoryOutputFolder =
                $"{context.OutputFolder.TrimEnd('/')}/{context.BuildingCategory}/{footprintSubFolder}";
            EnsureFolderExists(categoryOutputFolder);

            if (context.ActiveTemplate != null)
            {
                // Template-driven names read '<District>_<Template>_<W>x<D>',
                // e.g. 'Commercial_Kitchen_5x4' or 'Residential_2_Bedroom_6x4'.
                var templateBaseName = ModularBuildingCategoryResolver.FormatTemplatePrefabBaseName(
                    context.BuildingCategory,
                    context.ActiveTemplate.DisplayName,
                    context.Plan.Width,
                    context.Plan.Depth);
                context.AssetFileName = ResolveUniqueTemplateFileName(templateBaseName, categoryOutputFolder);
            }
            else
            {
                var buildingNumber = NextRandomBuildingNumber(
                    context.OutputFolder,
                    context.BuildingCategory.ToString(),
                    footprintSubFolder,
                    context.Random);
                context.AssetFileName = ModularBuildingCategoryResolver.FormatPrefabBaseName(
                    context.BuildingCategory,
                    buildingNumber);
            }

            context.AssetPath = $"{categoryOutputFolder}/{context.AssetFileName}.prefab";

            errorMessage = null;
            return true;
        }

        /// <summary>
        ///     Appends _2, _3, … to <paramref name="baseName"/> until no prefab with the
        ///     resulting name exists in <paramref name="outputFolder"/>.
        /// </summary>
        private static string ResolveUniqueTemplateFileName(string baseName, string outputFolder)
        {
            var candidate = baseName;
            for (var suffix = 2; suffix <= 999; suffix++)
            {
                if (AssetDatabase.LoadMainAssetAtPath($"{outputFolder}/{candidate}.prefab") == null)
                    return candidate;
                candidate = $"{baseName}_{suffix}";
            }

            return $"{baseName}_{DateTime.UtcNow.Ticks}";
        }

        /// <summary>
        ///     Picks front door indices only from perimeter segments that face non-blocking
        ///     room types (rooms where BlocksFrontDoor = false). Falls back to allowing any
        ///     segment only when zero non-blocking candidates exist.
        /// </summary>
        private static HashSet<int> PickNonBlockingDoorIndices(
            int doorCount, int totalSegments,
            GenerationPlan plan, RoomSettings roomSettings, GenerationRandom random)
        {
            if (doorCount <= 0 || totalSegments <= 0)
                return s_EmptyIntSet;

            // Build the set of perimeter segments that face a non-blocking room.
            var nonBlockingSegments = FindNonBlockingPerimeterSegments(plan, roomSettings, totalSegments);

            // Pick as many non-blocking segments as possible, capping at the requested doorCount.
            if (nonBlockingSegments.Count > 0)
                return PickUniqueIndicesFromSet(nonBlockingSegments,
                    Mathf.Min(nonBlockingSegments.Count, doorCount), random);

            // No non-blocking segments at all — last-resort fallback (should be rare with proper templates).
            return PickUniqueIndices(totalSegments, doorCount, random);
        }

        /// <summary>
        ///     Returns the set of perimeter segment indices whose adjacent interior cell
        ///     belongs to a room where BlocksFrontDoor = false.
        /// </summary>
        private static HashSet<int> FindNonBlockingPerimeterSegments(
            GenerationPlan plan, RoomSettings roomSettings, int totalSegments)
        {
            var result = new HashSet<int>();
            if (roomSettings == null) return result;

            var roomTypeAtCell = BuildRoomCellLookupForDoors(plan);
            if (roomTypeAtCell == null) return result;

            var gridW = roomTypeAtCell.GetLength(0);
            var gridD = roomTypeAtCell.GetLength(1);
            var perimeter = BuildPerimeterSegments(gridW, gridD, 0f, Vector3.zero);

            for (var i = 0; i < perimeter.Count && i < totalSegments; i++)
            {
                var blocked = false;
                foreach (RoomType rt in System.Enum.GetValues(typeof(RoomType)))
                {
                    if (!roomSettings.BlocksFrontDoor(rt)) continue;
                    if (IsSegmentFacingRoom(perimeter[i], roomTypeAtCell, gridW, gridD, rt))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                    result.Add(i);
            }

            return result;
        }

        /// <summary>
        ///     Picks <paramref name="count"/> unique random indices from the given set.
        /// </summary>
        private static HashSet<int> PickUniqueIndicesFromSet(
            HashSet<int> pool, int count, GenerationRandom random)
        {
            var result = new HashSet<int>();
            if (pool.Count == 0) return result;

            var list = new System.Collections.Generic.List<int>(pool);
            count = Mathf.Clamp(count, 0, list.Count);
            for (var pick = 0; pick < count; pick++)
            {
                var idx = random.RangeExclusive(0, list.Count);
                result.Add(list[idx]);
                list.RemoveAt(idx);
            }

            return result;
        }

        /// <summary>
        ///     Returns true if the perimeter wall segment faces a room of the specified type.
        /// </summary>
        private static bool IsSegmentFacingRoom(
            WallSegment segment,
            RoomType?[,] roomTypeAtCell,
            int gridW,
            int gridD,
            RoomType targetRoomType)
        {
            var pos = segment.Position;
            var rot = segment.Rotation;

            // Determine which direction this wall segment faces (inward)
            var inward = rot * Vector3.forward; // forward = inward for perimeter walls

            // The wall sits on the cell boundary — step INWARD by half a cell to
            // land on the adjacent interior cell's center.
            var cellX = Mathf.FloorToInt((pos.x + inward.x * CellSize * 0.5f) / CellSize);
            var cellZ = Mathf.FloorToInt((pos.z + inward.z * CellSize * 0.5f) / CellSize);

            if (cellX < 0 || cellX >= gridW || cellZ < 0 || cellZ >= gridD)
                return false;

            return roomTypeAtCell[cellX, cellZ] == targetRoomType;
        }

        /// <summary>
        ///     Builds a per-cell RoomType lookup from the ground-floor room plan for
        ///     front-door placement decisions.
        /// </summary>
        private static RoomType?[,] BuildRoomCellLookupForDoors(GenerationPlan plan)
        {
            foreach (var floorPlan in plan.FloorRoomPlans)
            {
                if (floorPlan.Level != 0) continue;

                var gridW = floorPlan.FloorPlanWidth;
                var gridD = floorPlan.FloorPlanDepth;
                var lookup = new RoomType?[gridW, gridD];

                FillRoomLookupFromFloorPlan(floorPlan, gridW, gridD, lookup);
                return lookup;
            }

            return null;
        }

        private static void FillRoomLookupFromFloorPlan(
            FloorRoomPlan floorPlan, int gridW, int gridD, RoomType?[,] lookup)
        {
            var roomAt = floorPlan.RoomAt;
            if (roomAt != null && roomAt.GetLength(0) == gridW && roomAt.GetLength(1) == gridD)
                FillRoomLookupFromGrid(floorPlan.Rooms, roomAt, gridW, gridD, lookup);
            else
                FillRoomLookupFromBounds(floorPlan.Rooms, gridW, gridD, lookup);
        }

        private static void FillRoomLookupFromGrid(
            System.Collections.Generic.List<RoomRegion> rooms,
            int[,] roomAt, int gridW, int gridD, RoomType?[,] lookup)
        {
            for (var x = 0; x < gridW; x++)
                for (var z = 0; z < gridD; z++)
                {
                    var ri = roomAt[x, z];
                    if (ri >= 0 && ri < rooms.Count)
                        lookup[x, z] = rooms[ri].RoomType;
                }
        }

        private static void FillRoomLookupFromBounds(
            System.Collections.Generic.List<RoomRegion> rooms,
            int gridW, int gridD, RoomType?[,] lookup)
        {
            for (var ri = 0; ri < rooms.Count; ri++)
            {
                var r = rooms[ri];
                for (var x = r.Ix0; x < r.Ix1; x++)
                    for (var z = r.Iz0; z < r.Iz1; z++)
                        if (x < gridW && z < gridD)
                            lookup[x, z] = r.RoomType;
            }
        }

        /// <summary>
        ///     Resolves the active <see cref="ResidentialHouseTemplate"/> from the settings'
        ///     ArchetypeLibrary and SelectedTemplateIndex. Returns null when no library is
        ///     assigned or the index is out of range (legacy mode).
        /// </summary>
        private static ResidentialHouseTemplate ResolveActiveTemplate(GeneratorSettings settings)
        {
            if (settings.ArchetypeLibrary == null)
                return null;

            var templates = settings.ArchetypeLibrary.GetTemplatesForLotType(
                settings.BuildingCategoryOverride);

            if (templates.Length == 0)
                return null;

            var idx = Mathf.Clamp(settings.SelectedTemplateIndex, 0, templates.Length - 1);
            return templates[idx];
        }

        /// <summary>
        ///     Picks a random <see cref="RoofTypeConfig"/> from the template's <see cref="ResidentialHouseTemplate.AllowedRoofTypes"/>.
        ///     Commercial buildings always resolve to a flat/parapet config (see <see cref="ResolveCommercialRoofType"/>).
        ///     Returns null when the template is null or has no allowed roof types (falls back to BuildingKitConfig defaults).
        /// </summary>
        private static RoofTypeConfig ResolveRoofType(ResidentialHouseTemplate template, GenerationRandom random, CityDistrictType category)
        {
            if (category == CityDistrictType.Commercial)
                return ResolveCommercialRoofType(template, random);

            if (template == null)
                return null;

            var allowed = template.AllowedRoofTypes;
            if (allowed == null || allowed.Length == 0)
            {
                Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Template '{template.DisplayName}' has no allowed roof types — using BuildingKitConfig default.");
                return null;
            }

            // Filter out null entries (unassigned slots in the inspector array).
            var valid = new System.Collections.Generic.List<RoofTypeConfig>(allowed.Length);
            for (var i = 0; i < allowed.Length; i++)
                if (allowed[i] != null)
                    valid.Add(allowed[i]);

            if (valid.Count == 0)
            {
                Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Template '{template.DisplayName}' has {allowed.Length} roof type slot(s), all null — using BuildingKitConfig default.");
                return null;
            }

            var idx = Mathf.FloorToInt(random.Value01() * valid.Count);
            var picked = valid[idx];

            Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Picked roof type '{picked.DisplayName}' (style={picked.Style}) — roll {idx} of {valid.Count} valid entries (from {allowed.Length} slots) for template '{template.DisplayName}'.");

            return picked;
        }

        /// <summary>
        ///     Commercial buildings only get flat or parapet roofs. Prefers the template's
        ///     flat-style entries, otherwise picks from the shared Flat roof-type folder.
        /// </summary>
        private static RoofTypeConfig ResolveCommercialRoofType(ResidentialHouseTemplate template, GenerationRandom random)
        {
            var flat = new List<RoofTypeConfig>();
            if (template != null && template.AllowedRoofTypes != null)
            {
                for (var i = 0; i < template.AllowedRoofTypes.Length; i++)
                {
                    var entry = template.AllowedRoofTypes[i];
                    if (entry != null && entry.Style == RoofStyle.Flat)
                        flat.Add(entry);
                }
            }

            if (flat.Count == 0)
            {
                var guids = AssetDatabase.FindAssets("t:RoofTypeConfig", new[] { CommercialRoofTypesFolder });
                foreach (var guid in guids)
                {
                    var cfg = AssetDatabase.LoadAssetAtPath<RoofTypeConfig>(AssetDatabase.GUIDToAssetPath(guid));
                    if (cfg != null && cfg.Style == RoofStyle.Flat)
                        flat.Add(cfg);
                }
            }

            if (flat.Count == 0)
            {
                Debug.LogWarning(
                    $"[ModularSingleLevelHouseGeneratorTool] No flat/parapet roof configs found for commercial in '{CommercialRoofTypesFolder}' — falling back to kit defaults.");
                return null;
            }

            var picked = flat[Mathf.FloorToInt(random.Value01() * flat.Count)];
            Debug.Log($"[ModularSingleLevelHouseGeneratorTool] Picked commercial roof '{picked.DisplayName}' (flat/parapet) — {flat.Count} available.");
            return picked;
        }
    }
}
#endif
