using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public readonly struct LotDrivewayRequest
    {
        public Vector2 StreetPointXZ { get; }
        public Vector2 DoorPointXZ { get; }
        public float WidthMeters { get; }
        public int LotId { get; }

        public LotDrivewayRequest(Vector2 streetPointXZ, Vector2 doorPointXZ, float widthMeters, int lotId)
        {
            StreetPointXZ = streetPointXZ;
            DoorPointXZ = doorPointXZ;
            WidthMeters = widthMeters;
            LotId = lotId;
        }
    }

    /// <summary>
    ///     Places procedural mesh driveways from street edge to lot door/garage anchors.
    /// </summary>
    public static class ProceduralDrivewayPlacer
    {
        public static int PlaceDriveways(
            Transform parent,
            IReadOnlyList<LotDrivewayRequest> requests,
            Func<Vector2, float> resolveHeight,
            Material drivewayMaterial,
            float surfaceLiftMeters = 0.05f,
            ProceduralLayerMeshAccumulator accumulator = null)
        {
            if (parent == null || requests == null || requests.Count == 0 || resolveHeight == null)
                return 0;
            if (drivewayMaterial == null)
            {
                Debug.LogWarning("[ProceduralDrivewayPlacer] Driveway material missing — skipping.");
                return 0;
            }

            var drivewaysRoot = EnsureChildFolder(parent, ProceduralRoadNetworkNames.Driveways);
            var placed = 0;
            var polyline = new List<Vector2>(2);
            var sampleHeight = surfaceLiftMeters > 0f
                ? (Func<Vector2, float>)(xz => resolveHeight(xz) + surfaceLiftMeters)
                : resolveHeight;

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.WidthMeters <= 0.5f)
                    continue;

                var span = request.DoorPointXZ - request.StreetPointXZ;
                if (span.sqrMagnitude < 0.25f)
                    continue;

                polyline.Clear();
                polyline.Add(request.StreetPointXZ);
                polyline.Add(request.DoorPointXZ);

                var mesh = RoadMeshBuilder.BuildStripMeshWithHeightSampler(
                    polyline, request.WidthMeters, sampleHeight);
                if (mesh == null)
                    continue;

                if (!ProceduralMeshEmitUtility.Emit(
                        drivewaysRoot,
                        ProceduralRoadNetworkNames.Driveways,
                        $"Lot_{request.LotId}",
                        mesh,
                        drivewayMaterial,
                        accumulator))
                    continue;

                placed++;
            }

            return placed;
        }

        private static Transform EnsureChildFolder(Transform parent, string folderName)
        {
            var existing = parent.Find(folderName);
            if (existing != null)
                return existing;

            var folder = new GameObject(folderName);
            folder.transform.SetParent(parent, false);
            return folder.transform;
        }
    }
}
