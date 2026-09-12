using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Parks cars on residential lot driveways instead of road shoulders.
    ///     Each residential house rolls a per-lot chance (default 10%) and, when it
    ///     passes, a car is placed on its painted asphalt driveway, facing the street.
    ///     Driveway geometry mirrors the lot terrain subdivision (door-anchored,
    ///     corner-lot secondary street, and geometric fallback) so cars sit on the
    ///     asphalt and never on grass.
    /// </summary>
    public static class CityParkedCarPlacer
    {
        private const string ContainerName = "ParkedCars";
        private const float EdgeSnapMeters = 0.05f;
        private const float DrivewayGapMeters = 0.4f;
        private const float MinDrivewaySpanMeters = 0.5f;
        private const float MaxDrivewayDepthMeters = 6f;
        private const float CarLateralClearanceMeters = 2.4f;

        private readonly struct LotDoorAnchor
        {
            public readonly Rect Footprint;
            public readonly Vector2 Door;
            public readonly bool HasDoor;

            public LotDoorAnchor(Rect footprint, Vector2 door, bool hasDoor)
            {
                Footprint = footprint;
                Door = door;
                HasDoor = hasDoor;
            }
        }

        /// <summary>
        ///     Rolls the per-lot chance for every residential house under
        ///     <paramref name="areasRoot"/> and parks a car on the driveway of each
        ///     winner. Cars are parented under a "ParkedCars" container on
        ///     <paramref name="containerRoot"/>.
        /// </summary>
        public static int PlaceOnLotDriveways(
            Transform containerRoot,
            Transform areasRoot,
            DistrictLotTerrainLayout terrainLayout,
            CityStreetscapeConfig streetscape,
            Func<Vector2, float> resolveHeight,
            int layoutSeed)
        {
            if (containerRoot == null || areasRoot == null || terrainLayout == null ||
                streetscape == null || resolveHeight == null)
                return 0;

            var prefabs = CityPlacerPrefabResolver.ParkedCars;
            if (!streetscape.spawnParkedCars || prefabs == null || prefabs.Length == 0)
                return 0;

            Clear(containerRoot);
            var container = GetOrCreateContainer(containerRoot);
            var rng = new System.Random(layoutSeed + 90210);
            var placed = 0;

            for (var a = 0; a < areasRoot.childCount; a++)
                placed += PlaceForArea(areasRoot.GetChild(a), terrainLayout,
                    streetscape.parkedCarLotChance, prefabs, resolveHeight, container, rng, a);

            return placed;
        }

        public static void Clear(Transform networkRoot)
        {
            if (networkRoot == null)
                return;

            var container = networkRoot.Find(ContainerName);
            if (container == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(container.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(container.gameObject);
        }

        private static int PlaceForArea(
            Transform area,
            DistrictLotTerrainLayout terrainLayout,
            float lotChance,
            GameObject[] prefabs,
            Func<Vector2, float> resolveHeight,
            Transform container,
            System.Random rng,
            int areaIndex)
        {
            var marker = area.GetComponent<CityNamedAreaMarker>();
            if (marker == null || marker.DistrictType != CityDistrictType.Residential)
                return 0;

            var lotsRoot = area.Find(CityPrefabResidentialLotBuilder.DistrictLotsContainerName)
                ?? area.Find("ResidentialLots");
            if (lotsRoot == null)
                return 0;

            var layout = terrainLayout.GetLayout(CityDistrictType.Residential);
            var blockBounds = marker.GetHubShiftedBoundsXZ();
            var anchors = CollectBuildingAnchors(
                area.Find(CityDistrictLotPlacement.PlacedBuildingsContainerName));

            var placed = 0;
            for (var l = 0; l < lotsRoot.childCount; l++)
            {
                if (rng.NextDouble() >= lotChance)
                    continue;

                if (TryPlaceCarOnLot(lotsRoot.GetChild(l), blockBounds, layout, anchors,
                        prefabs, resolveHeight, container, rng, areaIndex, l))
                    placed++;
            }

            return placed;
        }

        private static List<LotDoorAnchor> CollectBuildingAnchors(Transform placedBuildings)
        {
            var anchors = new List<LotDoorAnchor>();
            if (placedBuildings == null)
                return anchors;

            for (var i = 0; i < placedBuildings.childCount; i++)
            {
                var marker = placedBuildings.GetChild(i).GetComponent<CityBuildingStreetFacingMarker>();
                if (marker == null ||
                    marker.footprintXZ.width < 0.5f || marker.footprintXZ.height < 0.5f)
                    continue;

                anchors.Add(new LotDoorAnchor(
                    marker.footprintXZ,
                    new Vector2(marker.doorAnchorWorld.x, marker.doorAnchorWorld.z),
                    marker.hasDoorAnchor));
            }

            return anchors;
        }

        private static LotDoorAnchor? FindAnchorForLot(Rect lotRect, List<LotDoorAnchor> anchors)
        {
            if (anchors == null)
                return null;

            for (var i = 0; i < anchors.Count; i++)
                if (lotRect.Contains(anchors[i].Footprint.center))
                    return anchors[i];

            return null;
        }

        private static bool TryPlaceCarOnLot(
            Transform lot,
            Rect blockBounds,
            LotTerrainZoneLayout layout,
            List<LotDoorAnchor> anchors,
            GameObject[] prefabs,
            Func<Vector2, float> resolveHeight,
            Transform container,
            System.Random rng,
            int areaIndex,
            int lotIndex)
        {
            var lotRect = CityDistrictLotPlacement.ExtractLotRect(lot);
            if (lotRect.width < 4f || lotRect.height < 4f)
                return false;

            // Only park at houses — empty lots keep an empty driveway.
            var anchor = FindAnchorForLot(lotRect, anchors);
            if (!anchor.HasValue)
                return false;

            var facingMarker = lot.GetComponent<CityLotFacingMarker>();
            var streetFace = facingMarker != null ? facingMarker.streetFace : BlockFace.South;

            if (!TryResolveDriveway(lotRect, streetFace, blockBounds, layout, anchor,
                    out var driveRect, out var drivewayFace))
                return false;

            var prefab = prefabs[rng.Next(prefabs.Length)];
            if (prefab == null)
                return false;

            // Park in the middle-to-house half of the driveway, with a little
            // lateral jitter so the car stays inside the asphalt band.
            var isHorizontal = IsHorizontalFace(drivewayFace);
            var lateralCenter = isHorizontal ? driveRect.center.x : driveRect.center.y;
            var lateralSpan = isHorizontal ? driveRect.width : driveRect.height;
            var lateralJitter = Mathf.Max(0f, (lateralSpan - CarLateralClearanceMeters) * 0.5f);
            lateralCenter += (float)(rng.NextDouble() * 2.0 - 1.0) * lateralJitter;

            var deepFrac = 0.45f + (float)rng.NextDouble() * 0.15f;
            var deepCoord = isHorizontal
                ? Mathf.Lerp(driveRect.yMin, driveRect.yMax, deepFrac)
                : Mathf.Lerp(driveRect.xMin, driveRect.xMax, deepFrac);

            var pointXZ = isHorizontal
                ? new Vector2(lateralCenter, deepCoord)
                : new Vector2(deepCoord, lateralCenter);

            var outward = OutwardForFace(drivewayFace);
            var yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            yaw += (float)(rng.NextDouble() * 6.0 - 3.0);

            var position = new Vector3(pointXZ.x, resolveHeight(pointXZ), pointXZ.y);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation, container);
            instance.name = $"ParkedCar_D{areaIndex}_{lotIndex}";
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Parked Car");
#endif
            return true;
        }

        /// <summary>
        ///     Resolves the driveway rect for a lot, mirroring the terrain subdivision:
        ///     corner lots park on the secondary street, lots with a door anchor park
        ///     beside the door footpath, and the rest use the geometric driveway.
        /// </summary>
        private static bool TryResolveDriveway(
            Rect lotRect, BlockFace streetFace, Rect blockBounds,
            LotTerrainZoneLayout layout, LotDoorAnchor? anchor,
            out Rect driveRect, out BlockFace drivewayFace)
        {
            driveRect = default;
            drivewayFace = streetFace;
            if (layout == null || !layout.generateDriveway)
                return false;

            ResolveStreetFrame(streetFace, lotRect, out var isHorizontal,
                out var near, out var far, out var left, out var right);

            var spanAcross = Mathf.Abs(right - left);
            var spanDeep = Mathf.Abs(far - near);
            var sideWidth = ResolveSideYardWidth(spanAcross, layout.sideYardFractionOfLotWidth);

            var driveWidth = Mathf.Min(layout.drivewayWidthMeters,
                spanAcross - sideWidth * 2f - MinDrivewaySpanMeters);
            var driveDepth = Mathf.Min(spanDeep * layout.drivewayDepthFractionOfLotDepth,
                MaxDrivewayDepthMeters);
            if (driveWidth <= MinDrivewaySpanMeters || driveDepth <= MinDrivewaySpanMeters)
                return false;

            ComputeEdgeTouches(lotRect, blockBounds, out var south, out var north, out var west, out var east);
            if (CountTouches(south, north, west, east) >= 2)
            {
                // Corner lots park on the secondary street, like the painted driveway.
                drivewayFace = ResolveSecondFace(streetFace, south, north, west, east);
                EdgeMappingForFace(drivewayFace, lotRect,
                    out var dvNear, out var dvFar, out var dvLeft, out var dvRight);
                var dvSign = dvFar > dvNear ? 1f : -1f;
                var dvWidth = Mathf.Min(driveWidth,
                    Mathf.Abs(dvRight - dvLeft) - sideWidth * 2f - MinDrivewaySpanMeters);
                var dvOffset = dvLeft + layout.drivewaySideOffsetMeters;
                if (dvOffset < dvLeft + sideWidth) dvOffset = dvLeft + sideWidth;
                if (dvOffset + dvWidth > dvRight - sideWidth) dvOffset = dvRight - sideWidth - dvWidth;
                if (dvWidth <= MinDrivewaySpanMeters)
                    return false;

                driveRect = BuildRectForFace(IsHorizontalFace(drivewayFace),
                    dvNear, dvNear + dvSign * driveDepth, dvOffset, dvOffset + dvWidth);
                return true;
            }

            if (anchor.Value.HasDoor)
            {
                // Door-anchored driveway: beside the footpath, from street edge to door.
                var doorAcross = isHorizontal ? anchor.Value.Door.x : anchor.Value.Door.y;
                var doorDeep = isHorizontal ? anchor.Value.Door.y : anchor.Value.Door.x;
                var driveEnd = Mathf.Clamp(doorDeep, Mathf.Min(near, far), Mathf.Max(near, far));

                var padAcross = isHorizontal ? anchor.Value.Footprint.width : anchor.Value.Footprint.height;
                var footActive = layout.generateDoorFootpath && layout.generateBuildingPad
                    && layout.footpathWidthMeters > 0.4f
                    && padAcross > layout.footpathWidthMeters + MinDrivewaySpanMeters;
                var pathHalf = footActive
                    ? layout.footpathWidthMeters * 0.5f + DrivewayGapMeters
                    : 0f;

                var driveLeft = doorAcross + pathHalf;
                if (driveLeft + driveWidth > right)
                    driveLeft = doorAcross - pathHalf - driveWidth;
                driveLeft = Mathf.Clamp(driveLeft, left, right - driveWidth);

                driveRect = BuildRectForFace(isHorizontal, near, driveEnd, driveLeft, driveLeft + driveWidth);
                return true;
            }

            // Geometric driveway — same math the terrain subdivision uses when no
            // placed-building anchor exists.
            var geoLeft = left + layout.drivewaySideOffsetMeters;
            if (geoLeft < left + sideWidth) geoLeft = left + sideWidth;
            if (geoLeft + driveWidth > right - sideWidth) geoLeft = right - sideWidth - driveWidth;
            if (geoLeft < left)
            {
                geoLeft = left;
                driveWidth = Mathf.Min(driveWidth, spanAcross * 0.4f);
            }

            var signDeep = far > near ? 1f : -1f;
            driveRect = BuildRectForFace(isHorizontal, near, near + signDeep * driveDepth,
                geoLeft, geoLeft + driveWidth);
            return true;
        }

        // ── Mirrored lot-frame helpers (same math as the terrain subdivision) ──

        private static void ResolveStreetFrame(
            BlockFace streetFace, Rect lotRect,
            out bool isHorizontal, out float near, out float far, out float left, out float right)
        {
            switch (streetFace)
            {
                case BlockFace.South:
                    near = lotRect.yMin; far = lotRect.yMax;
                    left = lotRect.xMin; right = lotRect.xMax;
                    isHorizontal = true;
                    break;
                case BlockFace.North:
                    near = lotRect.yMax; far = lotRect.yMin;
                    left = lotRect.xMin; right = lotRect.xMax;
                    isHorizontal = true;
                    break;
                case BlockFace.West:
                    near = lotRect.xMin; far = lotRect.xMax;
                    left = lotRect.yMin; right = lotRect.yMax;
                    isHorizontal = false;
                    break;
                default:
                    near = lotRect.xMax; far = lotRect.xMin;
                    left = lotRect.yMin; right = lotRect.yMax;
                    isHorizontal = false;
                    break;
            }
        }

        private static void EdgeMappingForFace(
            BlockFace face, Rect lotRect,
            out float near, out float far, out float left, out float right)
        {
            switch (face)
            {
                case BlockFace.South:
                    near = lotRect.yMin; far = lotRect.yMax;
                    left = lotRect.xMin; right = lotRect.xMax;
                    break;
                case BlockFace.North:
                    near = lotRect.yMax; far = lotRect.yMin;
                    left = lotRect.xMin; right = lotRect.xMax;
                    break;
                case BlockFace.West:
                    near = lotRect.xMin; far = lotRect.xMax;
                    left = lotRect.yMin; right = lotRect.yMax;
                    break;
                default:
                    near = lotRect.xMax; far = lotRect.xMin;
                    left = lotRect.yMin; right = lotRect.yMax;
                    break;
            }
        }

        private static Rect BuildRectForFace(
            bool isHorizontalStreet, float near, float far, float left, float right)
        {
            float xMin, xMax, yMin, yMax;
            if (isHorizontalStreet)
            {
                xMin = Mathf.Min(left, right);
                xMax = Mathf.Max(left, right);
                yMin = Mathf.Min(near, far);
                yMax = Mathf.Max(near, far);
            }
            else
            {
                xMin = Mathf.Min(near, far);
                xMax = Mathf.Max(near, far);
                yMin = Mathf.Min(left, right);
                yMax = Mathf.Max(left, right);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static float ResolveSideYardWidth(float spanAcross, float fraction)
        {
            var width = spanAcross * fraction;
            if (spanAcross >= 8f)
                width = Mathf.Max(width, 1.5f);
            return Mathf.Min(width, spanAcross * 0.18f);
        }

        private static void ComputeEdgeTouches(
            Rect lotRect, Rect blockRect,
            out bool south, out bool north, out bool west, out bool east)
        {
            south = Mathf.Abs(lotRect.yMin - blockRect.yMin) < EdgeSnapMeters;
            north = Mathf.Abs(lotRect.yMax - blockRect.yMax) < EdgeSnapMeters;
            west = Mathf.Abs(lotRect.xMin - blockRect.xMin) < EdgeSnapMeters;
            east = Mathf.Abs(lotRect.xMax - blockRect.xMax) < EdgeSnapMeters;
        }

        private static int CountTouches(bool south, bool north, bool west, bool east) =>
            (south ? 1 : 0) + (north ? 1 : 0) + (west ? 1 : 0) + (east ? 1 : 0);

        private static BlockFace ResolveSecondFace(
            BlockFace streetFace, bool south, bool north, bool west, bool east)
        {
            switch (streetFace)
            {
                case BlockFace.South:
                    return PickSecondFace(west, BlockFace.West, east, BlockFace.East, north, BlockFace.North, streetFace);
                case BlockFace.North:
                    return PickSecondFace(east, BlockFace.East, west, BlockFace.West, south, BlockFace.South, streetFace);
                case BlockFace.West:
                    return PickSecondFace(north, BlockFace.North, south, BlockFace.South, east, BlockFace.East, streetFace);
                default:
                    return PickSecondFace(south, BlockFace.South, north, BlockFace.North, west, BlockFace.West, streetFace);
            }
        }

        private static BlockFace PickSecondFace(
            bool firstTouch, BlockFace firstFace,
            bool secondTouch, BlockFace secondFace,
            bool thirdTouch, BlockFace thirdFace,
            BlockFace fallback)
        {
            if (firstTouch) return firstFace;
            if (secondTouch) return secondFace;
            if (thirdTouch) return thirdFace;
            return fallback;
        }

        private static bool IsHorizontalFace(BlockFace face) =>
            face == BlockFace.South || face == BlockFace.North;

        private static Vector3 OutwardForFace(BlockFace face)
        {
            return face switch
            {
                BlockFace.South => Vector3.back,
                BlockFace.North => Vector3.forward,
                BlockFace.West => Vector3.left,
                _ => Vector3.right
            };
        }

        private static Transform GetOrCreateContainer(Transform networkRoot)
        {
            var existing = networkRoot.Find(ContainerName);
            if (existing != null)
                return existing;

            var go = new GameObject(ContainerName);
            go.transform.SetParent(networkRoot, false);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Parked Cars Container");
#endif
            return go.transform;
        }
    }
}
