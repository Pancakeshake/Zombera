#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    internal static partial class CityPrefabResidentialLotBuilder
    {
        private static void PopulateResidentialLots(PopulateArgs p, LotPhaseTimings timings, ref int lotsCreated)
        {
            var roadMeshes = p.RoadMeshes;
            var groundY = p.LotsRoot.position.y + p.Config.FloorVisualHeight;

            var visualWatch = System.Diagnostics.Stopwatch.StartNew();
            var facingWatch = new System.Diagnostics.Stopwatch();

            for (var li = 0; li < p.Lots.Count; li++)
            {
                var lot = p.Lots[li];
                CreateLotFillVisual(lot, li, p.LotsRoot, p.Config);

                var lotVisual = p.LotsRoot.GetChild(p.LotsRoot.childCount - 1);
                var facing = lotVisual.gameObject.AddComponent<CityLotFacingMarker>();
                facing.isCornerLot = lot.IsCornerLot;
                facing.isCurvedLot = lot.IsCurvedLot;

                visualWatch.Stop();
                timings.VisualMs += visualWatch.ElapsedMilliseconds;
                facingWatch.Restart();

                if (lot.OverrideKind != CommercialLotKind.Auto)
                {
                    // Block-layout store lot — the layout pre-resolved the parking
                    // side; road-mesh facing would point stores at side streets.
                    facing.streetFace = lot.OverrideFace;
                    facing.commercialKind = lot.OverrideKind;
                }
                else
                {
                    facing.streetFace = CityBuildingRoadFacingUtility.ResolveLotStreetFaceFromRoadMeshes(
                        lot.Bounds, p.Block, p.StreetFrontX, groundY, roadMeshes, p.Config.Outline);

                    if (p.DistrictType == CityDistrictType.Commercial)
                        facing.commercialKind = ResolveCommercialLotKind(
                            lot.Bounds, p.Block, facing.streetFace, p.Config, p.Rng);
                }

                facingWatch.Stop();
                timings.FacingMs += facingWatch.ElapsedMilliseconds;
                visualWatch.Restart();

                if (p.ResolvedFence != null && p.Config.PlaceFences)
                    PlaceFencesForLot(lot, p.ResolvedFence, p.LotsRoot, p.Block,
                        p.Config.FloorVisualHeight, p.PlacedEdges, p.Config.Outline,
                        p.Config.FenceAllEdges);

                lotsCreated++;
            }

            visualWatch.Stop();
            timings.VisualMs += visualWatch.ElapsedMilliseconds;
        }

        /// <summary>
        ///     Picks the commercial styling variant at subdivision time and stores
        ///     it on the lot marker, so building placement and terrain painting
        ///     later agree without re-classifying the lot. Strip / court lots may
        ///     convert to gas stations; corner lots keep their L shape.
        /// </summary>
        private static CommercialLotKind ResolveCommercialLotKind(
            Rect lotRect, Rect blockBounds, BlockFace streetFace, Config config, System.Random rng)
        {
            var kind = CityCommercialLotLayoutPlanner.ClassifyType(lotRect, blockBounds, streetFace) switch
            {
                CommercialLayoutType.Strip => CommercialLotKind.Strip,
                CommercialLayoutType.Corner => CommercialLotKind.CornerL,
                CommercialLayoutType.Court => CommercialLotKind.CourtU,
                _ => CommercialLotKind.Auto
            };

            var gasEligible = kind is CommercialLotKind.Strip or CommercialLotKind.CourtU;
            if (gasEligible && config.GasStationChance > 0f &&
                CityCommercialLotLayoutPlanner.SupportsGasStation(lotRect, streetFace) &&
                rng.NextDouble() < config.GasStationChance)
                return CommercialLotKind.GasStation;

            return kind;
        }

        private static Shader _cachedLotShader;
        private static readonly Dictionary<int, Material> LotFillMaterials = new();

        /// <summary>
        ///     Shared material per (district colour, parity) — the old per-lot
        ///     Material + Shader.Find was the second-largest cost in the District
        ///     Lots step. Colours are identical within a pair, so sharing is safe.
        /// </summary>
        private static Material ResolveLotFillMaterial(Color baseColor, bool evenIndex)
        {
            if (_cachedLotShader == null)
                _cachedLotShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (baseColor.r < 0.01f && baseColor.g < 0.01f && baseColor.b < 0.01f)
                baseColor = new Color(0.35f, 0.72f, 1.0f); // fallback blue (Residential)

            var c32 = (Color32)baseColor;
            var key = (evenIndex ? 1 : 0) << 24 | c32.r << 16 | c32.g << 8 | c32.b;
            if (LotFillMaterials.TryGetValue(key, out var cached))
                return cached;

            var color = evenIndex
                ? new Color(baseColor.r * 0.85f, baseColor.g * 0.85f, baseColor.b * 0.85f, 0.42f)
                : new Color(baseColor.r, baseColor.g, baseColor.b, 0.42f);

            var material = new Material(_cachedLotShader) { color = color };
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3001;
            }

            LotFillMaterials[key] = material;
            return material;
        }

        private static Mesh BuildSimpleLotQuad(Vector3[] verts, int[] tris)
        {
            var mesh = new Mesh { name = "LotQuad" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateLotFillVisual(LotPlacement lot, int lotIndex, Transform parent, Config config)
        {
            if (!config.CreateEditorFloorVisuals) return;

            var visual = new GameObject("Lot_" + lotIndex.ToString("D2"));
            visual.transform.SetParent(parent, false);

            var lotRect = lot.Bounds;
            var lotCenter = lotRect.center;
            var hw = lotRect.width * 0.5f;
            var hd = lotRect.height * 0.5f;
            var verts = new Vector3[]
            {
                new(-hw, 0f, -hd),
                new( hw, 0f, -hd),
                new( hw, 0f,  hd),
                new(-hw, 0f,  hd)
            };
            var tris = new int[] { 0, 2, 1, 0, 3, 2 };
            // World-space UVs so terrain textures tile at 1 unit per meter.
            var uvs = new Vector2[]
            {
                new(0f, 0f),
                new(lotRect.width, 0f),
                new(lotRect.width, lotRect.height),
                new(0f, lotRect.height)
            };

            Mesh mesh;
            if (lot.ClippedOutline != null && lot.ClippedOutline.Count >= 3)
            {
                // Curved / corner lot: convert world-space clip to local verts around lotCenter.
                var clipCount = lot.ClippedOutline.Count;
                var clipVerts = new Vector3[clipCount];
                var clipUvs = new Vector2[clipCount];
                for (var v = 0; v < clipCount; v++)
                {
                    var wx = lot.ClippedOutline[v].x;
                    var wy = lot.ClippedOutline[v].y;
                    clipVerts[v] = new Vector3(wx - lotCenter.x, 0.06f, wy - lotCenter.y);
                    // UVs in world space so tiling is consistent with simple quads.
                    clipUvs[v] = new Vector2(wx - lotRect.xMin, wy - lotRect.yMin);
                }

                mesh = new Mesh { name = "LotClipped" };
                mesh.vertices = clipVerts;
                mesh.uv = clipUvs;
                var clipTris = new int[(clipCount - 2) * 3];
                for (var t = 0; t < clipCount - 2; t++)
                {
                    clipTris[t * 3] = 0;
                    clipTris[t * 3 + 1] = t + 1;
                    clipTris[t * 3 + 2] = t + 2;
                }
                mesh.triangles = clipTris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
            else
            {
                mesh = BuildSimpleLotQuad(verts, tris);
                mesh.uv = uvs;
            }

            var material = ResolveLotFillMaterial(config.DistrictColor, lotIndex % 2 == 0);

            visual.transform.position = new Vector3(lotCenter.x, parent.position.y + config.FloorVisualHeight + 0.06f, lotCenter.y);

            var mf = visual.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
        }
    }
}
#endif
