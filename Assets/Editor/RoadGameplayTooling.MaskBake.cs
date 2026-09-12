#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    public static partial class RoadGameplayTooling
    {
        private readonly struct MaskBakeInputs
        {
            public readonly Vector2 WorldMin;
            public readonly Vector2 WorldMax;
            public readonly int Resolution;
            public readonly float PixelsPerMeter;
            public readonly float FalloffExponent;
            public readonly List<RoadSamplePoint> RoadSamples;
            public readonly List<ResolvedRoadSpawnPoint> SpawnSamples;
            public readonly float DefaultSpawnInfluenceMeters;
            public readonly float ExtraRoadWidthMeters;

            public MaskBakeInputs(
                Vector2 worldMin,
                Vector2 worldMax,
                int resolution,
                float pixelsPerMeter,
                float falloffExponent,
                List<RoadSamplePoint> roadSamples,
                List<ResolvedRoadSpawnPoint> spawnSamples,
                float defaultSpawnInfluenceMeters,
                float extraRoadWidthMeters)
            {
                WorldMin = worldMin;
                WorldMax = worldMax;
                Resolution = resolution;
                PixelsPerMeter = pixelsPerMeter;
                FalloffExponent = falloffExponent;
                RoadSamples = roadSamples;
                SpawnSamples = spawnSamples;
                DefaultSpawnInfluenceMeters = defaultSpawnInfluenceMeters;
                ExtraRoadWidthMeters = extraRoadWidthMeters;
            }
        }

        private sealed class MaskBakeBuffers
        {
            public readonly float[] FlattenMask;
            public readonly float[] MicroSplatMask;
            public readonly float[] NavMask;
            public readonly float[] SpawnMask;
            public readonly float[] PathAreaMask;
            public readonly float[] SidewalkMask;
            public readonly float[] DecalMask;
            public readonly float[] RoadsideLotMask;

            public MaskBakeBuffers(int resolution)
            {
                var sampleCount = resolution * resolution;
                FlattenMask = new float[sampleCount];
                MicroSplatMask = new float[sampleCount];
                NavMask = new float[sampleCount];
                SpawnMask = new float[sampleCount];
                PathAreaMask = new float[sampleCount];
                SidewalkMask = new float[sampleCount];
                DecalMask = new float[sampleCount];
                RoadsideLotMask = new float[sampleCount];
            }
        }

        private static bool TryBakeMasks(
            GameplayRoadGraph graph,
            RoadGameplayMaskBakeSettings maskSettings,
            RoadGameplayMaskSet maskSet,
            out string summary)
        {
            summary = string.Empty;
            if (!ResolveBakeInputs(graph, maskSettings, maskSet, out var inputs, out summary))
                return false;

            var buffers = new MaskBakeBuffers(inputs.Resolution);
            StampRoadSampleMasks(inputs, buffers);
            StampSpawnMasks(inputs, buffers);

            SaveMaskTexturesAndAssignSet(graph, maskSet, inputs, buffers, out summary);
            return true;
        }

        private static bool ResolveBakeInputs(
            GameplayRoadGraph graph,
            RoadGameplayMaskBakeSettings maskSettings,
            RoadGameplayMaskSet maskSet,
            out MaskBakeInputs inputs,
            out string failure)
        {
            inputs = default;
            failure = string.Empty;

            if (graph == null || maskSettings == null || maskSet == null)
            {
                failure = "Could not bake masks because one or more required assets are missing.";
                return false;
            }

            if (!TryResolveMaskBounds(graph, maskSettings, out var worldMin, out var worldMax))
            {
                failure = "Could not resolve world bounds for mask baking. Check graph points or mask settings bounds.";
                return false;
            }

            var resolution = Mathf.Clamp(maskSettings.textureResolution, 128, 4096);

            var roadSamples = new List<RoadSamplePoint>(1024);
            var spawnSamples = new List<ResolvedRoadSpawnPoint>(128);
            GameplayRoadBaker.BuildRuntimeCache(
                graph,
                Mathf.Max(0.5f, maskSettings.lineSampleStepMeters),
                roadSamples,
                spawnSamples);

            if (roadSamples.Count == 0)
            {
                failure = "No road samples were generated from the graph. Add valid segments and retry.";
                return false;
            }

            var widthMeters = Mathf.Max(0f, worldMax.x - worldMin.x);
            var heightMeters = Mathf.Max(0f, worldMax.y - worldMin.y);
            var pixelsPerMeter = (resolution - 1) / Mathf.Max(1f, Mathf.Max(widthMeters, heightMeters));
            var falloffExponent = Mathf.Max(0.1f, maskSettings.falloffExponent);

            inputs = new MaskBakeInputs(
                worldMin,
                worldMax,
                resolution,
                pixelsPerMeter,
                falloffExponent,
                roadSamples,
                spawnSamples,
                Mathf.Max(0f, maskSettings.defaultSpawnInfluenceMeters),
                maskSettings.extraRoadWidthMeters);

            return true;
        }

        private static void StampRoadSampleMasks(MaskBakeInputs inputs, MaskBakeBuffers buffers)
        {
            for (var i = 0; i < inputs.RoadSamples.Count; i++)
            {
                var sample = inputs.RoadSamples[i];
                if (!TryWorldToUv(sample.position, inputs.WorldMin, inputs.WorldMax, out var uv)) continue;

                var radiusMeters = Mathf.Max(0.5f, sample.widthMeters * 0.5f + inputs.ExtraRoadWidthMeters);
                var radiusPixels = Mathf.Max(1f, radiusMeters * inputs.PixelsPerMeter);

                var flattenStrength = 1f;
                var microSplatStrength = ResolveMicroSplatStrength(sample.roadType);
                var navStrength = ResolveNavMaskStrength(sample);
                var pathStrength = sample.contributesPathArea ? 1f : 0.35f;

                StampRadialMask(
                    buffers.FlattenMask,
                    inputs.Resolution,
                    uv,
                    radiusPixels,
                    flattenStrength,
                    inputs.FalloffExponent);
                StampRadialMask(
                    buffers.MicroSplatMask,
                    inputs.Resolution,
                    uv,
                    radiusPixels,
                    microSplatStrength,
                    inputs.FalloffExponent);
                StampRadialMask(
                    buffers.NavMask,
                    inputs.Resolution,
                    uv,
                    radiusPixels,
                    navStrength,
                    inputs.FalloffExponent);
                StampRadialMask(
                    buffers.PathAreaMask,
                    inputs.Resolution,
                    uv,
                    radiusPixels,
                    pathStrength,
                    inputs.FalloffExponent);

                if (sample.supportsSidewalks && sample.sidewalkWidthMeters > 0.05f)
                {
                    var sidewalkRadiusMeters = radiusMeters + Mathf.Max(0.25f, sample.sidewalkWidthMeters);
                    var sidewalkRadiusPixels = Mathf.Max(1f, sidewalkRadiusMeters * inputs.PixelsPerMeter);
                    StampRadialMask(
                        buffers.SidewalkMask,
                        inputs.Resolution,
                        uv,
                        sidewalkRadiusPixels,
                        0.9f,
                        inputs.FalloffExponent);
                }

                if (sample.supportsDecals && sample.decalDensity > 0.01f)
                    StampRadialMask(
                        buffers.DecalMask,
                        inputs.Resolution,
                        uv,
                        radiusPixels,
                        Mathf.Clamp01(sample.decalDensity),
                        inputs.FalloffExponent);

                if (sample.roadsideLotEligible && sample.roadsideLotWeight > 0f)
                {
                    var lotRadiusMeters = radiusMeters + Mathf.Max(2f, sample.roadsideLotDepthMeters * 0.5f);
                    var lotRadiusPixels = Mathf.Max(1f, lotRadiusMeters * inputs.PixelsPerMeter);
                    StampRadialMask(
                        buffers.RoadsideLotMask,
                        inputs.Resolution,
                        uv,
                        lotRadiusPixels,
                        Mathf.Clamp01(sample.roadsideLotWeight),
                        inputs.FalloffExponent);
                }
            }
        }

        private static void StampSpawnMasks(MaskBakeInputs inputs, MaskBakeBuffers buffers)
        {
            for (var i = 0; i < inputs.SpawnSamples.Count; i++)
            {
                var spawn = inputs.SpawnSamples[i];
                if (!TryWorldToUv(spawn.position, inputs.WorldMin, inputs.WorldMax, out var uv)) continue;

                var radiusMeters = Mathf.Max(inputs.DefaultSpawnInfluenceMeters, spawn.radiusMeters);
                var radiusPixels = Mathf.Max(1f, radiusMeters * inputs.PixelsPerMeter);
                var strength = Mathf.Clamp01(spawn.weight);

                StampRadialMask(
                    buffers.SpawnMask,
                    inputs.Resolution,
                    uv,
                    radiusPixels,
                    strength,
                    inputs.FalloffExponent);
            }
        }

        private static void SaveMaskTexturesAndAssignSet(
            GameplayRoadGraph graph,
            RoadGameplayMaskSet maskSet,
            MaskBakeInputs inputs,
            MaskBakeBuffers buffers,
            out string summary)
        {
            var folder = GetAssetDirectory(maskSet);
            var graphName = graph != null && !string.IsNullOrWhiteSpace(graph.name) ? graph.name : "GameplayRoadGraph";

            var flattenPath = folder + "/" + graphName + "_TerrainFlattenMask.asset";
            var microSplatPath = folder + "/" + graphName + "_MicroSplatMask.asset";
            var navPath = folder + "/" + graphName + "_NavModifierMask.asset";
            var spawnPath = folder + "/" + graphName + "_SpawnDensityMask.asset";
            var pathAreaPath = folder + "/" + graphName + "_PathAreaMask.asset";
            var sidewalkPath = folder + "/" + graphName + "_SidewalkMask.asset";
            var decalPath = folder + "/" + graphName + "_DecalMask.asset";
            var roadsideLotPath = folder + "/" + graphName + "_RoadsideLotMask.asset";

            var flattenTexture = CreateOrUpdateTextureAsset(flattenPath, inputs.Resolution, buffers.FlattenMask);
            var microSplatTexture = CreateOrUpdateTextureAsset(microSplatPath, inputs.Resolution, buffers.MicroSplatMask);
            var navTexture = CreateOrUpdateTextureAsset(navPath, inputs.Resolution, buffers.NavMask);
            var spawnTexture = CreateOrUpdateTextureAsset(spawnPath, inputs.Resolution, buffers.SpawnMask);
            var pathAreaTexture = CreateOrUpdateTextureAsset(pathAreaPath, inputs.Resolution, buffers.PathAreaMask);
            var sidewalkTexture = CreateOrUpdateTextureAsset(sidewalkPath, inputs.Resolution, buffers.SidewalkMask);
            var decalTexture = CreateOrUpdateTextureAsset(decalPath, inputs.Resolution, buffers.DecalMask);
            var roadsideLotTexture = CreateOrUpdateTextureAsset(roadsideLotPath, inputs.Resolution, buffers.RoadsideLotMask);

            Undo.RecordObject(maskSet, "Bake Gameplay Road Masks");
            maskSet.terrainFlattenMask = flattenTexture;
            maskSet.microSplatPaintMask = microSplatTexture;
            maskSet.navModifierMask = navTexture;
            maskSet.spawnDensityMask = spawnTexture;
            maskSet.pathAreaMask = pathAreaTexture;
            maskSet.sidewalkMask = sidewalkTexture;
            maskSet.decalPlacementMask = decalTexture;
            maskSet.roadsideLotMask = roadsideLotTexture;
            maskSet.SetMetadata(inputs.WorldMin, inputs.WorldMax, inputs.Resolution);
            EditorUtility.SetDirty(maskSet);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            summary =
                "Gameplay masks baked successfully.\n\n" +
                "Terrain flatten, MicroSplat paint, nav modifier, spawn density, path area, sidewalk, decal, and roadside-lot masks are assigned on the mask set asset.\n\n" +
                "Resolution: " + inputs.Resolution + "\n" +
                "Road samples: " + inputs.RoadSamples.Count + "\n" +
                "Spawn samples: " + inputs.SpawnSamples.Count;
        }

        private static bool TryResolveMaskBounds(
            GameplayRoadGraph graph,
            RoadGameplayMaskBakeSettings settings,
            out Vector2 worldMin,
            out Vector2 worldMax)
        {
            worldMin = Vector2.zero;
            worldMax = Vector2.zero;
            if (settings == null) return false;

            if (settings.useGraphBounds && graph != null)
            {
                var bounds = graph.ComputeWorldBounds(settings.boundsPaddingMeters);
                var ext = bounds.extents;
                worldMin = new Vector2(bounds.center.x - ext.x, bounds.center.z - ext.z);
                worldMax = new Vector2(bounds.center.x + ext.x, bounds.center.z + ext.z);
            }
            else
            {
                worldMin = Vector2.Min(settings.manualWorldMinXZ, settings.manualWorldMaxXZ);
                worldMax = Vector2.Max(settings.manualWorldMinXZ, settings.manualWorldMaxXZ);
            }

            return worldMax.x > worldMin.x && worldMax.y > worldMin.y;
        }

        private static float ResolveMicroSplatStrength(GameplayRoadType roadType)
        {
            return roadType switch
            {
                GameplayRoadType.Highway => 1f,
                GameplayRoadType.Arterial => 0.9f,
                GameplayRoadType.Local => 0.75f,
                GameplayRoadType.DirtTrack => 0.55f,
                GameplayRoadType.ServiceRoad => 0.65f,
                GameplayRoadType.Footpath => 0.45f,
                _ => 0.7f
            };
        }

        private static float ResolveNavMaskStrength(RoadSamplePoint sample)
        {
            if (!sample.contributesPathArea)
                return 0.35f;

            var baseStrength = sample.navArea switch
            {
                RoadNavAreaType.Default => 0.7f,
                RoadNavAreaType.Road => 1f,
                RoadNavAreaType.Sidewalk => 0.55f,
                RoadNavAreaType.Restricted => 0.25f,
                RoadNavAreaType.Offroad => 0.4f,
                _ => 0.7f
            };

            if (sample.vehicleRoute)
                baseStrength = Mathf.Max(baseStrength, 1f);

            return baseStrength;
        }

        private static void StampRadialMask(
            float[] map,
            int resolution,
            Vector2 uv,
            float radiusPixels,
            float strength,
            float falloffExponent)
        {
            if (map == null || resolution <= 0 || radiusPixels <= 0f || strength <= 0f) return;

            var centerX = Mathf.RoundToInt(uv.x * (resolution - 1));
            var centerY = Mathf.RoundToInt(uv.y * (resolution - 1));
            var radius = Mathf.CeilToInt(radiusPixels);

            var minX = Mathf.Clamp(centerX - radius, 0, resolution - 1);
            var maxX = Mathf.Clamp(centerX + radius, 0, resolution - 1);
            var minY = Mathf.Clamp(centerY - radius, 0, resolution - 1);
            var maxY = Mathf.Clamp(centerY + radius, 0, resolution - 1);

            var invRadius = 1f / Mathf.Max(0.0001f, radiusPixels);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                var d = Mathf.Sqrt(dx * dx + dy * dy) * invRadius;
                if (d > 1f) continue;

                var w = Mathf.Pow(1f - d, falloffExponent);
                var value = Mathf.Clamp01(strength * w);
                var index = y * resolution + x;
                if (value > map[index]) map[index] = value;
            }
        }

        private static bool TryWorldToUv(Vector3 worldPosition, Vector2 worldMin, Vector2 worldMax, out Vector2 uv)
        {
            uv = default;

            var width = worldMax.x - worldMin.x;
            var height = worldMax.y - worldMin.y;
            if (width <= 0.001f || height <= 0.001f) return false;

            uv = new Vector2(
                Mathf.InverseLerp(worldMin.x, worldMax.x, worldPosition.x),
                Mathf.InverseLerp(worldMin.y, worldMax.y, worldPosition.z));

            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        private static Texture2D CreateOrUpdateTextureAsset(string path, int resolution, float[] values)
        {
            RoadEditorAssetUtility.EnsureFolderHierarchy(Path.GetDirectoryName(path)?.Replace("\\", "/") ?? DefaultAssetFolder);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width != resolution || texture.height != resolution)
            {
                if (texture != null) AssetDatabase.DeleteAsset(path);

                texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                AssetDatabase.CreateAsset(texture, path);
            }

            var colors = new Color[resolution * resolution];
            for (var i = 0; i < colors.Length; i++)
            {
                var value = values != null && i < values.Length ? Mathf.Clamp01(values[i]) : 0f;
                colors[i] = new Color(value, value, value, 1f);
            }

            texture.SetPixels(colors);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }
    }
}
#endif
