using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Procedural mesh road builder for city math polylines (asphalt, sidewalks, footpaths, driveways).
    /// </summary>
    public static partial class ProceduralCityRoadBuilder
    {
        public readonly struct BuildResult
        {
            public int StripCount { get; }
            public int JunctionCount { get; }
            public int JunctionsT { get; }
            public int JunctionsX { get; }
            public Transform NetworkRoot { get; }
            public IReadOnlyList<JunctionRecord> Junctions { get; }

            public BuildResult(
                int stripCount,
                int junctionCount,
                int junctionsT,
                int junctionsX,
                Transform networkRoot,
                IReadOnlyList<JunctionRecord> junctions)
            {
                StripCount = stripCount;
                JunctionCount = junctionCount;
                JunctionsT = junctionsT;
                JunctionsX = junctionsX;
                NetworkRoot = networkRoot;
                Junctions = junctions;
            }
        }

        public static BuildResult Build(
            Transform hubRoot,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            Func<Vector2, float> resolveHeight,
            ProceduralCityRoadBuildOptions options = default)
        {
            if (settings == null)
            {
                Debug.LogError("[ProceduralCityRoadBuilder] RoadNetworkSettings is required.");
                return default;
            }

            if (roads == null || roads.Count == 0)
            {
                Debug.LogWarning("[ProceduralCityRoadBuilder] No polylines to build.");
                return default;
            }

            Clear(hubRoot);
            var root = EnsureNetworkRoot(hubRoot);
            resolveHeight ??= _ => 0f;
            options = ApplyDefaultOptions(options);

            // Junction Detect runs in PublishGeneratedRoadNetwork; skip duplicate scan here.
            IReadOnlyList<JunctionRecord> junctions = Array.Empty<JunctionRecord>();

            var stripCount = 0;
            if (options.PlaceAsphalt)
            {
                var asphaltStats = BuildAsphaltStrips(root, roads, settings, resolveHeight, options);
                stripCount = asphaltStats.StripCount;
            }

            if (stripCount == 0 && options.PlaceAsphalt)
            {
                Debug.LogError(
                    "[ProceduralCityRoadBuilder] Built 0 strips from " + roads.Count +
                    " polylines. Check RoadNetworkSettings materials and console skip counts.",
                    root);
            }

            Debug.Log(
                "[ProceduralCityRoadBuilder] strips=" + stripCount +
                " polylines=" + roads.Count + ".",
                root);

            return new BuildResult(stripCount, 0, 0, 0, root, junctions);
        }

        public static void Clear(Transform hubRoot)
        {
            var existing = FindNetworkRoot(hubRoot);
            if (existing == null)
                return;

            for (var i = existing.childCount - 1; i >= 0; i--)
            {
                var child = existing.GetChild(i);
                if (child == null)
                    continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    continue;
                }
#endif
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        /// <summary>
        ///     Rebuild asphalt meshes only (keeps sidewalks/props). Used after tunnel mouths
        ///     snap highways so roads abut portals and skip the bore.
        /// </summary>
        public static int RebuildAsphalt(
            Transform hubRoot,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            Func<Vector2, float> resolveHeight,
            ProceduralCityRoadBuildOptions options = default)
        {
            if (hubRoot == null || settings == null || roads == null || roads.Count == 0)
                return 0;

            var root = EnsureNetworkRoot(hubRoot);
            var asphaltRoot = EnsureChildFolder(root, ProceduralRoadNetworkNames.Asphalt);
            ClearFolderChildren(asphaltRoot);
            resolveHeight ??= _ => 0f;
            options = ApplyDefaultOptions(options);
            options.PlaceAsphalt = true;
            var stats = BuildAsphaltStrips(root, roads, settings, resolveHeight, options);
            Debug.Log(
                "[ProceduralCityRoadBuilder] RebuildAsphalt strips=" + stats.StripCount +
                " polylines=" + roads.Count + ".",
                root);
            return stats.StripCount;
        }

        private static void ClearFolderChildren(Transform folder)
        {
            if (folder == null)
                return;
            for (var i = folder.childCount - 1; i >= 0; i--)
            {
                var child = folder.GetChild(i);
                if (child == null)
                    continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    continue;
                }
#endif
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        public static Transform EnsureNetworkRoot(Transform hubRoot)
        {
            var existing = FindNetworkRoot(hubRoot);
            if (existing != null)
            {
                NormalizeWorldSpaceRoot(existing);
                return existing;
            }

            var go = new GameObject(ProceduralRoadNetworkNames.NetworkRoot);
            if (hubRoot != null)
            {
                // Road strip meshes store world-space vertices. Keep the
                // content root at world identity so the builder transform is
                // not applied a second time when Unity renders the meshes.
                go.transform.SetParent(hubRoot, true);
                NormalizeWorldSpaceRoot(go.transform);
            }
            return go.transform;
        }

        private static void NormalizeWorldSpaceRoot(Transform root)
        {
            if (root == null)
                return;

            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;
        }

        public static Transform FindNetworkRoot(Transform hubRoot)
        {
            if (hubRoot == null)
                return null;

            return hubRoot.Find(ProceduralRoadNetworkNames.NetworkRoot);
        }

        public static void DestroyEmptyNetworkRoot(Transform hubRoot)
        {
            var existing = FindNetworkRoot(hubRoot);
            if (existing == null || existing.childCount > 0)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
                return;
            }
#endif
            UnityEngine.Object.Destroy(existing.gameObject);
        }

        private static ProceduralCityRoadBuildOptions ApplyDefaultOptions(ProceduralCityRoadBuildOptions options)
        {
            var overlap = options.JunctionOverlapMeters > 0f ? options.JunctionOverlapMeters : 0.15f;
            var placeAsphalt = options.PlaceAsphalt ||
                (!options.PlaceSidewalks && !options.PlaceDistrictFootpaths && !options.PlaceDriveways);

            return new ProceduralCityRoadBuildOptions
            {
                PlaceAsphalt = placeAsphalt,
                PlaceSidewalks = options.PlaceSidewalks,
                PlaceDistrictFootpaths = options.PlaceDistrictFootpaths,
                PlaceDriveways = options.PlaceDriveways,
                JunctionOverlapMeters = overlap,
                ClipRect = options.ClipRect,
                Accumulator = options.Accumulator
            };
        }
    }

    public struct ProceduralCityRoadBuildOptions
    {
        public bool PlaceAsphalt;
        public bool PlaceSidewalks;
        public bool PlaceDistrictFootpaths;
        public bool PlaceDriveways;
        public float JunctionOverlapMeters;
        public Rect? ClipRect;
        public ProceduralLayerMeshAccumulator Accumulator;
    }
}
