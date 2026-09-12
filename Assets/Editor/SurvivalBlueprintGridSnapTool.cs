/// <summary>
/// Zombera — SurvivalBlueprintGridSnapTool.cs
/// Scene-view grid snap overlay for Survival Blueprint building pieces.
/// Toggle via Tools → Zombera → Building → Building Grid Snap (Scene View).
/// When active, any selected GameObject snaps to the configured building grid on
/// mouse-up, keeping pieces aligned without leaving the scene view.
/// </summary>

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    [InitializeOnLoad]
    internal static class SurvivalBlueprintGridSnapTool
    {
        // ── Menu path ─────────────────────────────────────────────────────────
        private const string MenuPath = "Tools/World/Buildings/Building Grid Snap (Scene View)";

        // ── Prefs keys ────────────────────────────────────────────────────────
        private const string PrefActive = "Zombera.GridSnap.Active";
        private const string PrefGridX  = "Zombera.GridSnap.GridX";
        private const string PrefGridY  = "Zombera.GridSnap.GridY";
        private const string PrefGridZ  = "Zombera.GridSnap.GridZ";
        private const string PrefSnapY  = "Zombera.GridSnap.SnapY";

        // ── State ─────────────────────────────────────────────────────────────
        private static bool  s_active;
        private static float s_gridX;
        private static float s_gridY;
        private static float s_gridZ;
        private static bool  s_snapY;

        // Cache last known positions so we only apply snap when objects move.
        private static Vector3[] s_lastPositions = new Vector3[0];
        private static int[]     s_lastIds        = new int[0];

        // ── Label style ───────────────────────────────────────────────────────
        private static GUIStyle s_labelStyle;

        // ─────────────────────────────────────────────────────────────────────
        static SurvivalBlueprintGridSnapTool()
        {
            LoadPrefs();
            EditorApplication.delayCall += () =>
            {
                Menu.SetChecked(MenuPath, s_active);
                if (s_active)
                    Subscribe();
            };
        }

        // ── Menu item ─────────────────────────────────────────────────────────
        [MenuItem(MenuPath, priority = 200)]
        private static void ToggleActive()
        {
            s_active = !s_active;
            Menu.SetChecked(MenuPath, s_active);
            SavePrefs();

            if (s_active)
                Subscribe();
            else
                Unsubscribe();

            SceneView.RepaintAll();
        }

        [MenuItem("Tools/World/Buildings/Reset Building Grid Snap Settings", priority = 201)]
        private static void ResetSettings()
        {
            s_gridX = 2f;
            s_gridY = 0.25f;
            s_gridZ = 2f;
            s_snapY = true;
            SavePrefs();
            SceneView.RepaintAll();
            Debug.Log("[GridSnap] Settings reset to defaults (X=2m, Y=0.25m, Z=2m).");
        }

        // ── Subscribe / unsubscribe ───────────────────────────────────────────
        private static void Subscribe()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void Unsubscribe()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // ── Scene GUI handler ─────────────────────────────────────────────────
        private static void OnSceneGUI(SceneView sv)
        {
            if (!s_active)
                return;

            DrawStatusLabel(sv);

            Event e = Event.current;

            // Snap on mouse-up (drag end) or when Enter/Return is pressed.
            bool shouldSnap = (e.type == EventType.MouseUp && e.button == 0) ||
                              (e.type == EventType.KeyDown &&
                               (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter));

            if (!shouldSnap)
                return;

            // Defer one frame so Unity has committed the transform change.
            EditorApplication.delayCall += SnapSelectedNow;
        }

        private static void SnapSelectedNow()
        {
            var gos = Selection.gameObjects;
            if (gos == null || gos.Length == 0)
                return;

            bool anyChanged = false;
            foreach (var go in gos)
            {
                if (go == null)
                    continue;

                var t   = go.transform;
                var pos = t.position;

                float snappedX = Mathf.Round(pos.x / s_gridX) * s_gridX;
                float snappedZ = Mathf.Round(pos.z / s_gridZ) * s_gridZ;
                float snappedY = s_snapY ? Mathf.Round(pos.y / s_gridY) * s_gridY : pos.y;

                var snapped = new Vector3(snappedX, snappedY, snappedZ);
                if (snapped == pos)
                    continue;

                Undo.RecordObject(t, "Snap to Building Grid");
                t.position = snapped;
                EditorUtility.SetDirty(go);
                anyChanged = true;
            }

            if (anyChanged)
                SceneView.RepaintAll();
        }

        // ── HUD label ─────────────────────────────────────────────────────────
        private static void DrawStatusLabel(SceneView sv)
        {
            if (s_labelStyle == null)
            {
                s_labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize  = 11,
                    alignment = TextAnchor.UpperLeft,
                    normal    = { textColor = new Color(0.35f, 1f, 0.45f, 0.92f) }
                };
            }

            Handles.BeginGUI();
            string label = $"⊞ Grid Snap  X:{s_gridX}m  Y:{(s_snapY ? s_gridY + "m" : "off")}  Z:{s_gridZ}m";
            GUI.Label(new Rect(8, sv.position.height - 52, 340, 22), label, s_labelStyle);
            Handles.EndGUI();
        }

        // ── Prefs ─────────────────────────────────────────────────────────────
        private static void LoadPrefs()
        {
            s_active = EditorPrefs.GetBool(PrefActive, false);
            s_gridX  = EditorPrefs.GetFloat(PrefGridX, 2f);
            s_gridY  = EditorPrefs.GetFloat(PrefGridY, 0.25f);
            s_gridZ  = EditorPrefs.GetFloat(PrefGridZ, 2f);
            s_snapY  = EditorPrefs.GetBool(PrefSnapY, true);
        }

        private static void SavePrefs()
        {
            EditorPrefs.SetBool(PrefActive, s_active);
            EditorPrefs.SetFloat(PrefGridX, s_gridX);
            EditorPrefs.SetFloat(PrefGridY, s_gridY);
            EditorPrefs.SetFloat(PrefGridZ, s_gridZ);
            EditorPrefs.SetBool(PrefSnapY, s_snapY);
        }

        // ── Inspector window for settings ─────────────────────────────────────
        internal sealed class SettingsWindow : EditorWindow
        {
            [MenuItem("Tools/World/Buildings/Building Grid Snap Settings...", priority = 202)]
            private static void Open()
            {
                var w = GetWindow<SettingsWindow>(true, "Building Grid Snap", true);
                w.minSize = new Vector2(240, 160);
                w.maxSize = new Vector2(360, 160);
                w.Show();
            }

            private void OnGUI()
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Grid Snap Sizes", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();

                s_gridX = EditorGUILayout.FloatField("Grid X (m)", s_gridX);
                s_gridZ = EditorGUILayout.FloatField("Grid Z (m)", s_gridZ);
                s_snapY = EditorGUILayout.Toggle("Snap Y", s_snapY);
                using (new EditorGUI.DisabledGroupScope(!s_snapY))
                    s_gridY = EditorGUILayout.FloatField("Grid Y (m)", s_gridY);

                if (EditorGUI.EndChangeCheck())
                    SavePrefs();

                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(s_active ? "Disable Snap" : "Enable Snap"))
                    ToggleActive();
                if (GUILayout.Button("Snap Selection Now"))
                    SnapSelectedNow();
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif
