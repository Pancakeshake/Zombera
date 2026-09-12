using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        private IGeneratedBuildingStateSink _generatedBuildingStateSink;

        internal IGeneratedBuildingStateSink GeneratedBuildingStateSink => _generatedBuildingStateSink;

        public void BindGeneratedBuildingStateSink(IGeneratedBuildingStateSink sink)
        {
            _generatedBuildingStateSink = sink;
        }

        public void ClearGeneratedBuildingStateSink(IGeneratedBuildingStateSink sink = null)
        {
            if (sink == null || ReferenceEquals(_generatedBuildingStateSink, sink))
                _generatedBuildingStateSink = null;
        }
    }
}
