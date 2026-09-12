using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Dual ShapeFFT: global heavy outer waves + local calm Blend over playable core.
    /// </summary>
    internal static partial class CrestOceanConfigurator
    {
        private const string GlobalWaveShapeName = "Crest Wave Shape";
        private const string CoastalWaveShapeName = "Crest Wave Shape Coastal";
        private const string DefaultOuterSpectrumPath =
            "Assets/03_ThirdParty/Crest/Crest-Examples/Shared/WaveSpectra/WavesModerate.asset";
        private const string DefaultCoastalSpectrumPath =
            "Assets/03_ThirdParty/Crest/Crest-Examples/Shared/WaveSpectra/WavesCalm.asset";

        private const float DefaultOuterWaveWeight = 1f;
        private const float DefaultOuterWindSpeedKph = 28f;
        private const float DefaultCoastalWaveWeight = 0.35f;
        private const float DefaultCoastalWindSpeedKph = 8f;
        private const float DefaultCoastalCalmPaddingMeters = 400f;
        private const float DefaultGlobalWindTurbulence = 0.05f;
        // ShapeFFT Blend draws shaderPass 1 — requires Gerstner Geometry (2 passes).
        // Do NOT use GerstnerPatch.mat / "Gerstner Batch Geometry" (1 pass; ShapeGerstnerBatched only).
        private const string LocalWaveShaderName = "Crest/Inputs/Animated Waves/Gerstner Geometry";
        private const string LegacyBatchGeometryShaderName =
            "Crest/Inputs/Animated Waves/Gerstner Batch Geometry";

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void HealBrokenLocalWaveShapesOnLoad()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                HealBrokenLocalWaveShapes();
            };
        }
