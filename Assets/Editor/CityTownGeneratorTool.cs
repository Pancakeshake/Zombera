using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.BuildingSystem;
using Zombera.World;

namespace Zombera.Editor
{
    /// <summary>
    /// Phase 1 city tooling: catalog authoring, selected-area generation, and destructible stamping.
    /// </summary>
    public static class CityTownGeneratorTool
    {
        private const string CatalogFolderPath = "Assets/ScriptableObjects/World";
        private const string CatalogDefaultAssetName = "TownPrefabCatalog.asset";
        private const string GeneratedTownRootPrefix = "GeneratedTown";
        private const int ProgressUpdateStride = 64;
        private const int UndoPerObjectThreshold = 1500;

        private static readonly string[] BuildingVendorFolders =
        {
            "Assets/Assets/Gameready3D/NYC_Building/Prefabs/Building_Modular"
        };

        private static readonly string[] PropVendorFolders =
        {
            "Assets/GameReady3D/Post_Apocalyptic_Asset_Pack/Prefabs"
        };

        [MenuItem("Tools/Zombera/City/Create Town Catalog Asset")]
        private static void CreateTownCatalogAsset()
        {
            EnsureFolderPath(CatalogFolderPath);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{CatalogFolderPath}/{CatalogDefaultAssetName}");
            TownPrefabCatalog catalog = ScriptableObject.CreateInstance<TownPrefabCatalog>();

            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = catalog;

            int choice = EditorUtility.DisplayDialogComplex(
                "Town Catalog Created",
                "Populate this catalog from the vendor packs now?",
                "Populate",
                "Later",
                "Populate + Keep Existing");

            if (choice == 0)
            {
                PopulateCatalogFromVendorFolders(catalog, clearExisting: true, out int added, out int skippedFolders);
                Debug.Log($"[CityTownGeneratorTool] Created and populated catalog: added {added} prefabs, skipped folders {skippedFolders}.", catalog);
            }
            else if (choice == 2)
            {
                PopulateCatalogFromVendorFolders(catalog, clearExisting: false, out int added, out int skippedFolders);
                Debug.Log($"[CityTownGeneratorTool] Created and appended vendor prefabs: added {added}, skipped folders {skippedFolders}.", catalog);
            }
        }

        [MenuItem("Tools/Zombera/City/Populate Selected Catalog From Vendor Packs")]
        private static void PopulateSelectedCatalogFromVendorPacks()
        {
            TownPrefabCatalog catalog = Selection.activeObject as TownPrefabCatalog;
            if (catalog == null)
            {
                EditorUtility.DisplayDialog(
                    "Town Catalog Required",
                    "Select a TownPrefabCatalog asset first.",
                    "OK");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Populate Catalog",
                "Replace existing entries or append to them?",
                "Replace",
                "Cancel",
                "Append");

            if (choice == 1)
            {
                return;
            }

            bool clearExisting = choice == 0;
            PopulateCatalogFromVendorFolders(catalog, clearExisting, out int added, out int skippedFolders);

            string mode = clearExisting ? "replaced" : "appended";
            Debug.Log($"[CityTownGeneratorTool] Catalog {mode}: added {added} prefabs, skipped folders {skippedFolders}.", catalog);
        }

        [MenuItem("Tools/Zombera/City/Populate Selected Catalog From Vendor Packs", true)]
        private static bool ValidatePopulateSelectedCatalogFromVendorPacks()
        {
            return Selection.activeObject is TownPrefabCatalog;
        }

        [MenuItem("Tools/Zombera/City/Generate Town (Selected Area)")]
        private static void GenerateTownSelectedArea()
        {
            GameObject areaObject = Selection.activeGameObject;
            if (areaObject == null)
            {
                EditorUtility.DisplayDialog("Area Required", "Select a scene object that defines the area.", "OK");
                return;
            }

            if (!TryResolveCatalog(out TownPrefabCatalog catalog))
            {
                EditorUtility.DisplayDialog(
                    "Catalog Missing",
                    "No TownPrefabCatalog found. Create one via Tools/Zombera/City/Create Town Catalog Asset.",
                    "OK");
                return;
            }

            if (catalog.entries == null || catalog.entries.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Catalog Empty",
                    "The selected catalog has no entries. Populate it before generating.",
                    "OK");
                Selection.activeObject = catalog;
                return;
            }

