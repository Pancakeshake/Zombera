using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Stable fingerprints for generation profiles used by WorldPlan and cache keys.</summary>
    public static class WorldProfileFingerprints
    {
        public static ulong Compute(WorldGenerationProfile profile)
        {
            var hasher = new StableHash64(0x50524F46494C4501UL);
            if (profile == null)
                return hasher.Finalize();

            hasher.Append(profile.ProfileVersion);
            hasher.Append(profile.SurfacePaintAlgorithmVersion);
            AppendMapSize(ref hasher, profile.MapSizeSettings);
            AppendTerrainGrid(ref hasher, profile.TerrainGrid);
            AppendLandforms(ref hasher, profile.Landforms);
            AppendHydrology(ref hasher, profile.Hydrology);
            AppendRoads(ref hasher, profile.RoadNetworkSettings);
            AppendWater(ref hasher, profile.Water);
            AppendEnvironment(ref hasher, profile.Environment);
            AppendNature(ref hasher, profile.Nature);
            return hasher.Finalize();
        }

        public static void AppendMapSize(ref StableHash64 hasher, WorldMapSizeSettings settings)
        {
            if (settings == null) return;
            hasher.Append(settings.SmallTilesPerSide);
            hasher.Append(settings.MediumTilesPerSide);
            hasher.Append(settings.LargeTilesPerSide);
            hasher.Append(settings.SmallOceanRingTiles);
            hasher.Append(settings.MediumOceanRingTiles);
            hasher.Append(settings.LargeOceanRingTiles);
            hasher.Append(settings.SmallMetropolisCount);
            hasher.Append(settings.SmallCityCount);
            hasher.Append(settings.SmallSettlementMinCount);
            hasher.Append(settings.SmallSettlementMaxCount);
            hasher.Append(settings.MediumMetropolisCount);
            hasher.Append(settings.MediumCityCount);
            hasher.Append(settings.MediumSettlementMinCount);
            hasher.Append(settings.MediumSettlementMaxCount);
            hasher.Append(settings.LargeMetropolisCount);
            hasher.Append(settings.LargeCityCount);
            hasher.Append(settings.LargeSettlementMinCount);
            hasher.Append(settings.LargeSettlementMaxCount);
            hasher.Append(settings.SmallCityTarget);
            hasher.Append(settings.MediumCityTarget);
            hasher.Append(settings.LargeCityTarget);
            hasher.Append(settings.InitialPlayAreaRadiusTiles);
            hasher.Append(settings.EdgeClearanceMeters);
        }

        public static void AppendTerrainGrid(ref StableHash64 hasher, TerrainGridProfile grid)
        {
            if (grid == null) return;
            hasher.Append(grid.HeightmapResolution);
            hasher.Append(grid.AlphamapResolution);
            hasher.Append(grid.BaseMapResolution);
            hasher.Append(grid.TerrainVerticalSize);
            hasher.Append(grid.SeaLevelOffsetY);
            hasher.Append(grid.DetailResolution);
            hasher.Append(grid.DetailSamplesPerPatch);
        }

        public static void AppendLandforms(ref StableHash64 hasher, LandformProfile landforms)
        {
            if (landforms == null) return;
            hasher.Append(landforms.LandformAlgorithmVersion);
            hasher.Append(landforms.ContinentalnessSeedOffset);
            hasher.Append(landforms.DomainWarpSeedOffset);
            hasher.Append(landforms.HillsSeedOffset);
            hasher.Append(landforms.MountainSeedOffset);
            hasher.Append(landforms.ContinentalnessScale);
            hasher.Append(landforms.DomainWarpScale);
            hasher.Append(landforms.HillsScale);
            hasher.Append(landforms.MountainScale);
            hasher.Append(landforms.ContinentalnessAmplitude);
            hasher.Append(landforms.DomainWarpAmplitude);
            hasher.Append(landforms.HillsAmplitude);
            hasher.Append(landforms.MountainAmplitude);
            hasher.Append(landforms.ResidualMountainAmplitudeMeters);
            hasher.Append(landforms.PlainsBias);
            hasher.Append(landforms.MainlandMaskLo);
            hasher.Append(landforms.MainlandMaskHi);
            hasher.Append(landforms.CoastFalloffWidthMeters);
            hasher.Append(landforms.OceanCoastErosionAmplitudeMeters);
            hasher.Append(landforms.OceanCoastErosionScaleMeters);
            hasher.Append(landforms.InlandDryFloorMetersAboveSea);
            hasher.Append(landforms.InlandDryFloorBlendMeters);
            hasher.Append(landforms.IslandNoiseScaleMeters);
            hasher.Append(landforms.ArchipelagoNoiseScaleMeters);
            hasher.Append(landforms.IslandPeakThreshold);
            hasher.Append(landforms.ArchipelagoPeakThreshold);
            hasher.Append(landforms.ThermalErosionIterations);
            hasher.Append(landforms.ThermalTalusAngleDegrees);
            hasher.Append(landforms.ThermalTransferFraction);
            hasher.Append(landforms.ForceOceanOnAllEdges ? 1 : 0);
            hasher.Append(landforms.OceanEdgeChance);
            hasher.Append(landforms.AllowLandlockedMaps ? 1 : 0);
            hasher.Append(landforms.UseMacroBiomeRegions ? 1 : 0);
            hasher.Append(landforms.BiomeForcedBoostScale);
            hasher.Append(landforms.BiomeDominantThreshold);
            hasher.Append(landforms.EdgeBarrierDepthMeters);
            hasher.Append(landforms.EdgeBarrierPeakMeters);
            hasher.Append(landforms.EdgeBarrierFalloffMeters);
            hasher.Append(landforms.EdgeBarrierNoiseScaleMeters);
            hasher.Append(landforms.EdgeBarrierNoiseAmplitudeMeters);
            hasher.Append(landforms.EdgeBarrierSeedOffset);
            hasher.Append(landforms.OceanOffshoreWidthMeters);
            hasher.Append(landforms.OceanShoreShelfWidthMeters);
            hasher.Append(landforms.OceanBeachWidthMeters);
            hasher.Append(landforms.OceanBeachMaxElevationMeters);
            hasher.Append(landforms.OceanTrenchDepthMeters);
            hasher.Append(landforms.OceanTrenchSteepness);
            hasher.Append(landforms.OceanTrenchNoiseAmplitudeMeters);
            hasher.Append(landforms.OceanShelfOuterDepthMeters);
            hasher.Append(landforms.OceanShelfInnerDepthMeters);
            hasher.Append(landforms.OceanShelfReefNoiseScaleMeters);
            hasher.Append(landforms.OceanShelfReefNoiseAmplitudeMeters);
            hasher.Append(landforms.OceanShelfReefCoverage);
            hasher.Append(landforms.InteriorMountainsEnabled ? 1 : 0);
            hasher.Append(landforms.InteriorMountainRangeCount);
            hasher.Append(landforms.InteriorMountainPeakMeters);
            hasher.Append(landforms.InteriorMountainWidthMeters);
            hasher.Append(landforms.InteriorMountainRuggedness);
            hasher.Append(landforms.InteriorMountainSeedOffset);
            hasher.Append(landforms.InteriorRollingScaleMeters);
            hasher.Append(landforms.InteriorRollingAmplitudeMeters);
            hasher.Append(landforms.InteriorReliefInsetMeters);
            hasher.Append(landforms.InteriorHillsMultiplier);
            hasher.Append(landforms.InteriorMountainPaintMinMask);
            hasher.Append(landforms.UseLegacyCoastalBlend ? 1 : 0);
            hasher.Append(landforms.FoothillSkirtMeters);
            hasher.Append(landforms.CrestSharpness);
            hasher.Append(landforms.SaddleDepth);
            hasher.Append(landforms.OrogenWarpFraction);
            hasher.Append(landforms.StructuralValleyDepthMeters);
            hasher.Append(landforms.MicroDetailAmplitudeMeters);
            hasher.Append(landforms.MaxOrogenCoverageFraction);
            hasher.Append(landforms.MinLowlandSlope12Fraction);
            hasher.Append(landforms.MinPassCountPerRange);
            hasher.Append(landforms.HighwayPassMaxSlopeDegrees);
            hasher.Append(landforms.PassCorridorHalfWidthMeters);
            hasher.Append(landforms.CityPadAlgorithmVersion);
            hasher.Append(landforms.CityPadFlatMarginMeters);
            hasher.Append(landforms.CityPadPlateauSlopeMeters);
            hasher.Append(landforms.CityPadFalloffMinMeters);
            hasher.Append(landforms.CityPadFalloffMaxMeters);
            hasher.Append(landforms.CityPadCornerRadiusFraction);
            hasher.Append(landforms.CityPadHydrologyPruneMarginMeters);
            hasher.Append(landforms.CityPadApproachSlopeDegrees);
            hasher.Append(landforms.CityPadMaxContinuityDegrees);
            hasher.Append(landforms.CityPadEdgeRelaxEnabled ? 1 : 0);
            hasher.Append(landforms.CityPadEdgeRelaxMaxIterations);
            hasher.Append(landforms.CityPadMinClearanceAboveSeaMeters);
            hasher.Append(landforms.CityPadMaxReclaimDepthMeters);
            AppendCurve(ref hasher, landforms.MountainMaskCurve);
        }

        public static void AppendRoads(ref StableHash64 hasher, RoadNetworkSettings roads)
        {
            if (roads == null) return;
            hasher.Append(roads.maxHighwayRoadSlopeDegrees);
            hasher.Append(roads.highwayTerrainShoulderMeters);
            hasher.Append(roads.highwayTerrainBlendMeters);
            hasher.Append(roads.highwayCoarseCorridorBlendMeters);
            hasher.Append(roads.highwayTerrainBedClearanceMeters);
            hasher.Append(roads.fastHighwayTerrainFollowSmoothingIterations);
            hasher.Append(roads.fastHighwayMaxCutFillMeters);
            hasher.Append(roads.fastHighwayMaxCutFillCeilingMeters);
            hasher.Append(roads.fastHighwaySteepGradeStartDegrees);
            hasher.Append(roads.fastHighwaySteepGradeFullDegrees);
            hasher.Append(roads.fastHighwaySteepFeatherExtraMeters);
            hasher.Append(roads.fastHighwayRoadBedShoulderMeters);
            hasher.Append(roads.fastHighwayRoadBedFeatherMeters);
            hasher.Append(roads.applyFinalHighwayRoadBedAlignment ? 1 : 0);
            hasher.Append(roads.terrainShoulderMeters);
            hasher.Append(roads.planInterCityHighwaysBeforePads ? 1 : 0);
            hasher.Append(roads.connectCitiesWithHighways ? 1 : 0);
            hasher.Append(roads.highwayExtraLoopChance);
            hasher.Append(roads.highwaySnowTunnelExtraLinks);
            hasher.Append(roads.highwaySnowTunnelMinPeakHeightMeters);
            hasher.Append(roads.highwayMinLinkDistanceMeters);
            hasher.Append(roads.maxConnectivityRerollAttemptsPerCity);
            hasher.Append(roads.enableTerrainValidatedInfrastructureFallback ? 1 : 0);
            hasher.Append(roads.infrastructureSpanTransitionMeters);
            hasher.Append(roads.infrastructureFallbackMaxSurfaceSlopeDegrees);
            hasher.Append(roads.enableMountainTunnels ? 1 : 0);
            hasher.Append(roads.tunnelEnterable ? 1 : 0);
            hasher.Append(roads.tunnelMinCoverMeters);
            hasher.Append(roads.tunnelMinPeakElevationAboveSeaMeters);
            hasher.Append(roads.tunnelRequireMajorOrogenCore ? 1 : 0);
            hasher.Append(roads.tunnelMinOrogenCoreMask);
            hasher.Append(roads.tunnelMinLengthMeters);
            hasher.Append(roads.tunnelMaxLengthMeters);
            hasher.Append(roads.tunnelPortalDaylightMeters);
            hasher.Append(roads.tunnelHoleApproachMeters);
            hasher.Append(roads.tunnelHoleShoulderMeters);
            hasher.Append(roads.tunnelPadExclusionMeters);
            hasher.Append(roads.tunnelChordMinPathToChordRatio);
            hasher.Append(roads.tunnelChordSampleStepMeters);
            hasher.Append(roads.tunnelChordAlgorithmVersion);
            hasher.Append(roads.roadSurfaceLiftMeters);
            hasher.Append(roads.rerouteRoadsWithTerrainPathfinding ? 1 : 0);
            hasher.Append(roads.pathfindingCellSizeMeters);
            hasher.Append(roads.highwayPathfindingCellSizeMeters);
            hasher.Append(roads.pathfindingMaxExpandedNodes);
            hasher.Append(roads.simplifySharpTurnsAfterPathfinding ? 1 : 0);
            hasher.Append(roads.cityExitUseDirectSpokes ? 1 : 0);
            hasher.Append(roads.fallbackToStraightPathWhenPathfindingFails ? 1 : 0);
            hasher.Append(roads.removePathfindingLoops ? 1 : 0);
            hasher.Append(roads.filterSteepRoadPointsAfterRefinement ? 1 : 0);
            hasher.Append(roads.pathfindingMarginMeters);
            // Hub Roads stage may skip pad/highway heightmap writes after ApplyCityPads.
            // v7: fail-loud highways + soft-cost A*.
            hasher.Append(7); // roadTerrainWritePolicyVersion
        }

        private static void AppendCurve(ref StableHash64 hasher, AnimationCurve curve)
        {
            if (curve == null)
            {
                hasher.Append(0);
                return;
            }

            var keys = curve.keys;
            hasher.Append(keys.Length);
            for (var i = 0; i < keys.Length; i++)
            {
                hasher.Append(keys[i].time);
                hasher.Append(keys[i].value);
                hasher.Append(keys[i].inTangent);
                hasher.Append(keys[i].outTangent);
            }
        }

        public static void AppendHydrology(ref StableHash64 hasher, HydrologyProfile hydrology)
        {
            if (hydrology == null) return;
            hasher.Append(hydrology.HydrologyAlgorithmVersion);
            hasher.Append(hydrology.WaterCrossingAlgorithmVersion);
            hasher.Append(hydrology.SeaLevelWorldY);
            hasher.Append(hydrology.CellSizeMeters);
            hasher.Append(hydrology.RiverFlowThreshold);
            hasher.Append(hydrology.MinimumRiverLengthMeters);
            hasher.Append(hydrology.RiverPlacementCoastBufferMeters);
            hasher.Append(hydrology.RiverPlacementObstacleBufferMeters);
            hasher.Append(hydrology.MaxRiverSystems);
            hasher.Append(hydrology.MaxVisibleRiverBranches);
            hasher.Append(hydrology.OutletClusterRadiusMeters);
            hasher.Append(hydrology.SecondarySystemScoreRatio);
            hasher.Append(hydrology.PrimaryOutletSeparationMeters);
            hasher.Append(hydrology.PrimaryRiverMinimumLengthMeters);
            hasher.Append(hydrology.TributaryMinimumLengthMeters);
            hasher.Append(hydrology.TributaryMinimumFlowFraction);
            hasher.Append(hydrology.MaxTributariesPerSystem);
            hasher.Append(hydrology.VisibleHeadwaterFlowThreshold);
            hasher.Append(hydrology.ValleyFlowPreference);
            hasher.Append(hydrology.CityWaterSetbackMeters);
            hasher.Append(hydrology.WaterfrontPreferenceDistanceMeters);
            hasher.Append(hydrology.MinRiverWidthMeters);
            hasher.Append(hydrology.MaxRiverWidthMeters);
            hasher.Append(hydrology.MinRiverDepthMeters);
            hasher.Append(hydrology.MaxRiverDepthMeters);
            hasher.Append(hydrology.LakeMinAreaMetersSq);
            hasher.Append(hydrology.LakeMinFillDepthMeters);
            hasher.Append(hydrology.LakeMinimumVolumeMetersCubed);
            hasher.Append(hydrology.LakeMaximumAreaFraction);
            hasher.Append(hydrology.LakeMaximumTotalAreaFraction);
            hasher.Append(hydrology.LakeMaximumAspectRatio);
            hasher.Append(hydrology.MaxVisibleLakes);
            hasher.Append(hydrology.LakeMinimumDepthMeters);
            hasher.Append(hydrology.LakeMinimumCoastDistanceMeters);
            hasher.Append(hydrology.RequireLakeRiverConnection ? 1 : 0);
            hasher.Append(hydrology.ShoreBlendWidthMeters);
            hasher.Append(hydrology.CarveShoulderWidthMeters);
            hasher.Append(hydrology.RiverCarveStrength);
            hasher.Append(hydrology.RiverMouthFlareMultiplier);
            hasher.Append(hydrology.RiverMouthFlareFraction);
            hasher.Append(hydrology.MeanderMaximumBankWidths);
            hasher.Append(hydrology.MeanderMinimumValleySlopeDegrees);
            hasher.Append(hydrology.MeanderWavelengthMeters);
            hasher.Append(hydrology.MaxRiverLandElevationAboveSeaMeters);
            hasher.Append(hydrology.MinLakeBedDepthBelowSea);
            hasher.Append(hydrology.MinRiverBedDepthBelowSea);
            hasher.Append(hydrology.FordMaxDepthMeters);
            hasher.Append(hydrology.FordMaxWidthMeters);
            hasher.Append(hydrology.FordMaxBankSlopeDegrees);
            hasher.Append(hydrology.CausewayMaxDepthMeters);
            hasher.Append(hydrology.CausewayMaxWidthMeters);
            hasher.Append(hydrology.BridgeCorridorMaxDepthMeters);
            hasher.Append(hydrology.WaterSoftCostPerMeterDepth);
            hasher.Append(hydrology.BridgeDeckClearanceMeters);
            hasher.Append(WorldWaterPlacementGate.DefaultMinDistanceToWaterMeters);
        }

        public static void AppendWater(ref StableHash64 hasher, WorldWaterProfile water)
        {
            if (water == null) return;
            hasher.Append(water.OceanRendererPrefab != null ? water.OceanRendererPrefab.name : string.Empty);
            hasher.Append(water.WaterBodyPrefab != null ? water.WaterBodyPrefab.name : string.Empty);
            hasher.Append(water.CrestOceanMaterial != null ? water.CrestOceanMaterial.name : string.Empty);
            hasher.Append(water.CrestLakeMaterial != null ? water.CrestLakeMaterial.name : string.Empty);
            hasher.Append(water.CrestLakeSeaLevelToleranceMeters);
            hasher.Append(water.SeaLevelWorldY);
            hasher.Append(water.EnableFoam ? 1 : 0);
            hasher.Append(water.FoamStrength);
            hasher.Append(water.FoamShoreWidthMeters);
            hasher.Append(water.RiverMinimumFlowSpeedMetersPerSecond);
            hasher.Append(water.RiverMaximumFlowSpeedMetersPerSecond);
            hasher.Append(water.RiverSplineRadius);
            hasher.Append(water.RiverSplineSubdivisions);
            hasher.Append(water.RiverHeightRadius);
            hasher.Append(water.RiverHeightSubdivisions);
            hasher.Append(water.RiverWaveResolution);
            hasher.Append(water.RiverWaveTurbulence);
            hasher.Append(water.LakeSplineRadius);
            hasher.Append(water.LakeSplineSubdivisions);
            hasher.Append(water.LakeHeightRadius);
            hasher.Append(water.LakeHeightSubdivisions);
            hasher.Append(water.LakeWaveResolution);
            hasher.Append(water.LakeWaveTurbulence);
            hasher.Append(water.PointWaveWeight);
            hasher.Append(water.PointFlowVelocity);
            hasher.Append(water.BankHeightTolerance);
            hasher.Append(water.MaximumEdgeError);
            hasher.Append(water.MinimumBedClearance);
            hasher.Append(water.RepairPassLimit);
            hasher.Append(water.MinPointSpacing);
            hasher.Append(water.MaxPointSpacing);
            hasher.Append(water.OuterSpectrum != null ? water.OuterSpectrum.name : string.Empty);
            hasher.Append(water.CoastalSpectrum != null ? water.CoastalSpectrum.name : string.Empty);
            hasher.Append(water.OuterWaveWeight);
            hasher.Append(water.OuterWindSpeedKph);
            hasher.Append(water.CoastalWaveWeight);
            hasher.Append(water.CoastalWindSpeedKph);
            hasher.Append(water.CoastalCalmPaddingMeters);
            hasher.Append(water.DepthCacheLayers.value);
            hasher.Append(water.SeaFloorGeometryLayers.value);
            hasher.Append(water.LakeSpectrum != null ? water.LakeSpectrum.name : string.Empty);
            hasher.Append(water.RiverSpectrum != null ? water.RiverSpectrum.name : string.Empty);
        }

        public static void AppendEnvironment(ref StableHash64 hasher, WorldEnvironmentProfile environment)
        {
            if (environment == null) return;
            hasher.Append(environment.StartingWeatherId);
            hasher.Append(environment.TransitionDurationSeconds);
            hasher.Append(environment.ChangeIntervalMinHours);
            hasher.Append(environment.ChangeIntervalMaxHours);
            hasher.Append(environment.BindGameplayCamera ? 1 : 0);
            hasher.Append(environment.EnableFog ? 1 : 0);
            hasher.Append(environment.EnableVolumetricFog ? 1 : 0);
            hasher.Append(environment.FogDensity);
            hasher.Append(environment.FogHeightFalloff);
            hasher.Append(environment.FogHeightWorldY);
            hasher.Append(environment.EnableVolumetricClouds ? 1 : 0);
            hasher.Append(environment.VolumetricCloudCoverage);
            hasher.Append(environment.VolumetricCloudDensity);
            hasher.Append(environment.EnableFlatClouds ? 1 : 0);
            hasher.Append(environment.EnableCirrusClouds ? 1 : 0);
            hasher.Append(environment.CirrusCloudAlpha);
            hasher.Append(environment.CirrusCloudCoverage);
            var weights = environment.WeatherWeights;
            if (weights == null) return;
            for (var i = 0; i < weights.Length; i++)
            {
                var entry = weights[i];
                if (entry == null) continue;
                hasher.Append(entry.WeatherId);
                hasher.Append(entry.Weight);
                hasher.Append(entry.BiomeOrRegionFilter);
            }
        }

        public static void AppendNature(ref StableHash64 hasher, WorldNatureProfile nature)
        {
            if (nature == null) return;
            var entries = nature.Entries;
            if (entries == null) return;
            hasher.Append(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;
                hasher.Append(entry.StableId);
                hasher.Append((int)entry.PlacementMode);
                hasher.Append(entry.ElevationRangeMeters.x);
                hasher.Append(entry.ElevationRangeMeters.y);
                hasher.Append(entry.RequireGrassSurface ? 1 : 0);
                hasher.Append(entry.MinGrassWeight);
                hasher.Append(entry.DensityPerKm2);
                hasher.Append(entry.MaxInstancesPerTile);
            }
        }
    }
}
