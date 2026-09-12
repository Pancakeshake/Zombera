using System;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Thrown when a specific pipeline stage fails.</summary>
    public sealed class WorldBuildStageException : Exception
    {
        public WorldBuildStageId Stage { get; }

        public WorldBuildStageException(WorldBuildStageId stage, string message)
            : base(message)
        {
            Stage = stage;
        }

        public WorldBuildStageException(WorldBuildStageId stage, string message, Exception innerException)
            : base(message, innerException)
        {
            Stage = stage;
        }
    }
}
