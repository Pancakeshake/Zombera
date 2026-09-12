using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Helpers for resolving tile centers without vendor types.</summary>
    public static class WorldTileInfoUtility
    {
        public const float DefaultCityAreaDominanceThreshold = 0.35f;

        public static bool TryGetLiveTerrain(WorldTileInfo tile, out Terrain terrain)
        {
            terrain = tile.Terrain;
            if (terrain == null)
                return false;

            try
            {
                if (terrain.terrainData == null)
                {
                    terrain = null;
                    return false;
                }
            }
            catch (MissingReferenceException)
            {
                terrain = null;
                return false;
            }

            return true;
        }

        public static bool TryGetTileCenterXZ(WorldTileInfo tile, out Vector2 centerXZ)
        {
            if (TryGetLiveTerrain(tile, out var terrain))
            {
                var pos = terrain.transform.position;
                var size = terrain.terrainData.size;
                centerXZ = new Vector2(pos.x + size.x * 0.5f, pos.z + size.z * 0.5f);
                return true;
            }

            var rect = tile.WorldRectXZ;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                centerXZ = default;
                return false;
            }

            centerXZ = rect.center;
            return true;
        }

        public static IWorldCityAreaClassifier FindCityAreaClassifier()
        {
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IWorldCityAreaClassifier classifier)
                    return classifier;
            }

            return null;
        }

        public static IWorldPinnedTileQuery FindPinnedTileQuery()
        {
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IWorldPinnedTileQuery query)
                    return query;
            }

            return null;
        }

        public static IWorldRoadPlanSource FindRoadPlanSource()
        {
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IWorldRoadPlanSource source)
                    return source;
            }

            return null;
        }

        public static IWorldSurfaceSyncBackend FindSurfaceSyncBackend()
        {
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IWorldSurfaceSyncBackend backend)
                    return backend;
            }

            return null;
        }

        public static bool TryGetPinnedTerrains(out System.Collections.Generic.List<Terrain> terrains)
        {
            terrains = null;
            var query = FindPinnedTileQuery();
            return query != null && query.TryGetPinnedTerrains(out terrains);
        }

        /// <summary>
        ///     Terrains parented under a scene <c>WorldTerrainGrid</c> root (procedural world builder).
        /// </summary>
        public static bool TryGetWorldTerrainGridTerrains(out System.Collections.Generic.List<Terrain> terrains)
        {
            terrains = new System.Collections.Generic.List<Terrain>(64);
            Transform gridRoot = null;
            var roots = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null || root.name != "WorldTerrainGrid")
                    continue;
                gridRoot = root;
                break;
            }

            if (gridRoot == null)
                return false;

            gridRoot.GetComponentsInChildren<Terrain>(true, terrains);
            return terrains.Count > 0;
        }

        public static bool TryGetTerrainBoundsUnderRoot(Transform root, out Rect boundsXZ)
        {
            boundsXZ = default;
            if (root == null)
                return false;

            var terrains = new System.Collections.Generic.List<Terrain>(64);
            root.GetComponentsInChildren<Terrain>(true, terrains);
            return TryUnionTerrainBounds(terrains, out boundsXZ);
        }

        /// <summary>
        ///     Exact XZ union of allocated <see cref="Terrain"/> tiles under
        ///     <c>WorldTerrainGrid</c> (transform position + terrain data size).
        /// </summary>
        public static bool TryGetWorldTerrainGridBounds(out Rect boundsXZ)
        {
            boundsXZ = default;
            if (!TryGetWorldTerrainGridTerrains(out var terrains))
                return false;

            return TryUnionTerrainBounds(terrains, out boundsXZ);
        }

        public static bool TryUnionTerrainBounds(
            System.Collections.Generic.IReadOnlyList<Terrain> terrains,
            out Rect boundsXZ)
        {
            boundsXZ = default;
            var found = false;

            for (var i = 0; i < terrains.Count; i++)
            {
                var terrain = terrains[i];
                if (terrain?.terrainData == null)
                    continue;

                var pos = terrain.transform.position;
                var size = terrain.terrainData.size;
                var tile = new Rect(pos.x, pos.z, size.x, size.z);
                boundsXZ = found ? UnionRect(boundsXZ, tile) : tile;
                found = true;
            }

            return found && boundsXZ.width > 0f && boundsXZ.height > 0f;
        }

        private static Rect UnionRect(Rect a, Rect b)
        {
            if (a.width <= 0f || a.height <= 0f)
                return b;
            if (b.width <= 0f || b.height <= 0f)
                return a;

            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        ///     Resolves terrains for city flattening: pinned MapMagic tiles first, then WorldTerrainGrid.
        /// </summary>
        public static bool TryResolveTerrainsOverlapping(
            Rect footprintXZ,
            out System.Collections.Generic.List<Terrain> terrains)
        {
            terrains = new System.Collections.Generic.List<Terrain>(8);
            if (footprintXZ.width <= 0f || footprintXZ.height <= 0f)
                return false;

            if (TryGetPinnedTerrains(out var pinned) && pinned != null)
                AppendOverlappingTerrains(pinned, footprintXZ, terrains);

            if (terrains.Count > 0)
                return true;

            if (TryGetWorldTerrainGridTerrains(out var gridTerrains))
                AppendOverlappingTerrains(gridTerrains, footprintXZ, terrains);

            if (terrains.Count > 0)
                return true;

            var active = Terrain.activeTerrains;
            if (active != null && active.Length > 0)
                AppendOverlappingTerrains(active, footprintXZ, terrains);

            return terrains.Count > 0;
        }

        /// <summary>
        ///     Like <see cref="TryResolveTerrainsOverlapping" /> but unions every overlapping
        ///     terrain source so long corridors (highways) can cut across tile boundaries.
        /// </summary>
        public static bool TryResolveAllTerrainsOverlapping(
            Rect footprintXZ,
            out System.Collections.Generic.List<Terrain> terrains)
        {
            terrains = new System.Collections.Generic.List<Terrain>(16);
            if (footprintXZ.width <= 0f || footprintXZ.height <= 0f)
                return false;

            if (TryGetPinnedTerrains(out var pinned) && pinned != null)
                AppendOverlappingTerrains(pinned, footprintXZ, terrains);

            if (TryGetWorldTerrainGridTerrains(out var gridTerrains))
                AppendOverlappingTerrains(gridTerrains, footprintXZ, terrains);

            var active = Terrain.activeTerrains;
            if (active != null && active.Length > 0)
                AppendOverlappingTerrains(active, footprintXZ, terrains);

            if (terrains.Count == 0)
            {
                var allTerrains = Object.FindObjectsByType<Terrain>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                if (allTerrains != null && allTerrains.Length > 0)
                    AppendOverlappingTerrains(allTerrains, footprintXZ, terrains);
            }

            return terrains.Count > 0;
        }

        private static void AppendOverlappingTerrains(
            System.Collections.Generic.IReadOnlyList<Terrain> candidates,
            Rect footprintXZ,
            System.Collections.Generic.List<Terrain> results)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var terrain = candidates[i];
                if (terrain?.terrainData == null)
                    continue;

                var pos = terrain.transform.position;
                var size = terrain.terrainData.size;
                var terrainRect = new Rect(pos.x, pos.z, size.x, size.z);
                if (!terrainRect.Overlaps(footprintXZ))
                    continue;

                if (!results.Contains(terrain))
                    results.Add(terrain);
            }
        }
    }
}
