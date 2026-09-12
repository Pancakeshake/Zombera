using System.Collections;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Executable world-build pipeline stage.</summary>
    public interface IWorldBuildStage
    {
        WorldBuildStageDescriptor Descriptor { get; }
        IEnumerator Execute(WorldBuildContext context);
    }
}
