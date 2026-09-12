using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Batches heightmap / alphamap writes and commits dirty Unity terrains.</summary>
    public sealed class WorldTerrainMutationService : IWorldTerrainMutation
    {
        private readonly HashSet<Terrain> _dirtyTerrains = new();
        private readonly Queue<WorldSurfacePaintCommand> _paintQueue = new();
        private IWorldSurfacePainter _surfacePainter;

        public void BindSurfacePainter(IWorldSurfacePainter painter) => _surfacePainter = painter;

        public void ApplyCityPads(IReadOnlyList<CityFlattenPad> pads)
        {
            if (pads == null || pads.Count == 0) return;

            for (var i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null) continue;
                FlattenPad(pad);
            }
        }

        private void FlattenPad(CityFlattenPad pad)
        {
            if (WorldTileInfoUtility.TryResolveTerrainsOverlapping(pad.PlateauBoundsXZ, out var terrains) &&
                terrains.Count > 0)
            {
                for (var i = 0; i < terrains.Count; i++)
                {
                    TerrainHeightFlattener.FlattenRect(
                        terrains[i],
                        pad.PlateauBoundsXZ,
                        pad.TargetHeightWorldY,
                        paddingMeters: 0f,
                        blendMeters: Mathf.Max(1f, pad.FalloffMeters));
                }

                for (var i = 0; i < terrains.Count; i++)
                    _dirtyTerrains.Add(terrains[i]);
                return;
            }

            var active = Terrain.activeTerrains;
            if (active == null || active.Length == 0)
            {
                MarkTerrainsIntersecting(pad.PlateauBoundsXZ);
                return;
            }

            FlattenPadOnTerrains(active, pad);
            MarkTerrainsIntersecting(pad.PlateauBoundsXZ);
        }

        private static void FlattenPadOnTerrains(Terrain[] terrains, CityFlattenPad pad)
        {
            for (var t = 0; t < terrains.Length; t++)
            {
                if (terrains[t] == null) continue;
                TerrainHeightFlattener.FlattenRect(
                    terrains[t],
                    pad.PlateauBoundsXZ,
                    pad.TargetHeightWorldY,
                    paddingMeters: 0f,
                    blendMeters: Mathf.Max(1f, pad.FalloffMeters));
            }
        }

        public void CarveHydrology(HydrologyPlan hydrology, WorldBuildScope scope)
        {
            if (hydrology == null) return;

            var bounds = scope.BoundsXZ;
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                bounds = new Rect(
                    hydrology.OriginXZ.x,
                    hydrology.OriginXZ.y,
                    hydrology.Width * hydrology.CellSizeMeters,
                    hydrology.Height * hydrology.CellSizeMeters);
            }

            MarkTerrainsIntersecting(bounds);
        }

        public void StampRoads(RoadNetworkRuntime roads, IReadOnlyList<WaterCrossing> crossings) =>
            StampRoads(roads, crossings, changedTerrains: null);

        public void StampRoads(
            RoadNetworkRuntime roads,
            IReadOnlyList<WaterCrossing> crossings,
            IReadOnlyList<Terrain> changedTerrains)
        {
            if (roads == null) return;

            if (changedTerrains != null)
            {
                for (var i = 0; i < changedTerrains.Count; i++)
                {
                    if (changedTerrains[i] != null)
                        _dirtyTerrains.Add(changedTerrains[i]);
                }
            }
            else
            {
                // Fallback: only mark terrains that intersect any road corridor.
                MarkTerrainsForRoads(roads.Roads);
            }

            // Height/alphamap skip for Bridge/Causeway spans is handled in
            // RoadTerrainStamper via the crossings list passed from StampInfrastructureTerrainStage.
            if (crossings != null && crossings.Count > 0)
            {
                Debug.Log(
                    "[WorldTerrainMutationService] StampRoads honor crossings=" + crossings.Count +
                    " (bridge/causeway span skip via RoadTerrainStamper).");
            }
        }

        private void MarkTerrainsForRoads(IReadOnlyList<RoadPolyline> roads)
        {
            if (roads == null) return;
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;
                MarkTerrainsIntersecting(road.BoundsXZ);
            }
        }

        public void QueueSurfacePaint(WorldSurfacePaintCommand command)
        {
            if (command == null) return;
            _paintQueue.Enqueue(command);
            if (command.Terrain != null)
                _dirtyTerrains.Add(command.Terrain);
        }

        public IEnumerator CommitDirtyTiles()
        {
            while (_paintQueue.Count > 0)
            {
                var command = _paintQueue.Dequeue();
                if (_surfacePainter == null || command?.Terrain == null) continue;
            }

            foreach (var terrain in _dirtyTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                terrain.terrainData.SyncHeightmap();
            }

            _dirtyTerrains.Clear();
            Physics.SyncTransforms();
            yield break;
        }

        private void MarkTerrainsIntersecting(Rect boundsXZ)
        {
            var terrains = Terrain.activeTerrains;
            if (terrains == null) return;

            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null) continue;
                var pos = terrain.transform.position;
                var size = terrain.terrainData != null ? terrain.terrainData.size : Vector3.one * 1000f;
                var terrainRect = new Rect(pos.x, pos.z, size.x, size.z);
                if (terrainRect.Overlaps(boundsXZ))
                    _dirtyTerrains.Add(terrain);
            }
        }
    }
}
