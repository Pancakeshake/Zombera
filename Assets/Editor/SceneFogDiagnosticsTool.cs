using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.Editor
{
    public static class SceneFogDiagnosticsTool
    {
        [MenuItem("Tools/Utilities/Rendering/Report Active Scene Fog Sources", priority = -500)]
        public static void ReportActiveSceneFogSources()
        {
            var lines = new List<string>
            {
                "[SceneFogDiagnostics] Active scene fog report",
                $"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}",
                $"RenderSettings.fog: {RenderSettings.fog}",
                $"RenderSettings.fogMode: {RenderSettings.fogMode}",
                $"RenderSettings.fogDensity: {RenderSettings.fogDensity}",
                $"RenderSettings.fogColor: {RenderSettings.fogColor}"
            };

            var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            lines.Add($"Volumes in scene: {volumes.Length}");

            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null || volume.profile == null) continue;

                var fogComponents = CountFogLikeOverrides(volume.profile);
                if (fogComponents == 0) continue;

                lines.Add($"- Volume '{volume.name}' enabled={volume.enabled} profile='{AssetDatabase.GetAssetPath(volume.profile)}' fog-like overrides={fogComponents}");
            }

            var sceneViews = SceneView.sceneViews;
            for (var i = 0; i < sceneViews.Count; i++)
            {
                if (sceneViews[i] is not SceneView sceneView) continue;
                lines.Add($"- SceneView '{sceneView.titleContent.text}': fogEnabled={sceneView.sceneViewState.fogEnabled} skyboxEnabled={sceneView.sceneViewState.skyboxEnabled}");
            }

            Debug.Log(string.Join("\n", lines));
            EditorUtility.DisplayDialog("Fog Report", "Report written to Console.", "OK");
        }

        [MenuItem("Tools/Utilities/Rendering/Disable Fog In Active Scene And SceneView", priority = -500)]
        public static void DisableFogInActiveSceneAndSceneView()
        {
            // Scene-level fog toggle.
            RenderSettings.fog = false;

            // Scene view visual fog/skybox can still make the world look hazy even when game fog is off.
            var sceneViews = SceneView.sceneViews;
            for (var i = 0; i < sceneViews.Count; i++)
            {
                if (sceneViews[i] is not SceneView sceneView) continue;
                sceneView.Repaint();
            }

            // Disable fog-like volume overrides in active scene profiles.
            var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var disabledOverrides = 0;

            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null || volume.profile == null) continue;

                var components = volume.profile.components;
                for (var c = 0; c < components.Count; c++)
                {
                    var component = components[c];
                    if (component == null) continue;

                    var typeName = component.GetType().Name;
                    if (!IsFogLikeName(typeName)) continue;

                    if (!component.active) continue;
                    component.active = false;
                    disabledOverrides++;
                    EditorUtility.SetDirty(volume.profile);
                }
            }

            var summary =
                $"[SceneFogDiagnostics] Disabled fog in active scene. Volume overrides disabled: {disabledOverrides}. " +
                "If haze remains in Scene view, toggle Fog/Skybox off in the Scene view Effects menu.";
            Debug.Log(summary);
            EditorUtility.DisplayDialog("Disable Fog", summary, "OK");
        }

        private static int CountFogLikeOverrides(VolumeProfile profile)
        {
            if (profile == null) return 0;

            var count = 0;
            var components = profile.components;
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component == null) continue;
                if (IsFogLikeName(component.GetType().Name)) count++;
            }

            return count;
        }

        private static bool IsFogLikeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;

            return typeName.IndexOf("fog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("volumetric", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("mist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("haze", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
