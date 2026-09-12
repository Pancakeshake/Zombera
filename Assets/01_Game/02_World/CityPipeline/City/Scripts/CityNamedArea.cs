using System;
using UnityEngine;

namespace Zombera.World.City
{
    [Serializable]
    public sealed class CityNamedArea
    {
        public int id;
        public string displayName = "Residential";
        public string clusterName = string.Empty;
        public int gridX;
        public int gridZ;
        public CityDistrictType districtType = CityDistrictType.Residential;
        public Rect boundsXZ;
        public Vector2 centerXZ;
        public float areaSquareMeters;
        public CityBlockCornerMask roundedCorners = CityBlockCornerMask.None;
        public float arterialCornerRadiusMeters;
        public Vector2[] outlineXZ = Array.Empty<Vector2>();

        /// <summary>City center this block belongs to — commercial blocks open toward it.</summary>
        public Vector2 cityCenterXZ;

        public Vector3 WorldCenter(float groundY)
        {
            return new Vector3(centerXZ.x, groundY, centerXZ.y);
        }

        public bool HasRoundedOutline =>
            roundedCorners != CityBlockCornerMask.None && outlineXZ != null && outlineXZ.Length >= 3;
    }
}
