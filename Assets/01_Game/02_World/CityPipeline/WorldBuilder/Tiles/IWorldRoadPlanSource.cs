using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Vendor-agnostic source of planned road polylines for a world tile.</summary>
    public interface IWorldRoadPlanSource
    {
        /// <summary>
        ///     Extracts road polylines covering <paramref name="worldRectXZ"/>.
        ///     Returns false when the source has no usable plan for the rect.
        /// </summary>
        bool TryGetRoadPlan(Rect worldRectXZ, List<Vector3> polylineScratch, List<IReadOnlyList<Vector3>> roadsOut);
    }
}
