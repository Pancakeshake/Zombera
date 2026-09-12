using System;
using System.Collections.Generic;
using JBooth.MicroSplat;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {

        /// <summary>
        ///     Step 4: Lay terrain surfaces over district lots —
        ///     concrete pads, carparks, backyards, etc.
        ///     Requires district lots (Step 3) and roads (Step 1).
        /// </summary>
        [ContextMenu("Generate District Lot Terrain")]
        public void GenerateDistrictLotTerrain()
        {
            if (!ResolvedGenerateResidentialLots)
            {
                Debug.Log("[CityPrefabRoadNetworkBuilder] District lot terrain disabled — skipped.", this);
                return;
            }

            if (!TryGetDistrictTerrainInputs(out var container, out var entries, out var tac))
                return;

            var terrainWatch = System.Diagnostics.Stopwatch.StartNew();

#if UNITY_EDITOR
            // Strip leftover hardscape overlay quads from earlier Lot Terrain runs.
            StripHardscapeOverlayMeshes(container);
#endif

            // All terrain paint is clipped to the union of named areas (district
            // footprints). That keeps full concrete fills inside the city and
            // blocks spill onto wilderness MapMagic tiles outside sites.
            var cityBounds = CollectNamedAreaPaintBounds(container);
            if (cityBounds.width <= 0f || cityBounds.height <= 0f)
                cityBounds = ResolveCityPaintBounds();

            var ctx = new DistrictTerrainPaintContext
            {
                Entries = entries,
                Config = tac,
                CityBounds = cityBounds,
                UseSubZones = districtLotTerrainLayout != null,
                TerrainLayout = districtLotTerrainLayout
            };

            if (cityBounds.width > 0f && cityBounds.height > 0f)
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Lot Terrain clip bounds=" + cityBounds, this);
            else
                Debug.LogWarning(
                    "[CityPrefabRoadNetworkBuilder] Lot Terrain has no clip bounds — paint may spill.",
                    this);

            PaintAreaTerrains(container, districtLotTerrainConfig, ctx, out var paintedLots, out var subZoneCount);

            // Apply every queued zone per terrain in one alphamap read/write and
            // rebuild the painted tiles' MicroSplat control textures once.
            CityLotTerrainPainter.FlushPendingZonePaints();

            if (ctx.UseSubZones && subZoneCount > 0)
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] District lot terrain — " + subZoneCount +
                    " sub-zone(s) across " + paintedLots + " lot(s) in " +
                    terrainWatch.ElapsedMilliseconds + "ms.", this);
            else
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] District lot terrain — " + paintedLots +
                    " lot surface(s) in " + terrainWatch.ElapsedMilliseconds + "ms.", this);
        }

        private bool TryGetDistrictTerrainInputs(
            out Transform container, out List<TextureArrayConfig.TextureEntry> entries,
            out TextureArrayConfig tac)
        {
            container = null;
            entries = null;
            tac = null;

            var config = districtLotTerrainConfig;
            if (config == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No DistrictLotTerrainConfig assigned.", this);
                return false;
            }

            tac = config.textureArrayConfig;
            if (tac == null || tac.sourceTextures == null || tac.sourceTextures.Count == 0)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] TextureArrayConfig not assigned or empty.", this);
                return false;
            }

            container = transform.Find(AreasContainerName);
            if (container == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No named areas container — run Generate Named Areas first.", this);
                return false;
            }

            entries = tac.sourceTextures;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        ///     Destroys Lot_*_Driveway / BuildingPad / Footpath overlay quads left
        ///     from earlier Lot Terrain runs that spawned hardscape meshes.
        /// </summary>
        private static void StripHardscapeOverlayMeshes(Transform areasRoot)
        {
            if (areasRoot == null) return;

            var transforms = areasRoot.GetComponentsInChildren<Transform>(true);
            for (var i = transforms.Length - 1; i >= 0; i--)
            {
                var t = transforms[i];
                if (t == null || t == areasRoot) continue;
                var n = t.name;
                if (!n.Contains("_Driveway", System.StringComparison.Ordinal)
                    && !n.Contains("_BuildingPad", System.StringComparison.Ordinal)
                    && !n.Contains("_Footpath", System.StringComparison.Ordinal)
                    && !n.Contains("_CommercialWalkway", System.StringComparison.Ordinal)
                    && !n.Contains("_CommercialParking", System.StringComparison.Ordinal))
                    continue;
                if (t.GetComponent<MeshFilter>() == null) continue;
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }
#endif

        private struct DistrictTerrainPaintContext
        {
            public List<TextureArrayConfig.TextureEntry> Entries;
            public TextureArrayConfig Config;
            public Rect CityBounds;
            public bool UseSubZones;
            public DistrictLotTerrainLayout TerrainLayout;
        }

        /// <summary>
        ///     Union of hub-shifted named-area bounds — the true city footprint
        ///     for Lot Terrain clipping (not the expanded flatten ring).
        /// </summary>
        private static Rect CollectNamedAreaPaintBounds(Transform areasRoot)
        {
            var union = default(Rect);
            if (areasRoot == null)
                return union;

            for (var i = 0; i < areasRoot.childCount; i++)
            {
                var marker = areasRoot.GetChild(i).GetComponent<CityNamedAreaMarker>();
                if (marker == null)
                    continue;

                var b = marker.GetHubShiftedBoundsXZ();
                if (b.width <= 0f || b.height <= 0f)
                    continue;

                union = UnionRect(union, b);
            }

            return union;
        }

        private static void PaintAreaTerrains(
            Transform container, DistrictLotTerrainConfig config,
            DistrictTerrainPaintContext ctx, out int paintedLots, out int subZoneCount)
        {
            paintedLots = 0;
            subZoneCount = 0;
            for (var a = 0; a < container.childCount; a++)
                PaintAreaTerrain(container.GetChild(a), config, ctx, ref paintedLots, ref subZoneCount);
        }

        private static void PaintAreaTerrain(
            Transform area, DistrictLotTerrainConfig config,
            DistrictTerrainPaintContext ctx, ref int paintedLots, ref int subZoneCount)
        {
            var marker = area.GetComponent<CityNamedAreaMarker>();
            if (marker == null) return;

            var preset = config.GetPreset(marker.DistrictType);
            if (preset == null) return;

            var idx = preset.textureLayerIndex;
            if (idx < 0 || idx >= ctx.Entries.Count) return;

            // ── Paint the ground under this named area (full district fill) ──
            // Commercial / Industrial / CityCore / Hospital presets use concrete
            // layers — that is intentional inside named areas. ClipRect keeps
            // stamps inside the city (union of named areas). Park skipped.
            // NOTE: hub-shifted bounds — region mode moves areas to their site;
            // unshifted BoundsXZ paints at the template-city origin (stray strips).
            var dt = marker.DistrictType;
            var areaBounds = marker.GetHubShiftedBoundsXZ();
            if (dt != CityDistrictType.Park)
                CityLotTerrainPainter.PaintDistrictArea(
                    areaBounds, ctx.Config, idx, blendMeters: 0f, clipRect: ctx.CityBounds);

            // ── DistrictLots (residential/commercial/industrial areas) ──
            var lotsRoot = area.Find(CityPrefabResidentialLotBuilder.DistrictLotsContainerName)
                ?? area.Find("ResidentialLots");
            if (lotsRoot == null) return;

            var zoneLayout = ctx.UseSubZones ? ctx.TerrainLayout.GetLayout(marker.DistrictType) : null;
            // Hub-shifted like the lots — edge-touch detection against unshifted
            // block bounds misclassifies corner lots in region mode.
            var blockBounds = areaBounds;

            // Placed-building anchors so residential paint matches the real slab
            // and door position (Buildings step now runs before Lot Terrain).
            var placedAnchors = CollectPlacedBuildingAnchors(area);

            // Block-level commercial layout (U / L / strip): the paint comes from
            // the block plan — walkway along the street and parking in front of
            // the stores. Store lots sit inside those zones, so skip per-lot paint.
            var blockLayout = lotsRoot.GetComponent<CityCommercialBlockLayout>();
            if (blockLayout != null)
            {
                PaintCommercialBlockLayout(blockLayout, ctx, ref paintedLots, ref subZoneCount);
                return;
            }

            for (var l = 0; l < lotsRoot.childCount; l++)
            {
                // Industrial lots are straight concrete for now — skip sub-zone
                // subdivision and paint the whole lot with the resolved concrete
                // layer (the industrial preset index is Asphalt, not concrete).
                // Commercial lots derive walkway + parking zones from the layout
                // planner (strip / corner L) instead of the residential section
                // splitter; Single lots keep the whole-lot concrete fill.
                if (dt == CityDistrictType.Commercial)
                {
                    PaintCommercialLotTerrain(lotsRoot.GetChild(l), blockBounds, idx, ctx,
                        ref paintedLots, ref subZoneCount);
                }
                else if (dt == CityDistrictType.Industrial)
                {
                    var concreteLayer = CityLotTerrainPainter.ResolveLayerFromConfig(
                        ctx.Config, LotSurfaceType.Concrete);
                    PaintFullLot(lotsRoot.GetChild(l),
                        concreteLayer >= 0 ? concreteLayer : idx, ctx, ref paintedLots);
                }
                else
                {
                    PaintLotTerrain(lotsRoot.GetChild(l), zoneLayout, blockBounds, idx, ctx,
                        placedAnchors, ref paintedLots, ref subZoneCount);
                }
            }
        }

        private static void PaintLotTerrain(
            Transform lotTransform, LotTerrainZoneLayout zoneLayout, Rect blockBounds,
            int textureLayerIndex, DistrictTerrainPaintContext ctx,
            List<PlacedBuildingAnchor> placedAnchors,
            ref int paintedLots, ref int subZoneCount)
        {
            if (ctx.UseSubZones && zoneLayout != null)
                PaintLotTerrainSubZones(lotTransform, zoneLayout, blockBounds, textureLayerIndex, ctx,
                    placedAnchors, ref paintedLots, ref subZoneCount);
            else
                PaintFullLot(lotTransform, textureLayerIndex, ctx, ref paintedLots);
        }

        private static void PaintLotTerrainSubZones(
            Transform lotTransform, LotTerrainZoneLayout zoneLayout, Rect blockBounds,
            int textureLayerIndex, DistrictTerrainPaintContext ctx,
            List<PlacedBuildingAnchor> placedAnchors,
            ref int paintedLots, ref int subZoneCount)
        {
            var facing = lotTransform.GetComponent<CityLotFacingMarker>();
            var streetFace = facing != null ? facing.streetFace : BlockFace.South;

            var lotRect = CityDistrictLotPlacement.ExtractLotRect(lotTransform);
            if (lotRect.width < 4f || lotRect.height < 4f)
            {
                // Fall back to a full-lot paint for tiny lots.
                PaintFullLot(lotTransform, textureLayerIndex, ctx, ref paintedLots);
                return;
            }

            var building = FindBuildingAnchorForLot(lotRect, placedAnchors);
            var subZones = CityPrefabResidentialLotBuilder.SubdivideLotIntoSections(
                lotRect, streetFace, blockBounds, zoneLayout, building);

            if (subZones.Count > 0)
            {
                // Per-lot yard variation so neighbouring lawns look distinct.
                zoneLayout.ApplyYardTextureVariation(subZones, lotTransform.position);
                // Full-lot grass underlay so backyard / side gaps use the same
                // pick as the front yard instead of the district base green.
                zoneLayout.EnsureLotYardUnderlay(subZones, lotRect);

                // Paint sub-zones directly into terrain alphamaps,
                // clipped to the generated city bounds.
                // MicroSplat blends them seamlessly with the world terrain.
                CityLotTerrainPainter.PaintLotSubZones(subZones, ctx.Config, blendMeters: 0f, clipRect: ctx.CityBounds);

                subZoneCount += subZones.Count;
                paintedLots++;
            }
            else
            {
                // Subdivision produced nothing — paint the whole lot instead.
                PaintFullLot(lotTransform, textureLayerIndex, ctx, ref paintedLots);
            }
        }

        private static void PaintCommercialLotTerrain(
            Transform lotTransform, Rect blockBounds, int textureLayerIndex,
            DistrictTerrainPaintContext ctx, ref int paintedLots, ref int subZoneCount)
        {
            if (!ctx.UseSubZones)
            {
                PaintFullLot(lotTransform, textureLayerIndex, ctx, ref paintedLots);
                return;
            }

            var facing = lotTransform.GetComponent<CityLotFacingMarker>();
            var streetFace = facing != null ? facing.streetFace : BlockFace.South;
            var kind = facing != null ? facing.commercialKind : CommercialLotKind.Auto;

            var lotRect = CityDistrictLotPlacement.ExtractLotRect(lotTransform);
            if (lotRect.width < 4f || lotRect.height < 4f)
            {
                PaintFullLot(lotTransform, textureLayerIndex, ctx, ref paintedLots);
                return;
            }

            var commercialLayout = ctx.TerrainLayout != null
                ? ctx.TerrainLayout.GetLayout(CityDistrictType.Commercial)
                : null;
            var walkwayLayer = commercialLayout != null ? commercialLayout.footpathTextureIndex : -1;
            var parkingLayer = commercialLayout != null ? commercialLayout.drivewayTextureIndex : -1;
            var stripeLayer = commercialLayout != null ? commercialLayout.paintStripeTextureIndex : -1;

            var zones = CityCommercialLotLayoutPlanner.BuildTerrainZones(
                lotRect, blockBounds, streetFace, kind, walkwayLayer, parkingLayer, stripeLayer);
            if (zones.Count == 0)
            {
                PaintFullLot(lotTransform, textureLayerIndex, ctx, ref paintedLots);
                return;
            }

            // Mark commercial hardscape sharp before paint.
            for (var i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z.DisplayName is "CommercialWalkway" or "CommercialParking" or "ParkingStripe"
                    or "Driveway" or "BuildingPad" or "Footpath")
                {
                    z.Sharp = true;
                    zones[i] = z;
                }
            }

            CityLotTerrainPainter.PaintLotSubZones(zones, ctx.Config, blendMeters: 0f, clipRect: ctx.CityBounds);

            subZoneCount += zones.Count;
            paintedLots++;
        }

        // Parking-stripe alphamap bumps (1024/2048) were removed — raising every
        // city tile's resolution dominated Lot Terrain Get/SetAlphamap time.
        // Stripes stamp at the tile's native resolution (typically 512).

        private static void PaintFullLot(
            Transform lotTransform, int textureLayerIndex, DistrictTerrainPaintContext ctx,
            ref int paintedLots)
        {
            var lotRect = CityDistrictLotPlacement.ExtractLotRect(lotTransform);
            if (lotRect.width < 2f || lotRect.height < 2f)
                return;

            CityLotTerrainPainter.PaintDistrictArea(lotRect, ctx.Config, textureLayerIndex,
                blendMeters: 0f, clipRect: ctx.CityBounds, sharp: true);
            paintedLots++;
        }

    }
}
