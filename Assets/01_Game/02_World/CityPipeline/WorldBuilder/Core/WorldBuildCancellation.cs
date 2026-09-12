using System;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Cooperative cancellation token for world-build stages.</summary>
    public sealed class WorldBuildCancellation
    {
        public bool IsRequested { get; private set; }

        public void Request()
        {
            IsRequested = true;
        }

        public void ThrowIfRequested()
        {
            if (!IsRequested) return;
            throw new OperationCanceledException("World build was cancelled.");
        }
    }
}
