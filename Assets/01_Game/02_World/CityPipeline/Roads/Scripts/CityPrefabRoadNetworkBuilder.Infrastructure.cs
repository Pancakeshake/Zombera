using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        private WorldBuildArtifacts _infrastructureArtifacts;
        private IWorldTerrainQuery _infrastructureTerrainQuery;
        private IWorldHydrologyQuery _infrastructureHydrologyQuery;
        private HydrologyProfile _infrastructureHydrologyProfile;

        /// <summary>Supplies build-session services for final-polyline bridge and tunnel resolution.</summary>
        public void ConfigureFinalRoadInfrastructure(
            WorldBuildArtifacts artifacts,
            IWorldTerrainQuery terrainQuery,
            IWorldHydrologyQuery hydrologyQuery,
            HydrologyProfile hydrologyProfile)
        {
            _infrastructureArtifacts = artifacts;
            _infrastructureTerrainQuery = terrainQuery;
            _infrastructureHydrologyQuery = hydrologyQuery;
            _infrastructureHydrologyProfile = hydrologyProfile;
            SetWaterFootprintPlan(artifacts?.Hydrology?.FootprintPlan);
        }

        public void ClearFinalRoadInfrastructureConfiguration()
        {
            _infrastructureArtifacts = null;
            _infrastructureTerrainQuery = null;
            _infrastructureHydrologyQuery = null;
            _infrastructureHydrologyProfile = null;
            SetWaterFootprintPlan(null);
        }

        private void ClearGeneratedInfrastructureContent()
        {
            DestroyInfrastructureRoot("MountainTunnels");
            DestroyInfrastructureRoot("WaterCrossings");
            MountainTunnelBuildCache.Clear();
            WaterCrossingBuildCache.Set(null);
        }

        /// <summary>
        /// Pipeline owns roads when the Roads artifact exists — even if the highway list is empty.
        /// </summary>
        private bool HasPipelineRoadAuthority() =>
            _infrastructureArtifacts?.Roads != null;

        private void DestroyInfrastructureRoot(string rootName)
        {
            var root = transform.Find(rootName);
            if (root == null)
                return;

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
                return;
            }
#endif
            UnityEngine.Object.Destroy(root.gameObject);
        }

        private void RefreshFinalRoadInfrastructure(RoadNetworkRuntime network)
        {
            if (_infrastructureArtifacts == null || network == null)
                return;

            // Resolve* stages own spans. Mesh must not invent crossings/tunnels when the
            // pipeline already published Roads (including intentional empty span lists).
            if (HasPipelineRoadAuthority())
            {
                WaterCrossingBuildCache.Set(_infrastructureArtifacts.Crossings);
                MountainTunnelBuildCache.Set(_infrastructureArtifacts.Tunnels);
                return;
            }

            RefreshCrossingsWithoutPipelineAuthority(network);
            RefreshTunnelsWithoutPipelineAuthority(network);
        }

        private void RefreshCrossingsWithoutPipelineAuthority(RoadNetworkRuntime network)
        {
            var existingCrossings = _infrastructureArtifacts.Crossings;
            if (existingCrossings != null && existingCrossings.Count > 0)
            {
                WaterCrossingBuildCache.Set(existingCrossings);
                return;
            }

            var crossings = WaterCrossingScanner.Scan(
                network,
                _infrastructureTerrainQuery,
                _infrastructureHydrologyQuery,
                _infrastructureHydrologyProfile);
            _infrastructureArtifacts.SetCrossings(crossings);
            _infrastructureArtifacts.Hydrology?.SetCrossings(crossings);
        }

        private void RefreshTunnelsWithoutPipelineAuthority(RoadNetworkRuntime network)
        {
            var existingTunnels = _infrastructureArtifacts.Tunnels;
            if (existingTunnels != null && existingTunnels.Count > 0)
            {
                MountainTunnelBuildCache.Set(existingTunnels);
                return;
            }

            var tunnels = MountainTunnelScanner.Scan(
                network,
                _infrastructureArtifacts.Landforms,
                roadNetworkSettings,
                _infrastructureArtifacts.CityPads,
                _infrastructureArtifacts.Crossings,
                _infrastructureHydrologyProfile != null
                    ? _infrastructureHydrologyProfile.SeaLevelWorldY
                    : 0f,
                orogen: _infrastructureArtifacts.Orogen);
            _infrastructureArtifacts.SetTunnels(tunnels);
        }
    }
}
