using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldPoiSink
    {
        void Publish(IReadOnlyList<WorldPoiRecord> pois);
        void Clear(WorldBuildScope scope);
    }
}
