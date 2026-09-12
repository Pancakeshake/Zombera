#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldBuildStageSectionContiguityTests
    {
        [Test]
        public void DependencyBandSections_AreEachOneContiguousSpan()
        {
            var stages = WorldBuildStageRegistry.Default.Stages;
            AssertContiguous(stages, "Planning Field");
            AssertContiguous(stages, "Environment");
            AssertContiguous(stages, "Sites");
            AssertContiguous(stages, "Layout");
            AssertContiguous(stages, "Roads");
            AssertContiguous(stages, "Wilderness");
            AssertContiguous(stages, "City");
            AssertContiguous(stages, "StreetDressing");
        }

        [Test]
        public void Environment_FollowsPlanningField_BeforeWater()
        {
            var stages = WorldBuildStageRegistry.Default.Stages;
            var planningLast = LastIndexOf(stages, "Planning Field");
            var environmentFirst = FirstIndexOf(stages, "Environment");
            var environmentLast = LastIndexOf(stages, "Environment");
            var waterFirst = FirstIndexOf(stages, "Water");

            Assert.GreaterOrEqual(planningLast, 0);
            Assert.AreEqual(planningLast + 1, environmentFirst);
            Assert.AreEqual(environmentLast + 1, waterFirst);

            var environment = CollectSection(stages, "Environment");
            Assert.AreEqual(3, environment.Count);
            Assert.AreEqual(WorldBuildStageId.ConfigureSkyAndWeather, environment[0]);
            Assert.AreEqual(WorldBuildStageId.InitializeEnvironmentState, environment[1]);
            Assert.AreEqual(WorldBuildStageId.BindWeatherConsumers, environment[2]);
        }

        [Test]
        public void Layout_IsNamedAreasThenFootpathsThenDistrictLots()
        {
            var layout = CollectSection(WorldBuildStageRegistry.Default.Stages, "Layout");
            Assert.AreEqual(3, layout.Count);
            Assert.AreEqual(WorldBuildStageId.GenerateNamedAreas, layout[0]);
            Assert.AreEqual(WorldBuildStageId.GenerateCityFootpaths, layout[1]);
            Assert.AreEqual(WorldBuildStageId.GenerateDistrictLots, layout[2]);
        }

        [Test]
        public void City_IsFiveVolumeStagesBeforeStreetDressing()
        {
            var city = CollectSection(WorldBuildStageRegistry.Default.Stages, "City");
            Assert.AreEqual(5, city.Count);
            Assert.AreEqual(WorldBuildStageId.PlaceBuildings, city[0]);
            Assert.AreEqual(WorldBuildStageId.GenerateLotTerrain, city[1]);
            Assert.AreEqual(WorldBuildStageId.GenerateFences, city[2]);
            Assert.AreEqual(WorldBuildStageId.BuildParks, city[3]);
            Assert.AreEqual(WorldBuildStageId.PlaceCityTrees, city[4]);
        }

        [Test]
        public void PlanningField_OrdersHydrologyBeforeReserveCityPads()
        {
            var planning = CollectSection(WorldBuildStageRegistry.Default.Stages, "Planning Field");
            var gen = planning.IndexOf(WorldBuildStageId.GenerateBaseLandforms);
            var erode = planning.IndexOf(WorldBuildStageId.ErodeLandforms);
            var solve = planning.IndexOf(WorldBuildStageId.SolveHydrology);
            var carve = planning.IndexOf(WorldBuildStageId.CarveWaterFeatures);
            var reserve = planning.IndexOf(WorldBuildStageId.ReserveCityPads);
            var classify = planning.IndexOf(WorldBuildStageId.ClassifyBiomesAndBuildability);
            Assert.GreaterOrEqual(gen, 0);
            Assert.AreEqual(gen + 1, erode);
            Assert.AreEqual(erode + 1, solve);
            Assert.AreEqual(solve + 1, carve);
            Assert.AreEqual(carve + 1, reserve);
            Assert.AreEqual(reserve + 1, classify);
        }

        [Test]
        public void Sites_EndsAtApplyCityPads_BeforeLayout()
        {
            var sites = CollectSection(WorldBuildStageRegistry.Default.Stages, "Sites");
            Assert.AreEqual(3, sites.Count);
            Assert.AreEqual(WorldBuildStageId.SelectCitySitesAndLandmarks, sites[0]);
            Assert.AreEqual(WorldBuildStageId.PlanInterCityHighways, sites[1]);
            Assert.AreEqual(WorldBuildStageId.ApplyCityPads, sites[2]);
        }

        [Test]
        public void Wilderness_FollowsRoads_BeforeCity()
        {
            var stages = WorldBuildStageRegistry.Default.Stages;
            var roadsLast = LastIndexOf(stages, "Roads");
            var wildernessFirst = FirstIndexOf(stages, "Wilderness");
            var wildernessLast = LastIndexOf(stages, "Wilderness");
            var cityFirst = FirstIndexOf(stages, "City");

            Assert.GreaterOrEqual(roadsLast, 0);
            Assert.AreEqual(roadsLast + 1, wildernessFirst);
            Assert.AreEqual(wildernessLast + 1, cityFirst);

            var wilderness = CollectSection(stages, "Wilderness");
            Assert.AreEqual(2, wilderness.Count);
            Assert.AreEqual(WorldBuildStageId.PlaceWildernessPois, wilderness[0]);
            Assert.AreEqual(WorldBuildStageId.PlaceWildernessNature, wilderness[1]);
        }

        private static int FirstIndexOf(IReadOnlyList<WorldBuildStageDescriptor> stages, string section)
        {
            for (var i = 0; i < stages.Count; i++)
            {
                if (stages[i].Section == section)
                    return i;
            }

            return -1;
        }

        private static int LastIndexOf(IReadOnlyList<WorldBuildStageDescriptor> stages, string section)
        {
            for (var i = stages.Count - 1; i >= 0; i--)
            {
                if (stages[i].Section == section)
                    return i;
            }

            return -1;
        }

        private static void AssertContiguous(
            IReadOnlyList<WorldBuildStageDescriptor> stages,
            string section)
        {
            var first = -1;
            var last = -1;
            for (var i = 0; i < stages.Count; i++)
            {
                if (stages[i].Section != section)
                    continue;
                if (first < 0)
                    first = i;
                last = i;
            }

            Assert.GreaterOrEqual(first, 0, section + " missing from registry");
            for (var i = first; i <= last; i++)
                Assert.AreEqual(section, stages[i].Section, section + " must be contiguous");
        }

        private static List<WorldBuildStageId> CollectSection(
            IReadOnlyList<WorldBuildStageDescriptor> stages,
            string section)
        {
            var ids = new List<WorldBuildStageId>();
            for (var i = 0; i < stages.Count; i++)
            {
                if (stages[i].Section == section)
                    ids.Add(stages[i].Id);
            }

            return ids;
        }
    }
}
#endif
