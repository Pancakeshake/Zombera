#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldWaterPlacementGateTests
    {
        [Test]
        public void PassesWaterSample_ReclaimableOcean_Passes()
        {
            var water = new WorldWaterSample(WorldWaterClass.Ocean, 0f, 3f, 0f);

            var ok = WorldWaterPlacementGate.PassesWaterSample(
                water,
                deepWaterDepth: 0.3f,
                minDistanceToWaterMeters: 2f,
                maxReclaimDepthMeters: 8f,
                out var code);

            Assert.IsTrue(ok);
            Assert.IsNull(code);
        }

        [Test]
        public void PassesWaterSample_DeepOcean_Rejects()
        {
            var water = new WorldWaterSample(WorldWaterClass.Ocean, 0f, 12f, 0f);

            var ok = WorldWaterPlacementGate.PassesWaterSample(
                water,
                deepWaterDepth: 0.3f,
                minDistanceToWaterMeters: 2f,
                maxReclaimDepthMeters: 8f,
                out var code);

            Assert.IsFalse(ok);
            Assert.AreEqual("reject_water", code);
        }

        [Test]
        public void FootprintIsDry_RequireQuery_NullQuery_Rejects()
        {
            var ok = WorldWaterPlacementGate.FootprintIsDry(
                new Rect(0f, 0f, 10f, 10f),
                terrainQuery: null,
                deepWaterDepth: 0.3f,
                minDistanceToWaterMeters: 2f,
                maxReclaimDepthMeters: 8f,
                requireQuery: true,
                out var code);

            Assert.IsFalse(ok);
            Assert.AreEqual("reject_water_query_missing", code);
        }

        [Test]
        public void FootprintIsDry_LabNullQuery_Accepts()
        {
            var ok = WorldWaterPlacementGate.FootprintIsDry(
                new Rect(0f, 0f, 10f, 10f),
                terrainQuery: null,
                deepWaterDepth: 0.3f,
                minDistanceToWaterMeters: 2f,
                out var code);

            Assert.IsTrue(ok);
            Assert.IsNull(code);
        }
    }
}
#endif
