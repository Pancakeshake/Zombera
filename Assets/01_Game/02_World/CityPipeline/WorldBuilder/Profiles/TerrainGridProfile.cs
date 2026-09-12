using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Per-tile TerrainData resolution and vertical extents for the finite grid.</summary>
    [CreateAssetMenu(
        fileName = "TerrainGridProfile",
        menuName = "Zombera/World/Terrain Grid Profile")]
    public sealed class TerrainGridProfile : ScriptableObject
    {
        [SerializeField] private int heightmapResolution = 513;
        [SerializeField] private int alphamapResolution = 512;
        [SerializeField] private int baseMapResolution = 1024;
        [SerializeField] private float terrainVerticalSize = 1000f;
        [SerializeField] private float seaLevelOffsetY = -200f;
        [SerializeField] private int detailResolution = 512;
        [SerializeField] private int detailSamplesPerPatch = 16;

        public int HeightmapResolution => heightmapResolution;
        public int AlphamapResolution => alphamapResolution;
        public int BaseMapResolution => baseMapResolution;
        public float TerrainVerticalSize => terrainVerticalSize;
        public float SeaLevelOffsetY => seaLevelOffsetY;
        public int DetailResolution => detailResolution;
        public int DetailSamplesPerPatch => detailSamplesPerPatch;

        /// <summary>Terrain GameObject base Y for a given sea level.</summary>
        public float GetTerrainBaseY(float seaLevelWorldY) => seaLevelWorldY + seaLevelOffsetY;
    }
}
