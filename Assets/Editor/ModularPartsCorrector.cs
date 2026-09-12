#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.BuildingSystem;

namespace Zombera.Editor
{
    /// <summary>
    ///     Ensures every prefab in <c>Building_Modular_Parts</c> has the components it needs
    ///     for in-game use: <see cref="BuildPiece"/> (EasyBuild identity), <see cref="StructureHealth"/>
    ///     (destructibility), and <see cref="Light"/> where appropriate.
    ///     Run via Tools → Build → Mod Kits → Modular Parts Corrector.
    /// </summary>
    internal static class ModularPartsCorrector
    {
        private const string PartsFolder = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts";
        private const string MenuPath = "Tools/Build/Mod Kits/Modular Parts Corrector";

        [MenuItem(MenuPath, priority = -498)]
        private static void CorrectAllParts()
        {
            if (!AssetDatabase.IsValidFolder(PartsFolder))
            {
                Debug.LogError($"[ModularPartsCorrector] Folder not found: {PartsFolder}");
                return;
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PartsFolder });
            if (prefabGuids.Length == 0)
            {
                Debug.LogWarning($"[ModularPartsCorrector] No prefabs found in {PartsFolder}");
                return;
            }

            var corrected = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var guid in prefabGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(assetPath);

                if (!TryCorrectPrefab(assetPath, fileName, out var summary))
                {
                    failed++;
                    Debug.LogError($"[ModularPartsCorrector] FAILED: {fileName} — {summary}");
                    continue;
                }

