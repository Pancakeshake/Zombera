using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Validates the flat river/lake collections as one connected drainage graph.</summary>
    public static class HydrologyTopologyValidator
    {
        public static bool Validate(
            RiverPolyline[] rivers,
            LakeRecord[] lakes,
            out string error)
        {
            error = string.Empty;
            var riverIds = new HashSet<ulong>();
            var lakeIds = new HashSet<ulong>();
            if (lakes != null)
            {
                for (var i = 0; i < lakes.Length; i++)
                {
                    var lake = lakes[i];
                    if (lake == null || lake.StableId == 0 || !lakeIds.Add(lake.StableId))
                    {
                        error = "Lake stable IDs must be non-zero and unique.";
                        return false;
                    }
                    if (lake.OutlineXZ == null || lake.OutlineXZ.Length < 4)
                    {
                        error = "Selected lake is missing a closed outline.";
                        return false;
                    }
                }
            }

            var riverById = new Dictionary<ulong, RiverPolyline>();
            if (rivers == null) return true;
            for (var i = 0; i < rivers.Length; i++)
            {
                var river = rivers[i];
                if (river == null || river.StableId == 0 || !riverIds.Add(river.StableId))
                {
                    error = "River stable IDs must be non-zero and unique.";
                    return false;
                }
                if (river.PointsXZ == null || river.PointsXZ.Length < 2)
                {
                    error = "River reach must contain at least two points.";
                    return false;
                }
                if (river.Kind == RiverKind.MainStem && !river.HasOceanMouth)
                {
                    error = "Every main stem must terminate at an ocean mouth.";
                    return false;
                }
                riverById.Add(river.StableId, river);
            }

            for (var i = 0; i < rivers.Length; i++)
            {
                var river = rivers[i];
                if (river.ParentRiverStableId != 0 && !riverById.ContainsKey(river.ParentRiverStableId))
                {
                    error = "Tributary parent reach is missing.";
                    return false;
                }
                if ((river.SourceLakeStableId != 0 && !lakeIds.Contains(river.SourceLakeStableId)) ||
                    (river.JoinLakeStableId != 0 && !lakeIds.Contains(river.JoinLakeStableId)))
                {
                    error = "River references a missing lake.";
                    return false;
                }
            }
            return true;
        }
    }
}
