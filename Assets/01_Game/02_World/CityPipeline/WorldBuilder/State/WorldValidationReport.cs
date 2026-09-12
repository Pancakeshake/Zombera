using System;
using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public enum WorldValidationSeverity
    {
        Error = 0,
        Warning = 1
    }

    public enum WorldValidationMode
    {
        Basic = 0,
        Canonical = 1
    }

    public sealed class WorldValidationContext
    {
        public bool RequireKnownArchetypes { get; set; }
        public ISet<string> BuildingArchetypeIds { get; set; }
        public ISet<string> PoiArchetypeIds { get; set; }
        public Func<string, bool> BuildingArchetypeExists { get; set; }
        public Func<string, bool> PoiArchetypeExists { get; set; }

        public bool KnowsBuildingArchetype(string archetypeId)
        {
            if (string.IsNullOrWhiteSpace(archetypeId))
                return false;

            if (BuildingArchetypeExists != null)
                return BuildingArchetypeExists(archetypeId);

            return BuildingArchetypeIds == null || BuildingArchetypeIds.Contains(archetypeId);
        }

        public bool KnowsPoiArchetype(string archetypeId)
        {
            if (string.IsNullOrWhiteSpace(archetypeId))
                return false;

            if (PoiArchetypeExists != null)
                return PoiArchetypeExists(archetypeId);

            return PoiArchetypeIds == null || PoiArchetypeIds.Contains(archetypeId);
        }
    }

    public sealed class WorldValidationIssue
    {
        public WorldValidationSeverity Severity { get; }
        public string Code { get; }
        public WorldEntityId EntityId { get; }
        public string FieldPath { get; }
        public string Message { get; }
        public string Expected { get; }
        public string Actual { get; }

        public WorldValidationIssue(
            WorldValidationSeverity severity,
            string code,
            WorldEntityId entityId,
            string fieldPath,
            string message,
            string expected = "",
            string actual = "")
        {
            Severity = severity;
            Code = code ?? string.Empty;
            EntityId = entityId;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
        }

        public override string ToString()
        {
            var path = string.IsNullOrEmpty(FieldPath) ? string.Empty : $" {FieldPath}:";
            var id = IsEmpty(EntityId) ? string.Empty : $" [{EntityId}]";
            return $"{Code}{id}{path} {Message}".Trim();
        }

        private static bool IsEmpty(WorldEntityId id) =>
            id.kind == WorldEntityKind.None && string.IsNullOrEmpty(id.value);
    }

    public sealed class WorldValidationReport
    {
        public bool IsValid = true;
        public List<WorldValidationIssue> Issues = new();
        public List<string> Errors = new();
        public List<string> Warnings = new();

        public void AddIssue(WorldValidationIssue issue)
        {
            if (issue == null)
                return;

            Issues.Add(issue);
            if (issue.Severity == WorldValidationSeverity.Warning)
            {
                Warnings.Add(issue.ToString());
                return;
            }

            Errors.Add(issue.ToString());
            IsValid = false;
        }

        public void AddIssue(
            WorldValidationSeverity severity,
            string code,
            WorldEntityId entityId,
            string fieldPath,
            string message,
            string expected = "",
            string actual = "")
        {
            AddIssue(new WorldValidationIssue(
                severity,
                code,
                entityId,
                fieldPath,
                message,
                expected,
                actual));
        }

        public void Merge(WorldValidationReport other)
        {
            if (other == null)
                return;

            for (var i = 0; i < other.Issues.Count; i++)
                AddIssue(other.Issues[i]);

            MergeLegacyMessages(other.Errors, Errors);
            MergeLegacyMessages(other.Warnings, Warnings);
            IsValid = Errors.Count == 0;
        }

        private static void MergeLegacyMessages(List<string> source, List<string> target)
        {
            if (source == null || target == null)
                return;

            for (var i = 0; i < source.Count; i++)
            {
                if (!target.Contains(source[i]))
                    target.Add(source[i]);
            }
        }
    }
}
