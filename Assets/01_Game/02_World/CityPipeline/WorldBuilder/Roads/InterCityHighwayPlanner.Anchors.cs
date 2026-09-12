using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Plateau-edge anchor selection for inter-city highways.</summary>
    public sealed partial class InterCityHighwayPlanner
    {
        public static bool ResolvePlateauRect(
            WorldCitySite site,
            LandformProfile landforms,
            out Rect plateau)
        {
            plateau = default;
            if (site == null)
                return false;

            var margin = CityPadCoreUtility.FlatMarginMeters(landforms);
            if (CityPadCoreUtility.TryResolveCorePlateau(site, margin, out plateau) &&
                plateau.width > 1f && plateau.height > 1f)
                return true;

            var halfW = Mathf.Max(40f, site.HalfWidthMeters);
            var halfD = Mathf.Max(40f, site.HalfDepthMeters);
            plateau = Rect.MinMaxRect(
                site.CenterXZ.x - halfW,
                site.CenterXZ.y - halfD,
                site.CenterXZ.x + halfW,
                site.CenterXZ.y + halfD);
            return true;
        }

        public static Vector2 ResolveSiteAnchor(
            WorldCitySite site,
            Vector2 toward,
            LandformProfile landforms,
            IWorldTerrainQuery terrainQuery,
            int faceRank)
        {
            if (site == null)
                return toward;

            ResolvePlateauRect(site, landforms, out var plateau);
            var center = plateau.center;
            var halfW = Mathf.Max(1f, plateau.width * 0.5f);
            var halfD = Mathf.Max(1f, plateau.height * 0.5f);
            var faces = new Vector2[4];
            InterCitySiteFootprintUtility.CollectCardinalFaceOutwards(center, toward, faces);
            var rank = Mathf.Clamp(faceRank, 0, faces.Length - 1);
            var outward = faces[rank];

            // Prefer flattest among the first two facing options when faceRank==0.
            if (faceRank == 0)
            {
                var best = faces[0];
                var bestScore = ScoreFace(center, halfW, halfD, best, terrainQuery, site);
                for (var i = 1; i < 2; i++)
                {
                    var score = ScoreFace(center, halfW, halfD, faces[i], terrainQuery, site);
                    if (score >= bestScore)
                        continue;
                    bestScore = score;
                    best = faces[i];
                }

                outward = best;
            }

            return InterCitySiteFootprintUtility.ResolveEdgePointOnFace(center, halfW, halfD, outward);
        }

        private static float ScoreFace(
            Vector2 center,
            float halfW,
            float halfD,
            Vector2 outward,
            IWorldTerrainQuery terrainQuery,
            WorldCitySite site)
        {
            var edge = InterCitySiteFootprintUtility.ResolveEdgePointOnFace(center, halfW, halfD, outward);
            var slope = 45f;
            if (terrainQuery != null && terrainQuery.TrySample(edge, out var sample))
                slope = sample.SlopeDegrees;

            // Penalize seaward faces for coastal sites.
            var coastalPenalty = 0f;
            if (site != null && site.IsCoastal && site.SeawardNormalXZ.sqrMagnitude > 0.01f)
            {
                var sea = site.SeawardNormalXZ.normalized;
                coastalPenalty = Mathf.Max(0f, Vector2.Dot(outward, sea)) * 40f;
            }

            return slope + coastalPenalty;
        }
    }
}
