#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Zombera.EditorTools
{
    internal static class SceneMissingScriptDiagnosticsTool
    {
        private const string ReportMenuPath = "Tools/Scenes/Diagnostics/Report Missing Scripts In Build Scenes";
        private const string RemoveMenuPath = "Tools/Scenes/Diagnostics/Remove Missing Scripts In Build Scenes";
        private const string ReportWithEventSystemsMenuPath =
            "Tools/Scenes/Diagnostics/Report Missing Scripts + Duplicate EventSystems In Build Scenes";
        private const string FixWithEventSystemsMenuPath =
            "Tools/Scenes/Diagnostics/Fix Missing Scripts + Duplicate EventSystems In Build Scenes";

        private static readonly List<EventSystem> EventSystemsBuffer = new(8);

        [MenuItem(ReportMenuPath, priority = -500)]
        private static void ReportMissingScriptsInBuildScenes()
        {
            ProcessBuildScenes(removeMissingScripts: false, fixDuplicateEventSystems: false);
        }

        [MenuItem(RemoveMenuPath, priority = -500)]
        private static void RemoveMissingScriptsInBuildScenes()
        {
            ProcessBuildScenes(removeMissingScripts: true, fixDuplicateEventSystems: false);
        }

        [MenuItem(ReportWithEventSystemsMenuPath, priority = -500)]
        private static void ReportMissingScriptsAndDuplicateEventSystemsInBuildScenes()
        {
            ProcessBuildScenes(removeMissingScripts: false, fixDuplicateEventSystems: false);
        }

        [MenuItem(FixWithEventSystemsMenuPath, priority = -500)]
        private static void FixMissingScriptsAndDuplicateEventSystemsInBuildScenes()
        {
            ProcessBuildScenes(removeMissingScripts: true, fixDuplicateEventSystems: true);
        }

        [MenuItem(ReportMenuPath, true, priority = -500)]
        [MenuItem(RemoveMenuPath, true, priority = -500)]
        [MenuItem(ReportWithEventSystemsMenuPath, true, priority = -500)]
        [MenuItem(FixWithEventSystemsMenuPath, true, priority = -500)]
        private static bool ValidateMenu()
        {
            return !EditorApplication.isPlaying;
        }

        private static void ProcessBuildScenes(bool removeMissingScripts, bool fixDuplicateEventSystems)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes == null || buildScenes.Length == 0)
            {
                Debug.LogWarning("[SceneMissingScriptDiagnosticsTool] No build scenes configured.");
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();

            var scannedScenes = 0;
            var totalMissing = 0;
            var totalRemoved = 0;
            var totalActiveEventSystems = 0;
            var totalDuplicateActiveEventSystems = 0;
            var totalEventSystemsDisabled = 0;
            var lines = new List<string>(buildScenes.Length + 2)
            {
                removeMissingScripts
                    ? "[SceneMissingScriptDiagnosticsTool] Missing script removal summary:"
                    : "[SceneMissingScriptDiagnosticsTool] Missing script report summary:"
            };

            if (fixDuplicateEventSystems)
                lines[0] = "[SceneMissingScriptDiagnosticsTool] Missing script + EventSystem fix summary:";

            try
            {
                for (var i = 0; i < buildScenes.Length; i++)
                {
                    var sceneEntry = buildScenes[i];
                    if (!sceneEntry.enabled) continue;
                    if (string.IsNullOrWhiteSpace(sceneEntry.path)) continue;

                    var scene = EditorSceneManager.OpenScene(sceneEntry.path, OpenSceneMode.Single);
                    var result = ScanScene(scene, removeMissingScripts, fixDuplicateEventSystems);

                    scannedScenes++;
                    totalMissing += result.missingCount;
                    totalRemoved += result.removedCount;
                    totalActiveEventSystems += result.activeEventSystems;
                    totalDuplicateActiveEventSystems += result.duplicateActiveEventSystems;
                    totalEventSystemsDisabled += result.disabledEventSystems;

                    if ((removeMissingScripts && result.removedCount > 0)
                        || (fixDuplicateEventSystems && result.disabledEventSystems > 0))
                        EditorSceneManager.SaveScene(scene);

                    lines.Add(
                        $"- {scene.name}: missing={result.missingCount}, removed={result.removedCount}, objectsWithMissing={result.objectsWithMissing}, " +
                        $"eventSystems(active/duplicate)={result.activeEventSystems}/{result.duplicateActiveEventSystems}, disabledEventSystems={result.disabledEventSystems}");
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            lines.Add($"Scanned scenes: {scannedScenes}");
            lines.Add($"Total missing scripts: {totalMissing}");
            lines.Add($"Total removed scripts: {totalRemoved}");
            lines.Add($"Total active EventSystems seen: {totalActiveEventSystems}");
            lines.Add($"Total duplicate active EventSystems: {totalDuplicateActiveEventSystems}");
            lines.Add($"Total duplicate EventSystems disabled: {totalEventSystemsDisabled}");

            Debug.Log(string.Join(System.Environment.NewLine, lines));
        }

        private static (
            int missingCount,
            int removedCount,
            int objectsWithMissing,
            int activeEventSystems,
            int duplicateActiveEventSystems,
            int disabledEventSystems) ScanScene(
            Scene scene,
            bool removeMissingScripts,
            bool fixDuplicateEventSystems)
        {
            var missingCount = 0;
            var removedCount = 0;
            var objectsWithMissing = 0;

            var roots = scene.GetRootGameObjects();
            var stack = new Stack<Transform>(256);

            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null) continue;

                stack.Push(root.transform);

                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (current == null) continue;

                    var go = current.gameObject;
                    var missingOnObject = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (missingOnObject > 0)
                    {
                        missingCount += missingOnObject;
                        objectsWithMissing++;

                        if (removeMissingScripts)
                        {
                            var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                            removedCount += removed;
                            if (removed > 0)
                                EditorSceneManager.MarkSceneDirty(scene);
                        }
                    }

                    for (var childIndex = 0; childIndex < current.childCount; childIndex++)
                        stack.Push(current.GetChild(childIndex));
                }
            }

            var (activeEventSystems, duplicateActiveEventSystems, disabledEventSystems) =
                ProcessSceneEventSystems(scene, fixDuplicateEventSystems);

            return (
                missingCount,
                removedCount,
                objectsWithMissing,
                activeEventSystems,
                duplicateActiveEventSystems,
                disabledEventSystems);
        }

        private static (int activeEventSystems, int duplicateActiveEventSystems, int disabledEventSystems)
            ProcessSceneEventSystems(Scene scene, bool fixDuplicateEventSystems)
        {
            EventSystemsBuffer.Clear();

            var allEventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < allEventSystems.Length; i++)
            {
                var eventSystem = allEventSystems[i];
                if (eventSystem == null) continue;
                if (eventSystem.gameObject.scene != scene) continue;
                EventSystemsBuffer.Add(eventSystem);
            }

            if (EventSystemsBuffer.Count == 0) return (0, 0, 0);

            var activeEventSystems = 0;
            EventSystem preferred = null;

            for (var i = 0; i < EventSystemsBuffer.Count; i++)
            {
                var candidate = EventSystemsBuffer[i];
                if (candidate == null) continue;

                if (!candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;

                activeEventSystems++;
                preferred ??= candidate;
            }

            preferred ??= EventSystemsBuffer[0];

            var duplicateActiveEventSystems = Mathf.Max(0, activeEventSystems - 1);
            if (!fixDuplicateEventSystems || duplicateActiveEventSystems <= 0)
                return (activeEventSystems, duplicateActiveEventSystems, 0);

            var disabledEventSystems = 0;

            for (var i = 0; i < EventSystemsBuffer.Count; i++)
            {
                var candidate = EventSystemsBuffer[i];
                if (candidate == null || candidate == preferred) continue;

                if (DisableEventSystemAndModules(candidate))
                    disabledEventSystems++;
            }

            if (disabledEventSystems > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            return (activeEventSystems, duplicateActiveEventSystems, disabledEventSystems);
        }

        private static bool DisableEventSystemAndModules(EventSystem eventSystem)
        {
            if (eventSystem == null) return false;

            var changed = false;

            if (eventSystem.enabled)
            {
                eventSystem.enabled = false;
                changed = true;
            }

            var modules = eventSystem.GetComponents<BaseInputModule>();
            for (var i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                if (module == null || !module.enabled) continue;

                module.enabled = false;
                changed = true;
            }

            if (eventSystem.gameObject.activeSelf)
            {
                eventSystem.gameObject.SetActive(false);
                changed = true;
            }

            return changed;
        }
    }
}
#endif