            if (!TryGetAreaBounds(areaObject, out Bounds areaBounds, out bool usedFallbackBounds))
            {
                EditorUtility.DisplayDialog("Invalid Area", "Could not resolve area bounds from selection.", "OK");
                return;
            }

            if (usedFallbackBounds)
            {
                Debug.LogWarning("[CityTownGeneratorTool] Selection had no Terrain/Collider/Renderer bounds; using fallback 64x64 area around selection.", areaObject);
            }

            string rootName = $"{GeneratedTownRootPrefix}_{areaObject.name}";
            GameObject existingRoot = FindRootObjectByName(areaObject.scene, rootName);
            if (existingRoot != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Generated Town Exists",
                    $"A generated root named '{rootName}' already exists in this scene. Replace it?",
                    "Replace",
                    "Cancel");

                if (!replace)
                {
                    return;
                }

                Undo.DestroyObjectImmediate(existingRoot);
            }

            float lotSize = Mathf.Max(2f, catalog.lotSize);
            float minX = areaBounds.min.x + catalog.edgePadding;
            float maxX = areaBounds.max.x - catalog.edgePadding;
            float minZ = areaBounds.min.z + catalog.edgePadding;
            float maxZ = areaBounds.max.z - catalog.edgePadding;

            if (maxX - minX < lotSize || maxZ - minZ < lotSize)
            {
                EditorUtility.DisplayDialog(
                    "Area Too Small",
                    "The selected area is too small for the current catalog lot size and edge padding.",
                    "OK");
                return;
            }

            int xLots = ComputeLotCountPerAxis(minX, maxX, lotSize);
            int zLots = ComputeLotCountPerAxis(minZ, maxZ, lotSize);
            int totalLots = xLots * zLots;

            if (totalLots <= 0)
            {
                EditorUtility.DisplayDialog(
                    "No Lots To Generate",
                    "The selected area and lot size produced zero valid lots.",
                    "OK");
                return;
            }

            int confirmThreshold = Mathf.Max(100, catalog.confirmLargeLotThreshold);
            if (totalLots >= confirmThreshold)
            {
                bool proceedLarge = EditorUtility.DisplayDialog(
                    "Large Generation",
                    $"Estimated lots: {totalLots:N0}.\nLot size: {lotSize:0.##}m\n\nThis may take a while. Continue?",
                    "Continue",
                    "Cancel");

                if (!proceedLarge)
                {
                    return;
                }
            }

