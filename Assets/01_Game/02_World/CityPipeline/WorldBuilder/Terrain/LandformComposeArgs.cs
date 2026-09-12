using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Noise sources for a landform compose pass (Sonar S107).</summary>
    public struct LandformComposeNoiseSet
    {
        public DeterministicNoise2D Continental;
        public DeterministicNoise2D Warp;
        public DeterministicNoise2D Hills;
        public DeterministicNoise2D Mountains;
        public DeterministicNoise2D Barrier;
        public DeterministicNoise2D InteriorRidge;
        public DeterministicNoise2D Rolling;
    }

    /// <summary>Precomputed inverse noise scales for a landform compose pass.</summary>
    public struct LandformComposeScales
    {
        public float InvC;
        public float InvH;
        public float InvHRidged;
        public float InvM;
        public float InvMRidged;
        public float InvMBarrier;
    }

    /// <summary>Octave counts for a landform compose pass (fast vs full quality).</summary>
    public struct LandformComposeOctaves
    {
        public int Cont;
        public int Hill;
        public int HillRidge;
        public int Mountain;
        public int MountainFine;
        public int Barrier;
        public int BarrierFine;
        public int BarrierMacro;
    }

    /// <summary>
    /// Shared compose context for <see cref="LandformGenerator"/> cell evaluation
    /// (keeps hot-path signatures under Sonar S107).
    /// </summary>
    public struct LandformComposeArgs
    {
        public LandformComposeNoiseSet Noises;
        public LandformComposeParams Profile;
        public WorldMapBoundaryLayout BoundaryLayout;
        public InteriorLandformRelief.MountainRange[] InteriorRanges;
        public Rect Bounds;
        public float SeaLevel;
        public float PlainsBias;
        public LandformComposeScales Scales;
        public LandformComposeOctaves Octaves;
        public bool FastLandforms;
        public bool UseOrogenAuthority;
    }
}
