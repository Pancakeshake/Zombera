using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.Editor
{
    /// <summary>
    /// Scene bootstrap for perception-driven fog-of-war wiring.
    /// </summary>
    public static class FogOfWarSetupTool
    {
        private const string OverlayRootName = "FogVisionOverlay";

        [MenuItem("Tools/Zombera/World/Fog Of War/Setup Fog Of War In Active Scene")]
        private static void SetupFogOfWarInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Fog Of War Setup", "Active scene is not valid or not loaded.", "OK");
                return;
            }

            FogOfWarSystem fogSystem = EnsureFogSystemInScene(activeScene, out bool createdSystem);
            ApplyRecommendedFogDefaults(fogSystem);

            int addedVisionSources = 0;
            int addedOverlays = 0;
            int addedTargets = 0;
            int tunedVisionSources = 0;
            int tunedOverlays = 0;

            Unit[] units = Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (unit == null || unit.gameObject.scene != activeScene)
                {
                    continue;
                }

                FogOfWarVisionSource visionSource = unit.GetComponent<FogOfWarVisionSource>();

                if (IsVisionSourceUnit(unit))
                {
                    if (visionSource == null)
                    {
                        visionSource = Undo.AddComponent<FogOfWarVisionSource>(unit.gameObject);
                        addedVisionSources++;
                    }

                    if (visionSource != null && ApplyRecommendedVisionDefaults(visionSource))
                    {
                        tunedVisionSources++;
                    }
                }

                if (ShouldHaveVisionOverlay(unit) && visionSource != null)
                {
                    FogOfWarVisionOverlay overlay = unit.GetComponent<FogOfWarVisionOverlay>();
                    if (overlay == null)
                    {
                        overlay = Undo.AddComponent<FogOfWarVisionOverlay>(unit.gameObject);
                        addedOverlays++;
                    }

                    if (overlay != null && ApplyRecommendedOverlayDefaults(overlay))
                    {
                        tunedOverlays++;
                    }
                }

                if (IsHostileTarget(unit))
                {
                    if (unit.GetComponent<FogOfWarTarget>() == null)
                    {
                        Undo.AddComponent<FogOfWarTarget>(unit.gameObject);
                        addedTargets++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log(
                $"[FogOfWarSetupTool] Scene='{activeScene.name}', " +
                $"System={(createdSystem ? "created" : "reused")}, " +
                $"AddedVisionSources={addedVisionSources}, TunedVisionSources={tunedVisionSources}, " +
                $"AddedOverlays={addedOverlays}, TunedOverlays={tunedOverlays}, AddedTargets={addedTargets}, " +
                $"SystemObject='{fogSystem.gameObject.name}'.");

            EditorUtility.DisplayDialog(
                "Fog Of War Setup",
                $"Fog system {(createdSystem ? "created" : "reused")} in '{activeScene.name}'.\n" +
                $"Added vision sources: {addedVisionSources}\n" +
                $"Updated vision source tuning: {tunedVisionSources}\n" +
                $"Added player overlays: {addedOverlays}\n" +
                $"Updated overlay tuning: {tunedOverlays}\n" +
                $"Added targets: {addedTargets}\n\n" +
                "Player/squad/survivor units become vision sources.\n" +
                "Zombie/Bandit/Enemy units become fog targets.",
                "OK");
        }

        [MenuItem("Tools/Zombera/World/Fog Of War/Remove Fog Of War Components In Active Scene")]
        private static void RemoveFogOfWarInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Fog Of War Removal", "Active scene is not valid or not loaded.", "OK");
                return;
            }

            int removedSystems = DestroyComponentsInActiveScene<FogOfWarSystem>(activeScene);
            int removedSources = DestroyComponentsInActiveScene<FogOfWarVisionSource>(activeScene);
            int removedOverlays = DestroyComponentsInActiveScene<FogOfWarVisionOverlay>(activeScene);
            int removedTargets = DestroyComponentsInActiveScene<FogOfWarTarget>(activeScene);

            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log(
                $"[FogOfWarSetupTool] Removed FogOfWar in scene='{activeScene.name}': " +
                $"Systems={removedSystems}, Sources={removedSources}, Overlays={removedOverlays}, Targets={removedTargets}.");

            EditorUtility.DisplayDialog(
                "Fog Of War Removal",
                $"Removed Fog Of War components from '{activeScene.name}'.\n" +
                $"Systems: {removedSystems}\nSources: {removedSources}\nOverlays: {removedOverlays}\nTargets: {removedTargets}",
                "OK");
        }

        [MenuItem("Tools/Zombera/World/Fog Of War/Cleanup Duplicate Vision Overlay Children In Active Scene")]
        private static void CleanupDuplicateVisionOverlayChildrenInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Fog Overlay Cleanup", "Active scene is not valid or not loaded.", "OK");
                return;
            }

            int removedOverlayChildren = 0;
            int removedOverlayComponents = 0;

            Unit[] units = Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
            {
                Unit unit = units[unitIndex];
                if (unit == null || unit.gameObject.scene != activeScene)
                {
                    continue;
                }

                int matchesFound = 0;
                for (int childIndex = 0; childIndex < unit.transform.childCount; childIndex++)
                {
                    Transform child = unit.transform.GetChild(childIndex);
                    if (child == null || child.name != OverlayRootName)
                    {
                        continue;
                    }

                    if (matchesFound == 0)
                    {
                        matchesFound++;
                        continue;
                    }

                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedOverlayChildren++;
                }

                FogOfWarVisionOverlay[] overlays = unit.GetComponents<FogOfWarVisionOverlay>();
                for (int overlayIndex = 1; overlayIndex < overlays.Length; overlayIndex++)
                {
                    if (overlays[overlayIndex] == null)
                    {
                        continue;
                    }

                    Undo.DestroyObjectImmediate(overlays[overlayIndex]);
                    removedOverlayComponents++;
                }
            }

            if (removedOverlayChildren > 0 || removedOverlayComponents > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log(
                $"[FogOfWarSetupTool] Cleanup complete in scene='{activeScene.name}'. " +
                $"RemovedOverlayChildren={removedOverlayChildren}, RemovedOverlayComponents={removedOverlayComponents}.");

            EditorUtility.DisplayDialog(
                "Fog Overlay Cleanup",
                $"Cleanup complete in '{activeScene.name}'.\n" +
                $"Removed overlay child objects: {removedOverlayChildren}\n" +
                $"Removed duplicate overlay components: {removedOverlayComponents}",
                "OK");
        }

        private static bool IsVisionSourceUnit(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.Role == UnitRole.Player || unit.Role == UnitRole.SquadMember || unit.Role == UnitRole.Survivor)
            {
                return true;
            }

            return unit.Faction == UnitFaction.Survivor;
        }

        private static bool IsHostileTarget(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            return UnitFactionUtility.AreHostile(UnitFaction.Survivor, unit.Faction);
        }

        private static bool ShouldHaveVisionOverlay(Unit unit)
        {
            if (unit == null || unit.Role != UnitRole.Player)
            {
                return false;
            }

            PlayerInputController inputController = unit.GetComponent<PlayerInputController>();
            return inputController != null;
        }

        private static bool ApplyRecommendedVisionDefaults(FogOfWarVisionSource visionSource)
        {
            if (visionSource == null)
            {
                return false;
            }

            SerializedObject serializedVision = new SerializedObject(visionSource);
            SerializedProperty minimumRangeProperty = serializedVision.FindProperty("minimumVisionRangeMeters");
            SerializedProperty maximumRangeProperty = serializedVision.FindProperty("maximumVisionRangeMeters");
            SerializedProperty minimumAngleProperty = serializedVision.FindProperty("minimumVisionAngleDegrees");
            SerializedProperty maximumAngleProperty = serializedVision.FindProperty("maximumVisionAngleDegrees");

            bool changed = false;

            if (minimumRangeProperty != null && Mathf.Abs(minimumRangeProperty.floatValue - 25f) > 0.001f)
            {
                minimumRangeProperty.floatValue = 25f;
                changed = true;
            }

            if (maximumRangeProperty != null && Mathf.Abs(maximumRangeProperty.floatValue - 100f) > 0.001f)
            {
                maximumRangeProperty.floatValue = 100f;
                changed = true;
            }

            if (minimumAngleProperty != null && Mathf.Abs(minimumAngleProperty.floatValue - 180f) > 0.001f)
            {
                minimumAngleProperty.floatValue = 180f;
                changed = true;
            }

            if (maximumAngleProperty != null && Mathf.Abs(maximumAngleProperty.floatValue - 270f) > 0.001f)
            {
                maximumAngleProperty.floatValue = 270f;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            serializedVision.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visionSource);
            return true;
        }

        private static bool ApplyRecommendedOverlayDefaults(FogOfWarVisionOverlay overlay)
        {
            if (overlay == null)
            {
                return false;
            }

            SerializedObject serializedOverlay = new SerializedObject(overlay);
            SerializedProperty fogOverlayColorProperty = serializedOverlay.FindProperty("fogOverlayColor");
            SerializedProperty blindSpotColorProperty = serializedOverlay.FindProperty("blindSpotOverlayColor");
            SerializedProperty drawBlindSpotProperty = serializedOverlay.FindProperty("drawBlindSpotConeOverlay");
            SerializedProperty outerOverlayRadiusProperty = serializedOverlay.FindProperty("outerOverlayRadiusMeters");
            SerializedProperty radialSegmentsProperty = serializedOverlay.FindProperty("radialSegments");
            SerializedProperty blindSpotEdgeSoftnessProperty = serializedOverlay.FindProperty("blindSpotEdgeSoftnessDegrees");
            SerializedProperty blindSpotOuterFadeStartProperty = serializedOverlay.FindProperty("blindSpotOuterFadeStartMultiplier");
            SerializedProperty useVerticalVolumeProperty = serializedOverlay.FindProperty("useVerticalBlindSpotVolume");
            SerializedProperty verticalCoverageHeightProperty = serializedOverlay.FindProperty("blindSpotVerticalCoverageHeightMeters");

            bool changed = false;

            if (fogOverlayColorProperty != null)
            {
                Color color = fogOverlayColorProperty.colorValue;
                Color desired = new Color(color.r, color.g, color.b, 0.78f);
                if (color != desired)
                {
                    fogOverlayColorProperty.colorValue = desired;
                    changed = true;
                }
            }

            if (blindSpotColorProperty != null)
            {
                Color color = blindSpotColorProperty.colorValue;
                Color desired = new Color(color.r, color.g, color.b, 0.9f);
                if (color != desired)
                {
                    blindSpotColorProperty.colorValue = desired;
                    changed = true;
                }
            }

            if (drawBlindSpotProperty != null && !drawBlindSpotProperty.boolValue)
            {
                drawBlindSpotProperty.boolValue = true;
                changed = true;
            }

            if (outerOverlayRadiusProperty != null && outerOverlayRadiusProperty.floatValue < 1200f)
            {
                outerOverlayRadiusProperty.floatValue = 1200f;
                changed = true;
            }

            if (radialSegmentsProperty != null && radialSegmentsProperty.intValue < 384)
            {
                radialSegmentsProperty.intValue = 384;
                changed = true;
            }

            if (blindSpotEdgeSoftnessProperty != null && Mathf.Abs(blindSpotEdgeSoftnessProperty.floatValue - 28f) > 0.001f)
            {
                blindSpotEdgeSoftnessProperty.floatValue = 28f;
                changed = true;
            }

            if (blindSpotOuterFadeStartProperty != null && Mathf.Abs(blindSpotOuterFadeStartProperty.floatValue - 0.93f) > 0.001f)
            {
                blindSpotOuterFadeStartProperty.floatValue = 0.93f;
                changed = true;
            }

            if (useVerticalVolumeProperty != null && !useVerticalVolumeProperty.boolValue)
            {
                useVerticalVolumeProperty.boolValue = true;
                changed = true;
            }

            if (verticalCoverageHeightProperty != null && verticalCoverageHeightProperty.floatValue < 2000f)
            {
                verticalCoverageHeightProperty.floatValue = 2000f;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            serializedOverlay.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(overlay);
            return true;
        }

        private static FogOfWarSystem EnsureFogSystemInScene(Scene activeScene, out bool created)
        {
            FogOfWarSystem[] existing = Object.FindObjectsByType<FogOfWarSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                FogOfWarSystem candidate = existing[i];
                if (candidate != null && candidate.gameObject.scene == activeScene)
                {
                    created = false;
                    return candidate;
                }
            }

            GameObject root = new GameObject("FogOfWarSystem");
            Undo.RegisterCreatedObjectUndo(root, "Create Fog Of War System");
            SceneManager.MoveGameObjectToScene(root, activeScene);

            FogOfWarSystem fogSystem = Undo.AddComponent<FogOfWarSystem>(root);
            created = true;
            return fogSystem;
        }

        private static void ApplyRecommendedFogDefaults(FogOfWarSystem fogSystem)
        {
            if (fogSystem == null)
            {
                return;
            }

            SerializedObject serializedFog = new SerializedObject(fogSystem);
            SerializedProperty requireLosProperty = serializedFog.FindProperty("requireLineOfSight");
            SerializedProperty autoBootstrapProperty = serializedFog.FindProperty("autoBootstrapUnitsAtRuntime");
            SerializedProperty includeSurvivorSourceProperty = serializedFog.FindProperty("includeSurvivorRoleAsVisionSource");
            SerializedProperty autoOverlayProperty = serializedFog.FindProperty("autoBootstrapPlayerVisionOverlay");

            bool changed = false;

            if (requireLosProperty != null && requireLosProperty.boolValue)
            {
                requireLosProperty.boolValue = false;
                changed = true;
            }

            if (autoBootstrapProperty != null && !autoBootstrapProperty.boolValue)
            {
                autoBootstrapProperty.boolValue = true;
                changed = true;
            }

            if (includeSurvivorSourceProperty != null && !includeSurvivorSourceProperty.boolValue)
            {
                includeSurvivorSourceProperty.boolValue = true;
                changed = true;
            }

            if (autoOverlayProperty != null && !autoOverlayProperty.boolValue)
            {
                autoOverlayProperty.boolValue = true;
                changed = true;
            }

            if (changed)
            {
                serializedFog.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fogSystem);
            }
        }

        private static int DestroyComponentsInActiveScene<TComponent>(Scene scene)
            where TComponent : Component
        {
            TComponent[] components = Object.FindObjectsByType<TComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int removed = 0;

            for (int i = 0; i < components.Length; i++)
            {
                TComponent component = components[i];
                if (component == null || component.gameObject.scene != scene)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(component);
                removed++;
            }

            return removed;
        }
    }
}