                if (summary.Contains("already correct"))
                {
                    skipped++;
                    Debug.Log($"[ModularPartsCorrector] Skipped: {fileName} — {summary}");
                }
                else
                {
                    corrected++;
                    Debug.Log($"[ModularPartsCorrector] Corrected: {fileName} — {summary}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var result = corrected > 0
                ? $"Corrected {corrected}, skipped {skipped}, failed {failed}."
                : $"All {skipped} prefabs already correct.";

            Debug.Log($"[ModularPartsCorrector] Done. {result}");
            EditorUtility.DisplayDialog("Modular Parts Corrector", result, "OK");
        }

        private static bool TryCorrectPrefab(string assetPath, string fileName, out string summary)
        {
            summary = string.Empty;
            var content = PrefabUtility.LoadPrefabContents(assetPath);
            if (content == null)
            {
                summary = "could not load prefab contents";
                return false;
            }

            try
            {
                var classification = ClassifyPrefab(fileName);
                var changes = new System.Collections.Generic.List<string>();

                // ── BuildPiece (EasyBuild identity) ──
                var buildPiece = content.GetComponent<BuildPiece>();
                if (buildPiece == null)
                {
                    buildPiece = content.AddComponent<BuildPiece>();
                    changes.Add("+BuildPiece");
                }

                buildPiece.SetCategory(classification.Category);
                if (classification.Category == BuildPieceCategory.Wall)
                    buildPiece.SetWallType(classification.WallType);

                // ── EasyBuild BuildingPart (third-party, resolved via reflection) ──
                EnsureEasyBuildBuildingPart(content, changes);

                // ── StructureHealth (destructibility) ──
                var health = content.GetComponent<StructureHealth>();
                if (health == null)
                {
                    health = content.AddComponent<StructureHealth>();
                    changes.Add("+StructureHealth");
                }

                health.SetMaxHealth(classification.MaxHealth, refillCurrentHealth: true);
                health.SetDestroyGameObjectOnDeath(shouldDestroy: true);

                // ── Light (Light + Torch only) ──
                if (classification.NeedsLight)
                {
                    var light = content.GetComponent<Light>();
                    if (light == null)
                    {
                        light = content.AddComponent<Light>();
                        changes.Add("+Light");
                    }

                    light.type = classification.LightType;
                    light.range = classification.LightRange;
                    light.intensity = classification.LightIntensity;
                    light.color = classification.LightColor;
                    light.shadows = LightShadows.None;
                }

                if (changes.Count == 0)
                {
                    summary = "already correct";
                    return true;
                }

                PrefabUtility.SaveAsPrefabAsset(content, assetPath);
                summary = string.Join(", ", changes) +
                          $" | cat={classification.Category}" +
                          (classification.Category == BuildPieceCategory.Wall
                              ? $", wall={classification.WallType}"
                              : string.Empty) +
                          $", hp={classification.MaxHealth}";
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(content);
            }
        }

        // ── EasyBuild BuildingPart (reflection, no hard dependency) ──────────

        private static Type s_buildingPartType;
        private static bool s_buildingPartTypeResolved;

        private static void EnsureEasyBuildBuildingPart(GameObject root, System.Collections.Generic.List<string> changes)
        {
            if (!s_buildingPartTypeResolved)
            {
                s_buildingPartType = Type.GetType(
                    "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.BuildingPart, " +
                    "MindCodeInteractive.EasyBuildSystem.Runtime");
                s_buildingPartTypeResolved = true;

                if (s_buildingPartType == null)
                    s_buildingPartType = Type.GetType(
                        "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.BuildingPart");
            }

            if (s_buildingPartType == null)
                return; // EasyBuild not in project — nothing to add

            var existing = root.GetComponent(s_buildingPartType);
            if (existing != null)
                return;

            root.AddComponent(s_buildingPartType);
            changes.Add("+BuildingPart");
        }

        // ── Classification by filename ──────────────────────────────────────

        private struct PartClassification
        {
            public BuildPieceCategory Category;
            public WallPieceType WallType;
            public float MaxHealth;
            public bool NeedsLight;
            public LightType LightType;
            public float LightRange;
            public float LightIntensity;
            public Color LightColor;
        }

        private static PartClassification ClassifyPrefab(string fileName)
        {
            var lower = fileName.ToLowerInvariant();

            // ── Walls ──
            if (lower.Contains("wall") && !lower.Contains("half"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Wall,
                    WallType = WallPieceType.Full,
                    MaxHealth = 150f
                };

            if (lower.Contains("half_wall") || lower.Contains("halfwall"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Wall,
                    WallType = WallPieceType.Full,
                    MaxHealth = 100f
                };

            if (lower.Contains("window"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Wall,
                    WallType = WallPieceType.Window,
                    MaxHealth = 100f
                };

            if (lower.Contains("door"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Wall,
                    WallType = WallPieceType.Door,
                    MaxHealth = 150f
                };

            // ── Floors / Foundations ──
            if (lower.Contains("foundation") || lower.Contains("floor"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Floor,
                    WallType = WallPieceType.Full, // unused for Floor category
                    MaxHealth = lower.Contains("foundation") ? 300f : 200f
                };

            // ── Roof ──
            if (lower.Contains("roof"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Roof,
                    WallType = WallPieceType.Full,
                    MaxHealth = 150f
                };

            // ── Stairs ──
            if (lower.Contains("stair"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Utility,
                    WallType = WallPieceType.Full,
                    MaxHealth = 120f
                };

            // ── Light / Torch ──
            if (lower.Contains("torch"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Utility,
                    WallType = WallPieceType.Full,
                    MaxHealth = 40f,
                    NeedsLight = true,
                    LightType = LightType.Point,
                    LightRange = 6f,
                    LightIntensity = 2f,
                    LightColor = new Color(1f, 0.55f, 0.2f) // warm fire orange
                };

            if (lower.Contains("light"))
                return new PartClassification
                {
                    Category = BuildPieceCategory.Utility,
                    WallType = WallPieceType.Full,
                    MaxHealth = 50f,
                    NeedsLight = true,
                    LightType = LightType.Point,
                    LightRange = 8f,
                    LightIntensity = 3f,
                    LightColor = new Color(1f, 0.9f, 0.75f) // warm white
                };

            // ── Fallback ──
            return new PartClassification
            {
                Category = BuildPieceCategory.Other,
                WallType = WallPieceType.Full,
                MaxHealth = 100f
            };
        }
    }
}
#endif
