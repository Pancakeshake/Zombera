using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Selects which system produces authoritative paved-road polylines for gameplay and EasyRoads.
    /// </summary>
    public enum RoadLayoutSource
    {
        /// <summary>Seed-based world map graph (ring, spokes, city grids) shared by preview and runtime.</summary>
        MathematicalWorldMap = 0,

        /// <summary>Legacy: MapMagic graph Spline Output drives road layout.</summary>
        MapMagicSplinesLegacy = 1
    }

    public enum RoadClass
    {
        Highway,
        Arterial,
        Local
    }

    public enum TownType
    {
        Hamlet,
        Village,
        Town,
        City,
        Industrial,
        Military
    }

    [Serializable]
    public sealed class RoadPolyline
    {
        public int id;
        public RoadClass roadClass;
        public float widthMeters;

        /// <summary>When true, keep extracted XZ on the source spline (no horizontal nudge/smooth).</summary>
        public bool preserveWorldPath;

        /// <summary>When true, EasyRoads uses rounded spline markers (arterial corner arcs).</summary>
        public bool curvedMarkers;

        public List<Vector2> pointsXZ = new();

        public Rect BoundsXZ
        {
            get
            {
                if (pointsXZ == null || pointsXZ.Count == 0) return default;
                var min = pointsXZ[0];
                var max = pointsXZ[0];
                for (var i = 1; i < pointsXZ.Count; i++)
                {
                    var p = pointsXZ[i];
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }

                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }
    }
}
