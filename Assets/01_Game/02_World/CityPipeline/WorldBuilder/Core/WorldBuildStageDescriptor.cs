using System;
using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Immutable metadata describing a pipeline stage.</summary>
    public sealed class WorldBuildStageDescriptor
    {
        public WorldBuildStageId Id { get; }
        public string Section { get; }
        public string DisplayName { get; }
        public IReadOnlyList<WorldBuildStageId> HardPrerequisites { get; }
        public IReadOnlyList<WorldBuildScopeKind> SupportedScopes { get; }
        public bool IsGlobalArtifactStage { get; }
        public bool CanCancel { get; }
        public string[] OutputArtifactKeys { get; }
        public WorldBuildStageId[] Invalidates { get; }

        public WorldBuildStageDescriptor(
            WorldBuildStageId id,
            string section,
            string displayName,
            IReadOnlyList<WorldBuildStageId> hardPrerequisites,
            IReadOnlyList<WorldBuildScopeKind> supportedScopes,
            bool isGlobalArtifactStage,
            bool canCancel,
            string[] outputArtifactKeys,
            WorldBuildStageId[] invalidates)
        {
            Id = id;
            Section = section ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            HardPrerequisites = hardPrerequisites ?? Array.Empty<WorldBuildStageId>();
            SupportedScopes = supportedScopes ?? Array.Empty<WorldBuildScopeKind>();
            IsGlobalArtifactStage = isGlobalArtifactStage;
            CanCancel = canCancel;
            OutputArtifactKeys = outputArtifactKeys ?? Array.Empty<string>();
            Invalidates = invalidates ?? Array.Empty<WorldBuildStageId>();
        }
    }
}