#endif

        public static void EnsureWaveShapes(
            Transform oceanRoot,
            WorldWaterProfile water,
            Rect coreBoundsXZ,
            int oceanRingTiles)
        {
            if (oceanRoot == null)
                return;

            RemoveGerstnerShapes(oceanRoot);
            HealBrokenLocalWaveShapes(oceanRoot);

            var outerWeight = water != null ? water.OuterWaveWeight : DefaultOuterWaveWeight;
            var outerWind = water != null ? water.OuterWindSpeedKph : DefaultOuterWindSpeedKph;
            var coastalWeight = water != null ? water.CoastalWaveWeight : DefaultCoastalWaveWeight;
            var coastalWind = water != null ? water.CoastalWindSpeedKph : DefaultCoastalWindSpeedKph;
            var padding = water != null ? water.CoastalCalmPaddingMeters : DefaultCoastalCalmPaddingMeters;

            var ocean = oceanRoot.GetComponent<OceanRenderer>()
                        ?? oceanRoot.GetComponentInParent<OceanRenderer>();
            if (ocean != null)
            {
                ocean._globalWindSpeed = outerWind;
                ocean._globalWindTurbulence = DefaultGlobalWindTurbulence;
            }

            var globalFft = FindOrCreateShapeFft(oceanRoot, GlobalWaveShapeName, localWaves: false);
            ApplyShapeSettings(
                globalFft,
                ResolveSpectrum(water != null ? water.OuterSpectrum : null, DefaultOuterSpectrumPath),
                outerWeight,
                outerWind,
                ShapeWaves.ShapeBlendMode.Additive,
                respectShallowAttenuation: 1f);

            if (oceanRingTiles > 0 && coreBoundsXZ.width > 0f && coreBoundsXZ.height > 0f)
            {
                var calmBounds = ExpandBounds(coreBoundsXZ, padding);
                var coastalFft = FindOrCreateShapeFft(oceanRoot, CoastalWaveShapeName, localWaves: true);
                var coastalRenderer = coastalFft.GetComponent<MeshRenderer>();
                if (coastalRenderer == null || coastalRenderer.sharedMaterial == null)
                {
                    Debug.LogWarning(
                        "[CrestOceanConfigurator] Coastal ShapeFFT missing wave material; using global waves only.",
                        coastalFft);
                    DestroyChildNamed(oceanRoot, CoastalWaveShapeName);
                }
                else
                {
                    PlaceCoastalMesh(coastalFft.transform, calmBounds, oceanRoot);
                    ApplyShapeSettings(
                        coastalFft,
                        ResolveSpectrum(water != null ? water.CoastalSpectrum : null, DefaultCoastalSpectrumPath),
                        coastalWeight,
                        coastalWind,
                        ShapeWaves.ShapeBlendMode.Blend,
                        respectShallowAttenuation: 1f);
                    // Material may have been missing on first create; restart so InitBatches binds it.
                    RestartShapeFft(coastalFft);
                    EnableEditModeLodInput(coastalFft);
                }
            }
            else
            {
                DestroyChildNamed(oceanRoot, CoastalWaveShapeName);
            }

            RestartShapeFft(globalFft);
            EnableEditModeLodInput(globalFft);
        }

        /// <summary>Legacy single-shape entry; prefers dual setup with empty core (global only).</summary>
        public static void EnsureWaveShape(Transform oceanRoot) =>
            EnsureWaveShapes(oceanRoot, water: null, coreBoundsXZ: default, oceanRingTiles: 0);

        private static ShapeFFT FindOrCreateShapeFft(Transform oceanRoot, string objectName, bool localWaves)
        {
            var existing = FindNamedShapeFft(oceanRoot, objectName);
            if (existing != null)
            {
                EnsureMeshComponents(existing.gameObject, localWaves);
                return existing;
            }

            // Reuse a lone unnamed ShapeFFT as the global shape when upgrading old scenes.
            if (!localWaves)
            {
                var orphans = oceanRoot.GetComponentsInChildren<ShapeFFT>(true);
                for (var i = 0; i < orphans.Length; i++)
                {
                    var orphan = orphans[i];
                    if (orphan == null)
                        continue;
                    if (orphan.name == CoastalWaveShapeName)
                        continue;
                    orphan.gameObject.name = objectName;
                    EnsureMeshComponents(orphan.gameObject, localWaves: false);
                    return orphan;
                }
            }

            var go = new GameObject(objectName);
            go.transform.SetParent(oceanRoot, false);
            EnsureMeshComponents(go, localWaves);
            return go.AddComponent<ShapeFFT>();
        }

        private static ShapeFFT FindNamedShapeFft(Transform oceanRoot, string objectName)
        {
            var ffts = oceanRoot.GetComponentsInChildren<ShapeFFT>(true);
            for (var i = 0; i < ffts.Length; i++)
            {
                if (ffts[i] != null && ffts[i].name == objectName)
                    return ffts[i];
            }

            return null;
        }

        private static void EnsureMeshComponents(GameObject go, bool localWaves)
        {
            if (!localWaves)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(renderer);
                    else
                        Object.DestroyImmediate(renderer);
                }

                var filter = go.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(filter);
                    else
                        Object.DestroyImmediate(filter);
                }

                return;
            }

            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = go.AddComponent<MeshFilter>();
            if (meshFilter.sharedMesh == null || !IsCrestGeometryWaveMesh(meshFilter.sharedMesh))
                meshFilter.sharedMesh = CreateUnitQuadMesh();

            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = go.AddComponent<MeshRenderer>();
            // Crest ShapeWaves.InitBatches uses MeshRenderer.sharedMaterial for local waves.
            // Blend mode draws shaderPass 1 — material must use Gerstner Geometry (2 passes).
            if (!IsValidLocalWaveMaterial(meshRenderer.sharedMaterial))
                meshRenderer.sharedMaterial = ResolveLocalWaveMaterial();
            if (meshRenderer.sharedMaterial == null)
            {
                // Cannot drive local FFT without a material; strip renderer so Crest
                // falls back to the global Gerstner material path instead of NRE.
                if (Application.isPlaying)
                {
                    Object.Destroy(meshRenderer);
                    Object.Destroy(meshFilter);
                }
                else
                {
                    Object.DestroyImmediate(meshRenderer);
                    Object.DestroyImmediate(meshFilter);
                }

                return;
            }

            meshRenderer.enabled = false;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        /// <summary>
        /// Repairs ShapeFFT MeshRenderers with missing or wrong-pass materials
        /// (Crest NRE / "invalid pass index 1" when Blend draws Gerstner Batch Geometry).
        /// Safe to call repeatedly from edit-mode ocean activation.
        /// </summary>
        public static void HealBrokenLocalWaveShapes(Transform oceanRoot = null)
        {
            ShapeFFT[] ffts;
            if (oceanRoot != null)
            {
                ffts = oceanRoot.GetComponentsInChildren<ShapeFFT>(true);
            }
            else
            {
                ffts = Object.FindObjectsByType<ShapeFFT>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            for (var i = 0; i < ffts.Length; i++)
            {
                var fft = ffts[i];
                if (fft == null)
                    continue;

                var renderer = fft.GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;
                if (IsValidLocalWaveMaterial(renderer.sharedMaterial))
                {
                    var filter = fft.GetComponent<MeshFilter>();
                    if (filter != null && !IsCrestGeometryWaveMesh(filter.sharedMesh))
                    {
                        filter.sharedMesh = CreateUnitQuadMesh();
                        RestartShapeFft(fft);
                    }

                    continue;
                }

                var repaired = ResolveLocalWaveMaterial();
                if (repaired != null)
                {
                    renderer.sharedMaterial = repaired;
                    renderer.enabled = false;
                    var filter = fft.GetComponent<MeshFilter>();
                    if (filter != null && !IsCrestGeometryWaveMesh(filter.sharedMesh))
                        filter.sharedMesh = CreateUnitQuadMesh();
                    RestartShapeFft(fft);
                    continue;
                }

                // Cannot repair — remove local geo so Crest uses the global material path.
                if (fft.name == CoastalWaveShapeName)
                {
                    var go = fft.gameObject;
                    if (Application.isPlaying)
                        Object.Destroy(go);
                    else
                        Object.DestroyImmediate(go);
                    continue;
                }

                EnsureMeshComponents(fft.gameObject, localWaves: false);
                RestartShapeFft(fft);
            }
        }

        private static Material _localWaveMaterial;

        private static bool IsValidLocalWaveMaterial(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            // Blend path requires pass 1; Batch Geometry only has pass 0.
            if (material.shader.name == LegacyBatchGeometryShaderName)
                return false;
            if (material.shader.passCount < 2)
                return false;

            return material.shader.name == LocalWaveShaderName;
        }

        private static bool IsCrestGeometryWaveMesh(Mesh mesh)
        {
            if (mesh == null)
                return false;
            // Gerstner Geometry expects UV1 (shoreline) + UV2 (weight) for Blend pass.
            return mesh.uv2 != null && mesh.uv2.Length == mesh.vertexCount
                   && mesh.uv3 != null && mesh.uv3.Length == mesh.vertexCount;
        }

        private static Material ResolveLocalWaveMaterial()
        {
            if (IsValidLocalWaveMaterial(_localWaveMaterial))
                return _localWaveMaterial;

            _localWaveMaterial = null;

            var shader = Shader.Find(LocalWaveShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    "[CrestOceanConfigurator] Missing shader '" + LocalWaveShaderName +
                    "'; coastal ShapeFFT cannot init.");
                return null;
            }

            _localWaveMaterial = new Material(shader)
            {
                name = "Crest Coastal Wave Input (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            return _localWaveMaterial;
        }

        private static void RestartShapeFft(ShapeFFT fft)
        {
            if (fft == null)
                return;

            fft.enabled = false;
            fft.enabled = true;
        }

        private static void PlaceCoastalMesh(Transform shapeTransform, Rect calmBoundsXZ, Transform oceanRoot)
        {
            if (shapeTransform == null || oceanRoot == null)
                return;

            var seaY = oceanRoot.position.y;
            var center = new Vector3(calmBoundsXZ.center.x, seaY, calmBoundsXZ.center.y);
            shapeTransform.SetPositionAndRotation(center, Quaternion.Euler(-90f, 0f, 0f));
            shapeTransform.localScale = new Vector3(
                Mathf.Max(1f, calmBoundsXZ.width),
                Mathf.Max(1f, calmBoundsXZ.height),
                1f);
        }

        private static void ApplyShapeSettings(
            ShapeFFT fft,
            OceanWaveSpectrum spectrum,
            float weight,
            float windSpeedKph,
            ShapeWaves.ShapeBlendMode blendMode,
            float respectShallowAttenuation)
        {
            if (fft == null)
                return;

            if (spectrum != null)
                fft._spectrum = spectrum;

            fft._weight = Mathf.Clamp01(weight);
            fft._overrideGlobalWindSpeed = true;
            fft._windSpeed = Mathf.Max(0f, windSpeedKph);
            fft._respectShallowWaterAttenuation = Mathf.Clamp01(respectShallowAttenuation);
            fft._blendMode = blendMode;
        }

        private static OceanWaveSpectrum ResolveSpectrum(ScriptableObject assigned, string fallbackPath)
        {
            if (assigned is OceanWaveSpectrum typed)
                return typed;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<OceanWaveSpectrum>(fallbackPath);
#else
            return null;
#endif
        }

        public static OceanWaveSpectrum ResolveInlandSpectrum(ScriptableObject assigned, bool river)
        {
            var fallback = river ? DefaultRiverSpectrumPath : DefaultLakeSpectrumPath;
            return ResolveSpectrum(assigned, fallback);
        }

        private const string DefaultLakeSpectrumPath =
            "Assets/03_ThirdParty/Crest/Crest-Examples/LakesAndRivers/Settings/LakesAndRivers_WaveSpectrum_Lake.asset";
        private const string DefaultRiverSpectrumPath =
            "Assets/03_ThirdParty/Crest/Crest-Examples/LakesAndRivers/Settings/LakesAndRivers_WaveSpectrum_River.asset";

        private static Rect ExpandBounds(Rect bounds, float paddingMeters)
        {
            var pad = Mathf.Max(0f, paddingMeters);
            return new Rect(
                bounds.xMin - pad,
                bounds.yMin - pad,
                bounds.width + pad * 2f,
                bounds.height + pad * 2f);
        }

        private static Mesh _unitQuadMesh;

        private static Mesh CreateUnitQuadMesh()
        {
            if (_unitQuadMesh != null)
                return _unitQuadMesh;

            // Channels match Crest Gerstner Geometry (ShapeFFT mesh / Blend):
            // UV0 = wave axis, UV1.x = invNormDistToShoreline, UV2.x = vertex weight.
            _unitQuadMesh = new Mesh
            {
                name = "CrestCoastalWaveQuad"
            };
            _unitQuadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            var axis = new[]
            {
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f)
            };
            var shoreline = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f)
            };
            var weight = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f)
            };
            _unitQuadMesh.SetUVs(0, axis);
            _unitQuadMesh.SetUVs(1, shoreline);
            _unitQuadMesh.SetUVs(2, weight);
            _unitQuadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _unitQuadMesh.RecalculateNormals();
            _unitQuadMesh.RecalculateBounds();
            return _unitQuadMesh;
        }

        private static void DestroyChildNamed(Transform parent, string childName)
        {
            if (parent == null)
                return;

            var child = parent.Find(childName);
            if (child == null)
            {
                var ffts = parent.GetComponentsInChildren<ShapeFFT>(true);
                for (var i = 0; i < ffts.Length; i++)
                {
                    if (ffts[i] != null && ffts[i].name == childName)
                    {
                        child = ffts[i].transform;
                        break;
                    }
                }
            }

            if (child == null)
                return;

            var go = child.gameObject;
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}