            int maxLotsPerRun = Mathf.Max(500, catalog.maxLotsPerRun);
            if (totalLots > maxLotsPerRun)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Large Area Detected",
                    $"Estimated lots ({totalLots:N0}) exceed max lots per run ({maxLotsPerRun:N0}).\n\nClamp this run (faster), cancel, or run full (can stall editor).",
                    "Clamp This Run",
                    "Cancel",
                    "Run Full");

                if (choice == 1)
                {
                    return;
                }

                bool shouldClamp = choice == 0 || (choice != 2 && catalog.autoClampLargeAreas);
                if (shouldClamp)
                {
                    float scale = Mathf.Sqrt(totalLots / (float)maxLotsPerRun);
                    lotSize *= Mathf.Max(1f, scale);

                    xLots = ComputeLotCountPerAxis(minX, maxX, lotSize);
                    zLots = ComputeLotCountPerAxis(minZ, maxZ, lotSize);
                    totalLots = xLots * zLots;

                    Debug.LogWarning(
                        $"[CityTownGeneratorTool] Clamped generation for this run. New lotSize={lotSize:0.##}, EstimatedLots={totalLots:N0}.");
                }
            }

            Terrain samplingTerrain = ResolveSamplingTerrain(areaObject, areaBounds);
            bool usePerObjectUndo = totalLots <= UndoPerObjectThreshold;

            if (!usePerObjectUndo)
            {
                Debug.LogWarning(
                    $"[CityTownGeneratorTool] Fast mode enabled for large generation ({totalLots:N0} lots). Per-object Undo disabled to reduce editor stalls.");
            }

            System.Random rng = new System.Random(catalog.generationSeed);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            GameObject generatedRoot = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(generatedRoot, "Generate Town Root");
            SceneManager.MoveGameObjectToScene(generatedRoot, areaObject.scene);

            int lotsVisited = 0;
            int lotsSkippedNoEntry = 0;
            int placedCount = 0;

            bool cancelledByUser = false;

            try
            {
                for (int xi = 0; xi < xLots && !cancelledByUser; xi++)
                {
                    float x = minX + xi * lotSize;

                    for (int zi = 0; zi < zLots; zi++)
                    {
                        float z = minZ + zi * lotSize;
                        lotsVisited++;

                        if (!TryPickLotCategory(catalog, rng, out TownPrefabCategory category))
                        {
                            if (ShouldCancelFromProgress(lotsVisited, totalLots, placedCount))
                            {
                                cancelledByUser = true;
                                break;
                            }

                            continue;
                        }

                        if (!catalog.TryGetWeightedEntry(category, rng, out TownPrefabEntry entry))
                        {
                            lotsSkippedNoEntry++;

                            if (ShouldCancelFromProgress(lotsVisited, totalLots, placedCount))
                            {
                                cancelledByUser = true;
                                break;
                            }

                            continue;
                        }

                        GameObject instance = PrefabUtility.InstantiatePrefab(entry.prefab) as GameObject;
                        if (instance == null)
                        {
                            if (ShouldCancelFromProgress(lotsVisited, totalLots, placedCount))
                            {
                                cancelledByUser = true;
                                break;
                            }

                            continue;
                        }

                        if (usePerObjectUndo)
                        {
                            Undo.RegisterCreatedObjectUndo(instance, "Place Town Prefab");
                        }

                        Vector3 center = new Vector3(x + lotSize * 0.5f, areaBounds.center.y, z + lotSize * 0.5f);
                        center = ApplyRandomOffset(center, catalog.randomPositionOffset, rng);
                        center.y = ResolveGroundHeight(center, areaBounds, samplingTerrain);

                        float yaw = ResolvePlacementYaw(catalog.randomYawJitterDegrees, rng);
                        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

                        instance.transform.SetPositionAndRotation(center, rotation);
                        instance.transform.SetParent(generatedRoot.transform, worldPositionStays: true);

                        ApplyEntryDestructible(instance, entry, usePerObjectUndo);
                        placedCount++;

                        if (ShouldCancelFromProgress(lotsVisited, totalLots, placedCount))
                        {
                            cancelledByUser = true;
                            break;
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (placedCount == 0)
            {
                Undo.DestroyObjectImmediate(generatedRoot);
                EditorUtility.DisplayDialog(
                    "No Placements",
                    "Generation completed but no prefabs were placed. Check catalog entries and lot distribution settings.",
                    "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(areaObject.scene);
            Selection.activeGameObject = generatedRoot;

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[CityTownGeneratorTool] Generated town root '{generatedRoot.name}'. " +
                $"Lots={lotsVisited}/{totalLots}, Placed={placedCount}, MissingCategoryEntries={lotsSkippedNoEntry}, " +
                $"Seed={catalog.generationSeed}, Cancelled={(cancelledByUser ? "yes" : "no")}.",
                generatedRoot);

            if (cancelledByUser)
            {
                EditorUtility.DisplayDialog(
                    "Generation Cancelled",
                    "Town generation was cancelled by user. The partial result has been kept in scene.",
                    "OK");
            }
        }

        [MenuItem("Tools/Zombera/City/Generate Town (Selected Area)", true)]
        private static bool ValidateGenerateTownSelectedArea()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem("Tools/Zombera/City/Stamp Destructibles (Selected Root)")]
        private static void StampDestructiblesSelectedRoot()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Root Required", "Select a scene root object first.", "OK");
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("No Renderers Found", "Selected root has no renderers to stamp.", "OK");
                return;
            }

            HashSet<GameObject> targets = new HashSet<GameObject>();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                GameObject nearestPrefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
                GameObject target = nearestPrefabRoot != null && nearestPrefabRoot.transform.IsChildOf(root.transform)
                    ? nearestPrefabRoot
                    : renderer.gameObject;

                if (target.scene.IsValid())
                {
                    targets.Add(target);
                }
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("No Targets", "No scene objects were found to stamp.", "OK");
                return;
            }

            int touchedObjects = 0;

            foreach (GameObject target in targets)
            {
                bool touched = AddOrUpdateDestructible(
                    target,
                    maxHealth: 120f,
                    destroyOnDeath: true,
                    addBuildPiece: true,
                    category: BuildPieceCategory.Other);

                if (touched)
                {
                    touchedObjects++;
                }
            }

            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log(
                $"[CityTownGeneratorTool] Stamped destructible components under '{root.name}'. " +
                $"Targets={targets.Count}, NewlyAdded={touchedObjects}.",
                root);
        }

        [MenuItem("Tools/Zombera/City/Stamp Destructibles (Selected Root)", true)]
        private static bool ValidateStampDestructiblesSelectedRoot()
        {
            return Selection.activeGameObject != null;
        }

        private static void PopulateCatalogFromVendorFolders(TownPrefabCatalog catalog, bool clearExisting, out int added, out int skippedFolders)
        {
            added = 0;
            skippedFolders = 0;

            if (catalog == null)
            {
                return;
            }

            Undo.RecordObject(catalog, "Populate Town Catalog");

            if (clearExisting)
            {
                catalog.entries.Clear();
            }

            HashSet<GameObject> knownPrefabs = new HashSet<GameObject>();
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                TownPrefabEntry existing = catalog.entries[i];
                if (existing != null && existing.prefab != null)
                {
                    knownPrefabs.Add(existing.prefab);
                }
            }

            added += AddEntriesFromFolders(
                catalog,
                knownPrefabs,
                BuildingVendorFolders,
                defaultCategory: TownPrefabCategory.Building,
                defaultPieceCategory: BuildPieceCategory.Wall,
                defaultHealth: 260f,
                defaultWeight: 1f,
                ref skippedFolders);

            added += AddEntriesFromFolders(
                catalog,
                knownPrefabs,
                PropVendorFolders,
                defaultCategory: TownPrefabCategory.Prop,
                defaultPieceCategory: BuildPieceCategory.Other,
                defaultHealth: 90f,
                defaultWeight: 0.8f,
                ref skippedFolders);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static int AddEntriesFromFolders(
            TownPrefabCatalog catalog,
            HashSet<GameObject> knownPrefabs,
            string[] folders,
            TownPrefabCategory defaultCategory,
            BuildPieceCategory defaultPieceCategory,
            float defaultHealth,
            float defaultWeight,
            ref int skippedFolders)
        {
            int added = 0;

            for (int folderIndex = 0; folderIndex < folders.Length; folderIndex++)
            {
                string folder = folders[folderIndex];

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    skippedFolders++;
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                Array.Sort(guids, StringComparer.Ordinal);

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (prefab == null || knownPrefabs.Contains(prefab))
                    {
                        continue;
                    }

                    ResolveCategoryAndDurability(
                        path,
                        defaultCategory,
                        defaultPieceCategory,
                        defaultHealth,
                        out TownPrefabCategory resolvedCategory,
                        out BuildPieceCategory resolvedPieceCategory,
                        out float resolvedHealth,
                        out float resolvedWeight);

                    TownPrefabEntry entry = new TownPrefabEntry
                    {
                        id = prefab.name,
                        prefab = prefab,
                        category = resolvedCategory,
                        weight = resolvedWeight,
                        structureMaxHealth = resolvedHealth,
                        destroyOnDeath = true,
                        addBuildPiece = true,
                        buildPieceCategory = resolvedPieceCategory
                    };

                    catalog.entries.Add(entry);
                    knownPrefabs.Add(prefab);
                    added++;
                }
            }

            return added;
        }

        private static void ResolveCategoryAndDurability(
            string assetPath,
            TownPrefabCategory defaultCategory,
            BuildPieceCategory defaultPieceCategory,
            float defaultHealth,
            out TownPrefabCategory category,
            out BuildPieceCategory pieceCategory,
            out float health,
            out float weight)
        {
            string pathLower = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath.ToLowerInvariant();

            category = defaultCategory;
            pieceCategory = defaultPieceCategory;
            health = defaultHealth;
            weight = 1f;

            if (pathLower.Contains("building_modular") || pathLower.Contains("living_quarters_modular"))
            {
                category = TownPrefabCategory.Building;
                pieceCategory = BuildPieceCategory.Wall;
                health = Mathf.Max(defaultHealth, 220f);
                weight = 1f;
                return;
            }

            if (pathLower.Contains("fence_modular") || pathLower.Contains("pipe_modular"))
            {
                category = TownPrefabCategory.Utility;
                pieceCategory = BuildPieceCategory.Utility;
                health = Mathf.Max(defaultHealth, 150f);
                weight = 0.9f;
                return;
            }

            category = defaultCategory;
            pieceCategory = defaultPieceCategory;
            health = defaultHealth;
            weight = defaultCategory == TownPrefabCategory.Prop ? 0.8f : 1f;
        }

        private static bool TryResolveCatalog(out TownPrefabCatalog catalog)
        {
            catalog = null;

            if (Selection.activeObject is TownPrefabCatalog selectedCatalog)
            {
                catalog = selectedCatalog;
                return true;
            }

            string[] catalogGuids = AssetDatabase.FindAssets("t:TownPrefabCatalog");
            if (catalogGuids.Length <= 0)
            {
                return false;
            }

            string firstPath = AssetDatabase.GUIDToAssetPath(catalogGuids[0]);
            catalog = AssetDatabase.LoadAssetAtPath<TownPrefabCatalog>(firstPath);

            if (catalog == null)
            {
                return false;
            }

            if (catalogGuids.Length > 1)
            {
                Debug.LogWarning($"[CityTownGeneratorTool] Multiple TownPrefabCatalog assets found. Using '{firstPath}'. Select another catalog asset to override.");
            }

            return true;
        }

        private static bool TryGetAreaBounds(GameObject areaObject, out Bounds bounds, out bool usedFallbackBounds)
        {
            usedFallbackBounds = false;
            bounds = default;

            if (areaObject == null)
            {
                return false;
            }

            Terrain terrain = areaObject.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 terrainSize = terrain.terrainData.size;
                bounds = new Bounds(terrain.transform.position + terrainSize * 0.5f, terrainSize);
                return true;
            }

            Collider collider = areaObject.GetComponent<Collider>();
            if (collider != null)
            {
                bounds = collider.bounds;
                return true;
            }

            Renderer[] renderers = areaObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;

                for (int i = 1; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                }

                return true;
            }

            usedFallbackBounds = true;
            bounds = new Bounds(areaObject.transform.position, new Vector3(64f, 20f, 64f));
            return true;
        }

        private static GameObject FindRootObjectByName(Scene scene, string rootName)
        {
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(rootName))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && string.Equals(root.name, rootName, StringComparison.Ordinal))
                {
                    return root;
                }
            }

            return null;
        }

        private static bool TryPickLotCategory(TownPrefabCatalog catalog, System.Random random, out TownPrefabCategory category)
        {
            category = TownPrefabCategory.Building;

            if (catalog == null || random == null)
            {
                return false;
            }

            float building = Mathf.Max(0f, catalog.buildingLotChance);
            float prop = Mathf.Max(0f, catalog.propLotChance);
            float utility = Mathf.Max(0f, catalog.utilityLotChance);
            float empty = Mathf.Max(0f, catalog.emptyLotChance);

            float total = building + prop + utility + empty;
            if (total <= 0f)
            {
                return false;
            }

            double roll = random.NextDouble() * total;

            if (roll < building)
            {
                category = TownPrefabCategory.Building;
                return true;
            }

            roll -= building;
            if (roll < prop)
            {
                category = TownPrefabCategory.Prop;
                return true;
            }

            roll -= prop;
            if (roll < utility)
            {
                category = TownPrefabCategory.Utility;
                return true;
            }

            return false;
        }

        private static Vector3 ApplyRandomOffset(Vector3 position, float maxOffset, System.Random random)
        {
            if (random == null || maxOffset <= 0f)
            {
                return position;
            }

            float offsetX = Mathf.Lerp(-maxOffset, maxOffset, (float)random.NextDouble());
            float offsetZ = Mathf.Lerp(-maxOffset, maxOffset, (float)random.NextDouble());
            return new Vector3(position.x + offsetX, position.y, position.z + offsetZ);
        }

        private static float ResolvePlacementYaw(float yawJitterDegrees, System.Random random)
        {
            float baseYaw = random != null ? random.Next(0, 4) * 90f : 0f;

            if (random == null || yawJitterDegrees <= 0f)
            {
                return baseYaw;
            }

            float jitter = Mathf.Lerp(-yawJitterDegrees, yawJitterDegrees, (float)random.NextDouble());
            return baseYaw + jitter;
        }

        private static int ComputeLotCountPerAxis(float minValue, float maxValue, float lotSize)
        {
            float span = maxValue - minValue;

            if (lotSize <= 0f || span < lotSize)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.FloorToInt(span / lotSize));
        }

        private static Terrain ResolveSamplingTerrain(GameObject areaObject, Bounds areaBounds)
        {
            if (areaObject != null)
            {
                Terrain selectedTerrain = areaObject.GetComponent<Terrain>();
                if (selectedTerrain != null && selectedTerrain.terrainData != null)
                {
                    return selectedTerrain;
                }
            }

            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain == null || activeTerrain.terrainData == null)
            {
                return null;
            }

            Vector3 terrainSize = activeTerrain.terrainData.size;
            Bounds terrainBounds = new Bounds(activeTerrain.transform.position + terrainSize * 0.5f, terrainSize);
            return terrainBounds.Intersects(areaBounds) ? activeTerrain : null;
        }

        private static bool ShouldCancelFromProgress(int processedLots, int totalLots, int placedCount)
        {
            if (totalLots <= 0)
            {
                return false;
            }

            bool shouldUpdate = processedLots % ProgressUpdateStride == 0 || processedLots >= totalLots;
            if (!shouldUpdate)
            {
                return false;
            }

            float progress = Mathf.Clamp01(processedLots / (float)totalLots);
            return EditorUtility.DisplayCancelableProgressBar(
                "Generate Town (Selected Area)",
                $"Processing lots {processedLots:N0}/{totalLots:N0} | Placed {placedCount:N0}",
                progress);
        }

        private static float ResolveGroundHeight(Vector3 position, Bounds areaBounds, Terrain preferredTerrain)
        {
            if (preferredTerrain != null && preferredTerrain.terrainData != null)
            {
                return preferredTerrain.SampleHeight(position) + preferredTerrain.transform.position.y;
            }

            float rayStartY = areaBounds.max.y + 500f;
            Vector3 rayOrigin = new Vector3(position.x, rayStartY, position.z);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5000f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null && activeTerrain.terrainData != null)
            {
                return activeTerrain.SampleHeight(position) + activeTerrain.transform.position.y;
            }

            return areaBounds.center.y;
        }

        private static void ApplyEntryDestructible(GameObject instance, TownPrefabEntry entry, bool useUndo)
        {
            if (instance == null || entry == null)
            {
                return;
            }

            AddOrUpdateDestructible(
                instance,
                Mathf.Max(1f, entry.structureMaxHealth),
                entry.destroyOnDeath,
                entry.addBuildPiece,
                entry.buildPieceCategory,
                useUndo);
        }

        private static bool AddOrUpdateDestructible(
            GameObject target,
            float maxHealth,
            bool destroyOnDeath,
            bool addBuildPiece,
            BuildPieceCategory category,
            bool useUndo = true)
        {
            if (target == null)
            {
                return false;
            }

            bool addedAny = false;

            StructureHealth health = target.GetComponent<StructureHealth>();
            if (health == null)
            {
                health = useUndo
                    ? Undo.AddComponent<StructureHealth>(target)
                    : target.AddComponent<StructureHealth>();
                addedAny = true;
            }

            if (health != null)
            {
                if (useUndo)
                {
                    Undo.RecordObject(health, "Configure StructureHealth");
                }

                health.SetMaxHealth(Mathf.Max(1f, maxHealth), refillCurrentHealth: true);
                health.SetDestroyGameObjectOnDeath(destroyOnDeath);
                health.ResetHealthToMax();
                EditorUtility.SetDirty(health);
            }

            if (!addBuildPiece)
            {
                return addedAny;
            }

            BuildPiece buildPiece = target.GetComponent<BuildPiece>();
            if (buildPiece == null)
            {
                buildPiece = useUndo
                    ? Undo.AddComponent<BuildPiece>(target)
                    : target.AddComponent<BuildPiece>();
                addedAny = true;
            }

            if (buildPiece != null)
            {
                if (useUndo)
                {
                    Undo.RecordObject(buildPiece, "Configure BuildPiece");
                }

                buildPiece.SetCategory(category);

                if (category == BuildPieceCategory.Wall)
                {
                    buildPiece.SetWallType(GuessWallTypeFromName(target.name));
                }

                EditorUtility.SetDirty(buildPiece);
            }

            return addedAny;
        }

        private static WallPieceType GuessWallTypeFromName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return WallPieceType.Full;
            }

            string lowered = objectName.ToLowerInvariant();

            if (lowered.Contains("window"))
            {
                return WallPieceType.Window;
            }

            if (lowered.Contains("door"))
            {
                return WallPieceType.Door;
            }

            if (lowered.Contains("damaged") || lowered.Contains("broken"))
            {
                return WallPieceType.Damaged;
            }

            return WallPieceType.Full;
        }

        private static void EnsureFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || folderPath == "Assets")
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
