using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    ///     After refine + water crossings: scan mountain tunnels, then carve/bake highways
    ///     with tunnel core skip + portal daylighting.
    /// </summary>
    public sealed class ResolveMountainTunnelsStage : WorldBuildStageBase
    {
        public ResolveMountainTunnelsStage() : base(WorldBuildStageId.ResolveMountainTunnels)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Roads == null)
                throw new WorldBuildStageException(Descriptor.Id, "RoadNetworkRuntime is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Scanning mountain tunnels");

            var settings = context.Profile?.RoadNetworkSettings;
            var hydrology = context.Profile?.Hydrology;
            if (settings == null || !settings.enableMountainTunnels || hydrology == null)
            {
                context.Artifacts.SetTunnels(null);
            }
            else
            {
                // Safety net: chord-rewrite any remaining ridge winders before scan/bake.
                HighwayTunnelChordRewriter.Rewrite(
                    context.Artifacts.Roads,
                    context.Artifacts.Landforms,
                    settings,
                    context.Artifacts.CityPads,
                    context.Artifacts.Crossings,
                    hydrology.SeaLevelWorldY);

                var pads = context.Artifacts.CityPads ?? BuildPadScratch(context.Artifacts.Sites);
                var tunnels = MountainTunnelScanner.Scan(
                    context.Artifacts.Roads,
                    context.Artifacts.Landforms,
                    settings,
                    pads,
                    context.Artifacts.Crossings,
                    hydrology.SeaLevelWorldY,
                    orogen: context.Artifacts.Orogen);
                context.Artifacts.SetTunnels(tunnels);
            }

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.55f, "Baking highway corridors (tunnel-aware)");
            var carved = HighwayCorridorTerrainBake.CarveAndBake(
                context,
                detailSeedXor: unchecked((int)0x8EF1BE01));
            yield return null;

            if (settings != null && settings.tunnelEnterable)
            {
                context.Progress?.Report(Descriptor.Id, 0.85f, "Applying tunnel mouth holes");
                TunnelTerrainHoleApplicator.ApplyMouthHoles(context.Artifacts.Tunnels, settings);
                yield return null;
            }

            var oceanRenderer = context.WorldBuilder?.OceanWaterRenderer;
            if (oceanRenderer != null && context.Profile.Hydrology != null)
            {
                oceanRenderer.ResyncOceanToBounds(
                    context.Session.WorldBoundsXZ,
                    context.Profile.Hydrology.SeaLevelWorldY);
            }

            var tunnelCount = context.Artifacts.Tunnels?.Count ?? 0;
            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Tunnels={tunnelCount}; carved {carved} highway corridor(s)");
        }

        private static List<CityFlattenPad> BuildPadScratch(WorldSitePlan sites)
        {
            var pads = new List<CityFlattenPad>(16);
            if (sites?.CitySites == null)
                return pads;

            for (var i = 0; i < sites.CitySites.Count; i++)
            {
                var site = sites.CitySites[i];
                if (site == null)
                    continue;
                var halfW = Mathf.Max(40f, site.HalfWidthMeters);
                var halfD = Mathf.Max(40f, site.HalfDepthMeters);
                var bounds = Rect.MinMaxRect(
                    site.CenterXZ.x - halfW,
                    site.CenterXZ.y - halfD,
                    site.CenterXZ.x + halfW,
                    site.CenterXZ.y + halfD);
                pads.Add(new CityFlattenPad(bounds, site.PadHeightWorldY, falloffMeters: 32f));
            }

            return pads;
        }
    }
}
