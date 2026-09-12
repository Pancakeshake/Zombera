using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.Crest
{
    /// <summary>
    /// On world-tile ContentReady: refresh OceanDepthCache (terrain seabed) and attach
    /// RegisterSeaFloorDepthInput to mesh bathymetry on configured layers.
    /// </summary>
    [AddComponentMenu("Zombera/World/Crest Sea Floor Tile Binder")]
    [DisallowMultipleComponent]
    public sealed class CrestSeaFloorTileBinder : MonoBehaviour
    {
        [SerializeField] private CrestOceanWaterBackend _oceanBackend;
        [SerializeField] private WorldWaterProfile _waterProfile;
        [SerializeField]
        [Min(0f)]
        private float _refreshCooldownSeconds = 0.25f;

        private WorldTileStreamSource _bridge;
        private float _nextRefreshTime;
        private bool _refreshQueued;
        private Rect _queuedBoundsXZ;
        private bool _hasQueuedBounds;
        private bool _infrastructureBoundsLogged;

        private void Awake() => ResolveRefs();

        private void OnValidate() => ResolveRefs();

        private void OnEnable()
        {
            ResolveRefs();
            _bridge = WorldTileStreamSourceUtility.FindBridge();
            WorldTileStreamSourceUtility.SubscribeTileApplied(_bridge, HandleTileApplied);
        }

        private void OnDisable()
        {
            WorldTileStreamSourceUtility.UnsubscribeTileApplied(_bridge, HandleTileApplied);
            _bridge = null;
        }

        private void Update()
        {
            AbsorbInfrastructureModifiedBounds();
            if (!_refreshQueued || Time.unscaledTime < _nextRefreshTime)
                return;
            _refreshQueued = false;
            FlushDepthRefresh();
        }

        /// <summary>
        ///     Unions terrain bounds modified by infrastructure heightmap writes (roads, highways,
        ///     city pads) into the pending depth refresh. Bounds are only consumed once the ocean is
        ///     active, so nothing is dropped while Crest is still building.
        /// </summary>
        private void AbsorbInfrastructureModifiedBounds()
        {
            if (_oceanBackend == null || !_oceanBackend.HasActiveOcean)
                return;
            if (!TerrainHeightFlattener.ConsumeModifiedWorldBoundsXZ(out var modifiedBoundsXZ))
                return;

            QueueDepthBounds(modifiedBoundsXZ, restartCooldown: false);
            LogInfrastructureBoundsOnce(modifiedBoundsXZ);
        }

        private void LogInfrastructureBoundsOnce(Rect modifiedBoundsXZ)
        {
            if (_infrastructureBoundsLogged)
                return;

            _infrastructureBoundsLogged = true;
            Debug.Log(
                "[CrestSeaFloorTileBinder] Union-refreshing ocean depth cache over infrastructure-modified " +
                "terrain bounds " + modifiedBoundsXZ + " (inland splines untouched).",
                this);
        }

        public void BindProfile(WorldWaterProfile water)
        {
            _waterProfile = water;
            ResolveRefs();
        }

        private void HandleTileApplied(WorldTileInfo tile)
        {
            if (tile.Terrain == null)
                return;

            var geometryLayers = _waterProfile != null
                ? _waterProfile.SeaFloorGeometryLayers
                : default;
            CrestSeaFloorDepthUtility.EnsureMeshSeaFloorInputs(tile.Terrain.transform, geometryLayers);

            var rect = tile.WorldRectXZ;
            QueueDepthBounds(rect);
        }

        /// <summary>
        ///     Queues a coalesced depth-cache refresh over <paramref name="boundsXZ" />, using the
        ///     existing union API so tile streaming and infrastructure writes share one refresh.
        ///     <paramref name="restartCooldown" /> false unions without pushing the flush out, so a
        ///     long infrastructure build cannot starve the refresh indefinitely.
        /// </summary>
        private void QueueDepthBounds(Rect boundsXZ, bool restartCooldown = true)
        {
            if (boundsXZ.width <= 0f || boundsXZ.height <= 0f)
                return;

            _queuedBoundsXZ = _hasQueuedBounds ? Encapsulate(_queuedBoundsXZ, boundsXZ) : boundsXZ;
            _hasQueuedBounds = true;
            if (!restartCooldown && _refreshQueued)
                return;

            _refreshQueued = true;
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, _refreshCooldownSeconds);
        }

        private void FlushDepthRefresh()
        {
            ResolveRefs();
            if (_oceanBackend == null || !_oceanBackend.HasActiveOcean)
                return;

            var bounds = _hasQueuedBounds ? _queuedBoundsXZ : default;
            _hasQueuedBounds = false;
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                _oceanBackend.RefreshOceanDepthCacheFromLast();
                return;
            }

            // Expand slightly so neighboring seams are included when a single tile lands.
            bounds = Expand(bounds, 8f);
            _oceanBackend.RefreshOceanDepthCacheUnion(bounds);
        }

        private void ResolveRefs()
        {
            if (_oceanBackend == null)
            {
                _oceanBackend = GetComponent<CrestOceanWaterBackend>()
                                ?? GetComponentInChildren<CrestOceanWaterBackend>(true);
            }
        }

        private static Rect Encapsulate(Rect a, Rect b)
        {
            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Rect Expand(Rect bounds, float pad)
        {
            pad = Mathf.Max(0f, pad);
            return new Rect(
                bounds.xMin - pad,
                bounds.yMin - pad,
                bounds.width + pad * 2f,
                bounds.height + pad * 2f);
        }
    }
}
