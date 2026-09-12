using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Mutable per-stage status record for a pipeline run.</summary>
    public sealed class WorldBuildStageRecord
    {
        public WorldBuildStageId StageId { get; set; }
        public WorldBuildStageStatus Status { get; set; }
        public float DurationSeconds { get; set; }
        public float Progress01 { get; set; }
        public ulong Fingerprint { get; set; }
        public string InvalidationReason { get; set; }
        public List<string> Messages { get; } = new();

        public WorldBuildStageRecord(WorldBuildStageId stageId)
        {
            StageId = stageId;
            Status = WorldBuildStageStatus.Pending;
        }
    }
}
