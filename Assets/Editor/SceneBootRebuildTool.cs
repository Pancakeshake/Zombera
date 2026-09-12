#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    /// <summary>
    ///     Boot scene optimization tooling:
    ///     - Report boot-load risk and cost hotspots
    ///     - Apply conservative optimization rules after presenting report findings
    /// </summary>
    public static class SceneBootRebuildTool
    {
        private const string MenuRoot = "Tools/Scenes/Boot/";
        private const string ReportMenuPath = MenuRoot + "Report Boot Optimization (Active Scene)";
        private const string OptimizeMenuPath = MenuRoot + "Optimize Boot (Report Then Apply Conservative Fixes)";

        private const string OutputRootFolder = "Assets/Editor/Generated/SceneBootOptimization";
        private const string SingleReportFileName = "BootOptimizationReport.txt";
        private const string PreReportFileName = "BootOptimizationReport_Pre.txt";
        private const string PostReportFileName = "BootOptimizationReport_Post.txt";

        private static readonly string[] CriticalNameTokens =
        {
            "boot",
            "startup",
            "scene loader",
            "sceneloader",
            "game manager",
            "gamemanager",
            "readiness",
            "validator",
            "eventsystem",
            "input",
            "systems"
        };

        private static readonly string[] CriticalComponentTokens =
        {
            "GameManager",
            "SceneLoader",
            "Startup",
            "Readiness",
            "Bootstrap",
            "EventSystem",
            "InputSystemUIInputModule"
        };

        [MenuItem(ReportMenuPath, priority = -500)]
        private static void ReportBootOptimization()
        {
            if (!TryGetActiveSavedScene(out var scene, out var sceneError))
            {
                EditorUtility.DisplayDialog("Scene Boot Optimization", sceneError, "OK");
                return;
            }

            var analysis = AnalyzeScene(scene);
            var reportPath = WriteReportAsset(analysis, SingleReportFileName);

            var summary = BuildSummaryText(analysis, reportPath, "Boot optimization report generated.");
            Debug.Log("[SceneBootOptimization] " + summary);
            EditorUtility.DisplayDialog("Scene Boot Optimization", summary, "OK");
            PingReportAsset(reportPath);
        }

        [MenuItem(OptimizeMenuPath, priority = -500)]
        private static void OptimizeBootReportThenApply()
        {
            if (!TryGetActiveSavedScene(out var scene, out var sceneError))
            {
                EditorUtility.DisplayDialog("Scene Boot Optimization", sceneError, "OK");
                return;
            }

            var preAnalysis = AnalyzeScene(scene);
            var preReportPath = WriteReportAsset(preAnalysis, PreReportFileName);

            var applyPrompt =
                "Initial boot optimization report generated.\n\n" +
                "Scene: " + scene.name + "\n" +
                "Roots recommended to disable: " + preAnalysis.RootsRecommendedToDisable.Count + "\n" +
                "AudioSource PlayOnAwake to disable: " + preAnalysis.AudioPlayOnAwakeToDisable.Count + "\n" +
                "ParticleSystem PlayOnAwake to disable: " + preAnalysis.ParticlePlayOnAwakeToDisable.Count + "\n" +
                "Animator culling upgrades: " + preAnalysis.AnimatorsToCullCompletely.Count + "\n\n" +
                "Pre-report:\n" + preReportPath + "\n\n" +
                "Apply conservative boot optimizations now?";

            var approved = EditorUtility.DisplayDialog(
                "Scene Boot Optimization",
                applyPrompt,
                "Apply Optimizations",
                "Cancel");

            if (!approved)
            {
                PingReportAsset(preReportPath);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var applyResult = ApplyConservativeOptimizations(scene, preAnalysis);
            var postAnalysis = AnalyzeScene(scene);
            var postReportPath = WriteReportAsset(postAnalysis, PostReportFileName);

            var summary =
                "Conservative boot optimizations applied.\n\n" +
                "Scene: " + scene.name + "\n" +
                "Roots disabled: " + applyResult.rootsDisabled + "\n" +
                "AudioSource PlayOnAwake disabled: " + applyResult.audioPlayOnAwakeDisabled + "\n" +
                "ParticleSystem PlayOnAwake disabled: " + applyResult.particlePlayOnAwakeDisabled + "\n" +
                "Animator culling upgraded: " + applyResult.animatorCullingUpgraded + "\n\n" +
                "Pre-report:\n" + preReportPath + "\n\n" +
                "Post-report:\n" + postReportPath;

            Debug.Log("[SceneBootOptimization] " + summary);
            EditorUtility.DisplayDialog("Scene Boot Optimization", summary, "OK");
            PingReportAsset(postReportPath);
        }

        private static AnalysisResult AnalyzeScene(Scene scene)
        {
            var result = new AnalysisResult
            {
                sceneName = scene.name,
                scenePath = scene.path
            };

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null) continue;

                var rootAnalysis = AnalyzeRoot(root, result);
                result.rootAnalyses.Add(rootAnalysis);
            }

            EnsureAtLeastOneCameraRemainsEnabled(result);
            result.rootAnalyses.Sort((left, right) => right.score.CompareTo(left.score));
            return result;
        }

        private static RootAnalysis AnalyzeRoot(GameObject root, AnalysisResult sceneResult)
        {
            var rootResult = new RootAnalysis
            {
                root = root,
                isCritical = IsCriticalRoot(root)
            };

            TraverseHierarchy(root.transform, transform =>
            {
                var go = transform.gameObject;
                rootResult.gameObjectCount++;
                sceneResult.totalGameObjects++;

                var components = go.GetComponents<Component>();
                for (var i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component == null)
                    {
                        rootResult.missingComponentCount++;
                        sceneResult.missingComponentCount++;
                        rootResult.score += 1;
                        continue;
                    }

                    rootResult.componentCount++;
                    sceneResult.totalComponents++;

                    if (component is MonoBehaviour)
                    {
                        rootResult.monoBehaviourCount++;
                        sceneResult.totalMonoBehaviours++;
                        rootResult.score += 2;
                    }

                    if (component is Renderer)
                    {
                        rootResult.rendererCount++;
                        sceneResult.totalRenderers++;
                        rootResult.score += 1;
                    }

                    if (component is Animator animator)
                    {
                        rootResult.animatorCount++;
                        sceneResult.totalAnimators++;
                        rootResult.score += 6;

                        if (!rootResult.isCritical && animator.cullingMode != AnimatorCullingMode.CullCompletely)
                            AddUnique(sceneResult.AnimatorsToCullCompletely, animator);
                    }

                    if (component is AudioSource audioSource)
                    {
                        rootResult.audioSourceCount++;
                        sceneResult.totalAudioSources++;

                        if (audioSource.playOnAwake)
                        {
                            rootResult.audioPlayOnAwakeCount++;
                            sceneResult.totalAudioPlayOnAwake++;
                            rootResult.score += 5;

                            if (!rootResult.isCritical) AddUnique(sceneResult.AudioPlayOnAwakeToDisable, audioSource);
                        }
                        else
                        {
                            rootResult.score += 1;
                        }
                    }

                    if (component is ParticleSystem particleSystem)
                    {
                        rootResult.particleSystemCount++;
                        sceneResult.totalParticleSystems++;
                        rootResult.score += 4;

                        var main = particleSystem.main;
                        if (!main.playOnAwake) continue;

                        rootResult.particlePlayOnAwakeCount++;
                        sceneResult.totalParticlePlayOnAwake++;
                        rootResult.score += 2;

                        if (!rootResult.isCritical) AddUnique(sceneResult.ParticlePlayOnAwakeToDisable, particleSystem);
                    }

                    if (component is Camera camera)
                    {
                        rootResult.cameraCount++;
                        sceneResult.totalCameras++;

                        if (camera.enabled)
                        {
                            rootResult.enabledCameraCount++;
                            sceneResult.totalEnabledCameras++;
                            rootResult.score += 8;
                        }
                    }

                    if (component is Light light)
                    {
                        rootResult.lightCount++;
                        sceneResult.totalLights++;

                        if (light.enabled)
                        {
                            rootResult.enabledLightCount++;
                            sceneResult.totalEnabledLights++;
                            rootResult.score += 3;
                        }
                    }

                    if (component is Canvas canvas)
                    {
                        rootResult.canvasCount++;
                        sceneResult.totalCanvases++;

                        if (canvas.enabled)
                        {
                            rootResult.enabledCanvasCount++;
                            sceneResult.totalEnabledCanvases++;
                            rootResult.score += 4;
                        }
                    }

                    if (component is Terrain)
                    {
                        rootResult.terrainCount++;
                        sceneResult.totalTerrains++;
                        rootResult.score += 15;
                    }

                    if (component is ReflectionProbe)
                    {
                        rootResult.reflectionProbeCount++;
                        sceneResult.totalReflectionProbes++;
                        rootResult.score += 6;
                    }

                    if (string.Equals(component.GetType().Name, "NavMeshSurface", StringComparison.Ordinal))
                    {
                        rootResult.navMeshSurfaceCount++;
                        sceneResult.totalNavMeshSurfaces++;
                        rootResult.score += 12;
                    }
                }
            });

            rootResult.recommendedDisable = ShouldRecommendDisablingRoot(rootResult);
            if (rootResult.recommendedDisable) AddUnique(sceneResult.RootsRecommendedToDisable, root);
            return rootResult;
        }

        private static bool ShouldRecommendDisablingRoot(RootAnalysis root)
        {
            if (root == null || root.root == null) return false;
            if (root.isCritical) return false;
            if (!root.root.activeSelf) return false;
            if (root.score < 24) return false;

            var hasRenderOrFxWeight =
                root.rendererCount > 0 ||
                root.animatorCount > 0 ||
                root.particleSystemCount > 0 ||
                root.enabledCanvasCount > 0 ||
                root.enabledCameraCount > 0 ||
                root.enabledLightCount > 0;

            return hasRenderOrFxWeight;
        }

        private static void EnsureAtLeastOneCameraRemainsEnabled(AnalysisResult result)
        {
            if (result == null || result.totalEnabledCameras <= 0) return;

            var remainingEnabledCameraCount = 0;
            for (var i = 0; i < result.rootAnalyses.Count; i++)
            {
                var root = result.rootAnalyses[i];
                if (root == null || root.root == null) continue;
                if (!root.root.activeSelf) continue;
                if (root.recommendedDisable) continue;

                remainingEnabledCameraCount += root.enabledCameraCount;
            }

            if (remainingEnabledCameraCount > 0) return;

            RootAnalysis fallback = null;
            for (var i = 0; i < result.rootAnalyses.Count; i++)
            {
                var candidate = result.rootAnalyses[i];
                if (candidate == null || candidate.root == null) continue;
                if (candidate.enabledCameraCount <= 0) continue;
                if (!candidate.recommendedDisable) continue;

                fallback = candidate;
                break;
            }

            if (fallback == null) return;

            fallback.recommendedDisable = false;
            result.RootsRecommendedToDisable.Remove(fallback.root);
        }

        private static ApplyResult ApplyConservativeOptimizations(Scene scene, AnalysisResult analysis)
        {
            var result = new ApplyResult();
            if (analysis == null) return result;

            for (var i = 0; i < analysis.RootsRecommendedToDisable.Count; i++)
            {
                var root = analysis.RootsRecommendedToDisable[i];
                if (root == null || !root.activeSelf) continue;

                Undo.RecordObject(root, "Disable Non-Critical Boot Root");
                root.SetActive(false);
                EditorUtility.SetDirty(root);
                result.rootsDisabled++;
            }

            for (var i = 0; i < analysis.AudioPlayOnAwakeToDisable.Count; i++)
            {
                var audioSource = analysis.AudioPlayOnAwakeToDisable[i];
                if (audioSource == null || !audioSource.playOnAwake) continue;

                Undo.RecordObject(audioSource, "Disable AudioSource PlayOnAwake");
                audioSource.playOnAwake = false;
                EditorUtility.SetDirty(audioSource);
                result.audioPlayOnAwakeDisabled++;
            }

            for (var i = 0; i < analysis.ParticlePlayOnAwakeToDisable.Count; i++)
            {
                var particleSystem = analysis.ParticlePlayOnAwakeToDisable[i];
                if (particleSystem == null) continue;

                var main = particleSystem.main;
                if (!main.playOnAwake) continue;

                Undo.RecordObject(particleSystem, "Disable ParticleSystem PlayOnAwake");
                main.playOnAwake = false;
                EditorUtility.SetDirty(particleSystem);
                result.particlePlayOnAwakeDisabled++;
            }

            for (var i = 0; i < analysis.AnimatorsToCullCompletely.Count; i++)
            {
                var animator = analysis.AnimatorsToCullCompletely[i];
                if (animator == null || animator.cullingMode == AnimatorCullingMode.CullCompletely) continue;

                Undo.RecordObject(animator, "Set Animator Culling To CullCompletely");
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                EditorUtility.SetDirty(animator);
                result.animatorCullingUpgraded++;
            }

            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            return result;
        }

        private static string WriteReportAsset(AnalysisResult analysis, string filename)
        {
            EnsureFolderHierarchy(OutputRootFolder);

            var sceneFolder = OutputRootFolder + "/" + SanitizeFileName(analysis.sceneName);
            EnsureFolderHierarchy(sceneFolder);

            var reportAssetPath = sceneFolder + "/" + filename;
            var reportAbsolutePath = ToAbsolutePath(reportAssetPath);

            var reportText = BuildReportText(analysis);
            File.WriteAllText(reportAbsolutePath, reportText);

            AssetDatabase.ImportAsset(reportAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
            return reportAssetPath;
        }

        private static string BuildSummaryText(AnalysisResult analysis, string reportPath, string header)
        {
            return header + "\n\n" +
                   "Scene: " + analysis.sceneName + "\n" +
                   "Total GameObjects: " + analysis.totalGameObjects + "\n" +
                   "Total Components: " + analysis.totalComponents + "\n" +
                   "Total MonoBehaviours: " + analysis.totalMonoBehaviours + "\n" +
                   "Enabled Cameras: " + analysis.totalEnabledCameras + "\n" +
                   "Enabled Lights: " + analysis.totalEnabledLights + "\n" +
                   "Audio PlayOnAwake: " + analysis.totalAudioPlayOnAwake + "\n" +
                   "Particle PlayOnAwake: " + analysis.totalParticlePlayOnAwake + "\n" +
                   "Missing Components: " + analysis.missingComponentCount + "\n" +
                   "\n" +
                   "Recommended roots to disable: " + analysis.RootsRecommendedToDisable.Count + "\n" +
                   "Recommended AudioSource changes: " + analysis.AudioPlayOnAwakeToDisable.Count + "\n" +
                   "Recommended ParticleSystem changes: " + analysis.ParticlePlayOnAwakeToDisable.Count + "\n" +
                   "Recommended Animator culling changes: " + analysis.AnimatorsToCullCompletely.Count + "\n" +
                   "\n" +
                   "Report: " + reportPath;
        }

        private static string BuildReportText(AnalysisResult analysis)
        {
            var sb = new StringBuilder(4096);

            sb.AppendLine("Boot Optimization Report");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Scene Name: " + analysis.sceneName);
            sb.AppendLine("Scene Path: " + analysis.scenePath);
            sb.AppendLine();

            sb.AppendLine("Totals");
            sb.AppendLine("- Root Objects: " + analysis.rootAnalyses.Count);
            sb.AppendLine("- GameObjects: " + analysis.totalGameObjects);
            sb.AppendLine("- Components: " + analysis.totalComponents);
            sb.AppendLine("- MonoBehaviours: " + analysis.totalMonoBehaviours);
            sb.AppendLine("- Missing Components: " + analysis.missingComponentCount);
            sb.AppendLine("- Enabled Cameras: " + analysis.totalEnabledCameras);
            sb.AppendLine("- Enabled Lights: " + analysis.totalEnabledLights);
            sb.AppendLine("- Enabled Canvases: " + analysis.totalEnabledCanvases);
            sb.AppendLine("- NavMeshSurface Components: " + analysis.totalNavMeshSurfaces);
            sb.AppendLine("- Terrains: " + analysis.totalTerrains);
            sb.AppendLine("- Reflection Probes: " + analysis.totalReflectionProbes);
            sb.AppendLine("- AudioSource PlayOnAwake: " + analysis.totalAudioPlayOnAwake);
            sb.AppendLine("- ParticleSystem PlayOnAwake: " + analysis.totalParticlePlayOnAwake);
            sb.AppendLine();

            sb.AppendLine("Recommended Actions");
            sb.AppendLine("- Disable non-critical heavy roots: " + analysis.RootsRecommendedToDisable.Count);
            for (var i = 0; i < analysis.RootsRecommendedToDisable.Count; i++)
                if (analysis.RootsRecommendedToDisable[i] != null)
                    sb.AppendLine("  - " + analysis.RootsRecommendedToDisable[i].name);

            sb.AppendLine("- Disable AudioSource PlayOnAwake: " + analysis.AudioPlayOnAwakeToDisable.Count);
            sb.AppendLine("- Disable ParticleSystem PlayOnAwake: " + analysis.ParticlePlayOnAwakeToDisable.Count);
            sb.AppendLine("- Set Animator culling to CullCompletely: " + analysis.AnimatorsToCullCompletely.Count);
            sb.AppendLine();

            sb.AppendLine("Top Root Cost Buckets");
            var max = Mathf.Min(10, analysis.rootAnalyses.Count);
            for (var i = 0; i < max; i++)
            {
                var root = analysis.rootAnalyses[i];
                if (root == null || root.root == null) continue;

                sb.AppendLine(
                    "- " + root.root.name +
                    " | Score=" + root.score +
                    " | Critical=" + root.isCritical +
                    " | RecommendDisable=" + root.recommendedDisable +
                    " | GO=" + root.gameObjectCount +
                    " | Comp=" + root.componentCount +
                    " | Mono=" + root.monoBehaviourCount +
                    " | Cameras=" + root.enabledCameraCount +
                    " | Lights=" + root.enabledLightCount +
                    " | Canvases=" + root.enabledCanvasCount +
                    " | AudioPOA=" + root.audioPlayOnAwakeCount +
                    " | ParticlePOA=" + root.particlePlayOnAwakeCount +
                    " | Animators=" + root.animatorCount +
                    " | NavMeshSurface=" + root.navMeshSurfaceCount +
                    " | MissingComp=" + root.missingComponentCount);
            }

            return sb.ToString();
        }

        private static bool IsCriticalRoot(GameObject root)
        {
            if (root == null) return true;

            var rootName = root.name.ToLowerInvariant();
            for (var i = 0; i < CriticalNameTokens.Length; i++)
                if (rootName.Contains(CriticalNameTokens[i]))
                    return true;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null) continue;

                var type = behaviour.GetType();
                var typeName = type.Name;

                for (var tokenIndex = 0; tokenIndex < CriticalComponentTokens.Length; tokenIndex++)
                    if (typeName.IndexOf(CriticalComponentTokens[tokenIndex], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                var typeNamespace = type.Namespace ?? string.Empty;
                if (typeNamespace.StartsWith("Zombera.Core", StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static void TraverseHierarchy(Transform root, Action<Transform> visitor)
        {
            if (root == null || visitor == null) return;

            var stack = new Stack<Transform>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current == null) continue;

                visitor(current);

                for (var i = current.childCount - 1; i >= 0; i--)
                {
                    var child = current.GetChild(i);
                    if (child != null) stack.Push(child);
                }
            }
        }

        private static void AddUnique<T>(ICollection<T> collection, T value)
        {
            if (collection == null) return;
            if (value == null) return;
            if (collection.Contains(value)) return;

            collection.Add(value);
        }

        private static bool TryGetActiveSavedScene(out Scene scene, out string error)
        {
            scene = SceneManager.GetActiveScene();
            error = string.Empty;

            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "No valid active scene is currently loaded.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(scene.path))
            {
                error = "The active scene is unsaved. Save it first so optimization reports can be written per scene.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(scene.name))
            {
                error = "The active scene does not have a valid name.";
                return false;
            }

            return true;
        }

        private static void EnsureFolderHierarchy(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            folderPath = folderPath.Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal)) return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(projectRoot)) return assetPath;

            var relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, relative);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Scene";

            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                    chars[i] = '_';

            var sanitized = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "Scene" : sanitized;
        }

        private static void PingReportAsset(string reportPath)
        {
            if (string.IsNullOrWhiteSpace(reportPath)) return;

            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(reportPath);
            if (textAsset == null) return;

            Selection.activeObject = textAsset;
            EditorGUIUtility.PingObject(textAsset);
        }

        private sealed class AnalysisResult
        {
            public string sceneName = string.Empty;
            public string scenePath = string.Empty;

            public int totalGameObjects;
            public int totalComponents;
            public int totalMonoBehaviours;
            public int missingComponentCount;
            public int totalRenderers;
            public int totalAnimators;
            public int totalAudioSources;
            public int totalAudioPlayOnAwake;
            public int totalParticleSystems;
            public int totalParticlePlayOnAwake;
            public int totalCameras;
            public int totalEnabledCameras;
            public int totalLights;
            public int totalEnabledLights;
            public int totalCanvases;
            public int totalEnabledCanvases;
            public int totalNavMeshSurfaces;
            public int totalTerrains;
            public int totalReflectionProbes;

            public readonly List<RootAnalysis> rootAnalyses = new();
            public readonly List<GameObject> RootsRecommendedToDisable = new();
            public readonly List<AudioSource> AudioPlayOnAwakeToDisable = new();
            public readonly List<ParticleSystem> ParticlePlayOnAwakeToDisable = new();
            public readonly List<Animator> AnimatorsToCullCompletely = new();
        }

        private sealed class RootAnalysis
        {
            public GameObject root;
            public bool isCritical;
            public bool recommendedDisable;

            public int score;
            public int gameObjectCount;
            public int componentCount;
            public int monoBehaviourCount;
            public int missingComponentCount;
            public int rendererCount;
            public int animatorCount;
            public int audioSourceCount;
            public int audioPlayOnAwakeCount;
            public int particleSystemCount;
            public int particlePlayOnAwakeCount;
            public int cameraCount;
            public int enabledCameraCount;
            public int lightCount;
            public int enabledLightCount;
            public int canvasCount;
            public int enabledCanvasCount;
            public int navMeshSurfaceCount;
            public int terrainCount;
            public int reflectionProbeCount;
        }

        private struct ApplyResult
        {
            public int rootsDisabled;
            public int audioPlayOnAwakeDisabled;
            public int particlePlayOnAwakeDisabled;
            public int animatorCullingUpgraded;
        }
    }
}
#endif
