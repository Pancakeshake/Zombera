using System;
using UnityEngine;

namespace Zombera.World.City
{
    [Serializable]
    public struct CityNamedAreaBuildingLayoutSettings
    {
        public float gridCellMeters;
        public float streetSetbackMeters;
        public float polygonSafetyMarginMeters;
        public float minBuildingSpacingMeters;
        public float footprintPaddingMeters;
        public int rowsPerRoadSide;
        public float rowSpacingMeters;
        public bool fillInterior;
        public bool placePerimeterRows;

        public static CityNamedAreaBuildingLayoutSettings CreateDefault()
        {
            return new CityNamedAreaBuildingLayoutSettings
            {
                gridCellMeters = 3f,
                streetSetbackMeters = 7f,
                polygonSafetyMarginMeters = 2f,
                minBuildingSpacingMeters = 1.5f,
                footprintPaddingMeters = 1.5f,
                rowsPerRoadSide = 1,
                rowSpacingMeters = 3f,
                fillInterior = true,
                placePerimeterRows = true
            };
        }

        public void Clamp()
        {
            gridCellMeters = Mathf.Max(1f, gridCellMeters);
            streetSetbackMeters = Mathf.Max(0f, streetSetbackMeters);
            polygonSafetyMarginMeters = Mathf.Max(0f, polygonSafetyMarginMeters);
            minBuildingSpacingMeters = Mathf.Max(0f, minBuildingSpacingMeters);
            footprintPaddingMeters = Mathf.Max(0f, footprintPaddingMeters);
            rowsPerRoadSide = Mathf.Max(0, rowsPerRoadSide);
            rowSpacingMeters = Mathf.Max(0f, rowSpacingMeters);
        }
    }
}
