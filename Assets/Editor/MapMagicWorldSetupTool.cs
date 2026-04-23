using System;
using System.Collections.Generic;
using System.IO;
using Den.Tools;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.World;

namespace Zombera.Editor
{
    /// <summary>
    /// Configures a scene for MapMagic streaming using a chosen graph asset.
    /// </summary>
    public static class MapMagicWorldSetupTool
    {
        private const string TargetGraphAssetPath = "Assets/ThirdParty/MapMagic/Zombera.asset";
        private const string TargetGraphAssetName = "Zombera";
        private const string MinuteIslandInfiniteGraphAssetPath = "Assets/ThirdParty/MapMagic/Demo/Graphs/MinuteIslandInfinite.asset";
        private const string TargetScenePath = "Assets/Scenes/World_MapMagicStream.unity";
        private const string TargetSceneName = "World_MapMagicStream";
        private const string TargetMapMagicObjectName = "MapMAgic";

        private const string TerrainLayersFolderPath = "Assets/Terrain/Layers";
        private const string RockLayerPath = "Assets/Terrain/Layers/Rock.terrainlayer";
        private const string GrassLayerPath = "Assets/Terrain/Layers/Grass.terrainlayer";
        private const string SnowLayerPath = "Assets/Terrain/Layers/Snow.terrainlayer";

        private static readonly string[] RequiredLayerDisplayNames = { "Rock", "Grass", "Snow" };

        [MenuItem("Tools/Zombera/World/MapMagic/Setup MapMagic World (Plains Preset + Streaming Defaults)")]
        public static void SetupZomberaMapMagicWorld()
        {
            if (!TryResolveSetupAssets(
                out Graph graph,
                out string graphPath,
                out TerrainLayer[] requiredLayers,
                out string layersSummary))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string streamScenePath = ResolveTargetScenePath();
            Scene targetScene;
            if (string.IsNullOrEmpty(streamScenePath))
            {
                targetScene = SceneManager.GetActiveScene();
                if (!targetScene.IsValid() || !targetScene.isLoaded)
                {
                    EditorUtility.DisplayDialog(
                        "MapMagic Streaming Setup",
                        $"Could not resolve target scene. Checked '{TargetScenePath}' and searched by scene name '{TargetSceneName}', and the active scene is not valid.",
                        "OK");
                    return;
                }

                Debug.LogWarning(
                    $"[MapMagicWorldSetupTool] Could not find '{TargetScenePath}' (or a scene named '{TargetSceneName}'). " +
                    $"Applying setup to the active scene instead: '{targetScene.path}'.");
            }
            else
            {
                targetScene = EditorSceneManager.OpenScene(streamScenePath, OpenSceneMode.Single);
            }

            Undo.RecordObject(graph, "Rebuild MapMagic Graph Wiring (MinuteIslandInfinite Clone)");
            int graphPresetChanges = RebuildGraphWiringMinuteIslandClonePreset(
                graph,
                requiredLayers,
                out int generatorsCreated,
                out int textureOutputsCreated,
                out int textureInletsWired);

            if (graphPresetChanges > 0)
            {
                EditorUtility.SetDirty(graph);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string graphPresetSummary =
                "[MapMagicWorldSetupTool] Graph preset rebuilt (MinuteIslandInfinite clone).\n" +
                $"  Graph: {graphPath}\n" +
                $"  Generators created: {generatorsCreated}\n" +
                $"  Texture outputs created: {textureOutputsCreated}\n" +
                $"  Texture layer inlets wired: {textureInletsWired}";

            Debug.Log(graphPresetSummary, graph);

            string streamingSummary = ApplyStreamingGraphSetupToScene(
                targetScene,
                graph,
                requiredLayers,
                $"target graph ({graphPath})",
                layersSummary,
                showSummaryDialog: false);

            string combinedSummary = graphPresetSummary + "\n\n" + streamingSummary;

            EditorUtility.DisplayDialog("MapMagic World Setup", combinedSummary, "OK");
        }

        [MenuItem("Tools/Zombera/World/NavMesh/Apply High Coverage + Fewer Holes Defaults (Active Scene)")]
        public static void ApplyHighCoverageNavMeshDefaultsInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                EditorUtility.DisplayDialog(
                    "NavMesh Defaults",
                    "Active scene is not valid or not loaded.",
                    "OK");
                return;
            }

            int playerSpawnerChanges = ApplyPlayerSpawnerMapMagicDefaults(activeScene);
            int navMeshServiceChanges = ApplyStreamingNavMeshTileServiceDefaults(activeScene);

            if (playerSpawnerChanges > 0 || navMeshServiceChanges > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                AssetDatabase.SaveAssets();
            }

            string summary =
                "Applied high-coverage/fewer-holes NavMesh defaults.\n" +
                $"Scene: {activeScene.name}\n" +
                $"PlayerSpawner fields changed: {playerSpawnerChanges}\n" +
                $"StreamingNavMeshTileService fields changed: {navMeshServiceChanges}";

            Debug.Log($"[MapMagicWorldSetupTool] {summary}");
            EditorUtility.DisplayDialog("NavMesh Defaults", summary, "OK");
        }

