#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    public static partial class RoadGameplayTooling
    {
        private const string MenuRoot = "Tools/World/Roads/";
        private const string DialogTitle = "Road Gameplay Tooling";
        private const string DefaultAssetFolder = "Assets/ScriptableObjects/World/Roads";
        private const string SceneStackRootName = "Road Gameplay Stack";
        private const string SceneAuthoringRootName = "RoadGameplayAuthoringRoot";
        private const string SceneServiceObjectName = "RoadGameplayService";
        private const string SceneNodesRootName = "Nodes";
        private const string SceneSegmentsRootName = "Segments";
        private const string SceneSpawnPointsRootName = "SpawnPoints";

        private const string DefaultGraphPath = DefaultAssetFolder + "/GameplayRoadGraph.asset";
        private const string DefaultDerivedPath = DefaultAssetFolder + "/GameplayRoadDerivedData.asset";
        private const string DefaultMaskSettingsPath = DefaultAssetFolder + "/RoadGameplayMaskBakeSettings.asset";

        private static void ExecuteBuildAutomatedRoadGameplayStackInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                ShowValidationDialog("No loaded active scene was found.");
                return;
            }

            RoadEditorAssetUtility.EnsureFolderHierarchy(DefaultAssetFolder);

            var graph = GetOrCreateAsset<GameplayRoadGraph>(DefaultGraphPath);
            var derived = GetOrCreateDerivedAssetForGraph(graph);
            var maskSettings = GetOrCreateMaskSettingsAsset();
            var maskSet = GetOrCreateMaskSetAssetForGraph(graph);
            if (graph == null || derived == null || maskSettings == null || maskSet == null)
            {
                ShowOperationFailedDialog("One or more required road gameplay assets could not be created.");
                return;
            }

            var stackRoot = EnsureRootObject(scene, SceneStackRootName);
            var authoringRootObject = EnsureChildObject(stackRoot.transform, SceneAuthoringRootName, scene);
            var authoringRoot = EnsureComponent<RoadGameplayAuthoringRoot>(authoringRootObject);

            Undo.RecordObject(authoringRoot, "Configure RoadGameplayAuthoringRoot");
            authoringRoot.TargetGraph = graph;
            EditorUtility.SetDirty(authoringRoot);

            EnsureDefaultAuthoringChildren(authoringRootObject.transform, out var segmentCount, out var spawnPointCount);
            authoringRoot.RefreshAuthoringReferences();
            EditorUtility.SetDirty(authoringRoot);

            if (!TryBuildGraphDataFromAuthoring(authoringRoot, out var buildArtifacts, out var buildSummary))
            {
                ShowOperationFailedDialog("Automated setup could not build graph data.\n\n" + buildSummary);
                return;
            }

            Undo.RecordObject(graph, "Automated Build GameplayRoadGraph");
            graph.ReplaceData(
                buildArtifacts.NodeData,
                buildArtifacts.SegmentData,
                buildArtifacts.SpawnData,
                buildArtifacts.TownConnections,
                buildArtifacts.BiomeConnections);
            EditorUtility.SetDirty(graph);

            var sampleSpacing = Mathf.Max(0.5f, derived.SampleSpacingMeters);
            if (!GameplayRoadBaker.BakeIntoAsset(graph, derived, sampleSpacing))
            {
                ShowOperationFailedDialog("Automated setup failed while baking derived data.");
                return;
            }

            EditorUtility.SetDirty(derived);

            if (!TryBakeMasks(graph, maskSettings, maskSet, out var maskSummary))
            {
                ShowOperationFailedDialog("Automated setup failed while baking masks.\n\n" + maskSummary);
                return;
            }

            var serviceObject = EnsureChildObject(stackRoot.transform, SceneServiceObjectName, scene);
            var service = EnsureComponent<RoadGameplayService>(serviceObject);
            Undo.RecordObject(service, "Configure RoadGameplayService");
            service.Configure(graph, derived);
            EditorUtility.SetDirty(service);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = stackRoot;
            EditorGUIUtility.PingObject(stackRoot);

            ShowInfoDialog(
                "Automated road gameplay stack is ready.\n\n" +
                "Scene root: " + SceneStackRootName + "\n" +
                "Authoring root: " + SceneAuthoringRootName + "\n" +
                "Service object: " + SceneServiceObjectName + "\n" +
                "Graph nodes: " + buildArtifacts.NodeData.Count + "\n" +
                "Graph segments: " + buildArtifacts.SegmentData.Count + "\n" +
                "Graph spawn points: " + buildArtifacts.SpawnData.Count + "\n" +
                "Biome connections: " + buildArtifacts.BiomeConnections.Count + "\n" +
                "Authoring segments ensured: " + segmentCount + "\n" +
                "Authoring spawn points ensured: " + spawnPointCount + "\n\n" +
                buildSummary + "\n\n" +
                maskSummary);
        }

        private static void ExecuteCreateOrSelectGraph()
        {
            RoadEditorAssetUtility.EnsureFolderHierarchy(DefaultAssetFolder);

            var graph = GetOrCreateAsset<GameplayRoadGraph>(DefaultGraphPath);
            if (graph == null)
            {
                ShowOperationFailedDialog("Gameplay road graph asset could not be created.");
                return;
            }

            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);

            ShowInfoDialog(
                "Gameplay road graph asset is ready.\n\n" +
                "Use this as your authoritative source for road type, lane metadata, biome/town links, nav/path areas, sidewalk/decal flags, roadside lots, and spawn anchors.");
        }

        private static void ExecuteCreateOrSelectDerivedData()
        {
            RoadEditorAssetUtility.EnsureFolderHierarchy(DefaultAssetFolder);

            var derived = GetOrCreateAsset<GameplayRoadDerivedData>(DefaultDerivedPath);
            if (derived == null)
            {
                ShowOperationFailedDialog("Derived data asset could not be created.");
                return;
            }

            Selection.activeObject = derived;
            EditorGUIUtility.PingObject(derived);
        }

        private static void ExecuteCreateOrSelectMaskSettings()
        {
            RoadEditorAssetUtility.EnsureFolderHierarchy(DefaultAssetFolder);

            var settings = GetOrCreateAsset<RoadGameplayMaskBakeSettings>(DefaultMaskSettingsPath);
            if (settings == null)
            {
                ShowOperationFailedDialog("Mask bake settings asset could not be created.");
                return;
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private static void ExecuteBuildGraphFromSelectedAuthoringRoot()
        {
            if (!TryResolveSelectedAuthoringRoot(out var authoringRoot))
            {
                ShowValidationDialog("Select a RoadGameplayAuthoringRoot in the scene.");
                return;
            }

            authoringRoot.EnsureAuthoringReferencesUpToDate();

            var graph = authoringRoot.TargetGraph != null
                ? authoringRoot.TargetGraph
                : GetOrCreateAsset<GameplayRoadGraph>(DefaultGraphPath);
            if (graph == null)
            {
                ShowOperationFailedDialog("GameplayRoadGraph asset could not be resolved from selection or defaults.");
                return;
            }

            if (authoringRoot.TargetGraph != graph)
            {
                Undo.RecordObject(authoringRoot, "Assign GameplayRoadGraph");
                authoringRoot.TargetGraph = graph;
                EditorUtility.SetDirty(authoringRoot);
            }

            if (!TryBuildGraphDataFromAuthoring(authoringRoot, out var buildArtifacts, out var summary))
            {
                ShowOperationFailedDialog("Authoring conversion failed.\n\n" + summary);
                return;
            }

            Undo.RecordObject(graph, "Build GameplayRoadGraph From Authoring Root");
            graph.ReplaceData(
                buildArtifacts.NodeData,
                buildArtifacts.SegmentData,
                buildArtifacts.SpawnData,
                buildArtifacts.TownConnections,
                buildArtifacts.BiomeConnections);
            EditorUtility.SetDirty(graph);

            var scene = authoringRoot.gameObject.scene;
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            AssetDatabase.SaveAssets();

            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);

            ShowInfoDialog("Graph built from scene authoring root.\n\n" + summary);
        }

        private static void ExecuteBakeSelectedGraphDerivedData()
        {
            if (!TryResolveSelectedGraph(out var graph))
            {
                ShowValidationDialog(
                    "Select a GameplayRoadGraph asset, or a scene object with RoadGameplayService that references one.");
                return;
            }

            var derived = GetOrCreateDerivedAssetForGraph(graph);
            if (derived == null)
            {
                ShowOperationFailedDialog("Derived data asset could not be created.");
                return;
            }

            Undo.RecordObject(derived, "Bake Gameplay Road Derived Data");

            var spacing = Mathf.Max(0.5f, derived.SampleSpacingMeters);
            if (!GameplayRoadBaker.BakeIntoAsset(graph, derived, spacing))
            {
                ShowOperationFailedDialog(
                    "Bake failed. Ensure your graph has at least one valid segment with 2+ points (control points or start/end nodes).");
                return;
            }

            EditorUtility.SetDirty(derived);
            AssetDatabase.SaveAssets();

            Selection.activeObject = derived;
            EditorGUIUtility.PingObject(derived);

            ShowInfoDialog("Derived gameplay road data baked successfully.");
        }

        public static bool TryBakeDerivedForGraph(GameplayRoadGraph graph, GameplayRoadDerivedData derived, out string summary)
        {
            summary = string.Empty;
            if (graph == null)
            {
                summary = "GameplayRoadGraph is null.";
                return false;
            }

            if (derived == null)
            {
                summary = "GameplayRoadDerivedData is null.";
                return false;
            }

            Undo.RecordObject(derived, "Bake Gameplay Road Derived Data");

            var spacing = Mathf.Max(0.5f, derived.SampleSpacingMeters);
            if (!GameplayRoadBaker.BakeIntoAsset(graph, derived, spacing))
            {
                summary =
                    "Derived bake failed. Ensure the graph has at least one valid segment with 2+ points.";
                return false;
            }

            EditorUtility.SetDirty(derived);
            AssetDatabase.SaveAssets();
            summary = "Derived gameplay road data baked successfully.";
            return true;
        }

        public static bool TryBakeMasksForGraph(GameplayRoadGraph graph, out string summary)
        {
            summary = string.Empty;
            if (graph == null)
            {
                summary = "GameplayRoadGraph is null.";
                return false;
            }

            RoadEditorAssetUtility.EnsureFolderHierarchy(DefaultAssetFolder);

            var maskSettings = GetOrCreateMaskSettingsAsset();
            var maskSet = GetOrCreateMaskSetAssetForGraph(graph);
            if (maskSettings == null || maskSet == null)
            {
                summary = "Mask settings or mask set assets could not be created.";
                return false;
            }

            if (!TryBakeMasks(graph, maskSettings, maskSet, out summary))
                return false;

            AssetDatabase.SaveAssets();
            return true;
        }

        private static void ExecuteBakeSelectedGraphMasks()
        {
            if (!TryResolveSelectedGraph(out var graph))
            {
                ShowValidationDialog(
                    "Select a GameplayRoadGraph asset, or a scene object with RoadGameplayService that references one.");
                return;
            }

            var maskSettings = GetOrCreateMaskSettingsAsset();
            var maskSet = GetOrCreateMaskSetAssetForGraph(graph);
            if (maskSettings == null || maskSet == null)
            {
                ShowOperationFailedDialog("Mask settings or mask set assets could not be created.");
                return;
            }

            if (!TryBakeMasks(graph, maskSettings, maskSet, out var maskSummary))
            {
                ShowOperationFailedDialog(maskSummary);
                return;
            }

            Selection.activeObject = maskSet;
            EditorGUIUtility.PingObject(maskSet);

            ShowInfoDialog(maskSummary);
        }

        private static void ExecuteSetupRoadGameplayServiceInActiveScene()
        {
            if (!TryResolveSelectedGraph(out var graph))
                graph = GetOrCreateAsset<GameplayRoadGraph>(DefaultGraphPath);

            if (graph == null)
            {
                ShowOperationFailedDialog("GameplayRoadGraph asset could not be resolved from selection or defaults.");
                return;
            }

            var derived = GetOrCreateDerivedAssetForGraph(graph);
            if (derived == null)
            {
                ShowOperationFailedDialog("Derived data asset could not be created.");
                return;
            }

            if (derived.Samples is not { Count: > 0 })
                GameplayRoadBaker.BakeIntoAsset(graph, derived, derived.SampleSpacingMeters);

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                ShowValidationDialog("No loaded active scene was found.");
                return;
            }

            var service = FindFirstInScene<RoadGameplayService>(scene);
            if (service == null)
            {
                var go = EnsureRootObject(scene, SceneServiceObjectName);
                service = EnsureComponent<RoadGameplayService>(go);
            }
            else
            {
                Undo.RecordObject(service, "Configure RoadGameplayService");
            }

            service.Configure(graph, derived);
            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeObject = service.gameObject;
            EditorGUIUtility.PingObject(service.gameObject);

            ShowInfoDialog(
                "RoadGameplayService is configured in the active scene.\n\n" +
                "AI, spawn, and nav systems can now query gameplay road metadata from one source.");
        }

        private static bool TryResolveSelectedGraph(out GameplayRoadGraph graph)
        {
            graph = null;

            if (Selection.activeObject is GameplayRoadGraph selectedGraph)
            {
                graph = selectedGraph;
                return true;
            }

            if (Selection.activeObject is RoadGameplayService selectedService && selectedService.RoadGraph != null)
            {
                graph = selectedService.RoadGraph;
                return true;
            }

            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.TryGetComponent<RoadGameplayService>(out var serviceOnSelection) &&
                serviceOnSelection != null &&
                serviceOnSelection.RoadGraph != null)
            {
                graph = serviceOnSelection.RoadGraph;
                return true;
            }

            return false;
        }

        private static bool TryResolveSelectedAuthoringRoot(out RoadGameplayAuthoringRoot root)
        {
            root = null;

            if (Selection.activeObject is RoadGameplayAuthoringRoot selectedRoot)
            {
                root = selectedRoot;
                return true;
            }

            if (Selection.activeGameObject != null)
            {
                root = Selection.activeGameObject.GetComponentInParent<RoadGameplayAuthoringRoot>();
                if (root != null) return true;
            }

            return false;
        }

        private static void ShowValidationDialog(string message)
        {
            ShowInfoDialog("Validation failed: " + message);
        }

        private static void ShowOperationFailedDialog(string message)
        {
            ShowInfoDialog("Operation failed: " + message);
        }

        private static void ShowInfoDialog(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
        }
    }
}
#endif
