using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    [Serializable]
    public sealed class CityStreetFurnitureSettings
    {
        [Min(0f)] public float minSeparationMeters = 4f;
        [Range(0f, 1f)] public float benchDensityPer100Meters = 0.15f;
        [Range(0f, 1f)] public float mailboxDensityPer100Meters = 0.08f;
        [Range(0f, 1f)] public float hydrantDensityPer100Meters = 0.05f;
        [Range(0f, 1f)] public float trashCanDensityPer100Meters = 0.1f;

        public static CityStreetFurnitureSettings FromStreetscape(CityStreetscapeConfig streetscape)
        {
            if (streetscape == null)
                return new CityStreetFurnitureSettings();

            return new CityStreetFurnitureSettings
            {
                minSeparationMeters = streetscape.streetFurnitureMinSeparationMeters,
                benchDensityPer100Meters = streetscape.benchDensityPer100Meters,
                mailboxDensityPer100Meters = streetscape.mailboxDensityPer100Meters,
                hydrantDensityPer100Meters = streetscape.hydrantDensityPer100Meters,
                trashCanDensityPer100Meters = streetscape.trashCanDensityPer100Meters
            };
        }
    }

    public static class CityStreetFurniturePlacer
    {
        private const string ContainerName = "StreetFurniture";

        private readonly struct PlacementRecord
        {
            public readonly Vector2 Position;
            public readonly float Radius;

            public PlacementRecord(Vector2 position, float radius)
            {
                Position = position;
                Radius = radius;
            }
        }

        public static int PlaceForHub(
            Transform networkRoot,
            IReadOnlyList<RoadPolyline> roads,
            Rect boundRect,
            RoadNetworkSettings roadSettings,
            CityStreetscapeConfig streetscape,
            Func<Vector2, float> resolveHeight,
            int layoutSeed)
        {
            if (networkRoot == null || roads == null || streetscape == null || resolveHeight == null)
                return 0;
            if (!streetscape.spawnStreetFurniture)
                return 0;

            Clear(networkRoot);
            var container = GetOrCreateContainer(networkRoot);
            var furnitureSettings = CityStreetFurnitureSettings.FromStreetscape(streetscape);
            var rng = new System.Random(layoutSeed + 55123);
            var records = new List<PlacementRecord>(256);
            var placed = 0;

            for (var r = 0; r < roads.Count; r++)
            {
                var road = roads[r];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                placed += ScatterAlongSidewalk(container, road, boundRect, roadSettings, streetscape, furnitureSettings, resolveHeight, rng, records, -1);
                placed += ScatterAlongSidewalk(container, road, boundRect, roadSettings, streetscape, furnitureSettings, resolveHeight, rng, records, 1);
            }

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

        private static int ScatterAlongSidewalk(
            Transform parent,
            RoadPolyline road,
            Rect boundRect,
            RoadNetworkSettings roadSettings,
            CityStreetscapeConfig streetscape,
            CityStreetFurnitureSettings settings,
            Func<Vector2, float> resolveHeight,
            System.Random rng,
            List<PlacementRecord> records,
            int sideSign)
        {
            var halfRoad = Mathf.Max(0.5f, road.widthMeters) * 0.5f;
            var sidewalk = roadSettings != null && roadSettings.spawnSidewalkMeshes
                ? roadSettings.ResolveSidewalkWidthMeters()
                : 0f;
            var inner = halfRoad + sidewalk * 0.25f;
            var outer = halfRoad + sidewalk + 0.4f;
            var centerOffset = (inner + outer) * 0.5f * sideSign;
            var offsetPolyline = RoadMeshBuilder.OffsetPolyline(road.pointsXZ, centerOffset);
            var length = PolylineLength(offsetPolyline);
            if (length < 5f)
                return 0;

            var placed = 0;
            // Benches are modeled with the seat along local X and the facing along local +Z,
            // so rotate them 90° per side to sit alongside the road facing it.
            var benchFacingOffsetDegrees = 90f * sideSign;
            placed += TryScatterType(parent, offsetPolyline, boundRect, CityPlacerPrefabResolver.Bench, settings.benchDensityPer100Meters, length, settings, resolveHeight, rng, records, "Bench", benchFacingOffsetDegrees);
            placed += TryScatterType(parent, offsetPolyline, boundRect, CityPlacerPrefabResolver.Mailbox, settings.mailboxDensityPer100Meters, length, settings, resolveHeight, rng, records, "Mailbox", 0f);
            placed += TryScatterType(parent, offsetPolyline, boundRect, CityPlacerPrefabResolver.FireHydrant, settings.hydrantDensityPer100Meters, length, settings, resolveHeight, rng, records, "Hydrant", 0f);
            placed += TryScatterType(parent, offsetPolyline, boundRect, CityPlacerPrefabResolver.TrashCan, settings.trashCanDensityPer100Meters, length, settings, resolveHeight, rng, records, "TrashCan", 0f);
            return placed;
        }

        private static int TryScatterType(
            Transform parent,
            List<Vector2> polyline,
            Rect boundRect,
            GameObject prefab,
            float densityPer100Meters,
            float lengthMeters,
            CityStreetFurnitureSettings settings,
            Func<Vector2, float> resolveHeight,
            System.Random rng,
            List<PlacementRecord> records,
            string label,
            float facingOffsetDegrees)
        {
            if (prefab == null || densityPer100Meters <= 0f)
                return 0;

            var targetCount = Mathf.Max(0, Mathf.RoundToInt(lengthMeters / 100f * densityPer100Meters));
            if (targetCount <= 0)
                return 0;

            var placed = 0;
            var attempts = targetCount * 12;
            for (var a = 0; a < attempts && placed < targetCount; a++)
            {
                var t = (float)rng.NextDouble();
                if (!TrySamplePolyline(polyline, t, out var point, out var tangent))
                    continue;
                if (!boundRect.Contains(point))
                    continue;
                if (!PassesSeparation(point, settings.minSeparationMeters, records))
                    continue;

                var yaw = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg + facingOffsetDegrees + (float)(rng.NextDouble() * 20.0 - 10.0);
                var pos = new Vector3(point.x, resolveHeight(point), point.y);
                var instance = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
                instance.name = label + "_" + placed;
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Street Furniture");
#endif
                records.Add(new PlacementRecord(point, settings.minSeparationMeters));
                placed++;
            }

            return placed;
        }

        private static bool PassesSeparation(Vector2 point, float minSeparation, List<PlacementRecord> records)
        {
            var minSepSq = minSeparation * minSeparation;
            for (var i = 0; i < records.Count; i++)
            {
                if ((records[i].Position - point).sqrMagnitude < minSepSq)
                    return false;
            }

            return true;
        }

        private static bool TrySamplePolyline(List<Vector2> polyline, float t, out Vector2 point, out Vector2 tangent)
        {
            point = default;
            tangent = Vector2.right;
            if (polyline == null || polyline.Count < 2)
                return false;

            var total = PolylineLength(polyline);
            var target = Mathf.Clamp01(t) * total;
            var traveled = 0f;
            for (var i = 1; i < polyline.Count; i++)
            {
                var a = polyline[i - 1];
                var b = polyline[i];
                var segLen = Vector2.Distance(a, b);
                if (traveled + segLen >= target)
                {
                    var localT = segLen > 0.001f ? (target - traveled) / segLen : 0f;
                    point = Vector2.Lerp(a, b, localT);
                    tangent = (b - a).normalized;
                    return true;
                }

                traveled += segLen;
            }

            point = polyline[^1];
            tangent = (polyline[^1] - polyline[^2]).normalized;
            return true;
        }

        private static float PolylineLength(List<Vector2> polyline)
        {
            var total = 0f;
            if (polyline == null)
                return total;

            for (var i = 1; i < polyline.Count; i++)
                total += Vector2.Distance(polyline[i - 1], polyline[i]);
            return total;
        }

        private static Transform GetOrCreateContainer(Transform networkRoot)
        {
            var existing = networkRoot.Find(ContainerName);
            if (existing != null)
                return existing;

            var go = new GameObject(ContainerName);
            go.transform.SetParent(networkRoot, false);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Street Furniture Container");
#endif
            return go.transform;
        }
    }
}