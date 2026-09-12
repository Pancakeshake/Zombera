using System;
using UnityEngine;

namespace Zombera.World.Roads
{
    [Serializable]
    public struct LotSizeRange
    {
        [Min(10f)] public float min;
        [Min(10f)] public float max;
        public LotSizeRange(float min, float max) { this.min = min; this.max = max; }
    }

    [CreateAssetMenu(
        menuName = "Zombera/City/Build Config (CityBuildConfig)",
        fileName = "CityBuildConfig",
        order = 310)]
    public sealed class CityBuildConfig : ScriptableObject
    {
        [Header("Alignment")]
        public bool alignLayoutCenterToTransform = true;

        [Header("Auto-Setup")]
        public bool autoEnsureMinimalRoadStack = true;
        public bool autoGenerateNamedAreas;

        [Header("Building Placement")]
        public int buildingLayoutSeed = 12345;
        public Zombera.World.City.CityNamedAreaBuildingLayoutSettings buildingLayoutSettings =
            Zombera.World.City.CityNamedAreaBuildingLayoutSettings.CreateDefault();
        public bool useProxyPrefabs = true;

        [Header("Lot Sizes by District")]
        public LotSizeRange cityCoreLotSize     = new(48, 96);
        public LotSizeRange commercialLotSize  = new(28, 56);
        public LotSizeRange industrialLotSize  = new(32, 64);
        public LotSizeRange residentialLotSize = new(16, 32);

        [Header("Commercial Lot Styling")]
        [Tooltip("Chance (0–1) that an eligible commercial strip / U lot becomes a gas station instead of shops.")]
        [Range(0f, 1f)]
        public float gasStationChance = 0.15f;

        [Header("District Generation")]
        public bool createEditorFloorVisuals = true;
        public bool showDistrictFillColors = true;
        [Min(0f)] public float floorVisualHeight = 0.06f;
        public bool generateResidentialLots = true;

        [Tooltip("Front yard depth as a fraction of lot depth (0–0.5). Always scales with lot size — no overflow possible.")]
        [Range(0f, 0.5f)]
        public float lotFrontYardFraction = 0.2f;

        [Header("Parks")]
        public Zombera.World.City.CityParkScatterSettings parkScatterSettings = new();

        [Header("Lot Decorations")]
        public Zombera.World.City.CityLotDecorationScatterSettings scatterSettings =
            Zombera.World.City.CityLotDecorationScatterSettings.CreateDefault();

        [Header("Test Layout")]
        [Min(10f)] public float tTestArmLengthMeters = 45f;
        public bool useTIntersectionTestLayout;

    }
}