        private static string ApplyStreamingGraphSetupToScene(
            Scene scene,
            Graph graph,
            TerrainLayer[] requiredLayers,
            string sourceLabel,
            string layersSummary,
            bool showSummaryDialog = true)
        {
            if (graph == null)
            {
                return string.Empty;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog(
                    "MapMagic Streaming Setup",
                    "Target scene is not valid or not loaded.",
                    "OK");
                return string.Empty;
            }

            int graphChanges = ApplyGraphEditorDefaults(graph);
            int textureLayerChanges = AssignRequiredTerrainLayersToAllTextureOutputs(
                graph,
                requiredLayers,
                out int textureOutputsFound);

            MapMagicObject mapMagic = EnsureMapMagicObject(
                scene,
                graph,
                out bool createdMapMagic,
                out bool renamedMapMagicObject,
                out bool assignedGraph,
                out int disabledExtraMapMagic);

            int mapMagicChanges = mapMagic != null ? ApplyMapMagicStreamingDefaults(mapMagic) : 0;
            int playerSpawnerChanges = ApplyPlayerSpawnerMapMagicDefaults(scene);
            int navMeshServiceChanges = ApplyStreamingNavMeshTileServiceDefaults(scene);

            if (mapMagic != null)
            {
                EditorUtility.SetDirty(mapMagic);

                try
                {
                    mapMagic.ApplyTerrainSettings();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[MapMagicWorldSetupTool] Could not apply terrain settings after setup: {exception.Message}");
                }
            }

            if (graphChanges > 0 || textureLayerChanges > 0)
            {
                EditorUtility.SetDirty(graph);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string graphPath = AssetDatabase.GetAssetPath(graph);
            string summary =
                "[MapMagicWorldSetupTool] Streaming graph setup complete.\n" +
                $"  Scene: {scene.name}\n" +
                $"  Graph source: {sourceLabel}\n" +
                $"  Graph asset: {graphPath}\n" +
                $"  Required layers: {layersSummary}\n" +
                $"  Graph editor fields changed: {graphChanges}\n" +
                $"  Texture outputs found: {textureOutputsFound}\n" +
                $"  Terrain layer prototype fields changed: {textureLayerChanges}\n" +
                $"  MapMagic created: {createdMapMagic}\n" +
                $"  MapMagic renamed to '{TargetMapMagicObjectName}': {renamedMapMagicObject}\n" +
                $"  Graph assigned/updated: {assignedGraph}\n" +
                $"  Extra MapMagic objects disabled: {disabledExtraMapMagic}\n" +
                $"  MapMagic streaming fields changed: {mapMagicChanges}\n" +
                $"  PlayerSpawner MapMagic fields changed: {playerSpawnerChanges}\n" +
                $"  StreamingNavMeshTileService fields changed: {navMeshServiceChanges}";

            UnityEngine.Object context = mapMagic != null ? mapMagic : graph;
            if (textureOutputsFound == 0)
            {
                Debug.LogWarning(
                    "[MapMagicWorldSetupTool] No TexturesOutput200 nodes were found in Zombera graph/sub-graphs, so Rock/Grass/Snow prototypes were not assigned.",
                    context);
            }

            Debug.Log(summary, context);
            if (showSummaryDialog)
            {
                EditorUtility.DisplayDialog("MapMagic Streaming Setup", summary, "OK");
            }

            return summary;
        }

        private static bool TryResolveSetupAssets(
            out Graph graph,
            out string graphPath,
            out TerrainLayer[] requiredLayers,
            out string layersSummary)
        {
            graph = ResolveTargetGraph(out graphPath);
            requiredLayers = null;
            layersSummary = string.Empty;

            if (graph == null)
            {
                EditorUtility.DisplayDialog(
                    "MapMagic Streaming Setup",
                    $"Could not resolve target graph. Checked '{TargetGraphAssetPath}' and searched by graph name '{TargetGraphAssetName}'.",
                    "OK");
                return false;
            }

            if (!TryResolveRequiredTerrainLayers(
                out requiredLayers,
                out layersSummary,
                out string layersError))
            {
                EditorUtility.DisplayDialog(
                    "MapMagic Streaming Setup",
                    layersError,
                    "OK");
                return false;
            }

            return true;
        }

        private static Graph ResolveTargetGraph(out string resolvedPath)
        {
            resolvedPath = TargetGraphAssetPath;

            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(TargetGraphAssetPath);
            if (graph != null)
            {
                return graph;
            }

            string[] graphGuids = AssetDatabase.FindAssets($"{TargetGraphAssetName} t:Graph", new[] { "Assets/ThirdParty/MapMagic" });
            Graph fallback = null;

            for (int i = 0; i < graphGuids.Length; i++)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(graphGuids[i]);
                Graph candidateGraph = AssetDatabase.LoadAssetAtPath<Graph>(candidatePath);
                if (candidateGraph == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidateGraph;
                    resolvedPath = candidatePath;
                }

                string fileName = Path.GetFileNameWithoutExtension(candidatePath);
                if (string.Equals(fileName, TargetGraphAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedPath = candidatePath;
                    return candidateGraph;
                }
            }

            return fallback;
        }

        private static string ResolveTargetScenePath()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) != null)
            {
                return TargetScenePath;
            }

            string[] sceneGuids = AssetDatabase.FindAssets($"{TargetSceneName} t:Scene", new[] { "Assets/Scenes" });
            string fallbackPath = null;

            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(candidatePath) == null)
                {
                    continue;
                }

                if (fallbackPath == null)
                {
                    fallbackPath = candidatePath;
                }

                string fileName = Path.GetFileNameWithoutExtension(candidatePath);
                if (string.Equals(fileName, TargetSceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidatePath;
                }
            }

            return fallbackPath;
        }

        private static bool TryResolveRequiredTerrainLayers(
            out TerrainLayer[] requiredLayers,
            out string layersSummary,
            out string errorMessage)
        {
            requiredLayers = new TerrainLayer[3];
            string[] layerPaths = new string[3];

            requiredLayers[0] = ResolveTerrainLayer(RockLayerPath, RequiredLayerDisplayNames[0], out layerPaths[0]);
            requiredLayers[1] = ResolveTerrainLayer(GrassLayerPath, RequiredLayerDisplayNames[1], out layerPaths[1]);
            requiredLayers[2] = ResolveTerrainLayer(SnowLayerPath, RequiredLayerDisplayNames[2], out layerPaths[2]);

            for (int i = 0; i < requiredLayers.Length; i++)
            {
                if (requiredLayers[i] != null)
                {
                    continue;
                }

                layersSummary = string.Empty;
                errorMessage =
                    $"Required terrain layer '{RequiredLayerDisplayNames[i]}' could not be resolved. " +
                    $"Checked '{GetLayerPathByIndex(i)}' and searched in '{TerrainLayersFolderPath}'.";
                return false;
            }

            layersSummary =
                $"{RequiredLayerDisplayNames[0]}={layerPaths[0]}, " +
                $"{RequiredLayerDisplayNames[1]}={layerPaths[1]}, " +
                $"{RequiredLayerDisplayNames[2]}={layerPaths[2]}";

            errorMessage = string.Empty;
            return true;
        }

        private static TerrainLayer ResolveTerrainLayer(string preferredPath, string layerName, out string resolvedPath)
        {
            resolvedPath = preferredPath;

            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(preferredPath);
            if (layer != null)
            {
                return layer;
            }

            string[] guids = AssetDatabase.FindAssets($"{layerName} t:TerrainLayer", new[] { TerrainLayersFolderPath });
            TerrainLayer fallback = null;

            for (int i = 0; i < guids.Length; i++)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                TerrainLayer candidate = AssetDatabase.LoadAssetAtPath<TerrainLayer>(candidatePath);
                if (candidate == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                    resolvedPath = candidatePath;
                }

                string fileName = Path.GetFileNameWithoutExtension(candidatePath);
                if (string.Equals(fileName, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedPath = candidatePath;
                    return candidate;
                }
            }

            return fallback;
        }

        private static string GetLayerPathByIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return RockLayerPath;
                case 1:
                    return GrassLayerPath;
                case 2:
                    return SnowLayerPath;
                default:
                    return TerrainLayersFolderPath;
            }
        }

        private static int ApplyGraphEditorDefaults(Graph graph)
        {
            if (graph == null)
            {
                return 0;
            }

            int changed = 0;
            Undo.RecordObject(graph, "Configure MapMagic Graph Defaults");

            changed += SetBool(ref graph.guiShowDependent, false);
            changed += SetBool(ref graph.guiShowShared, false);
            changed += SetBool(ref graph.guiShowExposed, true);
            changed += SetBool(ref graph.guiShowDebug, false);
            changed += SetVector2(ref graph.guiMiniPos, new Vector2(20f, 20f));
            changed += SetVector2(ref graph.guiMiniAnchor, new Vector2(0f, 0f));

            return changed;
        }

        private static int AssignRequiredTerrainLayersToAllTextureOutputs(
            Graph rootGraph,
            TerrainLayer[] requiredLayers,
            out int textureOutputsFound)
        {
            textureOutputsFound = 0;
            if (rootGraph == null || requiredLayers == null || requiredLayers.Length < 3)
            {
                return 0;
            }

            int changed = 0;

            foreach (Graph graph in EnumerateGraphHierarchy(rootGraph))
            {
                if (graph == null || graph.generators == null)
                {
                    continue;
                }

                bool recordedGraphForUndo = false;

                for (int i = 0; i < graph.generators.Length; i++)
                {
                    Generator generator = graph.generators[i];
                    if (generator is not TexturesOutput200 texturesOutput)
                    {
                        continue;
                    }

                    textureOutputsFound++;
                    if (!recordedGraphForUndo)
                    {
                        Undo.RecordObject(graph, "Assign MapMagic Terrain Layer Prototypes");
                        recordedGraphForUndo = true;
                    }

                    for (int layerIndex = 0; layerIndex < 3; layerIndex++)
                    {
                        changed += EnsureTextureOutputLayerAssignment(
                            texturesOutput,
                            layerIndex,
                            requiredLayers[layerIndex],
                            RequiredLayerDisplayNames[layerIndex]);
                    }
                }
            }

            return changed;
        }

        /// <summary>
        /// Plains + Occasional Hills topology (prototype-friendly):
        ///   BaseNoise → BaseCurve
        ///   HillNoise → HillCurve
        ///   HillMaskNoise → HillMaskCurve (thresholds to create patches)
        ///   HillsMasked = Blend(add HillCurve, multiply HillMaskCurve)
        ///   HeightBlend = Blend(add BaseCurve, add HillsMasked, add MicroNoise)
        ///   HeightBlend → HeightOutput200
        ///
        /// Textures (Snow/Rock/Grass order in TexturesOutput200):
        ///   Snow: only on rare tall peaks inside hill patches (Selector on height + mountain mask)
        ///   Rock: mostly on steeper slopes inside hill patches (Slope + mountain mask)
        ///   Grass: dominant base (constant 1 minus rock/snow contributions)
        /// </summary>
        private static int RebuildGraphWiringPlainsPreset(
            Graph rootGraph,
            TerrainLayer[] layers,
            out int generatorsCreated,
            out int textureOutputsWired,
            out int layersWired)
        {
            generatorsCreated = 0;
            textureOutputsWired = 0;
            layersWired = 0;

            if (rootGraph == null || layers == null || layers.Length < 3)
            {
                return 0;
            }

            Undo.RecordObject(rootGraph, "Rebuild MapMagic Graph Wiring (Plains Preset)");

            // --- 1. Clear existing generators/links ---
            Generator[] existing = rootGraph.generators != null
                ? (Generator[])rootGraph.generators.Clone()
                : Array.Empty<Generator>();

            foreach (Generator gen in existing)
            {
                if (gen != null)
                {
                    rootGraph.Remove(gen);
                }
            }

            rootGraph.groups = Array.Empty<Auxiliary>();

            // --- 2. Height chain ---
            // Base noise (very subtle, mostly plains)
            Noise200 baseNoise = (Noise200)Generator.Create(typeof(Noise200));
            baseNoise.guiPosition = new Vector2(-520f, -180f);
            baseNoise.type = Noise200.Type.Perlin;
            baseNoise.seed = 12345;
            baseNoise.intensity = 0.05f;
            baseNoise.size = 1100f;
            baseNoise.detail = 0.30f;
            baseNoise.turbulence = 0f;
            rootGraph.Add(baseNoise);
            generatorsCreated++;

            Curve200 baseCurve = (Curve200)Generator.Create(typeof(Curve200));
            baseCurve.guiPosition = new Vector2(-260f, -180f);
            baseCurve.curve = CreateLinearCurve(
                new Vector2(0f, 0f),
                new Vector2(0.70f, 0.08f),
                new Vector2(1f, 0.25f));
            rootGraph.Add(baseCurve);
            generatorsCreated++;
            rootGraph.Link(baseCurve, baseNoise);

            // Hills noise (only used where the mask allows it)
            Noise200 hillNoise = (Noise200)Generator.Create(typeof(Noise200));
            hillNoise.guiPosition = new Vector2(-520f, -20f);
            hillNoise.type = Noise200.Type.Perlin;
            hillNoise.seed = 22345;
            hillNoise.intensity = 0.55f;
            hillNoise.size = 260f;
            hillNoise.detail = 0.40f;
            hillNoise.turbulence = 0f;
            rootGraph.Add(hillNoise);
            generatorsCreated++;

            Curve200 hillCurve = (Curve200)Generator.Create(typeof(Curve200));
            hillCurve.guiPosition = new Vector2(-260f, -20f);
            hillCurve.curve = CreateLinearCurve(
                new Vector2(0f, 0f),
                new Vector2(0.70f, 0.18f),
                new Vector2(1f, 0.45f));
            rootGraph.Add(hillCurve);
            generatorsCreated++;
            rootGraph.Link(hillCurve, hillNoise);

            // Mask noise (large patches; most of map stays flat)
            Noise200 hillMaskNoise = (Noise200)Generator.Create(typeof(Noise200));
            hillMaskNoise.guiPosition = new Vector2(-520f, 150f);
            hillMaskNoise.type = Noise200.Type.Perlin;
            hillMaskNoise.seed = 32345;
            hillMaskNoise.intensity = 1f;
            hillMaskNoise.size = 1900f;
            hillMaskNoise.detail = 0.18f;
            hillMaskNoise.turbulence = 0f;
            rootGraph.Add(hillMaskNoise);
            generatorsCreated++;

            Curve200 hillMaskCurve = (Curve200)Generator.Create(typeof(Curve200));
            hillMaskCurve.guiPosition = new Vector2(-260f, 150f);
            // Threshold-like curve: keep most values near 0; only top range becomes 1.
            hillMaskCurve.curve = CreateLinearCurve(
                new Vector2(0f, 0f),
                new Vector2(0.94f, 0f),
                new Vector2(1f, 1f));
            rootGraph.Add(hillMaskCurve);
            generatorsCreated++;
            rootGraph.Link(hillMaskCurve, hillMaskNoise);

            // HillsMasked = hillCurve * hillMaskCurve (implemented as: add hill, then multiply mask)
            Blend200 hillsMasked = (Blend200)Generator.Create(typeof(Blend200));
            hillsMasked.guiPosition = new Vector2(40f, 40f);
            hillsMasked.layers = new Blend200.Layer[] { new Blend200.Layer(), new Blend200.Layer() };
            hillsMasked.layers[0].algorithm = Blend200.BlendAlgorithm.add;
            hillsMasked.layers[0].opacity = 1f;
            hillsMasked.layers[1].algorithm = Blend200.BlendAlgorithm.multiply;
            hillsMasked.layers[1].opacity = 1f;
            rootGraph.Add(hillsMasked);
            generatorsCreated++;
            rootGraph.Link(hillsMasked.layers[0].inlet, hillCurve);
            rootGraph.Link(hillsMasked.layers[1].inlet, hillMaskCurve);

            // Micro detail (visual breakup, kept subtle)
            Noise200 microNoise = (Noise200)Generator.Create(typeof(Noise200));
            microNoise.guiPosition = new Vector2(-520f, 320f);
            microNoise.type = Noise200.Type.Perlin;
            microNoise.seed = 42345;
            microNoise.intensity = 0.03f;
            microNoise.size = 80f;
            microNoise.detail = 0.60f;
            microNoise.turbulence = 0f;
            rootGraph.Add(microNoise);
            generatorsCreated++;

            // Final height blend
            Blend200 heightBlend = (Blend200)Generator.Create(typeof(Blend200));
            heightBlend.guiPosition = new Vector2(300f, -80f);
            heightBlend.layers = new Blend200.Layer[] { new Blend200.Layer(), new Blend200.Layer(), new Blend200.Layer() };
            heightBlend.layers[0].algorithm = Blend200.BlendAlgorithm.add;   // base
            heightBlend.layers[0].opacity = 1f;
            heightBlend.layers[1].algorithm = Blend200.BlendAlgorithm.add;   // hills
            heightBlend.layers[1].opacity = 0.16f; // flatter: rarer + lower amplitude hills
            heightBlend.layers[2].algorithm = Blend200.BlendAlgorithm.add;   // micro
            heightBlend.layers[2].opacity = 0.20f;
            rootGraph.Add(heightBlend);
            generatorsCreated++;
            rootGraph.Link(heightBlend.layers[0].inlet, baseCurve);
            rootGraph.Link(heightBlend.layers[1].inlet, hillsMasked);
            rootGraph.Link(heightBlend.layers[2].inlet, microNoise);

            HeightOutput200 heightOutput = (HeightOutput200)Generator.Create(typeof(HeightOutput200));
            heightOutput.guiPosition = new Vector2(580f, -80f);
            rootGraph.Add(heightOutput);
            generatorsCreated++;
            rootGraph.Link(heightOutput, heightBlend);

            // --- 3. Texture chain (Snow/Rock/Grass) ---
            TexturesOutput200 texturesOutput = (TexturesOutput200)Generator.Create(typeof(TexturesOutput200));
            texturesOutput.guiPosition = new Vector2(900f, 220f);
            rootGraph.Add(texturesOutput);
            generatorsCreated++;
            textureOutputsWired++;

            for (int i = 0; i < 3; i++)
            {
                EnsureTextureOutputLayerAssignment(texturesOutput, i, layers[i], RequiredLayerDisplayNames[i]);
            }

            // Shared constants for masking/mixing
            Constant200 constZero = (Constant200)Generator.Create(typeof(Constant200));
            constZero.guiPosition = new Vector2(300f, 520f);
            constZero.level = 0f;
            rootGraph.Add(constZero);
            generatorsCreated++;

            Constant200 constOne = (Constant200)Generator.Create(typeof(Constant200));
            constOne.guiPosition = new Vector2(300f, 600f);
            constOne.level = 1f;
            rootGraph.Add(constOne);
            generatorsCreated++;

            // Mountain mask = hill patches (reuse the same mask signal as height hills)
            Constant200 snowPeakBoost = (Constant200)Generator.Create(typeof(Constant200));
            snowPeakBoost.guiPosition = new Vector2(300f, 680f);
            snowPeakBoost.level = 1.05f;
            rootGraph.Add(snowPeakBoost);
            generatorsCreated++;

            // Snow: tall peaks, only where mountains exist
            Selector200 snowHeight = (Selector200)Generator.Create(typeof(Selector200));
            snowHeight.guiPosition = new Vector2(620f, 520f);
            snowHeight.rangeDet = Selector200.RangeDet.Transition;
            snowHeight.units = Selector200.Units.Map;
            snowHeight.tFrom = 0.86f;
            snowHeight.tTo = 0.995f;
            snowHeight.tTransition = 0.035f;
            rootGraph.Add(snowHeight);
            generatorsCreated++;
            rootGraph.Link(snowHeight, heightBlend);

            Curve200 snowCurve = (Curve200)Generator.Create(typeof(Curve200));
            snowCurve.guiPosition = new Vector2(760f, 520f);
            snowCurve.curve = CreateLinearCurve(
                new Vector2(0f, 0f),
                new Vector2(0.35f, 0f),
                new Vector2(1f, 1f));
            rootGraph.Add(snowCurve);
            generatorsCreated++;
            // Curve200 and Selector200 both implement IInlet<MatrixWorld> and IOutlet<MatrixWorld>; pick Link(inlet, outlet).
            rootGraph.Link((IInlet<object>)snowCurve, (IOutlet<object>)snowHeight);

            Mask200 snowMasked = (Mask200)Generator.Create(typeof(Mask200));
            snowMasked.guiPosition = new Vector2(1040f, 520f);
            rootGraph.Add(snowMasked);
            generatorsCreated++;
            rootGraph.Link(snowMasked.aIn, constZero);
            rootGraph.Link(snowMasked.bIn, constOne);
            rootGraph.Link(snowMasked.maskIn, hillMaskCurve);

            Blend200 snowMountains = (Blend200)Generator.Create(typeof(Blend200));
            snowMountains.guiPosition = new Vector2(1180f, 520f);
            snowMountains.layers = new Blend200.Layer[] { new Blend200.Layer(), new Blend200.Layer() };
            snowMountains.layers[0].algorithm = Blend200.BlendAlgorithm.multiply;
            snowMountains.layers[0].opacity = 1f;
            snowMountains.layers[1].algorithm = Blend200.BlendAlgorithm.multiply;
            snowMountains.layers[1].opacity = 1f;
            rootGraph.Add(snowMountains);
            generatorsCreated++;
            rootGraph.Link(snowMountains.layers[0].inlet, snowMasked);
            rootGraph.Link(snowMountains.layers[1].inlet, snowPeakBoost);

            rootGraph.Link(texturesOutput.layers[0], snowMountains);

            // Rock: steeper slopes, mostly on mountains
            Slope200 rockSlope = (Slope200)Generator.Create(typeof(Slope200));
            rockSlope.guiPosition = new Vector2(620f, 360f);
            rockSlope.from = 18f;
            rockSlope.to = 55f;
            rockSlope.range = 26f;
            rootGraph.Add(rockSlope);
            generatorsCreated++;
            rootGraph.Link(rockSlope, heightBlend);

            Curve200 rockCurve = (Curve200)Generator.Create(typeof(Curve200));
            rockCurve.guiPosition = new Vector2(760f, 360f);
            rockCurve.curve = CreateLinearCurve(
                new Vector2(0f, 0f),
                new Vector2(0.25f, 0.05f),
                new Vector2(1f, 0.35f));
            rootGraph.Add(rockCurve);
            generatorsCreated++;
            rootGraph.Link((IInlet<object>)rockCurve, (IOutlet<object>)rockSlope);

            Blend200 rockMountains = (Blend200)Generator.Create(typeof(Blend200));
            rockMountains.guiPosition = new Vector2(900f, 360f);
            rockMountains.layers = new Blend200.Layer[] { new Blend200.Layer(), new Blend200.Layer() };
            rockMountains.layers[0].algorithm = Blend200.BlendAlgorithm.multiply;
            rockMountains.layers[0].opacity = 1f;
            rockMountains.layers[1].algorithm = Blend200.BlendAlgorithm.multiply;
            rockMountains.layers[1].opacity = 1f;
            rootGraph.Add(rockMountains);
            generatorsCreated++;
            rootGraph.Link(rockMountains.layers[0].inlet, rockCurve);
            rootGraph.Link(rockMountains.layers[1].inlet, hillMaskCurve);

            rootGraph.Link(texturesOutput.layers[1], rockMountains);

            // Grass: dominant fill, reduced where rock/snow show through
            Constant200 grassBase = (Constant200)Generator.Create(typeof(Constant200));
            grassBase.guiPosition = new Vector2(620f, 200f);
            grassBase.level = 1f;
            rootGraph.Add(grassBase);
            generatorsCreated++;

            Blend200 grassWeights = (Blend200)Generator.Create(typeof(Blend200));
            grassWeights.guiPosition = new Vector2(1040f, 200f);
            grassWeights.layers = new Blend200.Layer[] { new Blend200.Layer(), new Blend200.Layer(), new Blend200.Layer() };
            grassWeights.layers[0].algorithm = Blend200.BlendAlgorithm.add;
            grassWeights.layers[0].opacity = 1f;
            grassWeights.layers[1].algorithm = Blend200.BlendAlgorithm.subtract;
            grassWeights.layers[1].opacity = 0.65f;
            grassWeights.layers[2].algorithm = Blend200.BlendAlgorithm.subtract;
            grassWeights.layers[2].opacity = 1f;
            rootGraph.Add(grassWeights);
            generatorsCreated++;
            rootGraph.Link(grassWeights.layers[0].inlet, grassBase);
            rootGraph.Link(grassWeights.layers[1].inlet, rockMountains);
            rootGraph.Link(grassWeights.layers[2].inlet, snowMountains);

            rootGraph.Link(texturesOutput.layers[2], grassWeights);

            layersWired += 3;

            EditorUtility.SetDirty(rootGraph);
            return generatorsCreated;
        }

        /// <summary>
        /// Clones MapMagic's demo graph "MinuteIslandInfinite" into the target graph asset, then
        /// re-assigns required terrain layer prototypes (Rock/Grass/Snow) on all TexturesOutput200 nodes.
        /// This gives a near-duplicate of the demo topology/parameters while keeping Zombera's layer assets.
        /// </summary>
        private static int RebuildGraphWiringMinuteIslandClonePreset(
            Graph rootGraph,
            TerrainLayer[] layers,
            out int generatorsCreated,
            out int textureOutputsWired,
            out int layersWired)
        {
            generatorsCreated = 0;
            textureOutputsWired = 0;
            layersWired = 0;

            if (rootGraph == null || layers == null || layers.Length < 3)
            {
                return 0;
            }

            Graph demo = AssetDatabase.LoadAssetAtPath<Graph>(MinuteIslandInfiniteGraphAssetPath);
            if (demo == null)
            {
                EditorUtility.DisplayDialog(
                    "MapMagic World Setup",
                    $"Could not load demo graph at '{MinuteIslandInfiniteGraphAssetPath}'. Falling back to Plains preset.",
                    "OK");

                return RebuildGraphWiringPlainsPreset(rootGraph, layers, out generatorsCreated, out textureOutputsWired, out layersWired);
            }

            Undo.RecordObject(rootGraph, "Clone MinuteIslandInfinite Graph");
            EditorUtility.CopySerialized(demo, rootGraph);
            EditorUtility.SetDirty(rootGraph);

            // Re-assign prototypes to match Zombera's terrain layers.
            int prototypeChanges = AssignRequiredTerrainLayersToAllTextureOutputs(rootGraph, layers, out int textureOutputsFound);
            textureOutputsWired = textureOutputsFound;

            // Best-effort counts: the demo topology is serialized, so we approximate using generator arrays.
            generatorsCreated = rootGraph.generators != null ? rootGraph.generators.Length : 0;
            layersWired = 0;

            return Mathf.Max(1, generatorsCreated) + prototypeChanges;
        }

        private static Curve CreateLinearCurve(params Vector2[] points)
        {
            if (points == null || points.Length < 2)
            {
                return new Curve(new Vector2(0, 0), new Vector2(1, 1));
            }

            Curve.Node[] nodes = new Curve.Node[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                nodes[i] = new Curve.Node(points[i]) { linear = true };
            }

            Curve curve = new Curve(nodes);
            curve.Refresh(updateLut: true);
            return curve;
        }

        private static IEnumerable<Graph> EnumerateGraphHierarchy(Graph rootGraph)
        {
            if (rootGraph == null)
            {
                yield break;
            }

            HashSet<Graph> visitedGraphs = new HashSet<Graph>();
            if (visitedGraphs.Add(rootGraph))
            {
                yield return rootGraph;
            }

            foreach (Graph subGraph in rootGraph.SubGraphs(recursively: true))
            {
                if (subGraph == null || !visitedGraphs.Add(subGraph))
                {
                    continue;
                }

                yield return subGraph;
            }
        }

        private static int EnsureTextureOutputLayerAssignment(
            TexturesOutput200 output,
            int index,
            TerrainLayer terrainLayer,
            string layerName)
        {
            int changed = 0;

            if (output.layers == null)
            {
                output.layers = new TexturesOutput200.TextureLayer[0];
                changed++;
            }

            if (output.layers.Length <= index)
            {
                int oldLength = output.layers.Length;
                Array.Resize(ref output.layers, index + 1);
                changed += index + 1 - oldLength;
            }

            if (output.layers[index] == null)
            {
                output.layers[index] = new TexturesOutput200.TextureLayer();
                changed++;
            }

            TexturesOutput200.TextureLayer textureLayer = output.layers[index];
            textureLayer.SetGen(output);

            if (!string.Equals(textureLayer.name, layerName, StringComparison.Ordinal))
            {
                textureLayer.name = layerName;
                changed++;
            }

            if (textureLayer.prototype != terrainLayer)
            {
                textureLayer.prototype = terrainLayer;
                changed++;
            }

            return changed;
        }

        private static MapMagicObject EnsureMapMagicObject(
            Scene scene,
            Graph graph,
            out bool createdMapMagic,
            out bool renamedMapMagicObject,
            out bool assignedGraph,
            out int disabledExtraMapMagic)
        {
            createdMapMagic = false;
            renamedMapMagicObject = false;
            assignedGraph = false;
            disabledExtraMapMagic = 0;

            List<MapMagicObject> sceneMapMagics = new List<MapMagicObject>();
            MapMagicObject[] allMapMagics = UnityEngine.Object.FindObjectsByType<MapMagicObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allMapMagics.Length; i++)
            {
                MapMagicObject candidate = allMapMagics[i];
                if (candidate == null || candidate.gameObject.scene != scene)
                {
                    continue;
                }

                sceneMapMagics.Add(candidate);
            }

            MapMagicObject primary = null;

            for (int i = 0; i < sceneMapMagics.Count; i++)
            {
                if (string.Equals(sceneMapMagics[i].gameObject.name, TargetMapMagicObjectName, StringComparison.Ordinal))
                {
                    primary = sceneMapMagics[i];
                    break;
                }
            }

            for (int i = 0; i < sceneMapMagics.Count; i++)
            {
                if (primary != null)
                {
                    break;
                }

                if (sceneMapMagics[i].graph == graph)
                {
                    primary = sceneMapMagics[i];
                    break;
                }
            }

            if (primary == null && sceneMapMagics.Count > 0)
            {
                primary = sceneMapMagics[0];
            }

            if (primary == null)
            {
                GameObject mapMagicObject = new GameObject(TargetMapMagicObjectName);
                SceneManager.MoveGameObjectToScene(mapMagicObject, scene);
                mapMagicObject.SetActive(false);

                primary = mapMagicObject.AddComponent<MapMagicObject>();
                primary.graph = graph;
                mapMagicObject.SetActive(true);

                createdMapMagic = true;
                assignedGraph = true;
            }

            if (!string.Equals(primary.gameObject.name, TargetMapMagicObjectName, StringComparison.Ordinal))
            {
                Undo.RecordObject(primary.gameObject, "Rename MapMagic Object");
                primary.gameObject.name = TargetMapMagicObjectName;
                renamedMapMagicObject = true;
            }

            if (primary.graph != graph)
            {
                Undo.RecordObject(primary, "Assign MapMagic Graph");
                primary.graph = graph;
                assignedGraph = true;
            }

            if (!primary.enabled)
            {
                Undo.RecordObject(primary, "Enable MapMagic Object");
                primary.enabled = true;
            }

            for (int i = 0; i < sceneMapMagics.Count; i++)
            {
                MapMagicObject candidate = sceneMapMagics[i];
                if (candidate == null || candidate == primary)
                {
                    continue;
                }

                if (!candidate.enabled)
                {
                    continue;
                }

                Undo.RecordObject(candidate, "Disable Extra MapMagic Object");
                candidate.enabled = false;
                EditorUtility.SetDirty(candidate);
                disabledExtraMapMagic++;
            }

            return primary;
        }

        private static int ApplyMapMagicStreamingDefaults(MapMagicObject mapMagic)
        {
            if (mapMagic == null)
            {
                return 0;
            }

            int changed = 0;
            Undo.RecordObject(mapMagic, "Configure MapMagic Streaming Defaults");

            changed += SetInt(ref mapMagic.mainRange, 1);
            changed += SetBool(ref mapMagic.hideFarTerrains, true);
            changed += SetBool(ref mapMagic.draftsInEditor, true);
            changed += SetBool(ref mapMagic.draftsInPlaymode, false);
            changed += SetBool(ref mapMagic.instantGenerate, true);
            changed += SetBool(ref mapMagic.saveIntermediate, true);

            changed += SetBool(ref mapMagic.tiles.generateLimited, false);
            changed += SetBool(ref mapMagic.tiles.generateInfinite, true);
            changed += SetInt(ref mapMagic.tiles.generateRange, 2);
            changed += SetInt(ref mapMagic.tiles.retainMargin, 1);
            changed += SetBool(ref mapMagic.tiles.genAroundMainCam, true);
            changed += SetBool(ref mapMagic.tiles.genAroundObjsTag, false);
            changed += SetBool(ref mapMagic.tiles.genAroundTfms, false);
            changed += SetBool(ref mapMagic.tiles.genAroundCoordinates, false);

            changed += SetBool(ref mapMagic.terrainSettings.allowAutoConnect, true);
            changed += SetInt(ref mapMagic.terrainSettings.groupingID, 0);
            changed += SetBool(ref mapMagic.terrainSettings.copyLayersTags, true);
            changed += SetBool(ref mapMagic.terrainSettings.copyComponents, false);

            if (mapMagic.tiles.genAroundTfmsList != null && mapMagic.tiles.genAroundTfmsList.Length > 0)
            {
                mapMagic.tiles.genAroundTfmsList = Array.Empty<Transform>();
                changed++;
            }

            if (mapMagic.tiles.genCoordinates != null && mapMagic.tiles.genCoordinates.Length > 0)
            {
                mapMagic.tiles.genCoordinates = Array.Empty<Coord>();
                changed++;
            }

            if (mapMagic.tileSize.x < 64 || mapMagic.tileSize.z < 64)
            {
                mapMagic.tileSize = new Vector2D(1000, 1000);
                changed++;
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(mapMagic);
            }

            return changed;
        }

        private static int ApplyPlayerSpawnerMapMagicDefaults(Scene scene)
        {
            int changedFields = 0;
            PlayerSpawner[] spawners = UnityEngine.Object.FindObjectsByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < spawners.Length; i++)
            {
                PlayerSpawner spawner = spawners[i];
                if (spawner == null || spawner.gameObject.scene != scene)
                {
                    continue;
                }

                SerializedObject serializedSpawner = new SerializedObject(spawner);
                int before = changedFields;

                changedFields += SetSerializedBool(serializedSpawner, "stabilizeMapMagicGenerationInPlayMode", true);
                changedFields += SetSerializedBool(serializedSpawner, "freezeMapMagicExpansionInPlayMode", false);
                changedFields += SetSerializedBool(serializedSpawner, "rebakeNavMeshOnMapMagicComplete", true);
                changedFields += SetSerializedFloat(serializedSpawner, "mapMagicNavMeshRebakeCooldownSeconds", 1.5f);
                changedFields += SetSerializedBool(serializedSpawner, "fitNavMeshBoundsToTerrain", true);
                changedFields += SetSerializedFloat(serializedSpawner, "navMeshBakeRadius", 420f);
                changedFields += SetSerializedFloat(serializedSpawner, "navMeshVerticalExtent", 180f);
                changedFields += SetSerializedFloat(serializedSpawner, "navMeshVoxelSize", 0.25f);
                changedFields += SetSerializedInt(serializedSpawner, "navMeshTileSize", 256);
                changedFields += SetSerializedFloat(serializedSpawner, "navMeshMaxSlopeDegrees", 72f);
                changedFields += SetSerializedFloat(serializedSpawner, "navMeshStepHeightMeters", 2.4f);
                changedFields += SetSerializedFloat(serializedSpawner, "navMeshMinRegionArea", 0.1f);
                changedFields += SetSerializedFloat(serializedSpawner, "spawnNavMeshBelowTerrainToleranceMeters", 8f);
                changedFields += SetSerializedFloat(serializedSpawner, "navMeshTerrainBoundsPadding", 16f);
                changedFields += SetSerializedInt(serializedSpawner, "navMeshRetryAttempts", 5);
                changedFields += SetSerializedFloat(serializedSpawner, "navMeshRetryDelaySeconds", 0.6f);
                changedFields += SetSerializedInt(serializedSpawner, "productionStreamingGenerateRange", 4);
                changedFields += SetSerializedInt(serializedSpawner, "productionStreamingRetainMargin", 3);

                if (changedFields == before)
                {
                    continue;
                }

                serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(spawner);
            }

            return changedFields;
        }

        private static int ApplyStreamingNavMeshTileServiceDefaults(Scene scene)
        {
            int changedFields = 0;
            StreamingNavMeshTileService[] services = UnityEngine.Object.FindObjectsByType<StreamingNavMeshTileService>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < services.Length; i++)
            {
                StreamingNavMeshTileService service = services[i];
                if (service == null || service.gameObject.scene != scene)
                {
                    continue;
                }

                SerializedObject serializedService = new SerializedObject(service);
                int before = changedFields;

                changedFields += SetSerializedBool(serializedService, "driveRuntimeNavMesh", true);
                changedFields += SetSerializedFloat(serializedService, "navMeshVoxelSize", 0.25f);
                changedFields += SetSerializedInt(serializedService, "navMeshTileSize", 256);
                changedFields += SetSerializedFloat(serializedService, "navMeshMaxSlopeDegrees", 72f);
                changedFields += SetSerializedFloat(serializedService, "navMeshStepHeightMeters", 2.4f);
                changedFields += SetSerializedFloat(serializedService, "navMeshMinRegionArea", 0.1f);
                changedFields += SetSerializedFloat(serializedService, "navMeshVerticalHalfExtent", 180f);
                changedFields += SetSerializedFloat(serializedService, "tileBoundsHorizontalPadding", 12f);

                if (changedFields == before)
                {
                    continue;
                }

                serializedService.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(service);
            }

            return changedFields;
        }

        private static int SetBool(ref bool currentValue, bool desiredValue)
        {
            if (currentValue == desiredValue)
            {
                return 0;
            }

            currentValue = desiredValue;
            return 1;
        }

        private static int SetInt(ref int currentValue, int desiredValue)
        {
            if (currentValue == desiredValue)
            {
                return 0;
            }

            currentValue = desiredValue;
            return 1;
        }

        private static int SetVector2(ref Vector2 currentValue, Vector2 desiredValue)
        {
            if (currentValue == desiredValue)
            {
                return 0;
            }

            currentValue = desiredValue;
            return 1;
        }

        private static int SetSerializedBool(SerializedObject serializedObject, string propertyName, bool desiredValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
            {
                return 0;
            }

            if (property.boolValue == desiredValue)
            {
                return 0;
            }

            property.boolValue = desiredValue;
            return 1;
        }

        private static int SetSerializedInt(SerializedObject serializedObject, string propertyName, int desiredValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
            {
                return 0;
            }

            if (property.intValue == desiredValue)
            {
                return 0;
            }

            property.intValue = desiredValue;
            return 1;
        }

        private static int SetSerializedFloat(SerializedObject serializedObject, string propertyName, float desiredValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
            {
                return 0;
            }

            if (Mathf.Abs(property.floatValue - desiredValue) <= 0.0001f)
            {
                return 0;
            }

            property.floatValue = desiredValue;
            return 1;
        }
    }
}
