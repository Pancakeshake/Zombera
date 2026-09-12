using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     City pad footprint for landform flattening.
    ///     <see cref="PlateauBoundsXZ"/> is the weight=1 flat core (exclusive of falloff).
    /// </summary>
    public sealed class CityFlattenPad
    {
        public Rect PlateauBoundsXZ;
        public Vector2 CenterXZ;
        public float TargetHeightWorldY;
        public float FalloffMeters = 150f;
        /// <summary>Seaward apron length when <see cref="IsCoastal"/> (quay band).</summary>
        public float FalloffMetersSeaward = 48f;
        /// <summary>Runtime inland cone slope ratio (rise/run); recomputed each Apply. Not persisted.</summary>
        public float ConeSlopeRatio;
        /// <summary>Runtime seaward cone slope ratio for quay terrace.</summary>
        public float ConeSlopeSeaward;
        public float HydrologyPruneMarginMeters = 16f;
        public bool HasHighwayEntry;
        public Vector2 HighwayEntryXZ;
        public float HighwayEntryHeightWorldY;

        /// <summary>Session coastal tag derived from hydrology adjacency.</summary>
        public bool IsCoastal;
        public Vector2 SeawardNormalXZ;
        public float CoastExposure01;

        /// <summary>Diagnostics: inland dry-annulus max |Δh| used for falloff.</summary>
        public float DiagnosticsInlandMaxDelta;
        /// <summary>Diagnostics: true when inland continuity would exceed FalloffMax×cap.</summary>
        public bool DiagnosticsExceedsContinuityCap;

        public CityFlattenPad()
        {
        }

        public CityFlattenPad(Rect plateauBoundsXZ, float targetHeightWorldY, float falloffMeters = 150f)
        {
            PlateauBoundsXZ = plateauBoundsXZ;
            CenterXZ = plateauBoundsXZ.center;
            TargetHeightWorldY = targetHeightWorldY;
            FalloffMeters = Mathf.Max(1f, falloffMeters);
        }

        /// <summary>Plateau expanded by max of inland/seaward falloff.</summary>
        public Rect OuterBoundsXZ
        {
            get
            {
                var outer = Mathf.Max(FalloffMeters, IsCoastal ? FalloffMetersSeaward : FalloffMeters);
                return Expand(PlateauBoundsXZ, Mathf.Max(1f, outer));
            }
        }

        /// <summary>Plateau expanded by hydrology prune margin only (not the full apron).</summary>
        public Rect HydrologyPruneBoundsXZ =>
            Expand(PlateauBoundsXZ, Mathf.Max(0f, HydrologyPruneMarginMeters));

        public float ResolveFalloffAt(Vector2 worldXZ)
        {
            if (!IsCoastal || SeawardNormalXZ.sqrMagnitude < 0.0001f)
                return Mathf.Max(1f, FalloffMeters);

            var fromCenter = worldXZ - CenterXZ;
            if (CityCoastalPadUtility.IsSeawardBearing(fromCenter, SeawardNormalXZ))
                return Mathf.Max(1f, FalloffMetersSeaward);
            return Mathf.Max(1f, FalloffMeters);
        }

        public float ResolveConeSlopeAt(Vector2 worldXZ, float fallbackRatio)
        {
            if (!IsCoastal || SeawardNormalXZ.sqrMagnitude < 0.0001f)
                return ConeSlopeRatio > 0.0001f ? ConeSlopeRatio : fallbackRatio;

            var fromCenter = worldXZ - CenterXZ;
            if (CityCoastalPadUtility.IsSeawardBearing(fromCenter, SeawardNormalXZ))
                return ConeSlopeSeaward > 0.0001f ? ConeSlopeSeaward : fallbackRatio;
            return ConeSlopeRatio > 0.0001f ? ConeSlopeRatio : fallbackRatio;
        }

        private static Rect Expand(Rect rect, float meters) =>
            Rect.MinMaxRect(rect.xMin - meters, rect.yMin - meters, rect.xMax + meters, rect.yMax + meters);
    }
}
