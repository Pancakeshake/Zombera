#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    internal sealed partial class CityPrefabRoadNetworkBuilderEditor : UnityEditor.Editor
    {
        // ──────────────────────────────────────────────
        //  Region (multi-city) section: active-site selection for steps 2-19
        //  plus inline editing of the assigned region asset's sites. The asset
        //  reference itself lives in the Scriptable Objects section.
        // ──────────────────────────────────────────────

        private static void DrawRegionSection(CityPrefabRoadNetworkBuilder builder)
        {
            EditorGUILayout.Space(8f);
            var headingStyle = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.90f, 0.49f, 0.13f) } };
            var descStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                { normal = { textColor = DescriptionColor } };

            EditorGUILayout.LabelField("Region (Multi-City)", headingStyle);

            var region = builder.RegionAsset;
            if (region == null)
            {
                EditorGUILayout.LabelField(
                    "No region asset assigned. Create one via Assets > Create > Zombera > City Region, "
                    + "then assign it in the Scriptable Objects section below.",
                    descStyle);
                return;
            }

            if (!builder.RegionModeEnabled)
                EditorGUILayout.HelpBox(
                    "Region mode is OFF — tick 'Multi-City (Region)' under Toggles, or run the world pipeline " +
                    "(Build EasyRoads aligns CityRegion.asset onto WorldTerrainGrid automatically).",
                    MessageType.Info);

            var sites = region.sites;
            if (sites.Count == 0)
            {
                EditorGUILayout.HelpBox("Region has no city sites — add sites below.", MessageType.Warning);
            }
            else
            {
                var siteNames = new string[sites.Count];
                for (var i = 0; i < sites.Count; i++)
                {
                    var site = sites[i];
                    siteNames[i] = site != null && !string.IsNullOrEmpty(site.displayName)
                        ? site.displayName
                        : "Site " + (i + 1);
                }

                var newIndex = EditorGUILayout.Popup("Active Site (steps 2-19)", builder.ActiveSiteIndex, siteNames);
                if (newIndex != builder.ActiveSiteIndex)
                {
                    Undo.RecordObject(builder, "Change Active Site");
                    builder.ActiveSiteIndex = newIndex;
                    MarkDirty(builder);
                }

                EditorGUILayout.LabelField(
                    "Step '1. Roads' builds every site in one pass, each with its own seeded layout, "
                    + "plus one inter-city highway per city pair, anchored at ring T-junctions. "
                    + "Steps 2-19 apply to the active site only — run them once per site.",
                    descStyle);
            }

            var regionSo = new SerializedObject(region);
            regionSo.Update();
            EditorGUILayout.PropertyField(regionSo.FindProperty("displayName"), new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("regionSeed"), new GUIContent("Region Seed"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("randomizeRegionSeedPerBuild"), new GUIContent("Randomize Seed Per Build"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("generateRegionHighways"), new GUIContent("Generate Highways"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("highwayMinLinkDistanceMeters"), new GUIContent("Min Highway Link (m)"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("sites"), new GUIContent("City Sites"), true);

            var overlapPairs = region.CountOverlappingSitePairs();
            if (overlapPairs > 0)
                EditorGUILayout.HelpBox(
                    overlapPairs + " overlapping city pair(s) detected — step '1. Roads' will auto-space " +
                    "them before building, or use 'Randomize Sites' below for a fresh scatter.",
                    MessageType.Warning);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Site Scatter", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(regionSo.FindProperty("mapTilesPerSide"), new GUIContent("Map Tiles Per Side"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("autoScatterOnBuild"), new GUIContent("Auto-Scatter On Build"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("scatterSiteCount"), new GUIContent("Target Site Count"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("randomizeCityNames"), new GUIContent("Randomize City Names"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("scatterCenterXZ"), new GUIContent("Scatter Center"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("scatterHalfExtentMeters"), new GUIContent("Scatter Half Extent (m)"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("minSiteClearanceMeters"), new GUIContent("Min Site Clearance (m)"));
            EditorGUILayout.PropertyField(regionSo.FindProperty("scatterSeed"), new GUIContent("Scatter Seed"));

            EditorGUILayout.Space(4f);
            var builderSo = new SerializedObject(builder);
            EditorGUILayout.PropertyField(
                builderSo.FindProperty("fixedRegionSeedOverride"),
                new GUIContent(
                    "Fixed Seed Override (A/B Tests)",
                    "0 = normal seed flow. Set > 0 to force the same region layout and site scatter " +
                    "on every Roads run for repeatable timing comparisons."));
            builderSo.ApplyModifiedProperties();

            region.ResolveScatterArea(out _, out var zoneHalf);
            EditorGUILayout.LabelField(
                "Scatter zone: " + (zoneHalf * 2f / 1000f).ToString("F0") + "km x " + (zoneHalf * 2f / 1000f).ToString("F0") +
                "km from " + region.mapTilesPerSide + " tile(s). Set Scatter Half Extent > 0 to override.",
                descStyle);

            if (GUILayout.Button("Randomize Sites", GUILayout.Height(26f)))
            {
                Undo.RecordObject(region, "Randomize City Sites");
                var placed = region.RandomizeSites();
                regionSo.Update();
                EditorUtility.SetDirty(region);
                MarkDirty(builder);
                if (placed < region.EffectiveScatterSiteCount)
                    Debug.LogWarning(
                        $"[CityRegion] Placed {placed}/{region.EffectiveScatterSiteCount} sites — increase the " +
                        "scatter extent or lower the clearance to fit more cities.");
            }

            var regionChanged = regionSo.hasModifiedProperties;
            regionSo.ApplyModifiedProperties();

            // Persist the region asset to disk whenever its fields changed — without
            // this, edits only live in memory and the next domain reload (every script
            // recompile) reverts them, which made toggles like 'Randomize Seed Per
            // Build' appear impossible to turn off.
            if (regionChanged)
            {
                EditorUtility.SetDirty(region);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.Space(6f);
        }
    }
}
#endif
