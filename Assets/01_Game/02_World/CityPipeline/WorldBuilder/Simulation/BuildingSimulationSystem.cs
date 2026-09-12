using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Simulation
{
    /// <summary>
    /// Thin facade over <see cref="WorldStateSimulationService"/> for building fire/abandon ticks.
    /// </summary>
    public sealed class BuildingSimulationSystem
    {
        private readonly WorldStateSimulationService _service;

        public BuildingSimulationSystem(WorldStateSimulationService service)
        {
            _service = service;
        }

        public bool AdvanceHours(int hours) => _service != null && _service.AdvanceHours(hours);

        public bool FireBuilding(WorldEntityId buildingId, float intensity01 = 1f) =>
            _service != null && _service.FireBuildingNow(buildingId, intensity01);

        public bool AbandonBuilding(WorldEntityId buildingId) =>
            _service != null && _service.AbandonBuildingNow(buildingId);
    }
}
