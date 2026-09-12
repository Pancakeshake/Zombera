using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public sealed partial class WorldStateSnapshot
    {
        private void AddRegions(string path, List<RegionState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = EntityPath(path, record.id);
                Add(item + ".id", record.id);
                Add(item + ".sourceId", record.sourceId);
                Add(item + ".displayName", record.displayName);
                Add(item + ".generationSeed", record.generationSeed);
                Add(item + ".boundsXZ", record.boundsXZ);
            }
        }

        private void AddSettlements(string path, List<SettlementState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = EntityPath(path, record.id);
                Add(item + ".id", record.id);
                Add(item + ".sourceId", record.sourceId);
                Add(item + ".regionId", record.regionId);
                Add(item + ".displayName", record.displayName);
                Add(item + ".centerXZ", record.centerXZ);
                Add(item + ".halfExtentsMeters", record.halfExtentsMeters);
                Add(item + ".padHeightWorldY", record.padHeightWorldY);
                Add(item + ".buildabilityScore", record.buildabilityScore);
                Add(item + ".layoutSeed", record.layoutSeed);
            }
        }

        private void AddRoads(string path, List<RoadState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = EntityPath(path, record.id);
                Add(item + ".id", record.id);
                Add(item + ".sourceId", record.sourceId);
                Add(item + ".sourceKind", record.sourceKind.ToString());
                Add(item + ".regionId", record.regionId);
                Add(item + ".settlementId", record.settlementId);
                Add(item + ".sourceRoadId", record.sourceRoadId);
                Add(item + ".roadClass", record.roadClass.ToString());
                Add(item + ".widthMeters", record.widthMeters);
                Add(item + ".preserveWorldPath", record.preserveWorldPath);
                Add(item + ".curvedMarkers", record.curvedMarkers);
                AddVectorList(item + ".pointsXZ", record.pointsXZ);
            }
        }

        private void AddDistricts(string path, List<DistrictState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = EntityPath(path, record.id);
                Add(item + ".id", record.id);
                Add(item + ".sourceId", record.sourceId);
                Add(item + ".settlementId", record.settlementId);
                Add(item + ".sourceAreaId", record.sourceAreaId);
                Add(item + ".displayName", record.displayName);
                Add(item + ".clusterName", record.clusterName);
                Add(item + ".districtType", record.districtType.ToString());
                Add(item + ".gridX", record.gridX);
                Add(item + ".gridZ", record.gridZ);
                Add(item + ".boundsXZ", record.boundsXZ);
                Add(item + ".centerXZ", record.centerXZ);
                Add(item + ".groundWorldY", record.groundWorldY);
                Add(item + ".areaSquareMeters", record.areaSquareMeters);
                Add(item + ".roundedCorners", record.roundedCorners.ToString());
                Add(item + ".arterialCornerRadiusMeters", record.arterialCornerRadiusMeters);
                AddVectorList(item + ".outlineXZ", record.outlineXZ);
            }
        }

        private void AddLots(string path, List<LotState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = EntityPath(path, record.id);
                Add(item + ".id", record.id);
                Add(item + ".sourceId", record.sourceId);
                Add(item + ".districtId", record.districtId);
                Add(item + ".sourceIndex", record.sourceIndex);
                Add(item + ".boundsXZ", record.boundsXZ);
                AddVectorList(item + ".outlineXZ", record.outlineXZ);
                Add(item + ".groundWorldY", record.groundWorldY);
                Add(item + ".streetFace", record.streetFace.ToString());
                Add(item + ".commercialKind", record.commercialKind.ToString());
                Add(item + ".isCornerLot", record.isCornerLot);
                Add(item + ".isCurvedLot", record.isCurvedLot);
            }
        }

        private void AddPois(string path, List<PoiState> records)
        {
            Add(path + ".count", records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = EntityPath(path, record.id);
                Add(item + ".id", record.id);
                Add(item + ".sourceId", record.sourceId);
                Add(item + ".regionId", record.regionId);
                Add(item + ".archetypeId", record.archetypeId);
                Add(item + ".mapMarkerId", record.mapMarkerId);
                Add(item + ".positionXZ", record.positionXZ);
                Add(item + ".yawDegrees", record.yawDegrees);
                Add(item + ".footprintMeters", record.footprintMeters);
                Add(item + ".discovered", record.discovered);
                Add(item + ".depleted", record.depleted);
            }
        }
    }
}
