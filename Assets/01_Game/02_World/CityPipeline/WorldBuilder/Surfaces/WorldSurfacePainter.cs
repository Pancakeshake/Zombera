using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Scene-visible surface painter; sync backend is a serialized MonoBehaviour cast.</summary>
    [AddComponentMenu("Zombera/World/World Surface Painter")]
    [DisallowMultipleComponent]
    public sealed partial class WorldSurfacePainter : MonoBehaviour, IWorldSurfacePainter
    {
        [SerializeField] private MonoBehaviour _syncBackendSource;
        [Header("Main grass blend (layers 0,1)")]
        [SerializeField] private int _mainGrassNoiseSeed = 0x47726173;
        [SerializeField] private float _mainGrassNoiseScaleMeters = 70f;
        [SerializeField, Range(0f, 1f)] private float _mainGrassYellowScale = 1f;
        [Header("Beach / shore sand blend")]
        [SerializeField] private int _beachBlendNoiseSeed = 0x42656163;
        [SerializeField] private float _beachNoiseScaleMeters = 55f;
        [SerializeField] private float _beachDetailNoiseScaleMeters = 22f;
        [SerializeField] private float _beachInlandBlendMeters = 55f;
        [SerializeField] private float _beachMaxElevationAboveSea = 7f;
        [SerializeField] private float _oceanBeachMaxWaterDistanceMeters = 140f;
        [SerializeField] private float _inlandWetSandFadeMeters = 6f;
        [Header("Mountain snow / rock blend")]
        [SerializeField] private int _mountainBlendNoiseSeed = 0x4D6F756E;
        [SerializeField] private float _cliffSnowMaxWeight = 0.88f;
        [SerializeField] private float _snowDetailNoiseScaleMeters = 22f;
        [SerializeField] private float _rockExposureNoiseScaleMeters = 42f;
        // Tuned for WorldBuilder grids with ~1200–1400 m real peak budget (terrain Y often 1600).
        [SerializeField] private float _rockMinElevationMeters = 350f;
        [SerializeField] private float _rockFullElevationMeters = 550f;
        [SerializeField] private float _dirtBandMinElevationMeters = 180f;
        [SerializeField] private float _dirtBandFullElevationMeters = 380f;
        [SerializeField] private float _snowMinElevationMeters = 500f;
        [SerializeField] private float _snowFullElevationMeters = 750f;
        [Tooltip("World-space noise scale for snow-line height jitter (meters).")]
        [SerializeField] private float _snowLineNoiseScaleMeters = 220f;
        [Tooltip("Snow line height jitter amplitude (±meters).")]
        [SerializeField] private float _snowLineNoiseAmplitudeMeters = 50f;
        [Tooltip("Above this elev (m above sea), peak surfaces begin replacing other layers with dark rock / snow-rock / snow only. Starts after snowFull so mid-slope keeps grass/dirt.")]
        [SerializeField] private float _peakExclusiveMinElevationMeters = 780f;
        [Tooltip("Above this elev (m above sea), peaks are painted entirely with CliffDark + SnowRock + Snow.")]
        [SerializeField] private float _peakExclusiveFullElevationMeters = 980f;
        [Header("Alphamap soft blend")]
        [SerializeField] private bool _softenAlphamapEdges = true;
        [SerializeField, Range(1, 4)] private int _alphamapSoftRadius = 2;
        [SerializeField, Range(0f, 1f)] private float _alphamapSoftStrength = 0.75f;
        [Header("Surface paint quality")]
        [Tooltip("Alphamap PaintTexel stride for Quality mode (2 = 4× fewer samples than full-res).")]
        [SerializeField, Range(1, 4)] private int _qualityPaintTexelStride = 2;
        [SerializeField, Range(1, 6)] private int _coarseSoftRadius = 4;
        [SerializeField, Range(0f, 1f)] private float _coarseSoftStrength = 1f;
        [Tooltip("Soften after paint (alphamap path) or after upscale (coarse path).")]
        [SerializeField, Range(0, 6)] private int _postUpscaleSoftRadius = 3;
        [SerializeField, Range(0f, 1f)] private float _postUpscaleSoftStrength = 0.92f;
        [SerializeField, Range(1, 3)] private int _postUpscaleSoftPasses = 2;
        private IWorldSurfaceSyncBackend _syncBackend;
        private WorldSurfacePalette _palette;
        private WorldBiomePalette _biomePalette;
        private WorldMapSession _paintSession;
        private LandformProfile _paintLandformProfile;
        private WorldMapBoundaryLayout _paintBoundaryLayout;
        private bool _paintBoundaryReady;
        private List<WorldBiomeRecord> _cachedNaturalRecords;
        private HydrologyPlan _cachedSeaLevelPlan;
        private float _cachedSeaLevel;
        private bool _paintContextReady;
        private LandformField _paintLandforms;
        private float _paintInvLandformCellSize;
        private Vector2 _paintLandformOrigin;
        private DeterministicNoise2D _beachBlendNoise;
        private DeterministicNoise2D _mountainBlendNoise;
        private DeterministicNoise2D _mainGrassNoise;
        public IWorldSurfaceSyncBackend SyncBackend
        {
            get
            {
                if (_syncBackend == null) ValidateBackend(log: false);
                return _syncBackend;
            }
        }
        private void Awake() => ValidateBackend(log: true);
        private void OnValidate()
        {
            _beachBlendNoise = null;
            _mountainBlendNoise = null;
            _mainGrassNoise = null;
            ValidateBackend(log: false);
        }
        public void PreparePalette(WorldSurfacePalette palette)
        {
            if (_palette != palette)
            {
                _microSplatBoundTerrains.Clear();
                _terrainLayersReady.Clear();
            }
            _palette = palette;
            EnsureLayerIndexCache(palette);
        }
        public void EnsureMicroSplatTemplate(Terrain terrain)
        {
            if (terrain == null || _palette == null)
                return;
            if (MicroSplatTerrainBinder.IsBound(terrain, _palette))
            {
                _microSplatBoundTerrains.Add(terrain.GetInstanceID());
                _terrainLayersReady.Add(terrain.GetInstanceID());
                return;
            }
            MicroSplatTerrainBinder.Bind(terrain, _palette, syncMicroSplat: true);
            _microSplatBoundTerrains.Add(terrain.GetInstanceID());
            _terrainLayersReady.Add(terrain.GetInstanceID());
        }

        /// <summary>Marks a terrain dirty for the end-of-stage MicroSplat control flush.</summary>
        public void MarkPaintedDirty(Terrain terrain)
        {
            if (terrain?.terrainData == null)
                return;
            var data = terrain.terrainData;
            MarkOrSyncPaintedTile(terrain, data.alphamapWidth, data.alphamapHeight);
        }
        public void BindTerrain(Terrain terrain, WorldSurfacePalette palette)
        {
            _palette = palette;
            if (terrain == null || palette == null) return;
            MicroSplatTerrainBinder.Bind(terrain, palette);
        }
        public void BindBiomePalette(WorldBiomePalette biomePalette)
        {
            if (_biomePalette == biomePalette)
                return;
            _biomePalette = biomePalette;
            _cachedNaturalRecords = null;
        }
        private const float BeachOuterMarginMeters = 24f;
        private float _cachedBeachOuterMeters;
        public void BindPaintContext(WorldMapSession session, LandformProfile landformProfile)
        {
            _paintSession = session;
            _paintLandformProfile = landformProfile;
            _paintContextReady = landformProfile != null;
            _paintBoundaryReady = landformProfile != null && session.TilesPerSide > 0;
            _paintBoundaryLayout = _paintBoundaryReady
                ? WorldMapBoundaryLayout.Resolve(session, landformProfile)
                : default;
            _cachedNaturalRecords = null;
            _cachedSeaLevelPlan = null;
            _paintSessionGridsReady = false;
            _loggedCoastalBlendPath = false;
            ClearCellSnapshots();
            _cachedBeachOuterMeters = landformProfile != null
                ? landformProfile.OceanCoastStripWidthMeters + BeachOuterMarginMeters
                : 0f;
        }
        public bool BindSyncBackend(MicroSplatAlphamapSyncBackend syncBackend)
        {
            if (syncBackend == null)
                return false;
            if (_syncBackendSource == syncBackend)
                return false;
            _syncBackendSource = syncBackend;
            ValidateBackend(log: false);
            return true;
        }
        public void PaintNaturalSurface(
            WorldTileInfo tile,
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes)
        {
            if (!WorldTileInfoUtility.TryGetLiveTerrain(tile, out var terrain)) return;
            if (landforms == null || biomes == null) return;
            if (_palette == null) return;
            EnsureMicroSplatTemplate(terrain);
            if (!_terrainLayersReady.Contains(terrain.GetInstanceID()) ||
                !MicroSplatTerrainBinder.IsBound(terrain, _palette))
            {
                if (!MicroSplatTerrainBinder.EnsureTerrainLayers(terrain, _palette))
                {
                    Debug.LogError(
                        "[WorldSurfacePainter] Cannot paint '" + terrain.name +
                        "' — terrain has 0 alphamap layers after bind.",
                        terrain);
                    return;
                }
                // Controls sync once at stage flush (SyncControlTexturesOnly) — not per tile.
                _terrainLayersReady.Add(terrain.GetInstanceID());
            }
            var data = terrain.terrainData;
            var w = data.alphamapWidth;
            var h = data.alphamapHeight;
            var layers = data.alphamapLayers;
            if (layers <= 0)
                return;
            var map = RentAlphamapBuffer(w, h, layers);
            var origin = terrain.transform.position;
            var size = data.size;
            float seaLevel;
            if (_cachedSeaLevelPlan == water)
            {
                seaLevel = _cachedSeaLevel;
            }
            else
            {
                seaLevel = EstimateSeaLevel(water);
                _cachedSeaLevel = seaLevel;
            }
            _cachedSeaLevelPlan = water;
            var naturalRecords = _cachedNaturalRecords ??= CollectNaturalBiomeRecords(_biomePalette);
            EnsureNaturalRecordIndices(biomes, naturalRecords);
            EnsureLayerIndexCache(_palette);
            _paintLandforms = landforms;
            _paintLandformOrigin = landforms.OriginXZ;
            _paintInvLandformCellSize = 1f / landforms.CellSize;
            if (!_paintSessionGridsReady)
                ClearCellSnapshots();
            if (_useCellResolutionPaint && landforms.CellSize > 0.01f)
            {
                PaintNaturalSurfaceAtCellResolution(new NaturalSurfacePaintArgs
                {
                    Terrain = terrain,
                    Data = data,
                    Map = map,
                    Width = w,
                    Height = h,
                    Layers = layers,
                    Origin = origin,
                    Size = size,
                    Landforms = landforms,
                    Water = water,
                    Biomes = biomes,
                    SeaLevel = seaLevel,
                    NaturalRecords = naturalRecords
                });
                return;
            }

            PaintNaturalSurfaceAtAlphamapResolution(new NaturalSurfacePaintArgs
            {
                Terrain = terrain,
                Data = data,
                Map = map,
                Width = w,
                Height = h,
                Layers = layers,
                Origin = origin,
                Size = size,
                Landforms = landforms,
                Water = water,
                Biomes = biomes,
                SeaLevel = seaLevel,
                NaturalRecords = naturalRecords
            });
        }
        public void PaintInfrastructure(WorldTileInfo tile, WorldBuildArtifacts artifacts)
        {
            _ = tile;
            _ = artifacts;
        }
        public IEnumerator SyncDirtyTiles()
        {
            var backend = SyncBackend;
            if (backend == null) yield break;
            var flush = backend.Flush();
            while (flush.MoveNext())
                yield return flush.Current;
        }
        private void AddLayer(float[,,] map, int z, int x, int layers, string semantic, float amount)
        {
            AddLayerForMacro(map, z, x, layers, semantic, amount);
        }

        /// <summary>Non-allocating macro paint entry (avoids per-texel lambda).</summary>
        internal void AddLayerForMacro(
            float[,,] map,
            int z,
            int x,
            int layers,
            string semantic,
            float amount)
        {
            if (amount <= 0f || string.IsNullOrEmpty(semantic))
                return;
            if (!_layerIndexBySemantic.TryGetValue(semantic, out var layer))
                return;
            AddLayerByIndex(map, z, x, layers, layer, amount);
        }
        private static List<WorldBiomeRecord> CollectNaturalBiomeRecords(WorldBiomePalette palette)
        {
            var list = new List<WorldBiomeRecord>(8);
            if (palette?.Biomes == null) return list;
            for (var i = 0; i < palette.Biomes.Count; i++)
            {
                var record = palette.Biomes[i];
                if (record == null || record.IsCityAreaOverlay) continue;
                list.Add(record);
            }
            return list;
        }
        private static float EstimateSeaLevel(HydrologyPlan water)
        {
            if (water?.WaterClass == null) return 0f;
            for (var i = 0; i < water.WaterClass.Length; i++)
            {
                if (water.WaterClass[i] != WorldWaterClass.Ocean) continue;
                return water.SurfaceWorldY[i];
            }
            return 0f;
        }
        private void SamplePlanningCell(float worldX, float worldZ, out int x, out int z)
        {
            if (_paintLandforms == null)
            {
                x = 0;
                z = 0;
                return;
            }
            x = Mathf.Clamp(
                Mathf.FloorToInt((worldX - _paintLandformOrigin.x) * _paintInvLandformCellSize),
                0,
                _paintLandforms.Width - 1);
            z = Mathf.Clamp(
                Mathf.FloorToInt((worldZ - _paintLandformOrigin.y) * _paintInvLandformCellSize),
                0,
                _paintLandforms.Height - 1);
        }
        private static void SamplePlanningCell(
            LandformField field,
            float worldX,
            float worldZ,
            out int x,
            out int z)
        {
            x = Mathf.Clamp(
                Mathf.FloorToInt((worldX - field.OriginXZ.x) / field.CellSize),
                0,
                field.Width - 1);
            z = Mathf.Clamp(
                Mathf.FloorToInt((worldZ - field.OriginXZ.y) / field.CellSize),
                0,
                field.Height - 1);
        }
        private void ValidateBackend(bool log)
        {
            _syncBackend = null;
            if (_syncBackendSource == null)
            {
                if (log)
                    Debug.LogWarning("[WorldSurfacePainter] Sync backend source is not assigned.", this);
                return;
            }
            if (_syncBackendSource is IWorldSurfaceSyncBackend backend)
            {
                _syncBackend = backend;
                return;
            }
            if (log)
            {
                Debug.LogError(
                    $"[WorldSurfacePainter] '{_syncBackendSource.name}' does not implement IWorldSurfaceSyncBackend.",
                    this);
            }
        }
    }
}
