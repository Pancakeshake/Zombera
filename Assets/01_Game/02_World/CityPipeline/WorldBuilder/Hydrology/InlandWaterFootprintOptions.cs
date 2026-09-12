using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Serialized-profile values that drive footprint control-point density, fitting tolerances and
    /// the bounded carve/fit/repair loop. Kept separate from the Crest spline presets because the
    /// carver and the road builder consume it without any Crest dependency.
    /// </summary>
    public readonly struct InlandWaterFootprintOptions
    {
        public readonly float MinPointSpacingMeters;
        public readonly float MaxPointSpacingMeters;
        public readonly float LateralToleranceMeters;
        public readonly float WidthToleranceMeters;
        public readonly float HeightToleranceMeters;
        public readonly float BankHeightToleranceMeters;
        public readonly float MinimumBedClearanceMeters;
        public readonly float MaximumBankExtensionMeters;
        public readonly int RepairPassLimit;

        public InlandWaterFootprintOptions(
            float minPointSpacingMeters,
            float maxPointSpacingMeters,
            float lateralToleranceMeters,
            float widthToleranceMeters,
            float heightToleranceMeters,
            float bankHeightToleranceMeters,
            float minimumBedClearanceMeters,
            float maximumBankExtensionMeters,
            int repairPassLimit)
        {
            MinPointSpacingMeters = Mathf.Max(0f, minPointSpacingMeters);
            MaxPointSpacingMeters = Mathf.Max(MinPointSpacingMeters, maxPointSpacingMeters);
            LateralToleranceMeters = Mathf.Max(0.05f, lateralToleranceMeters);
            WidthToleranceMeters = Mathf.Max(0.05f, widthToleranceMeters);
            HeightToleranceMeters = Mathf.Max(0.01f, heightToleranceMeters);
            BankHeightToleranceMeters = Mathf.Max(0.01f, bankHeightToleranceMeters);
            MinimumBedClearanceMeters = Mathf.Max(0.05f, minimumBedClearanceMeters);
            MaximumBankExtensionMeters = Mathf.Max(0f, maximumBankExtensionMeters);
            RepairPassLimit = Mathf.Max(0, repairPassLimit);
        }

        /// <summary>Authored defaults, matching the WorldWaterProfile field initializers.</summary>
        public static InlandWaterFootprintOptions Default => new(
            minPointSpacingMeters: 16f,
            maxPointSpacingMeters: 128f,
            lateralToleranceMeters: 8f,
            widthToleranceMeters: 8f,
            heightToleranceMeters: 0.25f,
            bankHeightToleranceMeters: 0.25f,
            minimumBedClearanceMeters: 0.5f,
            maximumBankExtensionMeters: 32f,
            repairPassLimit: 2);

        /// <summary>True when the struct carries real values rather than <c>default</c>.</summary>
        public bool IsSpecified => MaxPointSpacingMeters > 0f;

        public InlandWaterFootprintOptions Normalized() => IsSpecified ? this : Default;

        public static InlandWaterFootprintOptions FromWater(WorldWaterProfile water)
        {
            if (water == null)
                return Default;
            return new InlandWaterFootprintOptions(
                water.MinPointSpacing,
                water.MaxPointSpacing,
                water.MaximumEdgeError,
                water.MaximumEdgeError,
                water.BankHeightTolerance,
                water.BankHeightTolerance,
                water.MinimumBedClearance,
                water.MaximumEdgeError * 4f,
                water.RepairPassLimit);
        }
    }
}
