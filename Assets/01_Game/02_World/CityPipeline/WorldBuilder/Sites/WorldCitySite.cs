using System;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    [Serializable]
    public sealed class WorldCitySite
    {
        public ulong StableId;
        public string DisplayName = "City";
        public CitySiteType SiteType = CitySiteType.Town;
        public Vector2 CenterXZ;
        public float HalfWidthMeters = 280f;
        public float HalfDepthMeters = 240f;
        public float PadHeightWorldY;
        public float BuildabilityScore = 1f;
        public int LayoutSeed;

        /// <summary>Session-only: derived from hydrology at accept/apply (not SettlementState).</summary>
        public bool IsCoastal;

        /// <summary>Unit XZ toward nearest deep ocean when <see cref="IsCoastal"/>.</summary>
        public Vector2 SeawardNormalXZ;

        public float CoastExposure01;

        /// <summary>Footprint edge where the primary inter-city highway meets this site.</summary>
        public Vector2 HighwayEntryXZ;

        public float HighwayEntryHeightWorldY;

        /// <summary>Length of the highway edge that owns <see cref="HighwayEntryXZ"/>.</summary>
        public float HighwayEntryEdgeLengthMeters;

        public bool HasHighwayEntry => HighwayEntryEdgeLengthMeters > 0.01f;

        public void ClearHighwayEntry()
        {
            HighwayEntryXZ = default;
            HighwayEntryHeightWorldY = 0f;
            HighwayEntryEdgeLengthMeters = 0f;
        }
    }
}
