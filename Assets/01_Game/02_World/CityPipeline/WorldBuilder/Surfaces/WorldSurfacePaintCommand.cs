using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Queued alphamap paint request for a terrain tile.</summary>
    public sealed class WorldSurfacePaintCommand
    {
        public Terrain Terrain;
        public RectInt AlphamapRect;
        public int LayerIndex;
        public float Strength = 1f;
        public bool IsInfrastructure;
        public string SemanticName;
    }
}
