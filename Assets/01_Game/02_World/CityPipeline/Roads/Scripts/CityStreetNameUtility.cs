using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Derives display street names from city math layout and road polylines for hub signage.
    ///     Uses deterministic name pools keyed on road ID so the same layout always gets the same names.
    /// </summary>
    public static class CityStreetNameUtility
    {
        // ── Name pools ────────────────────────────────────────────────────

        private static readonly string[] StreetNames =
        {
            "Adams", "Washington", "Jefferson", "Lincoln", "Franklin",
            "Madison", "Monroe", "Jackson", "Harrison", "Taylor",
            "Wilson", "Roosevelt", "Truman", "Kennedy", "Reagan",
            "Oak", "Elm", "Maple", "Cedar", "Pine",
            "Birch", "Walnut", "Cherry", "Willow", "Ash",
            "First", "Second", "Third", "Fourth", "Fifth",
            "Sixth", "Seventh", "Eighth", "Ninth", "Tenth",
            "Church", "School", "Mill", "Bridge", "Station",
            "Market", "Broad", "High", "Spring", "River",
            "Lake", "Hill", "Meadow", "Forest", "Valley",
            "Sunset", "Prospect", "Fairview", "Grandview", "Park",
            "College", "Liberty", "Union", "Center", "Court",
            "Locust", "Spruce", "Sycamore", "Magnolia", "Dogwood",
            "Hemlock", "Juniper", "Poplar", "Laurel", "Holly",
            "Victoria", "Elizabeth", "Margaret", "Catherine", "Beatrice",
            "Victoria", "Regent", "Duke", "Countess", "Windsor",
            "Baker", "Cooper", "Fletcher", "Mason", "Porter",
            "Sawyer", "Tanner", "Weaver", "Carter", "Miller",
            "Smith", "Taylor", "Walker", "Turner", "Fisher",
            "Division", "Frontier", "Heritage", "Legacy", "Horizon",
            "Crestview", "Rolling Hills", "Timber Creek", "Stone Gate", "Fox Run",
        };

        private static readonly string[] AvenueNames =
        {
            "Central", "Broadway", "Park", "Grand", "Commonwealth",
            "Constitution", "Independence", "Congress", "Capitol", "Empire",
            "Harbor", "Beacon", "Gateway", "Monument", "Pioneer",
            "Lancaster", "York", "Chester", "Devon", "Sussex",
            "Cambridge", "Oxford", "Windsor", "Brighton", "Richmond",
            "Lexington", "Concord", "Plymouth", "Savannah", "Augusta",
            "Montgomery", "Franklin", "Hamilton", "Burlington", "Kingston",
            "Atlantic", "Pacific", "Colonial", "Armory", "Commercial",
            "Railroad", "Summit", "Crescent", "Ridge", "Overlook",
        };

        private static readonly string[] ArterialNames =
        {
            "Main St", "Central Ave", "Broadway", "Market St", "Park Ave",
            "Washington Blvd", "Lincoln Hwy", "Roosevelt Pkwy", "Kennedy Dr",
            "King Blvd", "Grant Ave", "Lafayette St", "Jackson Ave",
            "Dixie Hwy", "Continental", "Palmetto Expy", "Sunrise Hwy",
        };

        private static readonly string[] HighwayNames =
        {
            "Interstate", "Skyline", "Parkway", "Turnpike", "Freeway",
            "Beltway", "Thruway", "Expressway", "Crosstown", "Perimeter",
        };

        private static readonly string[] Suffixes =
        {
            "St", "St", "St", "St",  // Street is most common
            "Ave", "Ave", "Ave",
            "Dr", "Dr",
            "Ln", "Ln",
            "Ct",
            "Way",
            "Pl",
            "Blvd",
            "Cir",
            "Ter",
        };

        private static readonly string[] AvenueSuffixes =
        {
            "Ave", "Ave", "Ave", "Ave", "Ave",
            "Blvd", "Blvd",
            "Pkwy",
            "Dr",
        };

        // ── Public API ─────────────────────────────────────────────────────

        public static string ResolveRoadDisplayName(RoadPolyline road, CityMathRoadLayout layout, int roadIndex)
        {
            if (road == null)
                return "Unknown St";

            if (road.roadClass == RoadClass.Highway)
                return HighwayNames[Mathf.Abs(road.id) % HighwayNames.Length] + " " + (roadIndex + 1);

            if (road.roadClass == RoadClass.Arterial)
                return ArterialNames[Mathf.Abs(road.id) % ArterialNames.Length];

            return ResolveLocalStreetName(road, layout);
        }

        public static string ResolveRoadDisplayNameAtPoint(
            Vector3 worldPoint,
            IReadOnlyList<RoadPolyline> roads,
            CityMathRoadLayout layout)
        {
            if (roads == null || roads.Count == 0)
                return "Unknown St";

            var bestDist = float.MaxValue;
            var bestName = "Unknown St";
            var p = new Vector2(worldPoint.x, worldPoint.z);

            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                var dist = DistancePointToPolyline(p, road.pointsXZ);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                bestName = ResolveRoadDisplayName(road, layout, i);
            }

            return bestName;
        }

        public static void BuildRoadNameLookup(
            RoadNetworkRuntime network,
            CityMathRoadLayout layout,
            Dictionary<int, string> nameByRoadId)
        {
            nameByRoadId?.Clear();
            if (nameByRoadId == null || network?.Roads == null)
                return;

            for (var i = 0; i < network.Roads.Count; i++)
            {
                var road = network.Roads[i];
                if (road == null)
                    continue;
                nameByRoadId[road.id] = ResolveRoadDisplayName(road, layout, i);
            }
        }

        // ── Local street naming ────────────────────────────────────────────

        private static string ResolveLocalStreetName(RoadPolyline road, CityMathRoadLayout layout)
        {
            if (road.pointsXZ == null || road.pointsXZ.Count < 2)
                return PickName(road, StreetNames, Suffixes);

            var a = road.pointsXZ[0];
            var b = road.pointsXZ[^1];
            var delta = b - a;

            var isEastWest = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);

            if (isEastWest)
                return PickName(road, StreetNames, Suffixes);
            else
                return PickName(road, AvenueNames, AvenueSuffixes);
        }

        private static string PickName(RoadPolyline road, string[] names, string[] suffixes)
        {
            var seed = (uint)Mathf.Abs(road.id);
            var nameIndex = (int)(seed % (uint)names.Length);
            var suffixIndex = (int)((seed >> 8) % (uint)suffixes.Length);

            return names[nameIndex] + " " + suffixes[suffixIndex];
        }

        // ── Math helpers ───────────────────────────────────────────────────

        private static string Ordinal(int number)
        {
            var mod100 = number % 100;
            if (mod100 is >= 11 and <= 13)
                return number + "th";

            return (number % 10) switch
            {
                1 => number + "st",
                2 => number + "nd",
                3 => number + "rd",
                _ => number + "th"
            };
        }

        private static float DistancePointToPolyline(Vector2 point, List<Vector2> polyline)
        {
            var best = float.MaxValue;
            for (var i = 1; i < polyline.Count; i++)
            {
                var dist = DistancePointToSegment(point, polyline[i - 1], polyline[i]);
                if (dist < best)
                    best = dist;
            }

            return best;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var len = ab.sqrMagnitude;
            if (len < 0.0001f)
                return Vector2.Distance(p, a);

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
            var proj = a + ab * t;
            return Vector2.Distance(p, proj);
        }
    }
}
