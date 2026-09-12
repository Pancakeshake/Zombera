using System;
using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public sealed class WorldStateChangeSet
    {
        public long Revision;
        public string Reason = string.Empty;
        public List<WorldEntityId> AddedEntityIds = new();
        public List<WorldEntityId> RemovedEntityIds = new();
        public List<WorldEntityId> UpdatedEntityIds = new();

        public bool IsEmpty =>
            AddedEntityIds.Count == 0 &&
            RemovedEntityIds.Count == 0 &&
            UpdatedEntityIds.Count == 0;
    }
}
