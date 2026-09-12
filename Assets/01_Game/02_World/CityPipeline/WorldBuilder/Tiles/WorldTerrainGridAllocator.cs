using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Allocates Unity Terrain tiles for a finite world session grid.</summary>
    public sealed class WorldTerrainGridAllocator
    {
        public Transform Root { get; set; }

        public Terrain[] Allocate(WorldMapSession session, TerrainGridProfile gridProfile, float seaLevelWorldY)
        {
            if (gridProfile == null) return System.Array.Empty<Terrain>();

            EnsureRoot();
            var tiles = Mathf.Max(1, session.TilesPerSide);
            var terrains = new Terrain[tiles * tiles];
            var index = 0;
            for (var z = 0; z < tiles; z++)
            {
                for (var x = 0; x < tiles; x++)
                    terrains[index++] = AllocateTile(session, gridProfile, seaLevelWorldY, new WorldTileCoord(x, z));
            }

            return terrains;
        }

        public Terrain AllocateTile(
            WorldMapSession session,
            TerrainGridProfile gridProfile,
            float seaLevelWorldY,
            WorldTileCoord coord)
        {
            if (gridProfile == null) return null;

            EnsureRoot();
            var tileSize = session.TileSizeMeters > 0f ? session.TileSizeMeters : WorldMapSizeSettings.TileSizeMeters;
            var baseY = gridProfile.GetTerrainBaseY(seaLevelWorldY);
            var origin = session.WorldOriginXZ;

            var data = new TerrainData
            {
                heightmapResolution = gridProfile.HeightmapResolution,
                alphamapResolution = gridProfile.AlphamapResolution,
                baseMapResolution = gridProfile.BaseMapResolution,
                size = new Vector3(tileSize, gridProfile.TerrainVerticalSize, tileSize)
            };

            try
            {
                data.SetDetailResolution(gridProfile.DetailResolution, gridProfile.DetailSamplesPerPatch);
            }
            catch
            {
                // Detail resolution may be rejected for invalid combos; keep terrain usable.
            }

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = $"Terrain_{coord.X}_{coord.Z}";
            go.transform.SetParent(Root, false);
            go.transform.position = new Vector3(
                origin.x + coord.X * tileSize,
                baseY,
                origin.y + coord.Z * tileSize);

            return go.GetComponent<Terrain>();
        }

        private void EnsureRoot()
        {
            if (Root != null) return;
            var go = new GameObject("WorldTerrainGrid");
            Root = go.transform;
        }
    }
}
