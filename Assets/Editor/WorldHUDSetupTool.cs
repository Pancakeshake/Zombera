#region

using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zombera.UI;
using Object = UnityEngine.Object;

#endregion

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming

namespace Zombera.Editor
{
    /// <summary>
    ///     Builds the full WorldHUDCanvas hierarchy in the active scene.
    ///     Menu: Tools/World/Build World HUD
    ///     Layout produced:
    ///     WorldHUDCanvas
    ///     TopBar          — full-width 52 px strip at top
    ///     DimOverlay      — full-screen transparent black (shown when a panel is open)
    ///     Panels          — clip region between bars; contains all 5 overlay panels
    ///     AlertBanner     — 48 px strip that toggles just below top bar
    ///     BottomBar       — full-width 90 px strip at bottom
    /// </summary>
    public static partial class WorldHudSetupTool
    {
        private const string WorldHudCanvasPrefabPath = "Assets/02_Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab";

        // ── Dimensions ────────────────────────────────────────────────────────

        private const float TOP_H = 120f;
        private const float BOT_H = 200f;
        private const float PORT_W = 200f;
        private const float PORT_H = 140f;

        private const float TBTN = 100f;
        // ── Palette ───────────────────────────────────────────────────────────

        private static readonly Color C_Bar = new(0.06f, 0.07f, 0.09f, 0.97f);
        private static readonly Color C_Panel = new(0.08f, 0.08f, 0.10f, 0.93f);
        private static readonly Color C_Tab = new(0.12f, 0.13f, 0.16f, 1f);
        private static readonly Color C_Btn = new(0.14f, 0.15f, 0.18f, 1f);
        private static readonly Color C_Text = new(0.85f, 0.82f, 0.74f, 1f);
        private static readonly Color C_TextDim = new(0.50f, 0.48f, 0.42f, 1f);
        private static readonly Color C_Accent = new(0.22f, 0.62f, 0.40f, 1f);
        private static readonly Color C_HP = new(0.22f, 0.65f, 0.30f, 1f);
        private static readonly Color C_HPBg = new(0.25f, 0.08f, 0.06f, 1f);
        private static readonly Color C_Stam = new(0.28f, 0.52f, 0.78f, 1f);
        private static readonly Color C_StamBg = new(0.06f, 0.11f, 0.22f, 1f);
        private static readonly Color C_Port = new(0.11f, 0.11f, 0.14f, 1f);

        // ── Menu Entries ──────────────────────────────────────────────────────

        [MenuItem("Tools/World/Build World HUD", priority = -500)]
        private static void BuildWorldHUD()
        {
            // Warn if the active scene is not exactly the World scene.
            const string RequiredScene = "World";
            var sceneName = SceneManager.GetActiveScene().name;
            if (!string.Equals(sceneName, RequiredScene, StringComparison.Ordinal))
            {
                var proceed = EditorUtility.DisplayDialog(
                    "Wrong Scene",
                    $"The active scene is '{sceneName}'.\n\nThis tool is intended for the '{RequiredScene}' scene.\n\nOpen '{RequiredScene}' first, or build here anyway?",
                    "Build Anyway", "Cancel");
                if (!proceed) return;
            }

            // Destroy ALL existing WorldHUDController roots, regardless of name.
            // Old builds may be named "WorldHUD" instead of "WorldHUDCanvas".
            var existingControllers = Object.FindObjectsByType<WorldHUDController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existingControllers.Length > 0)
            {
                if (!EditorUtility.DisplayDialog("Rebuild World HUD",
                        $"{existingControllers.Length} existing World HUD object(s) found. Destroy and rebuild from scratch?",
                        "Rebuild", "Cancel"))
                    return;
                foreach (var ctrl in existingControllers)
                    if (ctrl != null)
                        Undo.DestroyObjectImmediate(ctrl.gameObject);
            }

