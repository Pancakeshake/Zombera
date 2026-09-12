using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.City
{
    /// <summary>
    ///     Static lot-based placement logic for residential district buildings.
    ///     Extracted from <see cref="CityPrefabDistrictBuildingPlacer"/>.
    /// </summary>
    internal static partial class CityDistrictLotPlacement
    {
#if UNITY_EDITOR
        private static bool LotFootprintIsDry(DistrictLotBuildArgs a, Rect lotRect)
        {
            if (a.TerrainQuery == null)
            {
                if (a.RequireWaterGate)
                {
                    Debug.LogError(
                        "[CityDistrictLotPlacement] IWorldTerrainQuery required for hub lot water gate but missing.");
                    return false;
                }

                LogMissingTerrainQueryOnce();
                return true;
            }

            var deep = a.DeepWaterDepthMeters > 0.01f
                ? a.DeepWaterDepthMeters
                : WorldWaterPlacementGate.DefaultDeepWaterDepthMeters;
            var margin = a.MinDistanceToWaterMeters > 0.01f
                ? a.MinDistanceToWaterMeters
                : WorldWaterPlacementGate.DefaultMinDistanceToWaterMeters;
            return WorldWaterPlacementGate.FootprintIsDry(
                lotRect,
                a.TerrainQuery,
                deep,
                margin,
                a.MaxReclaimDepthMeters,
                a.RequireWaterGate,
                out _);
        }

        private static bool _loggedMissingTerrainQuery;

        private static void LogMissingTerrainQueryOnce()
        {
            if (_loggedMissingTerrainQuery) return;
            _loggedMissingTerrainQuery = true;
            Debug.Log(
                "[CityDistrictLotPlacement] IWorldTerrainQuery not bound; skipping lot water gate " +
                "(standalone city lab).");
        }

        private static int PlaceLotsInContainer(
            DistrictLotBuildArgs a, Rect blockBounds, float setback,
            List<CityAssembledBuildingCatalogEntry> candidates, Transform lotContainer, float groundY)
        {
            var placed = 0;
            var placedFootprints = new List<Rect>(32);
            for (var l = 0; l < a.LotsContainer.childCount; l++)
            {
                var lotTransform = a.LotsContainer.GetChild(l);
                if (lotTransform.GetComponent<CityLotFacingMarker>() == null)
                    continue;

                var lotRect = ExtractLotRect(lotTransform);

                if (!LotFootprintIsDry(a, lotRect))
                    continue;

                // Commercial districts lay out strip / corner L plans first and
                // fall back to the legacy single-building path for small lots.
                var commercialPlaced = a.Marker.DistrictType == CityDistrictType.Commercial &&
                    TryPlaceCommercialPlan(a, blockBounds, setback, candidates, lotContainer, groundY,
                        lotTransform, lotRect, placedFootprints) > 0;

                if (commercialPlaced)
                {
                    placed++;
                }
                else if (TryPlaceSingleLot(new SingleLotArgs
                    {
                        LotTransform = lotTransform, BlockBounds = blockBounds,
                        GroundY = groundY, Setback = setback, Candidates = candidates,
                        Parent = lotContainer, Rng = a.Rng, UseProxy = a.UseProxy,
                        RoadMeshes = a.RoadMeshes, PlacedFootprints = placedFootprints,
                        StateSink = a.StateSink
                    }))
                {
                    placed++;
                }
                else
                {
                    CityPipelineFailureMarkers.Add(
                        new Vector3(lotRect.center.x, groundY, lotRect.center.y),
                        "No building candidate fitted this lot");
                }
            }

            return placed;
        }

        private static List<CityAssembledBuildingCatalogEntry> ResolveFilteredCandidates(
            List<CityAssembledBuildingCatalogEntry> catalog,
            CityDistrictType districtType)
        {
            var candidates = new List<CityAssembledBuildingCatalogEntry>();
            for (var ci = 0; ci < catalog.Count; ci++)
            {
                var ce = catalog[ci];
                if (ce?.prefab == null) continue;
                if (districtType == CityDistrictType.Mixed || ce.districtType == districtType)
                    candidates.Add(ce);
            }

            return candidates;
        }

        private static void SortCandidatesByFootprintDesc(
            List<CityAssembledBuildingCatalogEntry> candidates, bool useProxy)
        {
            candidates.Sort((x, y) =>
            {
                var areaY = y.ResolvePlacementWidth(useProxy) * y.ResolvePlacementDepth(useProxy);
                var areaX = x.ResolvePlacementWidth(useProxy) * x.ResolvePlacementDepth(useProxy);
                return areaY.CompareTo(areaX);
            });
        }
#endif
    }
}
