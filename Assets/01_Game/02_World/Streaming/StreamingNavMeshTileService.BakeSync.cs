using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World
{
    public sealed partial class StreamingNavMeshTileService
    {
        private TileBakeOutcome BakeTileDetailed(WorldTileInfo tile)
        {
            var bakeStartedAt = Time.realtimeSinceStartup;
            var sourceCount = 0;
            var agentTypeCount = 0;
            var coord = tile.Coord;

            var terrain = tile.Terrain;
            if (terrain == null || terrain.terrainData == null) return Complete(TileBakeOutcome.MissingTerrain);

            RemoveTile(coord);

            var planarBounds = BuildTileBounds(terrain, tile.WorldRectXZ);
            EnsureSceneObjectCachesFresh();

            var sources = BuildNavMeshSources(terrain, planarBounds);
            sourceCount = sources.Count;
            if (sourceCount == 0) return Complete(TileBakeOutcome.NoSources);

            var agentTypeIds = CollectAgentTypeIds();
            agentTypeCount = agentTypeIds.Count;
            var anyInstance = BuildAndRegisterInstances(coord, planarBounds, sources, agentTypeIds);
            if (anyInstance) RebindNearbyUnitsAfterWorldTileBaked(tile);
            return Complete(anyInstance ? TileBakeOutcome.Success : TileBakeOutcome.NoInstances);

            TileBakeOutcome Complete(TileBakeOutcome outcome)
            {
                var elapsedMs = (Time.realtimeSinceStartup - bakeStartedAt) * 1000f;
                MaybeLogSlowTileBake(coord, outcome, sourceCount, agentTypeCount, elapsedMs);
                return outcome;
            }
        }

        private Bounds BuildTileBounds(Terrain terrain, Rect worldRect)
        {
            var profile = ResolveNavMeshBakeProfile();
            var pad = Mathf.Max(0f, profile.NavMeshTileBoundsPadding);
            var verticalHalf = profile.NavMeshVerticalHalfExtent;
            return new Bounds(
                new Vector3(
                    worldRect.x + worldRect.width * 0.5f,
                    terrain.transform.position.y + terrain.terrainData.size.y * 0.5f,
                    worldRect.y + worldRect.height * 0.5f),
                new Vector3(
                    worldRect.width + pad * 2f,
                    Mathf.Max(terrain.terrainData.size.y + 8f, verticalHalf * 2f),
                    worldRect.height + pad * 2f));
        }

        /// <summary>
        /// Collects terrain (and optional tunnel floors) as walkable NavMesh sources.
        /// Water exclusion (hydrology DepthMeters / Crest volumes) is not applied here yet —
        /// relative lake/river beds keep DepthMeters gameplay-scale; do not deepen beds further
        /// without adding non-walkable water modifiers.
        /// </summary>
        private List<NavMeshBuildSource> BuildNavMeshSources(Terrain primaryTerrain, Bounds planarBounds)
        {
            var sources = _navMeshSourceBuffer;
            sources.Clear();

            var included = _includedTerrainBuffer;
            included.Clear();
            var skipInvalid = 0;
            var skipOutside = 0;

            if (useTileLocalTerrainSourcesOnly)
            {
                TryAddTerrainIntersectingBounds(primaryTerrain, planarBounds, sources, included, ref skipInvalid,
                    ref skipOutside);
                if (includeAllowlistedTunnelFloorSources)
                    TryAddAllowlistedTunnelFloors(planarBounds, sources);
                return sources;
            }

            for (var ti = 0; ti < _cachedTerrains.Length; ti++)
            {
                var t = _cachedTerrains[ti];
                if (t == null || t.terrainData == null) continue;

                if (_ownerScene.IsValid() && t.gameObject.scene != _ownerScene) continue;

                TryAddTerrainIntersectingBounds(t, planarBounds, sources, included, ref skipInvalid, ref skipOutside);
            }

            if (includeAllowlistedTunnelFloorSources)
                TryAddAllowlistedTunnelFloors(planarBounds, sources);

            return sources;
        }

        private readonly List<MeshCollider> _tunnelFloorScratch = new(32);

        private void TryAddAllowlistedTunnelFloors(Bounds planarBounds, List<NavMeshBuildSource> sources)
        {
            if (TunnelRuntimeRegistry.FloorCount == 0)
                return;

            var query = planarBounds;
            query.Expand(8f);
            _tunnelFloorScratch.Clear();
            TunnelRuntimeRegistry.CollectFloorsIntersecting(query, _tunnelFloorScratch);
            for (var i = 0; i < _tunnelFloorScratch.Count; i++)
            {
                var col = _tunnelFloorScratch[i];
                if (col == null || col.sharedMesh == null)
                    continue;
                if (!col.gameObject.activeInHierarchy)
                    continue;

                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    sourceObject = col.sharedMesh,
                    transform = col.transform.localToWorldMatrix,
                    area = 0
                });
            }
        }

        private HashSet<int> CollectAgentTypeIds()
        {
            var agentTypeIds = _agentTypeIdBuffer;
            agentTypeIds.Clear();

            if (usePrimaryAgentTypeOnly)
            {
                agentTypeIds.Add(primaryAgentTypeId);
                return agentTypeIds;
            }

            for (var ai = 0; ai < _cachedNavMeshAgents.Length; ai++)
            {
                var agent = _cachedNavMeshAgents[ai];
                if (agent == null) continue;

                if (_ownerScene.IsValid() && agent.gameObject.scene != _ownerScene) continue;

                agentTypeIds.Add(agent.agentTypeID);
            }

            if (agentTypeIds.Count == 0) agentTypeIds.Add(0);

            return agentTypeIds;
        }

        private bool BuildAndRegisterInstances(
            WorldTileCoord coord,
            Bounds planarBounds,
            List<NavMeshBuildSource> sources,
            HashSet<int> agentTypeIds)
        {
            var entry = new TileNavInstances();
            var anyInstance = false;

            foreach (var agentTypeId in agentTypeIds)
            {
                var settings = NavMesh.GetSettingsByID(agentTypeId);
                ApplyActiveNavMeshBuildSettings(ref settings);

                var buildStartedAt = Time.realtimeSinceStartup;
                var data = NavMeshBuilder.BuildNavMeshData(
                    settings, sources, planarBounds, Vector3.zero, Quaternion.identity);
                var buildElapsedMs = (Time.realtimeSinceStartup - buildStartedAt) * 1000f;
                MaybeLogSlowAgentBake(coord, agentTypeId, sources.Count, buildElapsedMs);

                if (data == null) continue;

                var instance = NavMesh.AddNavMeshData(data);
                if (instance.valid)
                {
                    entry.Datas.Add(data);
                    entry.Instances.Add(instance);
                    anyInstance = true;
                }
            }

            if (anyInstance) _tiles[coord] = entry;

            return anyInstance;
        }

        private static void TryAddTerrainIntersectingBounds(
            Terrain terrain,
            Bounds worldBounds,
            List<NavMeshBuildSource> sources,
            HashSet<Terrain> includedTerrains,
            ref int skippedInvalidTransforms,
            ref int skippedOutsideBounds)
        {
            if (terrain == null || terrain.terrainData == null) return;

            if (includedTerrains != null && includedTerrains.Contains(terrain)) return;

            var terrainTransform = terrain.transform;
            if (!IsFiniteTransform(terrainTransform))
            {
                skippedInvalidTransforms++;
                return;
            }

            var terrainBounds = TerrainBoundsWorld(terrain);
            if (!worldBounds.Intersects(terrainBounds))
            {
                skippedOutsideBounds++;
                return;
            }

            var terrainTransformMatrix = Matrix4x4.TRS(
                terrainTransform.position,
                terrainTransform.rotation,
                Vector3.one);

            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Terrain,
                sourceObject = terrain.terrainData,
                transform = terrainTransformMatrix,
                area = 0
            });

            includedTerrains?.Add(terrain);
        }

        private static Bounds TerrainBoundsWorld(Terrain terrain)
        {
            var size = terrain.terrainData.size;
            var center = terrain.transform.position + new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
            return new Bounds(center, size);
        }

        private static bool IsFiniteTransform(Transform transform)
        {
            if (transform == null) return false;

            var lossy = transform.lossyScale;
            return !(float.IsNaN(lossy.x) || float.IsInfinity(lossy.x) || Mathf.Approximately(lossy.x, 0f));
        }
    }
}
