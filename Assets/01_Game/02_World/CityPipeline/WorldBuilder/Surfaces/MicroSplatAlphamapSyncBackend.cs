using System.Collections;
using System.Collections.Generic;
using JBooth.MicroSplat;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Marks dirty alphamap regions and flushes MicroSplatTerrain.Sync.</summary>
    [AddComponentMenu("Zombera/World/MicroSplat Alphamap Sync Backend")]
    [DisallowMultipleComponent]
    public sealed class MicroSplatAlphamapSyncBackend : MonoBehaviour, IWorldSurfaceSyncBackend
    {
        private readonly HashSet<Terrain> _dirty = new();

        public void MarkDirty(Terrain terrain, RectInt alphamapRect)
        {
            if (terrain == null) return;
            _ = alphamapRect;
            _dirty.Add(terrain);
        }

        public void ResetToBase(Terrain terrain, RectInt alphamapRect, int baseLayerIndex)
        {
            if (terrain?.terrainData == null) return;

            var data = terrain.terrainData;
            var w = Mathf.Clamp(alphamapRect.width, 1, data.alphamapWidth);
            var h = Mathf.Clamp(alphamapRect.height, 1, data.alphamapHeight);
            var x = Mathf.Clamp(alphamapRect.x, 0, data.alphamapWidth - 1);
            var y = Mathf.Clamp(alphamapRect.y, 0, data.alphamapHeight - 1);
            var layers = data.alphamapLayers;
            if (layers <= 0) return;

            baseLayerIndex = Mathf.Clamp(baseLayerIndex, 0, layers - 1);
            var map = data.GetAlphamaps(x, y, w, h);
            for (var zi = 0; zi < h; zi++)
            {
                for (var xi = 0; xi < w; xi++)
                {
                    for (var l = 0; l < layers; l++)
                        map[zi, xi, l] = l == baseLayerIndex ? 1f : 0f;
                }
            }

            data.SetAlphamaps(x, y, map);
            _dirty.Add(terrain);
        }

        public IEnumerator Flush()
        {
            var pending = new List<Terrain>(_dirty);
            _dirty.Clear();

            for (var i = 0; i < pending.Count; i++)
            {
                var terrain = pending[i];
                if (!WorldTileInfoUtility.TryGetLiveTerrain(
                        new WorldTileInfo(default, default, terrain, WorldTileState.None),
                        out terrain))
                    continue;

                var micro = terrain.GetComponent<MicroSplatTerrain>();
                if (micro == null || terrain.materialTemplate == null)
                    micro?.Sync();
                else
                    MicroSplatTerrainBinder.SyncControlTexturesOnly(terrain);

                // Yield occasionally so the editor stays responsive without one-frame-per-tile cost.
                if ((i & 15) == 15)
                    yield return null;
            }
        }

        public bool OwnsControlSync(Terrain terrain) => false;

        public void SyncAlphamapsToControls(Terrain terrain, float[,,] alphas, int xBase, int zBase)
        {
            _ = terrain;
            _ = alphas;
            _ = xBase;
            _ = zBase;
        }

        public bool TryGetControlTexture(Terrain terrain, int controlIndex, out Texture2D texture)
        {
            _ = terrain;
            _ = controlIndex;
            texture = null;
            return false;
        }

        public void SetControlTexture(Terrain terrain, int controlIndex, Texture2D texture)
        {
            _ = terrain;
            _ = controlIndex;
            _ = texture;
        }

        public void ApplyControls(Terrain terrain) => _ = terrain;
    }
}
