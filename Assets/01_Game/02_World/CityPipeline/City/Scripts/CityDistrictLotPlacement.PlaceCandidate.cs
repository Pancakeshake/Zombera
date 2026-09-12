using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.City
{
    /// <summary>
    ///     Single-candidate placement paths for <see cref="CityDistrictLotPlacement"/>
    ///     (fit-within-lot and commercial fill-exact).
    /// </summary>
    internal static partial class CityDistrictLotPlacement
    {
#if UNITY_EDITOR
        private static bool TryPlaceCandidate(DistrictLotPlacementRequest request)
        {
            var entry = request.Entry;
            var lot = request.Lot;
            var instantiatePrefab = request.UseProxy && entry.proxyPrefab != null
                ? entry.proxyPrefab : entry.prefab;
            if (instantiatePrefab == null) return false;

            // Commercial layouts place into a slot — otherwise the lot itself.
            var lotRect = request.OverrideLotRect ?? lot.LotRect;
            var streetFace = request.OverrideFace ?? lot.StreetFace;

            if (request.FillLotExactly)
                return TryPlaceCandidateFillExact(request, instantiatePrefab, lotRect, streetFace);

            var setback = request.OverrideSetback ?? lot.Setback;

            // Prefer a fresh measurement of the actual prefab being instantiated.
            // Entry dims can be stale (footprint cache) or remain at the 12x12 default
            // when catalog measurement failed — oversized buildings then pass the lot
            // fit test and spill into neighbouring lots.
            var footprintW = entry.ResolvePlacementWidth(request.UseProxy && entry.proxyPrefab != null);
            var footprintD = entry.ResolvePlacementDepth(request.UseProxy && entry.proxyPrefab != null);
            if (CityBuildingPrefabFootprintUtility.TryMeasureFootprint(instantiatePrefab, out var measured) &&
                measured.IsValid)
            {
                footprintW = measured.WidthMeters;
                footprintD = measured.DepthMeters;
            }
            if (footprintW < 0.5f || footprintD < 0.5f) return false;

            // Always measure door yaw from the real prefab, not the proxy.
            var placeResult = CityBuildingRoadFacingUtility.TryComputeLotPlacement(
                lotRect, lot.BlockBounds, lot.GroundY, setback,
                footprintW, footprintD,
                entry.prefab,
                entry.doorYawResolved ? entry.doorYawOffsetDegrees : entry.yawOffsetDegrees,
                extraBackMeters: request.OverrideSetback.HasValue
                    ? 0f
                    : (float)request.Rng.NextDouble() * Mathf.Min(setback, 2f),
                resolvedFace: streetFace);
            if (!placeResult.Fits) return false;

            var rootPos = CityBuildingPrefabFootprintUtility.ResolveRootPosition(
                placeResult.WorldPosition, placeResult.Rotation,
                entry.ResolvePlacementCenterOffset(request.UseProxy && entry.proxyPrefab != null));

            var instance = Object.Instantiate(instantiatePrefab, rootPos, placeResult.Rotation, request.Parent);
            if (instance == null) return false;

            // Post-placement guard: the catalog dims can understate the real prefab
            // (stale measurements, pivot offsets, roof overhang). Validate the ACTUAL
            // instantiated bounds against the lot and against already-placed buildings
            // before committing — otherwise oversized builds land on top of neighbours.
            // Clipped corner/curved lots additionally require the building to fit the
            // lot's actual polygon — the AABB covers fillet regions outside the polygon
            // (past the fence line) where a building would visually sit on the fences.
            if (!CityBuildingPrefabFootprintUtility.TryMeasureWorldBoundsXZ(instance, out var placedBounds))
            {
                Object.DestroyImmediate(instance);
                return false;
            }

            var polyFits = lot.Polygon == null ||
                CityNamedAreaPolygonUtility.ContainsAxisAlignedRectSampled(
                    lot.Polygon, placedBounds.center,
                    Mathf.Max(0.05f, placedBounds.width * 0.5f - 0.02f),
                    Mathf.Max(0.05f, placedBounds.height * 0.5f - 0.02f),
                    2f);
            if (!FitsWithinLotWithTolerance(placedBounds, lotRect, 0.1f) ||
                !polyFits ||
                OverlapsAnyPlaced(placedBounds, request.PlacedFootprints, 0.05f))
            {
                Object.DestroyImmediate(instance);
                return false;
            }

            return CommitPlacedBuilding(
                request, instance, streetFace, placeResult, placedBounds, footprintW, footprintD);
        }

        /// <summary>
        ///     Commercial store/slot fill: yaw to the street, scale XZ to the
        ///     lot/slot rect, snap centre. Catalog piece size does not need to
        ///     match — overhang and missing width variants are absorbed by scale.
        /// </summary>
        private static bool TryPlaceCandidateFillExact(
            DistrictLotPlacementRequest request,
            GameObject instantiatePrefab,
            Rect lotRect,
            BlockFace streetFace)
        {
            var entry = request.Entry;
            var lot = request.Lot;
            var catalogYaw = entry.doorYawResolved ? entry.doorYawOffsetDegrees : entry.yawOffsetDegrees;
            var doorOffset = Mathf.Abs(catalogYaw) > 0.01f
                ? catalogYaw
                : CityBuildingRoadFacingUtility.GetDoorYawOffset(entry.prefab, 0f);
            var finalYaw = (CityBuildingRoadFacingUtility.GetRoadFacingYaw(streetFace) + doorOffset) % 360f;
            if (finalYaw < 0f) finalYaw += 360f;
            var rotation = Quaternion.Euler(0f, finalYaw, 0f);

            var center = new Vector3(lotRect.center.x, lot.GroundY, lotRect.center.y);
            var rootPos = CityBuildingPrefabFootprintUtility.ResolveRootPosition(
                center, rotation,
                entry.ResolvePlacementCenterOffset(request.UseProxy && entry.proxyPrefab != null));

            var instance = Object.Instantiate(instantiatePrefab, rootPos, rotation, request.Parent);
            if (instance == null) return false;

            if (!CityBuildingPrefabFootprintUtility.TryScaleInstanceToLotRect(
                    instance, lotRect, out var filledBounds))
            {
                Object.DestroyImmediate(instance);
                return false;
            }

            if (OverlapsAnyPlaced(filledBounds, request.PlacedFootprints, 0.05f))
            {
                Object.DestroyImmediate(instance);
                return false;
            }

            var placeResult = new CityBuildingRoadFacingUtility.LotPlacementResult(
                center, rotation, filledBounds.width, filledBounds.height, true);
            return CommitPlacedBuilding(
                request, instance, streetFace, placeResult, filledBounds,
                filledBounds.width, filledBounds.height);
        }

        private static bool CommitPlacedBuilding(
            DistrictLotPlacementRequest request,
            GameObject instance,
            BlockFace streetFace,
            CityBuildingRoadFacingUtility.LotPlacementResult placeResult,
            Rect placedBounds,
            float footprintW,
            float footprintD)
        {
            Undo.RegisterCreatedObjectUndo(instance, "Place District Building");
            var facing = instance.AddComponent<CityBuildingStreetFacingMarker>();
            facing.streetFace = streetFace;
            RecordPlacedBuildingAnchor(instance, facing, placeResult, footprintW, footprintD, placedBounds);
            if (!TryPublishWorldStateBuilding(request, instance, facing, placeResult, placedBounds))
                return false;

            request.PlacedFootprints?.Add(placedBounds);
            return true;
        }

        private static bool TryPublishWorldStateBuilding(
            DistrictLotPlacementRequest request,
            GameObject instance,
            CityBuildingStreetFacingMarker facing,
            CityBuildingRoadFacingUtility.LotPlacementResult placeResult,
            Rect placedBounds)
        {
            var sink = request.StateSink;
            if (sink == null)
                return true;

            if (!TryCreateBuildingPlacementData(
                    request, instance, facing, placeResult, placedBounds, out var data))
                return DestroyFailedStateBuilding(instance);

            if (!sink.TryStageBuilding(data, out var id, out _))
                return DestroyFailedStateBuilding(instance);

            WorldStateEntityViewBinding.Bind(instance, id);
            sink.RegisterProvisionalView(instance);
            return true;
        }

        private static bool TryCreateBuildingPlacementData(
            DistrictLotPlacementRequest request,
            GameObject instance,
            CityBuildingStreetFacingMarker facing,
            CityBuildingRoadFacingUtility.LotPlacementResult placeResult,
            Rect placedBounds,
            out GeneratedBuildingPlacementData data)
        {
            data = null;
            var areaMarker = request.LotTransform != null
                ? request.LotTransform.GetComponentInParent<CityNamedAreaMarker>()
                : null;

            WorldStateEntityViewBinding.TryGetBoundId(
                areaMarker, WorldEntityKind.District, out var districtId);
            WorldStateEntityViewBinding.TryGetBoundId(
                request.LotTransform, WorldEntityKind.Lot, out var lotId);
            if (districtId.kind == WorldEntityKind.None && lotId.kind == WorldEntityKind.None)
                return false;

            var archetypeId = ResolveBuildingArchetypeId(request.Entry);
            data = new GeneratedBuildingPlacementData
            {
                SourceId = archetypeId,
                ArchetypeId = archetypeId,
                TypeId = request.Entry?.id ?? string.Empty,
                DistrictId = districtId,
                LotId = lotId,
                DistrictType = areaMarker != null
                    ? areaMarker.DistrictType
                    : request.Entry != null ? request.Entry.districtType : CityDistrictType.Mixed,
                Position = placeResult.WorldPosition,
                Rotation = placeResult.Rotation,
                Scale = instance != null ? instance.transform.localScale : Vector3.one,
                FootprintXZ = placedBounds,
                StreetFace = facing.streetFace,
                HasDoorAnchor = facing.hasDoorAnchor,
                DoorAnchorWorld = facing.doorAnchorWorld,
                Ordinal = request.Ordinal
            };
            return true;
        }

        private static string ResolveBuildingArchetypeId(CityAssembledBuildingCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(entry.assetPath))
                return entry.assetPath;
            if (!string.IsNullOrWhiteSpace(entry.id))
                return entry.id;
            return entry.prefab != null ? entry.prefab.name : string.Empty;
        }

        private static bool DestroyFailedStateBuilding(GameObject instance)
        {
            Undo.DestroyObjectImmediate(instance);
            return false;
        }
#endif
    }
}
