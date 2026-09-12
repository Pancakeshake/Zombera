using System;
using UnityEngine;

namespace Zombera.World.City
{
    [Serializable]
    public struct CityLotDecorationCategoryCounts
    {
        [Min(0)] public int trees;
        [Min(0)] public int foliage;
        [Min(0)] public int props;

        public static CityLotDecorationCategoryCounts Create(int trees, int foliage, int props) =>
            new() { trees = trees, foliage = foliage, props = props };
    }

    /// <summary>
    ///     Per-lot scatter tuning for hub lot decoration (trees, grass foliage, props).
    /// </summary>
    [Serializable]
    public struct CityLotDecorationScatterSettings
    {
        [Min(0.25f)] public float minSpacingMeters;
        [Min(0.1f)] public float buildingClearanceMeters;
        [Min(0f)] public float lotEdgeMarginMeters;
        [Min(0f)] public float positionJitterMeters;
        [Min(0f)] public float yawJitterDegrees;
        [Min(1)] public int maxPlacementAttemptsPerItem;
        [Min(0f)] public float treeHeightOffsetMeters;
        [Min(0f)] public float foliageHeightOffsetMeters;
        [Min(0f)] public float propHeightOffsetMeters;

        public CityLotDecorationCategoryCounts residential;
        public CityLotDecorationCategoryCounts commercial;
        public CityLotDecorationCategoryCounts industrial;
        public CityLotDecorationCategoryCounts cityCore;
        public CityLotDecorationCategoryCounts park;
        public CityLotDecorationCategoryCounts hospital;
        public CityLotDecorationCategoryCounts military;
        public CityLotDecorationCategoryCounts mixed;

        public static CityLotDecorationScatterSettings CreateDefault()
        {
            return new CityLotDecorationScatterSettings
            {
                minSpacingMeters = 1.8f,
                buildingClearanceMeters = 1.2f,
                lotEdgeMarginMeters = 0.6f,
                positionJitterMeters = 0.35f,
                yawJitterDegrees = 180f,
                maxPlacementAttemptsPerItem = 12,
                treeHeightOffsetMeters = 0f,
                foliageHeightOffsetMeters = 0f,
                propHeightOffsetMeters = 0f,
                residential = CityLotDecorationCategoryCounts.Create(3, 4, 1),
                commercial = CityLotDecorationCategoryCounts.Create(1, 1, 3),
                industrial = CityLotDecorationCategoryCounts.Create(0, 2, 5),
                cityCore = CityLotDecorationCategoryCounts.Create(1, 2, 2),
                park = CityLotDecorationCategoryCounts.Create(5, 8, 1),
                hospital = CityLotDecorationCategoryCounts.Create(1, 2, 2),
                military = CityLotDecorationCategoryCounts.Create(1, 2, 2),
                mixed = CityLotDecorationCategoryCounts.Create(1, 2, 2)
            };
        }

        public void Clamp()
        {
            minSpacingMeters = Mathf.Max(0.25f, minSpacingMeters);
            buildingClearanceMeters = Mathf.Max(0.1f, buildingClearanceMeters);
            lotEdgeMarginMeters = Mathf.Max(0f, lotEdgeMarginMeters);
            positionJitterMeters = Mathf.Max(0f, positionJitterMeters);
            yawJitterDegrees = Mathf.Max(0f, yawJitterDegrees);
            maxPlacementAttemptsPerItem = Mathf.Max(1, maxPlacementAttemptsPerItem);
        }

        public CityLotDecorationCategoryCounts ResolveCounts(CityDistrictType districtType) =>
            districtType switch
            {
                CityDistrictType.Residential => residential,
                CityDistrictType.Commercial => commercial,
                CityDistrictType.Industrial => industrial,
                CityDistrictType.CityCore => cityCore,
                CityDistrictType.Park => park,
                CityDistrictType.Hospital => hospital,
                CityDistrictType.Military => military,
                _ => mixed
            };
    }
}
