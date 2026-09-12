namespace Zombera.World.CityPipeline.WorldBuilder
{
    public readonly struct WorldBiomeSample
    {
        public readonly int DominantBiomeIndex;
        public readonly string DominantStableId;
        public readonly float Dominance;
        public readonly float Temperature;
        public readonly float Moisture;

        public WorldBiomeSample(
            int dominantBiomeIndex,
            string dominantStableId,
            float dominance,
            float temperature,
            float moisture)
        {
            DominantBiomeIndex = dominantBiomeIndex;
            DominantStableId = dominantStableId;
            Dominance = dominance;
            Temperature = temperature;
            Moisture = moisture;
        }
    }
}
