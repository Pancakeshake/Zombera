using System;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Prefab references for the modular RoadKit used by the Road Tester generate path.
    /// </summary>
    [Serializable]
    public sealed class RoadKitPrefabs
    {
        public const string DefaultPrefabRoot = "Assets/02_Shared/Prefabs/Roads/RoadKit";
        public const string DefaultRoadAsphaltMaterialPath =
            "Assets/02_Shared/Meshes/Roads/RoadKit/Driveway/Materials/clean_asphalt_diff_2k.mat";
        public const string NetworkRootName = "RoadKitNetwork";

        public const float SegmentLengthMeters = 10f;
        public const float JunctionFootprintMeters = 14f;
        public const float EndCapLengthMeters = 2f;
        public const float KerbWidthMeters = 0.15f;
        public const float SidewalkWidthMeters = 1.5f;

        public const string DefaultFootpathConcreteMaterialPath =
            "Assets/02_Shared/Materials/Generic/Concrete/concrete_floor_worn_001.mat";

        public const string DefaultLotSidewalkConcreteMaterialPath =
            "Assets/02_Shared/Meshes/Roads/RoadKit/Sidewalk/Materials/fresh_concrete_67_46_basecolor_diffuse.mat";

        public const string DefaultPerimeterFootpathMaterialPath =
            "Assets/02_Shared/Materials/Generic/Concrete/concrete_tiles_02.mat";

        [Header("Materials")]
        [SerializeField] private Material _roadAsphaltMaterial;
        [SerializeField] private Material _footpathConcreteMaterial;

        [Header("Road")]
        public GameObject straight7m;
        public GameObject straight7mSidewalk;
        public GameObject straight8m;
        public GameObject endCap7m;
        public GameObject corner90;
        public GameObject tJunction;
        public GameObject xJunction;
        public GameObject widthTransition7to8;

        [Header("Footpath (outside road edge)")]
        public GameObject sidewalkStraight;
        public GameObject sidewalkCorner;
        public GameObject kerbStraight;

        public Material RoadAsphaltMaterial => _roadAsphaltMaterial;
        public Material FootpathConcreteMaterial => _footpathConcreteMaterial;

        public bool HasCoreRoadPieces =>
            straight7m != null &&
            endCap7m != null &&
            tJunction != null &&
            xJunction != null &&
            corner90 != null;

        public bool HasFootpathPieces => sidewalkStraight != null;

#if UNITY_EDITOR
        public void EnsureDefaultMaterials()
        {
            if (_roadAsphaltMaterial == null)
            {
                _roadAsphaltMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    DefaultRoadAsphaltMaterialPath);
            }

            if (_footpathConcreteMaterial == null)
            {
                _footpathConcreteMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    DefaultFootpathConcreteMaterialPath);
            }
        }
#endif

        public GameObject ResolveStraight(float widthMeters, bool withSidewalk)
        {
            if (widthMeters >= 7.5f && straight8m != null)
                return straight8m;
            if (withSidewalk && straight7mSidewalk != null)
                return straight7mSidewalk;
            return straight7m != null ? straight7m : straight7mSidewalk;
        }

        /// <summary>
        ///     Distance from road centerline to sidewalk slab center (half road + kerb + half sidewalk).
        /// </summary>
        public static float ResolveSidewalkCenterOffset(float roadWidthMeters) =>
            Mathf.Max(0.5f, roadWidthMeters) * 0.5f + KerbWidthMeters + SidewalkWidthMeters * 0.5f;

        /// <summary>
        ///     Distance from road centerline to kerb strip center.
        /// </summary>
        public static float ResolveKerbCenterOffset(float roadWidthMeters) =>
            Mathf.Max(0.5f, roadWidthMeters) * 0.5f + KerbWidthMeters * 0.5f;
    }
}
