#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Views;

namespace Zombera.Editor
{
    /// <summary>
    ///     Development Hub — building view streaming and audit controls.
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow
    {
        private void DrawStreamingTab()
        {
            var manager = ResolveStateManager();
            var materializer = ResolveMaterializer();
            if (manager == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a CityPrefabRoadNetworkBuilder and provision the World Builder stack.",
                    MessageType.Warning);
                return;
            }

            if (!manager.HasState)
            {
                EditorGUILayout.HelpBox("No WorldState loaded. Run the Build pipeline first.", MessageType.Info);
                return;
            }

            if (materializer == null)
            {
                EditorGUILayout.HelpBox(
                    "WorldBuildingMaterializer not found on WorldBuilderStack.",
                    MessageType.Warning);
            }

            EditorGUILayout.LabelField("Tile Streaming", EditorStyles.boldLabel);
            streamTileX = EditorGUILayout.IntField("Tile X", streamTileX);
            streamTileZ = EditorGUILayout.IntField("Tile Z", streamTileZ);
            streamRadius = EditorGUILayout.Slider("Radius (tiles)", streamRadius, 0f, 8f);

            using (new EditorGUI.DisabledGroupScope(materializer == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Load Tile"))
                    LoadStreamTile(materializer);
                if (GUILayout.Button("Unload Tile"))
                    UnloadStreamTile(materializer);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Load Radius"))
                    LoadStreamRadius(materializer);

                if (GUILayout.Button("Destroy All Views"))
                    materializer.DestroyAllViews();

                if (GUILayout.Button("Audit Loaded Views"))
                    AuditStreamViews(materializer);
            }
        }

        private void LoadStreamTile(WorldBuildingMaterializer materializer)
        {
            var tile = new WorldTileKey(streamTileX, streamTileZ);
            var loaded = materializer.LoadTile(tile);
            _sessionLog.Add("Loaded tile " + tile + " — " + loaded + " view(s).");
            Repaint();
        }

        private void UnloadStreamTile(WorldBuildingMaterializer materializer)
        {
            var tile = new WorldTileKey(streamTileX, streamTileZ);
            var unloaded = materializer.UnloadTile(tile);
            _sessionLog.Add("Unload tile " + tile + " — " + (unloaded ? "removed" : "not loaded") + ".");
            Repaint();
        }

        private void LoadStreamRadius(WorldBuildingMaterializer materializer)
        {
            var radius = Mathf.Max(0, Mathf.RoundToInt(streamRadius));
            var total = 0;
            for (var z = streamTileZ - radius; z <= streamTileZ + radius; z++)
            {
                for (var x = streamTileX - radius; x <= streamTileX + radius; x++)
                    total += materializer.LoadTile(new WorldTileKey(x, z));
            }

            _sessionLog.Add(
                "Loaded radius " + radius + " around (" + streamTileX + "," + streamTileZ + ") — " +
                total + " view(s).");
            Repaint();
        }

        private void AuditStreamViews(WorldBuildingMaterializer materializer)
        {
            var report = materializer.AuditLoadedViews();
            _sessionLog.Add("View audit — " + report);
            if (report.HasIssues)
                EditorUtility.DisplayDialog("View Audit", report.ToString(), "OK");
            Repaint();
        }
    }
}
#endif
