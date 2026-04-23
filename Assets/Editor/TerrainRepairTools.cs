using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    /// Utilities to repair terrain authoring issues caused by read-only data or bad material overrides.
    /// </summary>
    public static class TerrainRepairTools
    {
        private const string TerrainDataOutputFolder = "Assets/Generated/TerrainData";
        private const string GrassLayerPath = "Assets/grass_layer.terrainlayer";
        private const string DirtLayerPath = "Assets/dirt_layer.terrainlayer";
        private const string UrpTerrainLitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat";

        [MenuItem("Tools/Zombera/World/Terrain/Fix Selected Terrain Rendering")]
        private static void FixSelectedTerrainRendering()
        {
            List<Terrain> terrains = CollectSelectedTerrains();

            if (terrains.Count <= 0)
            {
                EditorUtility.DisplayDialog(
                    "No Terrain Selected",
                    "Select one or more Terrain objects in the hierarchy and run this command again.",
                    "OK");
                return;
            }

            int processed = 0;
            int clonedTerrainData = 0;
            int clearedMaterialOverrides = 0;
            int updatedLayerSets = 0;
            int filledAlphaMaps = 0;
            int normalizedRenderFlags = 0;
            int clearedHoleMaps = 0;
            int forcedVisibilityFills = 0;

            try
            {
                for (int i = 0; i < terrains.Count; i++)
                {
                    Terrain terrain = terrains[i];
                    if (terrain == null)
                    {
                        continue;
                    }

                    string progressLabel = $"Processing {i + 1}/{terrains.Count}: {terrain.name}";
                    if (EditorUtility.DisplayCancelableProgressBar("Fix Selected Terrain Rendering", progressLabel, (i + 1f) / terrains.Count))
                    {
                        break;
                    }

                    processed++;

                    if (!TryEnsureWritableTerrainData(terrain, out TerrainData terrainData, out bool cloned))
                    {
                        continue;
                    }

                    if (cloned)
                    {
                        clonedTerrainData++;
                    }

                    if (ClearMaterialOverride(terrain))
                    {
                        clearedMaterialOverrides++;
                    }

                    if (EnsurePreferredLayers(terrainData))
                    {
                        updatedLayerSets++;
                    }

                    if (EnsureVisibleAlphamap(terrainData))
                    {
                        filledAlphaMaps++;
                    }

                    if (ForceVisibilityFillIfMostlyEmpty(terrainData))
                    {
                        forcedVisibilityFills++;
                    }

                    if (NormalizeTerrainRenderFlags(terrain))
                    {
                        normalizedRenderFlags++;
                    }

                    if (EnsureNoTerrainHoles(terrainData))
                    {
                        clearedHoleMaps++;
                    }

                    terrain.Flush();

                    if (terrain.gameObject.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();

            Debug.Log(
                "[TerrainRepairTools] Terrain repair complete. " +
                $"Processed={processed}, ClonedData={clonedTerrainData}, ClearedMaterialOverrides={clearedMaterialOverrides}, " +
                $"UpdatedLayers={updatedLayerSets}, FilledBlankAlphamaps={filledAlphaMaps}, " +
                $"ForcedVisibilityFills={forcedVisibilityFills}, NormalizedRenderFlags={normalizedRenderFlags}, ClearedHoleMaps={clearedHoleMaps}.");
        }

        [MenuItem("Tools/Zombera/World/Terrain/Fix Selected Terrain Rendering", true)]
        private static bool ValidateFixSelectedTerrainRendering()
        {
            return Selection.activeObject != null;
        }

        [MenuItem("Tools/Zombera/World/Terrain/Force Apply Grass (Unconditional)")]
        private static void ForceApplyGrassUnconditional()
        {
            List<Terrain> terrains = CollectSelectedTerrains();

            if (terrains.Count <= 0)
            {
                EditorUtility.DisplayDialog(
                    "No Terrain Selected",
                    "Select one or more Terrain objects in the hierarchy and run this command again.",
                    "OK");
                return;
            }

            int filled = 0;

            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                if (!TryEnsureWritableTerrainData(terrain, out TerrainData data, out _))
                {
                    continue;
                }

                if (data.terrainLayers == null || data.terrainLayers.Length == 0)
                {
                    EnsurePreferredLayers(data);
                }

                int layerCount = data.terrainLayers != null ? data.terrainLayers.Length : 0;
                if (layerCount == 0)
                {
                    Debug.LogWarning($"[TerrainRepairTools] {terrain.name}: no terrain layers assigned, cannot fill.");
                    continue;
                }

                int w = data.alphamapWidth;
                int h = data.alphamapHeight;

                float[,,] fill = new float[h, w, layerCount];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        fill[y, x, 0] = 1f;
                    }
                }

                Undo.RecordObject(data, "Force Grass Alphamap");
                data.SetAlphamaps(0, 0, fill);
                EditorUtility.SetDirty(data);

                terrain.Flush();

                if (terrain.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                }

                filled++;
                Debug.Log($"[TerrainRepairTools] Force-filled alphamap on '{terrain.name}' ({w}x{h}, {layerCount} layers).");
            }

            AssetDatabase.SaveAssets();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();

            Debug.Log($"[TerrainRepairTools] Force Apply Grass complete. FilledTerrains={filled}.");
        }

        [MenuItem("Tools/Zombera/World/Terrain/Force Apply Grass (Unconditional)", true)]
        private static bool ValidateForceApplyGrassUnconditional()
        {
            return Selection.activeObject != null;
        }

        [MenuItem("Tools/Zombera/World/Terrain/Rebake Terrain Basemap")]
        private static void RebakeTerrainBasemap()
        {
            List<Terrain> terrains = CollectSelectedTerrains();

            if (terrains.Count <= 0)
            {
                EditorUtility.DisplayDialog(
                    "No Terrain Selected",
                    "Select one or more Terrain objects in the hierarchy and run this command again.",
                    "OK");
                return;
            }

            int rebaked = 0;

            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                TerrainData data = terrain.terrainData;
                if (data == null)
                {
                    continue;
                }

                // Cycling basemapDistance forces Unity to discard the cached basemap and rebake
                // splat layers at the new distance. Without this, Unity may show a stale
                // pre-baked basemap that was generated before layers were assigned.
                float original = terrain.basemapDistance;
                float cycle = original > 0f ? 0f : 1024f;

                Undo.RecordObject(terrain, "Rebake Terrain Basemap");
                terrain.basemapDistance = cycle;
                EditorUtility.SetDirty(terrain);
                terrain.basemapDistance = original > 0f ? original : 1024f;
                EditorUtility.SetDirty(terrain);

                // Refresh prototypes so splatmap textures are re-read from disk.
                data.RefreshPrototypes();

                terrain.Flush();

                if (terrain.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                }

                rebaked++;
            }

            AssetDatabase.SaveAssets();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();

            Debug.Log($"[TerrainRepairTools] Basemap rebake complete. Rebaked={rebaked}.");
        }

        [MenuItem("Tools/Zombera/World/Terrain/Rebake Terrain Basemap", true)]
        private static bool ValidateRebakeTerrainBasemap()
        {
            return Selection.activeObject != null;
        }

        // -------------------------------------------------------------------------
        // Scene environment diagnostic — finds fog, ambient, volumes, camera renderer
        // -------------------------------------------------------------------------

        [MenuItem("Tools/Zombera/World/Terrain/Diagnose Scene Lighting & Volumes")]
        private static void DiagnoseSceneLightingAndVolumes()
        {
            // --- Fog ---
            string fogInfo = $"Enabled={RenderSettings.fog}, Color={RenderSettings.fogColor}, " +
                             $"Mode={RenderSettings.fogMode}, Density={RenderSettings.fogDensity:F4}, " +
                             $"StartDistance={RenderSettings.fogStartDistance:F1}, EndDistance={RenderSettings.fogEndDistance:F1}";

            // --- Ambient ---
            string ambientInfo = $"Mode={RenderSettings.ambientMode}, " +
                                 $"SkyColor={RenderSettings.ambientSkyColor}, " +
                                 $"EquatorColor={RenderSettings.ambientEquatorColor}, " +
                                 $"GroundColor={RenderSettings.ambientGroundColor}, " +
                                 $"Intensity={RenderSettings.ambientIntensity:F2}";

            // --- Skybox ---
            string skyboxInfo = RenderSettings.skybox != null
                ? $"'{RenderSettings.skybox.name}' shader='{RenderSettings.skybox.shader.name}'"
                : "none";

            // --- Post-processing volumes ---
            // Use v.profile (not v.sharedProfile) so instanced profiles are also inspected.
            UnityEngine.Rendering.Volume[] volumes = UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(UnityEngine.FindObjectsSortMode.None);
            System.Text.StringBuilder volSb = new System.Text.StringBuilder();
            foreach (UnityEngine.Rendering.Volume v in volumes)
            {
                string profileName = v.sharedProfile != null ? v.sharedProfile.name : "(instanced – no shared asset)";
                volSb.Append($"\n    [{v.name}] IsGlobal={v.isGlobal}, Priority={v.priority}, Profile='{profileName}'");
                // v.profile reads the active profile (instanced or shared), creating an instance if needed.
                UnityEngine.Rendering.VolumeProfile activeProfile = v.sharedProfile != null ? v.sharedProfile : (v.HasInstantiatedProfile() ? v.profile : null);
                if (activeProfile != null && activeProfile.components != null)
                {
                    foreach (UnityEngine.Rendering.VolumeComponent comp in activeProfile.components)
                    {
                        volSb.Append($"\n      - {comp.GetType().Name} active={comp.active}");
                    }
                }
                else
                {
                    volSb.Append("\n      (no profile components readable)");
                }
            }
            string volumeInfo = volumes.Length == 0 ? "none" : volSb.ToString();

            // --- Cameras ---
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(UnityEngine.FindObjectsSortMode.None);
            System.Text.StringBuilder camSb = new System.Text.StringBuilder();
            foreach (Camera cam in cameras)
            {
                string bgMode = cam.clearFlags.ToString();
                string bgColor = cam.backgroundColor.ToString();
                camSb.Append($"\n    [{cam.name}] ClearFlags={bgMode}, BgColor={bgColor}, CullingMask={cam.cullingMask}");
                UnityEngine.Rendering.Universal.UniversalAdditionalCameraData urpData =
                    cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (urpData != null)
                {
                    string rendererName = urpData.scriptableRenderer != null ? urpData.scriptableRenderer.GetType().Name : "null";
                    camSb.Append($", Renderer={rendererName}");
                }
            }
            string cameraInfo = cameras.Length == 0 ? "none" : camSb.ToString();

            Debug.Log(
                "[TerrainRepairTools] Scene Lighting Diagnostic =>" +
                $"\n  FOG: {fogInfo}" +
                $"\n  AMBIENT: {ambientInfo}" +
                $"\n  SKYBOX: {skyboxInfo}" +
                $"\n  VOLUMES ({volumes.Length}): {volumeInfo}" +
                $"\n  CAMERAS ({cameras.Length}): {cameraInfo}");
        }

        [MenuItem("Tools/Zombera/World/Terrain/Toggle Scene Fog (Test)")]
        private static void ToggleSceneFog()
        {
            bool wasEnabled = RenderSettings.fog;
            RenderSettings.fog = !wasEnabled;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            Debug.Log($"[TerrainRepairTools] Fog toggled: {wasEnabled} -> {RenderSettings.fog}. " +
                      "If terrain looks correct now, fog color/density is the cause of the blue tint.");
        }

        [MenuItem("Tools/Zombera/World/Terrain/Diagnose Selected Terrain")]
        private static void DiagnoseSelectedTerrain()
        {
            List<Terrain> terrains = CollectSelectedTerrains();

            if (terrains.Count <= 0)
            {
                EditorUtility.DisplayDialog(
                    "No Terrain Selected",
                    "Select one or more Terrain objects in the hierarchy and run this command again.",
                    "OK");
                return;
            }

            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                TerrainData data = terrain.terrainData;
                string dataPath = data != null ? AssetDatabase.GetAssetPath(data) : "(none)";
                string materialPath = terrain.materialTemplate != null
                    ? AssetDatabase.GetAssetPath(terrain.materialTemplate)
                    : "(none)";

                int layerCount = data != null && data.terrainLayers != null ? data.terrainLayers.Length : 0;

                string holeInfo = "unknown";
                if (data != null)
                {
                    try
                    {
                        // true = solid (no hole), false = hole.
                        bool[,] oneHole = data.GetHoles(0, 0, 1, 1);
                        bool isSolid = oneHole != null && oneHole.Length > 0 && oneHole[0, 0];
                        holeInfo = isSolid ? "solid-at-0,0" : "HOLE-at-0,0";
                    }
                    catch
                    {
                        holeInfo = "holes-api-unavailable";
                    }
                }

                // Sample alphamap at 5 points: corners + center.
                string alphamapInfo = "no-data";
                if (data != null && layerCount > 0 && data.alphamapWidth > 0 && data.alphamapHeight > 0)
                {
                    int aw = data.alphamapWidth;
                    int ah = data.alphamapHeight;
                    int[,] sampleCoords =
                    {
                        { 0, 0 }, { aw - 1, 0 }, { 0, ah - 1 }, { aw - 1, ah - 1 }, { aw / 2, ah / 2 }
                    };
                    string[] sampleLabels = { "BL", "BR", "TL", "TR", "C" };
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int s = 0; s < sampleCoords.GetLength(0); s++)
                    {
                        float[,,] p = data.GetAlphamaps(sampleCoords[s, 0], sampleCoords[s, 1], 1, 1);
                        sb.Append(sampleLabels[s]).Append("=[");
                        for (int l = 0; l < layerCount; l++)
                        {
                            if (l > 0) sb.Append(',');
                            sb.Append(p[0, 0, l].ToString("F2"));
                        }
                        sb.Append("] ");
                    }
                    alphamapInfo = sb.ToString().TrimEnd();
                }

                // Log layer names.
                string layerNames = "none";
                if (data != null && data.terrainLayers != null && data.terrainLayers.Length > 0)
                {
                    var names = new System.Text.StringBuilder();
                    for (int l = 0; l < data.terrainLayers.Length; l++)
                    {
                        if (l > 0) names.Append(", ");
                        names.Append(data.terrainLayers[l] != null ? data.terrainLayers[l].name : "null");
                    }
                    layerNames = names.ToString();
                }

                Debug.Log(
                    "[TerrainRepairTools] Terrain Diagnostic => " +
                    $"Name='{terrain.name}', Scene='{terrain.gameObject.scene.name}', Enabled={terrain.enabled}, " +
                    $"DrawHeightmap={terrain.drawHeightmap}, DrawTrees={terrain.drawTreesAndFoliage}, DrawInstanced={terrain.drawInstanced}, " +
                    $"MaterialTemplate='{materialPath}', TerrainData='{dataPath}', " +
                    $"Layers={layerCount} ({layerNames}), Holes={holeInfo}, Size={(data != null ? data.size.ToString() : "(none)")}, " +
                    $"AlphamapSize={( data != null ? $"{data.alphamapWidth}x{data.alphamapHeight}" : "none")}, AlphamapSamples={alphamapInfo}",
                    terrain);
            }
        }

        [MenuItem("Tools/Zombera/World/Terrain/Diagnose Selected Terrain", true)]
        private static bool ValidateDiagnoseSelectedTerrain()
        {
            return Selection.activeObject != null;
        }

        private static List<Terrain> CollectSelectedTerrains()
        {
            HashSet<Terrain> unique = new HashSet<Terrain>();
            GameObject[] selected = Selection.gameObjects;

            for (int i = 0; i < selected.Length; i++)
            {
                GameObject selectedObject = selected[i];
                if (selectedObject == null)
                {
                    continue;
                }

                Terrain direct = selectedObject.GetComponent<Terrain>();
                if (direct != null)
                {
                    unique.Add(direct);
                }

                Terrain[] children = selectedObject.GetComponentsInChildren<Terrain>(includeInactive: true);
                for (int c = 0; c < children.Length; c++)
                {
                    if (children[c] != null)
                    {
                        unique.Add(children[c]);
                    }
                }
            }

            return new List<Terrain>(unique);
        }

        private static bool TryEnsureWritableTerrainData(Terrain terrain, out TerrainData terrainData, out bool cloned)
        {
            terrainData = terrain != null ? terrain.terrainData : null;
            cloned = false;

            if (terrain == null || terrainData == null)
            {
                return false;
            }

            string dataAssetPath = AssetDatabase.GetAssetPath(terrainData);
            bool isAssetsPath = !string.IsNullOrWhiteSpace(dataAssetPath) && dataAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
            bool isReadOnlyAsset = isAssetsPath && IsAssetReadOnly(dataAssetPath);
            bool needsClone = !isAssetsPath || isReadOnlyAsset;

            if (!needsClone)
            {
                return true;
            }

            EnsureFolderPath(TerrainDataOutputFolder);

            string safeName = string.IsNullOrWhiteSpace(terrain.name) ? "Terrain" : terrain.name;
            string clonePath = AssetDatabase.GenerateUniqueAssetPath($"{TerrainDataOutputFolder}/{safeName}_TerrainData.asset");

            TerrainData clonedData = UnityEngine.Object.Instantiate(terrainData);
            AssetDatabase.CreateAsset(clonedData, clonePath);

            Undo.RecordObject(terrain, "Assign Writable Terrain Data");
            terrain.terrainData = clonedData;
            EditorUtility.SetDirty(terrain);

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                Undo.RecordObject(terrainCollider, "Assign Writable Terrain Data");
                terrainCollider.terrainData = clonedData;
                EditorUtility.SetDirty(terrainCollider);
            }

            terrainData = clonedData;
            cloned = true;
            return true;
        }

        private static bool ClearMaterialOverride(Terrain terrain)
        {
            if (terrain == null || terrain.materialTemplate == null)
            {
                return false;
            }

            // Preserve valid URP or render-pipeline package materials — these are required,
            // not erroneous overrides. Only wipe materials assigned from the project's Assets/
            // folder that could be leftover custom overrides.
            string matPath = AssetDatabase.GetAssetPath(terrain.materialTemplate);
            bool isPackageMaterial = !string.IsNullOrEmpty(matPath) &&
                (matPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                 matPath.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase));

            if (isPackageMaterial)
            {
                return false;
            }

            Undo.RecordObject(terrain, "Clear Terrain Material Override");
            terrain.materialTemplate = null;
            EditorUtility.SetDirty(terrain);
            return true;
        }

        private static bool EnsurePreferredLayers(TerrainData terrainData)
        {
            if (terrainData == null)
            {
                return false;
            }

            TerrainLayer grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerPath);
            TerrainLayer dirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DirtLayerPath);

            if (grass == null && dirt == null)
            {
                return false;
            }

            TerrainLayer[] existing = terrainData.terrainLayers ?? Array.Empty<TerrainLayer>();
            List<TerrainLayer> merged = new List<TerrainLayer>(existing.Length + 2);

            for (int i = 0; i < existing.Length; i++)
            {
                TerrainLayer layer = existing[i];
                if (layer != null && !merged.Contains(layer))
                {
                    merged.Add(layer);
                }
            }

            if (grass != null && !merged.Contains(grass))
            {
                merged.Add(grass);
            }

            if (dirt != null && !merged.Contains(dirt))
            {
                merged.Add(dirt);
            }

            bool changed = merged.Count != existing.Length;
            if (!changed)
            {
                return false;
            }

            Undo.RecordObject(terrainData, "Assign Terrain Layers");
            terrainData.terrainLayers = merged.ToArray();
            EditorUtility.SetDirty(terrainData);
            return true;
        }

        private static bool EnsureVisibleAlphamap(TerrainData terrainData)
        {
            if (terrainData == null || terrainData.terrainLayers == null || terrainData.terrainLayers.Length <= 0)
            {
                return false;
            }

            int width = terrainData.alphamapWidth;
            int height = terrainData.alphamapHeight;
            int layerCount = terrainData.terrainLayers.Length;

            if (width <= 0 || height <= 0 || layerCount <= 0)
            {
                return false;
            }

            float[,,] probe = terrainData.GetAlphamaps(0, 0, 1, 1);
            float sum = 0f;

            for (int layer = 0; layer < layerCount; layer++)
            {
                sum += probe[0, 0, layer];
            }

            if (sum > 0.0001f)
            {
                return false;
            }

            float[,,] fill = new float[height, width, layerCount];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    fill[y, x, 0] = 1f;
                }
            }

            Undo.RecordObject(terrainData, "Fill Terrain Alphamap");
            terrainData.SetAlphamaps(0, 0, fill);
            EditorUtility.SetDirty(terrainData);
            return true;
        }

        private static bool ForceVisibilityFillIfMostlyEmpty(TerrainData terrainData)
        {
            if (terrainData == null || terrainData.terrainLayers == null || terrainData.terrainLayers.Length <= 0)
            {
                return false;
            }

            int width = terrainData.alphamapWidth;
            int height = terrainData.alphamapHeight;
            int layerCount = terrainData.terrainLayers.Length;

            if (width <= 0 || height <= 0 || layerCount <= 0)
            {
                return false;
            }

            int sampleCount = 0;
            int emptySamples = 0;
            int[] xSamples =
            {
                0,
                Mathf.Max(0, width / 2),
                Mathf.Max(0, width - 1)
            };

            int[] ySamples =
            {
                0,
                Mathf.Max(0, height / 2),
                Mathf.Max(0, height - 1)
            };

            for (int yi = 0; yi < ySamples.Length; yi++)
            {
                for (int xi = 0; xi < xSamples.Length; xi++)
                {
                    float[,,] sample = terrainData.GetAlphamaps(xSamples[xi], ySamples[yi], 1, 1);
                    float sum = 0f;

                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        sum += sample[0, 0, layer];
                    }

                    sampleCount++;
                    if (sum <= 0.0001f)
                    {
                        emptySamples++;
                    }
                }
            }

            // If most samples have no layer weight at all, force-fill layer 0 so terrain is visible.
            if (emptySamples < Mathf.Max(7, sampleCount - 1))
            {
                return false;
            }

            float[,,] fill = new float[height, width, layerCount];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    fill[y, x, 0] = 1f;
                }
            }

            Undo.RecordObject(terrainData, "Force Terrain Visibility Fill");
            terrainData.SetAlphamaps(0, 0, fill);
            EditorUtility.SetDirty(terrainData);
            return true;
        }

        private static bool NormalizeTerrainRenderFlags(Terrain terrain)
        {
            if (terrain == null)
            {
                return false;
            }

            bool changed = false;

            Undo.RecordObject(terrain, "Normalize Terrain Render Flags");

            // For URP: if no materialTemplate is set, assign TerrainLit.mat.
            // materialType is obsolete in modern Unity — setting materialTemplate is sufficient.
            if (terrain.materialTemplate == null)
            {
                Material urpLit = AssetDatabase.LoadAssetAtPath<Material>(UrpTerrainLitPath);
                if (urpLit != null)
                {
                    terrain.materialTemplate = urpLit;
                    changed = true;
                }
            }

            if (!terrain.enabled)
            {
                terrain.enabled = true;
                changed = true;
            }

            if (!terrain.drawHeightmap)
            {
                terrain.drawHeightmap = true;
                changed = true;
            }

            if (!terrain.drawTreesAndFoliage)
            {
                terrain.drawTreesAndFoliage = true;
                changed = true;
            }

            if (terrain.basemapDistance < 128f)
            {
                terrain.basemapDistance = 1024f;
                changed = true;
            }

            if (terrain.heightmapPixelError > 20f)
            {
                terrain.heightmapPixelError = 5f;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(terrain);
            }

            return changed;
        }

        private static bool EnsureNoTerrainHoles(TerrainData terrainData)
        {
            if (terrainData == null)
            {
                return false;
            }

            int width;
            int height;
            bool[,] holes;

            try
            {
                width = terrainData.holesResolution;
                height = terrainData.holesResolution;

                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                holes = terrainData.GetHoles(0, 0, width, height);
            }
            catch
            {
                return false;
            }

            if (holes == null || holes.Length == 0)
            {
                return false;
            }

            // Unity convention: true = solid (no hole), false = hole (invisible terrain).
            bool hasAnyHole = false;
            for (int y = 0; y < height && !hasAnyHole; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!holes[y, x])  // false = hole
                    {
                        hasAnyHole = true;
                        break;
                    }
                }
            }

            if (!hasAnyHole)
            {
                return false;
            }

            // Fill entirely with true = solid (no holes anywhere).
            bool[,] solid = new bool[height, width];
            for (int sy = 0; sy < height; sy++)
            {
                for (int sx = 0; sx < width; sx++)
                {
                    solid[sy, sx] = true;
                }
            }

            Undo.RecordObject(terrainData, "Clear Terrain Holes");
            terrainData.SetHoles(0, 0, solid);
            EditorUtility.SetDirty(terrainData);
            return true;
        }

        private static bool IsAssetReadOnly(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            FileAttributes attributes = File.GetAttributes(fullPath);
            return (attributes & FileAttributes.ReadOnly) != 0;
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
