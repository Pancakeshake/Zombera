using System;
using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class WorldStateCloner
    {
        public static WorldState Clone(WorldState source)
        {
            if (source == null)
                return null;

            return new WorldState
            {
                header = Clone(source.header),
                revision = source.revision,
                clock = Clone(source.clock),
                tiles = CloneList(source.tiles, record => Clone(record)),
                pendingEvents = CloneList(source.pendingEvents, record => Clone(record)),
                eventHistory = CloneList(source.eventHistory, record => Clone(record))
            };
        }

        public static WorldStateHeader Clone(WorldStateHeader source)
        {
            if (source == null)
                return null;

            return new WorldStateHeader
            {
                schemaVersion = source.schemaVersion,
                canonicalFormatVersion = source.canonicalFormatVersion,
                idAlgorithmVersion = source.idAlgorithmVersion,
                generatorId = source.generatorId ?? string.Empty,
                generatorVersion = source.generatorVersion,
                worldSeed = source.worldSeed,
                mapSizeTier = source.mapSizeTier,
                profileVersion = source.profileVersion,
                profileFingerprint = source.profileFingerprint ?? string.Empty,
                planFingerprint = source.planFingerprint ?? string.Empty,
                worldOriginXZ = source.worldOriginXZ,
                tilesPerSide = source.tilesPerSide,
                tileSizeMeters = source.tileSizeMeters,
                worldBoundsXZ = source.worldBoundsXZ
            };
        }

        public static WorldSimulationClockState Clone(WorldSimulationClockState source)
        {
            if (source == null)
                return null;

            return new WorldSimulationClockState
            {
                currentHour = source.currentHour,
                nextEventSequence = source.nextEventSequence
            };
        }

        public static WorldTilePartitionState Clone(WorldTilePartitionState source)
        {
            if (source == null)
                return null;

            return new WorldTilePartitionState
            {
                key = source.key,
                terrain = Clone(source.terrain),
                regions = CloneList(source.regions, record => Clone(record)),
                settlements = CloneList(source.settlements, record => Clone(record)),
                roads = CloneList(source.roads, record => Clone(record)),
                districts = CloneList(source.districts, record => Clone(record)),
                lots = CloneList(source.lots, record => Clone(record)),
                buildings = CloneList(source.buildings, record => Clone(record)),
                pois = CloneList(source.pois, record => Clone(record))
            };
        }

        public static RegionState Clone(RegionState source)
        {
            if (source == null)
                return null;

            return new RegionState
            {
                id = source.id,
                sourceId = source.sourceId ?? string.Empty,
                displayName = source.displayName ?? string.Empty,
                generationSeed = source.generationSeed,
                boundsXZ = source.boundsXZ
            };
        }

        public static SettlementState Clone(SettlementState source)
        {
            if (source == null)
                return null;

            return new SettlementState
            {
                id = source.id,
                sourceId = source.sourceId ?? string.Empty,
                regionId = source.regionId,
                displayName = source.displayName ?? string.Empty,
                centerXZ = source.centerXZ,
                halfExtentsMeters = source.halfExtentsMeters,
                padHeightWorldY = source.padHeightWorldY,
                buildabilityScore = source.buildabilityScore,
                layoutSeed = source.layoutSeed
            };
        }

        public static RoadState Clone(RoadState source)
        {
            if (source == null)
                return null;

            return new RoadState
            {
                id = source.id,
                sourceId = source.sourceId ?? string.Empty,
                sourceKind = source.sourceKind,
                regionId = source.regionId,
                settlementId = source.settlementId,
                sourceRoadId = source.sourceRoadId,
                roadClass = source.roadClass,
                widthMeters = source.widthMeters,
                preserveWorldPath = source.preserveWorldPath,
                curvedMarkers = source.curvedMarkers,
                pointsXZ = CopyList(source.pointsXZ)
            };
        }

        public static DistrictState Clone(DistrictState source)
        {
            if (source == null)
                return null;

            return new DistrictState
            {
                id = source.id,
                sourceId = source.sourceId ?? string.Empty,
                settlementId = source.settlementId,
                sourceAreaId = source.sourceAreaId,
                displayName = source.displayName ?? string.Empty,
                clusterName = source.clusterName ?? string.Empty,
                districtType = source.districtType,
                gridX = source.gridX,
                gridZ = source.gridZ,
                boundsXZ = source.boundsXZ,
                centerXZ = source.centerXZ,
                groundWorldY = source.groundWorldY,
                areaSquareMeters = source.areaSquareMeters,
                roundedCorners = source.roundedCorners,
                arterialCornerRadiusMeters = source.arterialCornerRadiusMeters,
                outlineXZ = CopyList(source.outlineXZ)
            };
        }

        public static LotState Clone(LotState source)
        {
            if (source == null)
                return null;

            return new LotState
            {
                id = source.id,
                sourceId = source.sourceId ?? string.Empty,
                districtId = source.districtId,
                sourceIndex = source.sourceIndex,
                boundsXZ = source.boundsXZ,
                outlineXZ = CopyList(source.outlineXZ),
                groundWorldY = source.groundWorldY,
                streetFace = source.streetFace,
                commercialKind = source.commercialKind,
                isCornerLot = source.isCornerLot,
                isCurvedLot = source.isCurvedLot
            };
        }

        public static BuildingState Clone(BuildingState source)
        {
            if (source == null)
                return null;

            return new BuildingState
            {
                id = source.id,
                sourceId = source.sourceId ?? string.Empty,
                settlementId = source.settlementId,
                districtId = source.districtId,
                lotId = source.lotId,
                archetypeId = source.archetypeId ?? string.Empty,
                typeId = source.typeId ?? string.Empty,
                districtType = source.districtType,
                position = source.position,
                rotation = source.rotation,
                scale = source.scale,
                footprintXZ = source.footprintXZ,
                streetFace = source.streetFace,
                hasDoorAnchor = source.hasDoorAnchor,
                doorAnchorWorld = source.doorAnchorWorld,
                condition01 = source.condition01,
                abandoned = source.abandoned,
                abandonedAtHour = source.abandonedAtHour,
                occupancy = Clone(source.occupancy),
                ownership = Clone(source.ownership),
                utilities = Clone(source.utilities),
                damage = Clone(source.damage),
                fire = Clone(source.fire),
                loot = Clone(source.loot),
                modifications = CloneList(source.modifications, record => Clone(record)),
                modules = CloneList(source.modules, record => Clone(record))
            };
        }

        public static PoiState Clone(PoiState source)
        {
            if (source == null)
                return null;

            return new PoiState
            {
                id = source.id,
                sourceId = source.sourceId ?? string.Empty,
                regionId = source.regionId,
                archetypeId = source.archetypeId ?? string.Empty,
                mapMarkerId = source.mapMarkerId ?? string.Empty,
                positionXZ = source.positionXZ,
                yawDegrees = source.yawDegrees,
                footprintMeters = source.footprintMeters,
                discovered = source.discovered,
                depleted = source.depleted
            };
        }

        public static TerrainChunkState Clone(TerrainChunkState source)
        {
            if (source == null)
                return null;

            return new TerrainChunkState
            {
                baseGeneratorId = source.baseGeneratorId ?? string.Empty,
                baseGeneratorVersion = source.baseGeneratorVersion,
                baseGenerationFingerprint = source.baseGenerationFingerprint ?? string.Empty,
                seaLevelWorldY = source.seaLevelWorldY,
                terrainBaseWorldY = source.terrainBaseWorldY,
                verticalSizeMeters = source.verticalSizeMeters,
                heightmapResolution = source.heightmapResolution,
                alphamapResolution = source.alphamapResolution,
                baseMapResolution = source.baseMapResolution,
                detailResolution = source.detailResolution,
                detailSamplesPerPatch = source.detailSamplesPerPatch,
                modifications = CloneList(source.modifications, record => Clone(record))
            };
        }

        public static TerrainModificationState Clone(TerrainModificationState source)
        {
            if (source == null)
                return null;

            return new TerrainModificationState
            {
                id = source.id,
                kind = source.kind,
                sourceEntityId = source.sourceEntityId,
                createdAtHour = source.createdAtHour,
                boundsXZ = source.boundsXZ,
                outlineXZ = CopyList(source.outlineXZ),
                intensity01 = source.intensity01,
                active = source.active
            };
        }

        public static WorldEventState Clone(WorldEventState source)
        {
            if (source == null)
                return null;

            return new WorldEventState
            {
                id = source.id,
                sequence = source.sequence,
                type = source.type,
                targetId = source.targetId,
                scheduledHour = source.scheduledHour,
                resolvedHour = source.resolvedHour,
                magnitude = source.magnitude,
                status = source.status,
                resultCode = source.resultCode ?? string.Empty
            };
        }

        public static BuildingOccupancyState Clone(BuildingOccupancyState source)
        {
            if (source == null) return null;
            return new BuildingOccupancyState { occupied = source.occupied, capacity = source.capacity, currentCount = source.currentCount };
        }

        public static BuildingOwnershipState Clone(BuildingOwnershipState source)
        {
            if (source == null) return null;
            return new BuildingOwnershipState { ownerTypeId = source.ownerTypeId ?? string.Empty, ownerId = source.ownerId ?? string.Empty };
        }

        public static BuildingUtilityState Clone(BuildingUtilityState source)
        {
            if (source == null) return null;
            return new BuildingUtilityState { power = source.power, water = source.water };
        }

        public static BuildingDamageState Clone(BuildingDamageState source)
        {
            if (source == null) return null;
            return new BuildingDamageState { structuralDamage01 = source.structuralDamage01, fireDamage01 = source.fireDamage01, destroyed = source.destroyed };
        }

        public static BuildingFireState Clone(BuildingFireState source)
        {
            if (source == null) return null;
            return new BuildingFireState { active = source.active, startedAtHour = source.startedAtHour, intensity01 = source.intensity01 };
        }

        public static BuildingLootState Clone(BuildingLootState source)
        {
            if (source == null)
                return null;

            return new BuildingLootState
            {
                generated = source.generated,
                generationSeed = source.generationSeed,
                looted = source.looted,
                items = CloneList(source.items, record => Clone(record))
            };
        }

        public static WorldItemStackState Clone(WorldItemStackState source)
        {
            if (source == null) return null;
            return new WorldItemStackState { itemId = source.itemId ?? string.Empty, quantity = source.quantity };
        }

        public static BuildingModificationState Clone(BuildingModificationState source)
        {
            if (source == null)
                return null;

            return new BuildingModificationState
            {
                modificationId = source.modificationId ?? string.Empty,
                typeId = source.typeId ?? string.Empty,
                slotId = source.slotId ?? string.Empty,
                enabled = source.enabled,
                localPosition = source.localPosition,
                localRotation = source.localRotation,
                localScale = source.localScale
            };
        }

        public static BuildingModuleState Clone(BuildingModuleState source)
        {
            if (source == null) return null;
            return new BuildingModuleState { moduleId = source.moduleId ?? string.Empty, typeId = source.typeId ?? string.Empty, enabled = source.enabled, condition01 = source.condition01 };
        }

        private static List<T> CloneList<T>(List<T> source, Func<T, T> clone)
        {
            var result = new List<T>(source != null ? source.Count : 0);
            if (source == null)
                return result;

            for (var i = 0; i < source.Count; i++)
                result.Add(clone(source[i]));
            return result;
        }

        private static List<T> CopyList<T>(List<T> source) =>
            source == null ? new List<T>() : new List<T>(source);
    }
}