            // Preferred path: instantiate canonical prefab so tool output always matches the maintained asset.
            if (TryInstantiateWorldHudCanvasPrefab(out var prefabInstance))
            {
                ForceTruncateOverflowOnHudText(prefabInstance.transform);
                Selection.activeGameObject = prefabInstance;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[WorldHUDSetupTool] WorldHUDCanvas instantiated from prefab.", prefabInstance);
                return;
            }

            // ── Root canvas ───────────────────────────────────────────────────
            var canvasGO = new GameObject("WorldHUDCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build World HUD");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ZomberaCanvasLayer.Hud;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            var hud = canvasGO.AddComponent<WorldHUDController>();

            // Disable the canvas at build time — WorldHUDController re-enables it on
            // GameState.Playing so it never renders in non-world scenes or during menus.
            canvas.enabled = false;

            // ── Sections (order = draw order) ────────────────────────────────
            // Dim overlay must be below the top/bottom bars so tab buttons remain
            // clickable while a panel is open.
            var dimGO = BuildDimOverlay(canvasGO.transform);
            var topBarGO = BuildTopBar(canvasGO.transform, hud);
            var panelsGO = BuildPanelsRoot(canvasGO.transform);
            var alertGO = BuildAlertBanner(canvasGO.transform);
            var bottomGO = BuildBottomBar(canvasGO.transform, hud, topBarGO.GetComponent<TopBarController>());

            // ── Panels ────────────────────────────────────────────────────────
            var sqPanel = BuildSquadPanel(panelsGO.transform);
            var invPanel = BuildInventoryPanel(panelsGO.transform);
            var crtPanel = MakePlaceholder(panelsGO.transform, "CraftingPanel",
                "CRAFTING", "Crafting system coming soon.");
            var mapPanel = MakePlaceholder(panelsGO.transform, "MapPanel",
                "TACTICAL MAP", "Overhead map coming soon.");
            var misPanel = BuildMissionsPanel(panelsGO.transform);
            var formPanel = BuildFormationsPanel(panelsGO.transform);
            var jobsPanelGo = BuildJobsPanel(panelsGO.transform);
            var factionsPanelGo = BuildFactionsPanel(panelsGO.transform);

            foreach (var p in new[] { sqPanel, invPanel, crtPanel, mapPanel, misPanel, formPanel, jobsPanelGo, factionsPanelGo })
                p.SetActive(false);

            // ── Wire WorldHUDController ───────────────────────────────────────
            var so = new SerializedObject(hud);
            so.FindProperty("topBar").objectReferenceValue = topBarGO.GetComponent<TopBarController>();
            so.FindProperty("portraitStrip").objectReferenceValue =
                bottomGO.GetComponentInChildren<SquadPortraitStrip>();
            so.FindProperty("squadPanel").objectReferenceValue = sqPanel;
            so.FindProperty("inventoryPanel").objectReferenceValue = invPanel;
            so.FindProperty("craftingPanel").objectReferenceValue = crtPanel;
            so.FindProperty("mapPanel").objectReferenceValue = mapPanel;
            so.FindProperty("missionsPanel").objectReferenceValue = misPanel;
            so.FindProperty("formationsPanel").objectReferenceValue = formPanel;
            so.FindProperty("jobsPanel").objectReferenceValue = jobsPanelGo;
            so.FindProperty("factionsPanel").objectReferenceValue = factionsPanelGo;
            so.FindProperty("panelsRoot").objectReferenceValue = panelsGO.GetComponent<RectTransform>();
            so.FindProperty("bottomBarRoot").objectReferenceValue = bottomGO.GetComponent<RectTransform>();
            so.FindProperty("dimOverlay").objectReferenceValue = dimGO.GetComponent<Image>();
            so.FindProperty("alertBanner").objectReferenceValue = alertGO.GetComponent<RectTransform>();
            so.FindProperty("alertText").objectReferenceValue = alertGO.GetComponentInChildren<TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();

            ForceTruncateOverflowOnHudText(canvasGO.transform);

            Selection.activeGameObject = canvasGO;
            Debug.Log("[WorldHUDSetupTool] WorldHUDCanvas built successfully.", canvasGO);
        }

    }
}
