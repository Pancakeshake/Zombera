using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    ///     Bakes the discovered-chunk set from <see cref="MapStateService" /> into a fog RenderTexture.
    /// </summary>
    public sealed class FogOfWarTextureBuilder : MonoBehaviour
    {
        [SerializeField] [Min(1)] private int chunkRadius = 64;

        private MapStateService _mapStateService;
        private RenderTexture _fogTexture;
        private Texture2D _stagingTexture;
        private Color32[] _pixelBuffer;
        private Vector2Int _lastCenterChunk = new(int.MinValue, int.MinValue);
        private bool _isDirty = true;

        public RenderTexture FogTexture => _fogTexture;
        public int ChunkRadius => chunkRadius;

        public void Configure(MapStateService mapStateService)
        {
            if (_mapStateService == mapStateService) return;

            UnbindMapStateEvents();
            _mapStateService = mapStateService;
            BindMapStateEvents();
            MarkDirty();
        }

        private void OnDestroy()
        {
            UnbindMapStateEvents();
            ReleaseTextures();
        }

        private void LateUpdate()
        {
            if (_mapStateService == null) return;

            var centerChunk = _mapStateService.CurrentPlayerChunk;
            if (centerChunk != _lastCenterChunk)
            {
                _lastCenterChunk = centerChunk;
                _isDirty = true;
            }

            if (!_isDirty) return;

            RebuildFogTexture(centerChunk);
            _isDirty = false;
        }

        private void BindMapStateEvents()
        {
            if (_mapStateService == null) return;

            _mapStateService.DiscoveredChunkAdded -= HandleDiscoveryChanged;
            _mapStateService.DiscoveredChunkAdded += HandleDiscoveryChanged;
        }

        private void UnbindMapStateEvents()
        {
            if (_mapStateService == null) return;

            _mapStateService.DiscoveredChunkAdded -= HandleDiscoveryChanged;
        }

        private void HandleDiscoveryChanged(Vector2Int _)
        {
            MarkDirty();
        }

        private void MarkDirty()
        {
            _isDirty = true;
        }

        private void EnsureTextures()
        {
            var size = Mathf.Max(3, chunkRadius * 2 + 1);

            if (_stagingTexture != null && _stagingTexture.width == size) return;

            ReleaseTextures();

            _stagingTexture = new Texture2D(size, size, TextureFormat.R8, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            _pixelBuffer = new Color32[size * size];
            _fogTexture = new RenderTexture(size, size, 0, RenderTextureFormat.R8)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "FogOfWar"
            };
            _fogTexture.Create();
        }

        private void RebuildFogTexture(Vector2Int centerChunk)
        {
            EnsureTextures();
            if (_stagingTexture == null || _pixelBuffer == null || _fogTexture == null) return;

            var size = _stagingTexture.width;
            var radius = Mathf.Max(1, chunkRadius);
            var discovered = new Color32(255, 255, 255, 255);
            var fog = new Color32(0, 0, 0, 255);

            for (var y = 0; y < size; y++)
            {
                var chunkZ = centerChunk.y + (y - radius);
                for (var x = 0; x < size; x++)
                {
                    var chunkX = centerChunk.x + (x - radius);
                    var chunk = new Vector2Int(chunkX, chunkZ);
                    _pixelBuffer[y * size + x] = _mapStateService.IsChunkDiscovered(chunk) ? discovered : fog;
                }
            }

            _stagingTexture.SetPixels32(_pixelBuffer);
            _stagingTexture.Apply(false, false);
            Graphics.Blit(_stagingTexture, _fogTexture);
        }

        private void ReleaseTextures()
        {
            if (_fogTexture != null)
            {
                _fogTexture.Release();
                Destroy(_fogTexture);
                _fogTexture = null;
            }

            if (_stagingTexture != null)
            {
                Destroy(_stagingTexture);
                _stagingTexture = null;
            }

            _pixelBuffer = null;
        }
    }
}
