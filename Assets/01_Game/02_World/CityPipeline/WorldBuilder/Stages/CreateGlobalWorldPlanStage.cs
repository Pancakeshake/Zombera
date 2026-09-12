using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Builds <see cref="WorldPlan"/> from session + profile (planning grid + subsystem seeds).</summary>
    public sealed class CreateGlobalWorldPlanStage : WorldBuildStageBase
    {
        public CreateGlobalWorldPlanStage() : base(WorldBuildStageId.CreateGlobalWorldPlan)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context == null)
                throw new WorldBuildStageException(Descriptor.Id, "Context is null.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Creating world plan");

            var session = context.Session;
            var profile = context.Profile;
            if (profile == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldGenerationProfile is required.");

            var hydrology = profile.Hydrology;
            var cellSize = hydrology != null && hydrology.CellSizeMeters > 0.01f
                ? hydrology.CellSizeMeters
                : 16f;

            var bounds = session.WorldBoundsXZ;
            if (!IsFiniteRect(bounds) || bounds.width <= 0f || bounds.height <= 0f)
                throw new WorldBuildStageException(Descriptor.Id, "Session WorldBoundsXZ is invalid.");

            var origin = new Vector2(bounds.xMin, bounds.yMin);
            var width = Mathf.Max(2, Mathf.CeilToInt(bounds.width / cellSize) + 1);
            var height = Mathf.Max(2, Mathf.CeilToInt(bounds.height / cellSize) + 1);

            var seeds = WorldSubsystemSeeds.Create(session.Seed, session.ProfileVersion);
            var sessionFingerprint = WorldSubsystemSeeds.Fingerprint(session, cellSize, width, height, seeds);
            var profileFingerprint = profile.ComputeFingerprint();

            var hasher = new StableHash64(0x574F524C44504C41UL);
            hasher.Append(sessionFingerprint);
            hasher.Append(profileFingerprint);
            var fingerprint = hasher.Finalize();

            var plan = new WorldPlan(session, origin, cellSize, width, height, seeds, fingerprint);
            context.Artifacts.SetPlan(plan);
            Zombera.World.ProceduralWorldSession.UpdatePlanFingerprint(fingerprint);
            UpdatePlanFingerprint(context, fingerprint);

            context.Progress?.Report(Descriptor.Id, 1f, $"Plan {width}x{height} @ {cellSize}m");
            yield break;
        }

        private void UpdatePlanFingerprint(WorldBuildContext context, ulong fingerprint)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out _))
                return;

            if (!context.StateManager.TryUpdateHeader(
                    "CreateGlobalWorldPlan",
                    header => header.planFingerprint = fingerprint.ToString("x16"),
                    out _,
                    out var report))
            {
                var message = report != null && report.Errors.Count > 0
                    ? string.Join("; ", report.Errors)
                    : "Failed to update WorldState plan fingerprint.";
                throw new WorldBuildStageException(Descriptor.Id, message);
            }
        }

        private static bool IsFiniteRect(Rect rect) =>
            IsFinite(rect.x) && IsFinite(rect.y) && IsFinite(rect.width) && IsFinite(rect.height);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
