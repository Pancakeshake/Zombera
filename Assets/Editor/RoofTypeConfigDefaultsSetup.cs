#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     One-shot setup: creates the default RoofTypeConfig assets
    ///     under <c>Assets/02_Shared/ScriptableObjects/Buildings/RoofTypes/</c>.
    ///     Run via Tools → Build → Mod Kits → Building Generator → Setup Default Roof Types.
    /// </summary>
    internal static class RoofTypeConfigDefaultsSetup
    {
        private const string OutputFolder = "Assets/02_Shared/ScriptableObjects/Buildings/RoofTypes";
        private const string KitPartsFolder = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts";

        private const string MenuPath =
            "Tools/Build/Mod Kits/Building Generator/Setup Default Roof Types";

        [MenuItem(MenuPath, priority = -495)]
        private static void SetupDefaults()
        {
            EnsureFolder(OutputFolder);

            // ── Gable Roof (matches original hardcoded PlaceScaledRoofAssembly params) ──
            var gablePath = $"{OutputFolder}/GableRoof_Default.asset";
            var gableConfig = GetOrCreateConfig(gablePath, "Gable Roof (Default)");
            gableConfig.Style = RoofStyle.Gable;

            // Scale params (matching original hardcoded values)
            gableConfig.PeakHeightMultiplier = 1f;
            gableConfig.PanelDiagonalNorm = 2.2f;
            gableConfig.PanelWidthMultiplier = 0.5f;
            gableConfig.PanelZOverhang = 0.1f;
            gableConfig.GableXSnugness = 0.98f;
            gableConfig.GableYBaseHeight = 1.5f;

            // Position offsets
            gableConfig.RidgeYOffset = 0.04f;
            gableConfig.RidgeYScale = 2f;
            gableConfig.RidgeZOverhang = 0.11f;
            gableConfig.PanelXOffset = 0.07f;
            gableConfig.PanelYOffset = 0.04f;
            gableConfig.PanelXPositionMultiplier = 0.5f;
            gableConfig.PanelYPositionMultiplier = 0.5f;
            gableConfig.GableYPositionMultiplier = 0.5f;

            // Try to auto-assign kit prefabs
            gableConfig.RidgePrefab = LoadPrefab($"{KitPartsFolder}/Roof_Ridge.prefab");
            gableConfig.PanelPrefab = LoadPrefab($"{KitPartsFolder}/Roof_Panel.prefab");
            gableConfig.GablePrefab = LoadPrefab($"{KitPartsFolder}/Roof_Gable.prefab");

            EditorUtility.SetDirty(gableConfig);
            Debug.Log($"[RoofTypeConfigDefaultsSetup] Created/updated '{gableConfig.DisplayName}' at {gablePath}.");

            // ── Flat Roof ──
            var flatPath = $"{OutputFolder}/FlatRoof_Default.asset";
            var flatConfig = GetOrCreateConfig(flatPath, "Flat Roof");
            flatConfig.Style = RoofStyle.Flat;
            flatConfig.TilePrefab = LoadPrefab($"{KitPartsFolder}/Floor.prefab");

            EditorUtility.SetDirty(flatConfig);
            Debug.Log($"[RoofTypeConfigDefaultsSetup] Created/updated '{flatConfig.DisplayName}' at {flatPath}.");

            // ── Saltbox Roof ──
            var saltboxPath = $"{OutputFolder}/SaltboxRoof_Default.asset";
            var saltboxConfig = GetOrCreateConfig(saltboxPath, "Saltbox Roof");
            saltboxConfig.Style = RoofStyle.Saltbox;

            // Reuse gable geometry params (same PeakHeight, panel norm, etc.)
            saltboxConfig.PeakHeightMultiplier = 1f;
            saltboxConfig.PanelDiagonalNorm = 2.2f;
            saltboxConfig.PanelZOverhang = 0.1f;
            saltboxConfig.GableYBaseHeight = 1.5f;

            // Saltbox-specific
            saltboxConfig.SaltboxRidgeOffset = 0.33f;
            saltboxConfig.SaltboxRidgeTowardPositiveX = true;

            // Ridge offsets
            saltboxConfig.RidgeYOffset = 0.04f;
            saltboxConfig.RidgeYScale = 2f;
            saltboxConfig.RidgeZOverhang = 0.11f;

            // Panel offsets
            saltboxConfig.PanelXOffset = 0.07f;
            saltboxConfig.PanelYOffset = 0.04f;
            saltboxConfig.PanelXPositionMultiplier = 0.5f;
            saltboxConfig.PanelYPositionMultiplier = 0.5f;

            // Gable offset
            saltboxConfig.GableYPositionMultiplier = 0.5f;

            // Assign kit pieces: Ridge + Panel from gable, Gable = right-angle piece
            saltboxConfig.RidgePrefab = LoadPrefab($"{KitPartsFolder}/Roof_Ridge.prefab");
            saltboxConfig.PanelPrefab = LoadPrefab($"{KitPartsFolder}/Roof_Panel.prefab");
            saltboxConfig.SaltboxGablePrefab = LoadPrefab($"{KitPartsFolder}/Gable_RightAngle_3m.prefab");

            EditorUtility.SetDirty(saltboxConfig);
            Debug.Log($"[RoofTypeConfigDefaultsSetup] Created/updated '{saltboxConfig.DisplayName}' at {saltboxPath}.");

            // ── Gambrel Roof ──
            var gambrelPath = $"{OutputFolder}/GambrelRoof_Default.asset";
            var gambrelConfig = GetOrCreateConfig(gambrelPath, "Gambrel Roof");
            gambrelConfig.Style = RoofStyle.Gambrel;

            gambrelConfig.PeakHeightMultiplier = 1f;
            gambrelConfig.PanelDiagonalNorm = 2.2f;
            gambrelConfig.PanelWidthMultiplier = 0.5f;
            gambrelConfig.PanelZOverhang = 0.1f;
            gambrelConfig.GableXSnugness = 0.98f;

            // Gambrel profile
            gambrelConfig.GambrelBreakHeight = 0.45f;
            gambrelConfig.GambrelBreakWidth = 0.40f;
            gambrelConfig.GambrelRidgeWidth = 0.10f;

            // Ridge offsets
            gambrelConfig.RidgeYOffset = 0.04f;
            gambrelConfig.RidgeYScale = 2f;
            gambrelConfig.RidgeZOverhang = 0.11f;

            // Panel offsets
            gambrelConfig.PanelXOffset = 0.07f;
            gambrelConfig.PanelYOffset = 0.04f;
            gambrelConfig.PanelXPositionMultiplier = 0.5f;
            gambrelConfig.PanelYPositionMultiplier = 0.5f;

            gambrelConfig.RidgePrefab = LoadPrefab($"{KitPartsFolder}/Roof_Ridge.prefab");
            gambrelConfig.PanelPrefab = LoadPrefab($"{KitPartsFolder}/Roof_Panel.prefab");
            gambrelConfig.GambrelGablePrefab = LoadPrefab($"{KitPartsFolder}/Roof_Gable.prefab");

            EditorUtility.SetDirty(gambrelConfig);
            Debug.Log($"[RoofTypeConfigDefaultsSetup] Created/updated '{gambrelConfig.DisplayName}' at {gambrelPath}.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = gableConfig;
            EditorGUIUtility.PingObject(gableConfig);

            EditorUtility.DisplayDialog("Setup Default Roof Types",
                $"Created 4 roof type configs under {OutputFolder}:\n" +
                $"• GableRoof_Default (Gable style, matching original params)\n" +
                $"• FlatRoof_Default (Flat style, uses Floor.prefab as tile)\n" +
                $"• SaltboxRoof_Default (Saltbox style, offset ridge, right-angle gables)\n" +
                $"• GambrelRoof_Default (Barn-style two-slope, reuses panel + ridge)\n\n" +
                "Assign these to your ResidentialHouseTemplate → Allowed Roof Types in the inspector.",
                "OK");
        }

        private static RoofTypeConfig GetOrCreateConfig(string path, string displayName)
        {
            if (File.Exists(ToAbsolutePath(path)))
            {
                var existing = AssetDatabase.LoadAssetAtPath<RoofTypeConfig>(path);
                if (existing != null)
                    return existing;
            }

            var config = ScriptableObject.CreateInstance<RoofTypeConfig>();
            config.DisplayName = displayName;
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        private static GameObject LoadPrefab(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            assetFolderPath = assetFolderPath.Replace('\\', '/');
            var parts = assetFolderPath.Split('/');
            if (parts.Length < 2 || parts[0] != "Assets")
                return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", System.StringComparison.Ordinal))
                return assetPath;
            var tail = assetPath.Length > "Assets/".Length ? assetPath["Assets/".Length..] : string.Empty;
            return Path.Combine(Application.dataPath, tail);
        }
    }
}
#endif
