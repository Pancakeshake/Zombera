using System.Collections.Generic;
using System.Text;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Views
{
    public static class WorldBuildingViewAuditor
    {
        public static WorldBuildingViewAuditReport AuditLoadedViews(
            WorldStateManager stateManager,
            WorldStateViewRegistry registry)
        {
            var report = new WorldBuildingViewAuditReport();
            if (stateManager == null || registry == null)
            {
                report.AddIssue("Missing WorldStateManager or WorldStateViewRegistry.");
                return report;
            }

            var loadedTiles = new List<WorldTileKey>(16);
            var ids = new List<WorldEntityId>(32);
            var views = new List<WorldBuildingView>(64);

            registry.CopyLoadedTiles(loadedTiles);
            registry.CopyBuildingViews(views);
            report.LoadedTileCount = loadedTiles.Count;
            report.RegisteredViewCount = views.Count;

            AuditMissingViews(stateManager, registry, loadedTiles, ids, report);
            AuditRegisteredViews(stateManager, views, report);
            return report;
        }

        private static void AuditMissingViews(
            WorldStateManager stateManager,
            WorldStateViewRegistry registry,
            List<WorldTileKey> loadedTiles,
            List<WorldEntityId> ids,
            WorldBuildingViewAuditReport report)
        {
            for (var i = 0; i < loadedTiles.Count; i++)
            {
                stateManager.CopyEntityIdsCoveringTile(loadedTiles[i], WorldEntityKind.Building, ids);
                for (var j = 0; j < ids.Count; j++)
                {
                    if (registry.HasBuildingView(ids[j]))
                        continue;

                    report.MissingViewCount++;
                    report.AddIssue("Missing building view for " + ids[j] + " on tile " + loadedTiles[i] + ".");
                }
            }
        }

        private static void AuditRegisteredViews(
            WorldStateManager stateManager,
            List<WorldBuildingView> views,
            WorldBuildingViewAuditReport report)
        {
            for (var i = 0; i < views.Count; i++)
            {
                var view = views[i];
                if (view == null || !view.HasWorldEntityId || view.WorldEntityId.kind != WorldEntityKind.Building)
                {
                    report.InvalidBindingCount++;
                    report.AddIssue("Registered building view has no valid building binding.");
                    continue;
                }

                if (stateManager.TryCopyBuilding(view.WorldEntityId, out _))
                    continue;

                report.OrphanedViewCount++;
                report.AddIssue("Registered building view no longer has state: " + view.WorldEntityId + ".");
            }
        }
    }

    public sealed class WorldBuildingViewAuditReport
    {
        private const int MaxIssueLines = 32;

        public int LoadedTileCount;
        public int RegisteredViewCount;
        public int MissingViewCount;
        public int OrphanedViewCount;
        public int InvalidBindingCount;
        public readonly List<string> Issues = new();

        public bool HasIssues => MissingViewCount > 0 || OrphanedViewCount > 0 || InvalidBindingCount > 0;

        public void AddIssue(string issue)
        {
            if (Issues.Count >= MaxIssueLines)
                return;

            Issues.Add(issue ?? string.Empty);
        }

        public override string ToString()
        {
            var builder = new StringBuilder(160);
            builder.Append("loadedTiles=").Append(LoadedTileCount)
                .Append(", views=").Append(RegisteredViewCount)
                .Append(", missing=").Append(MissingViewCount)
                .Append(", orphaned=").Append(OrphanedViewCount)
                .Append(", invalid=").Append(InvalidBindingCount);

            for (var i = 0; i < Issues.Count; i++)
                builder.AppendLine().Append("- ").Append(Issues[i]);

            return builder.ToString();
        }
    }
}
