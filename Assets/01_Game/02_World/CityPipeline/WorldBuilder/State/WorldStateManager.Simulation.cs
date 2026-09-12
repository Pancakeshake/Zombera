using System;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateManager
    {
        internal bool TryApplySimulationTransaction(
            string reason,
            Action<WorldState, WorldStateChangeSet> mutation,
            out WorldStateChangeSet changes,
            out WorldValidationReport report)
        {
            changes = CreateChangeSet(reason);
            if (!CanMutate(Revision, out report))
                return false;

            if (mutation == null)
            {
                report = ErrorReport("Simulation mutation delegate is null.");
                return false;
            }

            var candidate = WorldStateCloner.Clone(_state);
            candidate.clock ??= new WorldSimulationClockState();
            candidate.pendingEvents ??= new();
            candidate.eventHistory ??= new();
            mutation(candidate, changes);
            return TryCommitCandidate(
                candidate,
                changes,
                WorldStateLifecycleReason.Mutated,
                out report);
        }
    }
}
