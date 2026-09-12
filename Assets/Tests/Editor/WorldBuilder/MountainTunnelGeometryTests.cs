#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class MountainTunnelGeometryTests
    {
        [TestCase(true, 20f, 0f, 40f, 20f)]
        [TestCase(false, 60f, 0f, 40f, 20f)]
        [TestCase(false, 100f, 10f, 90f, 20f)]
        public void DaylightWalk_MovesExactDistanceAlongPolyline(
            bool towardStart,
            float expectedX,
            float expectedY,
            float startX,
            float distance)
        {
            var points = new List<Vector2>
            {
                new(0f, 0f),
                new(100f, 0f),
                new(100f, 100f)
            };
            var result = InvokeWalk(points, 0, new Vector2(startX, 0f), distance, towardStart);

            Assert.That(result.x, Is.EqualTo(expectedX).Within(0.001f));
            Assert.That(result.y, Is.EqualTo(expectedY).Within(0.001f));
        }

        [Test]
        public void EnsurePortalSockets_PreservesAuthoredPositionsAndDrivesCutFootprint()
        {
            var root = new GameObject("Portal");
            var mesh = new Mesh
            {
                bounds = new Bounds(Vector3.zero, new Vector3(18f, 10f, 14f))
            };
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var approach = CreateSocket(root.transform, InfrastructureKitSockets.SocketApproach, 2f);
            var bore = CreateSocket(root.transform, InfrastructureKitSockets.SocketBore, -4f);

            try
            {
                InfrastructureKitSockets.EnsurePortalSockets(root);

                Assert.That(approach.localPosition.z, Is.EqualTo(2f).Within(0.001f));
                Assert.That(bore.localPosition.z, Is.EqualTo(-4f).Within(0.001f));
                Assert.IsTrue(InfrastructureKitSockets.TryGetPortalCutFootprint(
                    root,
                    out var halfWidth,
                    out var inwardDepth));
                Assert.That(halfWidth, Is.EqualTo(9f).Within(0.001f));
                Assert.That(inwardDepth, Is.EqualTo(6f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MouthFootprint_IsRectangleWithoutCapsuleEndOvercut()
        {
            Assert.IsTrue(InvokeFootprint(new Vector2(5f, 2f)));
            Assert.IsFalse(InvokeFootprint(new Vector2(-0.1f, 0f)));
            Assert.IsFalse(InvokeFootprint(new Vector2(10.1f, 0f)));
            Assert.IsFalse(InvokeFootprint(new Vector2(5f, 2.1f)));
        }

        [Test]
        public void BoreClearance_UsesInnerWallInsteadOfOuterShell()
        {
            var root = new GameObject("Bore");
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(-7.6f, 1f, 0f),
                    new Vector3(-4.25f, 1f, 0f),
                    new Vector3(4.25f, 1f, 0f),
                    new Vector3(7.6f, 1f, 0f),
                    new Vector3(0f, 6f, 0f)
                }
            };
            mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;

            try
            {
                Assert.IsTrue(InfrastructureKitSockets.TryGetBoreClearHalfWidth(root, out var halfWidth));
                Assert.That(halfWidth, Is.EqualTo(4.25f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ApproachLinker_ProjectsMouthsOntoSparseHighwaySegments()
        {
            var road = new RoadPolyline
            {
                id = 7,
                roadClass = RoadClass.Highway,
                pointsXZ = new List<Vector2> { Vector2.zero, new(1000f, 0f) }
            };
            var tunnel = new MountainTunnel(
                1UL, 7, new Vector2(300f, 0f), new Vector2(700f, 0f), 0f, 0f, 400f, 20f, 20f);

            var linked = HighwayTunnelApproachLinker.SnapHighwaysToTunnelMouths(
                new List<RoadPolyline> { road },
                new List<MountainTunnel> { tunnel });

            Assert.That(linked, Is.EqualTo(1));
            Assert.That(road.pointsXZ, Is.EqualTo(new[]
            {
                Vector2.zero,
                new Vector2(300f, 0f),
                new Vector2(700f, 0f),
                new Vector2(1000f, 0f)
            }));
        }

        [Test]
        public void VariableWidthStrip_TapersToRequestedPortalAperture()
        {
            var mesh = RoadMeshBuilder.BuildVariableWidthStripMeshWithHeightSampler(
                new List<Vector2> { Vector2.zero, new(20f, 0f) },
                point => Mathf.Lerp(8f, 12f, point.x / 20f),
                _ => 0f,
                4f);

            try
            {
                Assert.IsNotNull(mesh);
                var vertices = mesh.vertices;
                Assert.That(Vector3.Distance(vertices[0], vertices[1]), Is.EqualTo(8f).Within(0.001f));
                Assert.That(
                    Vector3.Distance(vertices[^2], vertices[^1]),
                    Is.EqualTo(12f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(60.98f, 60.54f, true)]
        [TestCase(2.45f, 2f, true)]
        [TestCase(2.51f, 2f, false)]
        public void PadMouthSurface_RequiresTerrainSettledToPadLevel(
            float surfaceY,
            float padY,
            bool expected)
        {
            Assert.That(InvokeSettledPadSurface(surfaceY, padY), Is.EqualTo(expected));
        }

        [Test]
        public void AsphaltSplit_RemovesDirectMouthToMouthSegment()
        {
            MountainTunnelBuildCache.Set(new List<MountainTunnel>
            {
                new(1UL, 7, new Vector2(10f, 0f), new Vector2(90f, 0f), 0f, 0f, 80f, 20f, 20f)
            });

            try
            {
                var polyline = new List<Vector2>
                {
                    Vector2.zero,
                    new(10f, 0f),
                    new(90f, 0f),
                    new(100f, 0f)
                };
                var parts = InvokeInfrastructureSplit(polyline, roadId: 7, lateralSlop: 2f);

                Assert.That(parts, Has.Count.EqualTo(2));
                Assert.That(parts[0][^1], Is.EqualTo(new Vector2(10f, 0f)));
                Assert.That(parts[1][0], Is.EqualTo(new Vector2(90f, 0f)));
            }
            finally
            {
                MountainTunnelBuildCache.Clear();
            }
        }

        private static Transform CreateSocket(Transform parent, string name, float localZ)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = new Vector3(0f, 0f, localZ);
            return socket;
        }

        private static Vector2 InvokeWalk(
            IReadOnlyList<Vector2> points,
            int segmentIndex,
            Vector2 start,
            float distance,
            bool towardStart)
        {
            var method = typeof(MountainTunnelScanner).GetMethod(
                "WalkAlongPolyline",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (Vector2)method.Invoke(
                null,
                new object[] { points, segmentIndex, start, distance, towardStart });
        }

        private static bool InvokeFootprint(Vector2 point)
        {
            var method = typeof(TunnelTerrainHoleApplicator).GetMethod(
                "IsInsideMouthFootprint",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(
                null,
                new object[] { point, Vector2.zero, new Vector2(10f, 0f), 2f });
        }

        private static bool InvokeSettledPadSurface(float surfaceY, float padY)
        {
            var method = typeof(MountainTunnelScanner).GetMethod(
                "IsSettledPadSurface",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { surfaceY, padY });
        }

        private static List<List<Vector2>> InvokeInfrastructureSplit(
            IReadOnlyList<Vector2> polyline,
            int roadId,
            float lateralSlop)
        {
            var assembly = typeof(ProceduralCityRoadBuilder).Assembly;
            var samplerType = assembly.GetType("Zombera.World.Roads.AsphaltInfrastructureSkipSampler");
            Assert.IsNotNull(samplerType);
            var sampler = System.Activator.CreateInstance(
                samplerType,
                roadId,
                Rect.MinMaxRect(0f, -2f, 100f, 2f),
                lateralSlop);
            var method = typeof(ProceduralCityRoadBuilder).GetMethod(
                "SplitExcludingInfrastructureSpans",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (List<List<Vector2>>)method.Invoke(
                null,
                new[] { polyline, sampler, (object)lateralSlop });
        }
    }
}
#endif
