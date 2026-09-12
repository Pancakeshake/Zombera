using System.Collections;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldNaturePlacer
    {
        IEnumerator Place(WorldBuildContext context);
        void Clear(WorldBuildScope scope);
    }
}
