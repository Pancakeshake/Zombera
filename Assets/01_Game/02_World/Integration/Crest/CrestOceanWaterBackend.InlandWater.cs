using UnityEngine;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Inland Crest water ownership. Generated rivers and lakes are direct
    /// <c>WaterBodies/River_&lt;id&gt;</c> and <c>WaterBodies/Lake_&lt;id&gt;</c> spline roots: no inland
    /// <c>WaterBody</c> wrapper, clip-mesh geometry or <c>RiverSystem</c>/<c>Centerlines</c> hierarchy is
    /// created. Ocean-edge WaterBodies remain owned by <see cref="CrestOceanWaterBackend"/>.
    /// </summary>
    public sealed partial class CrestOceanWaterBackend
    {
        /// <summary>Shared authored-style parent for direct inland Crest spline roots.</summary>
        public const string InlandWaterBodiesFolderName = "WaterBodies";

        /// <summary>Folder names used by the retired split river/lake topology.</summary>
        public const string LegacyLakesFolderName = "Lakes";
        public const string LegacyRiversFolderName = "Rivers";

        /// <summary>Shared parent folder for generated inland Crest spline roots.</summary>
        public Transform EnsureInlandWaterFolder() => EnsureChildFolder(InlandWaterBodiesFolderName);

        /// <summary>
        /// Removes generated inland spline roots, including anything left by the retired split
        /// folder topology, so a rebuild can never leave duplicate roots behind.
        /// </summary>
        public void ClearInlandWater()
        {
            ClearChildFolder(InlandWaterBodiesFolderName);
            ClearChildFolder(LegacyLakesFolderName);
            ClearChildFolder(LegacyRiversFolderName);
        }
    }
}
