namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Hub measure protocol gate for shipping Balanced as iteration default.
    /// Lock numbers after Medium-seed Fast/Balanced@128/Quality paintTiming runs.
    /// </summary>
    public static class SurfacePaintBudgetGate
    {
        /// <summary>
        /// Prefer Balanced when Quality exceeds the Hub iteration cap and Balanced stays
        /// at or under 55% of Quality wall time.
        /// </summary>
        public static bool ShouldPreferBalanced(
            long balancedMedianMs,
            long qualityMedianMs,
            long qualityIterationCapMs,
            double maxBalancedFractionOfQuality = 0.55)
        {
            if (qualityMedianMs <= 0 || balancedMedianMs <= 0)
                return false;
            if (qualityMedianMs <= qualityIterationCapMs)
                return false;
            return balancedMedianMs <= qualityMedianMs * maxBalancedFractionOfQuality;
        }
    }
}
