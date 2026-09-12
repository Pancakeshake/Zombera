#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {

        /// <summary>
        /// Places roof tiles: a cap on the top floor, plus step-back roofs at each level
        /// where the footprint shrinks, filling the exposed ring between the larger lower
        /// floor walls and the smaller upper floor.
        /// </summary>
        private static void PlaceSkyscraperRoofs(GenerationContext context, Transform roofParent)
        {
            var config = context.ActiveRoofType;
            if (config != null)
            {
                DispatchConfigRoof(context, roofParent, config);
                return;
            }

            PlaceFallbackRoof(context, roofParent);
        }

        private static void DispatchConfigRoof(GenerationContext context, Transform roofParent, RoofTypeConfig config)
        {
            var plan = context.Plan;
            switch (config.Style)
            {
                case RoofStyle.Gable:
                    PlaceKitRoofCapFromConfig(roofParent, config, plan);
                    if (plan.SkyscraperMode)
                        PlaceKitStepBackRoofsFromConfig(roofParent, config, plan);
                    break;
                case RoofStyle.Flat:
                    if (config.TilePrefab != null)
                    {
                        PlaceFlatRoofCap(roofParent, config, plan);
                        if (plan.SkyscraperMode)
                            PlaceFlatStepBackRoofs(roofParent, config, plan);
                    }
                    break;
                case RoofStyle.Shed:
                    PlaceShedRoof(roofParent, config, plan, context.Prefabs);
                    break;
                case RoofStyle.Saltbox:
                    PlaceSaltboxRoof(roofParent, config, plan);
                    break;
                case RoofStyle.Gambrel:
                    PlaceGambrelRoof(roofParent, config, plan);
                    break;
            }
        }

        private static void PlaceFallbackRoof(GenerationContext context, Transform roofParent)
        {
            var plan = context.Plan;
            var prefs = context.Prefabs;
            if (prefs.RoofRidge != null || prefs.RoofPanel != null)
            {
                PlaceKitRoofCap(roofParent, prefs, plan);
                if (plan.SkyscraperMode)
                    PlaceKitStepBackRoofs(roofParent, prefs, plan);
            }
            else if (prefs.Roof != null)
            {
                PlaceLegacyRoofCap(roofParent, prefs.Roof, plan);
                if (plan.SkyscraperMode)
                    PlaceLegacyStepBackRoofs(roofParent, prefs.Roof, plan);
            }
        }

        private static void PlaceLegacyRoofCap(Transform roofParent, GameObject roofPrefab, GenerationPlan plan)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
            for (var ix = 0; ix < w; ix++)
                for (var iz = 0; iz < d; iz++)
                    PlaceRoofTile(roofPrefab, roofParent, $"Roof_Top_{ix}_{iz}", ix * CellSize + off.x, iz * CellSize + off.z, wallTopY);
        }

        private static void PlaceLegacyStepBackRoofs(Transform roofParent, GameObject roofPrefab, GenerationPlan plan)
        {
            for (var level = 0; level < plan.FloorCount - 1; level++)
            {
                var lw = plan.FloorWidths[level]; var ld = plan.FloorDepths[level];
                var uw = plan.FloorWidths[level + 1]; var ud = plan.FloorDepths[level + 1];
                if (lw == uw && ld == ud) continue;
                var ux = (lw - uw) / 2; var uz = (ld - ud) / 2;
                var y = LevelFloorSurfaceY(level) + WallHeight;
                for (var ix = 0; ix < lw; ix++)
                    for (var iz = 0; iz < ld; iz++)
                    {
                        if (ix >= ux && ix < ux + uw && iz >= uz && iz < uz + ud) continue;
                        PlaceRoofTile(roofPrefab, roofParent, $"Roof_Step_L{level}_{ix}_{iz}",
                            ix * CellSize + plan.FloorOffsets[level].x, iz * CellSize + plan.FloorOffsets[level].z, y);
                    }
            }
        }

        // ── Kit-of-parts roof (Ridge + Panel + Gable, scaled to footprint) ──

        private static void PlaceKitRoofCap(Transform roofParent, PrefabDependencies prefs, GenerationPlan plan)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
            var doorOnXWall = IsDoorOnXWall(plan, topLevel);
            PlaceScaledRoofAssembly(roofParent, prefs, "Top", w, d, off, wallTopY, doorOnXWall);
        }

        private static void PlaceKitStepBackRoofs(Transform roofParent, PrefabDependencies prefs, GenerationPlan plan)
        {
            for (var level = 0; level < plan.FloorCount - 1; level++)
            {
                var lw = plan.FloorWidths[level]; var ld = plan.FloorDepths[level];
                var uw = plan.FloorWidths[level + 1]; var ud = plan.FloorDepths[level + 1];
                if (lw == uw && ld == ud) continue;
                var ux = (lw - uw) / 2; var uz = (ld - ud) / 2;

                var haveNorth = uz > 0;
                var haveSouth = uz + ud < ld;
                var haveWest  = ux > 0;
                var haveEast  = ux + uw < lw;

                var stepY = LevelFloorSurfaceY(level) + WallHeight;
                var lowerOff = plan.FloorOffsets[level];
                var doorOnXWall = IsDoorOnXWall(plan, level);

                if (haveNorth)
                    PlaceScaledRoofStrip(roofParent, prefs,
                        new RoofStripInfo($"StepN_L{level}", new Vector2Int(ux, 0), new Vector2Int(uw, uz)),
                        lowerOff, stepY, doorOnXWall);
                if (haveSouth)
                    PlaceScaledRoofStrip(roofParent, prefs,
                        new RoofStripInfo($"StepS_L{level}", new Vector2Int(ux, uz + ud), new Vector2Int(uw, ld - uz - ud)),
                        lowerOff, stepY, doorOnXWall);
                if (haveWest)
                    PlaceScaledRoofStrip(roofParent, prefs,
                        new RoofStripInfo($"StepW_L{level}", new Vector2Int(0, uz), new Vector2Int(ux, ud)),
                        lowerOff, stepY, doorOnXWall);
                if (haveEast)
                    PlaceScaledRoofStrip(roofParent, prefs,
                        new RoofStripInfo($"StepE_L{level}", new Vector2Int(ux + uw, uz), new Vector2Int(lw - ux - uw, ud)),
                        lowerOff, stepY, doorOnXWall);
            }
        }

        private readonly struct RoofStripInfo
        {
            public readonly string Label;
            public readonly Vector2Int Start, Size;
            public RoofStripInfo(string label, Vector2Int start, Vector2Int size)
            { Label = label; Start = start; Size = size; }
        }

        private static void PlaceScaledRoofStrip(Transform roofParent, RoofTypeConfig config,
            in RoofStripInfo strip, Vector3 baseOffset, float wallTopY, bool doorOnXWall)
        {
            if (strip.Size.x <= 0 || strip.Size.y <= 0) return;
            var off = new Vector3(baseOffset.x + strip.Start.x * CellSize, 0f, baseOffset.z + strip.Start.y * CellSize);
            PlaceScaledRoofAssembly(roofParent, config, strip.Label, strip.Size.x, strip.Size.y, off, wallTopY, doorOnXWall);
        }

        private static void PlaceScaledRoofStrip(Transform roofParent, PrefabDependencies prefs,
            in RoofStripInfo strip, Vector3 baseOffset, float wallTopY, bool doorOnXWall)
        {
            if (strip.Size.x <= 0 || strip.Size.y <= 0) return;
            var off = new Vector3(baseOffset.x + strip.Start.x * CellSize, 0f, baseOffset.z + strip.Start.y * CellSize);
            PlaceScaledRoofAssembly(roofParent, prefs, strip.Label, strip.Size.x, strip.Size.y, off, wallTopY, doorOnXWall);
        }

        private static void PlaceScaledRoofAssembly(Transform roofParent, RoofTypeConfig config, string label,
            int w, int d, Vector3 off, float wallTopY, bool doorOnXWall)
        {
            var buildW = w * CellSize;
            var buildD = d * CellSize;
            var halfW = buildW * 0.5f;
            var halfCell = CellSize * 0.5f;
            var cx = off.x + halfW - halfCell;
            var cz = off.z + buildD * 0.5f - halfCell;

            var peakH = WallHeight * config.PeakHeightMultiplier;
            var slopeHalfSpan = doorOnXWall ? buildD * 0.5f : halfW;
            // Ridge/panel meshes are long along their LOCAL Z — for door-on-X the
            // ridge runs along world X, so the piece is rotated 90° and the long
            // scale lands on Z while X stays thickness.
            var ridgeScaleZ   = doorOnXWall ? w + config.RidgeZOverhang           : d + config.RidgeZOverhang;
            var ridgeScaleX   = 1f;
            var diag          = Mathf.Sqrt(slopeHalfSpan * slopeHalfSpan + peakH * peakH);
            var roofAngleDeg  = Mathf.Atan(slopeHalfSpan / peakH) * Mathf.Rad2Deg;

            var gc = new GableAsmCtx
            {
                RoofParent = roofParent, Label = label, Cx = cx, Cz = cz,
                BuildW = buildW, BuildD = buildD, HalfW = halfW, HalfD = buildD * 0.5f,
                PeakH = peakH, WallTopY = wallTopY, RoofAngleDeg = roofAngleDeg,
                RidgeScaleX = ridgeScaleX, RidgeScaleZ = ridgeScaleZ,
                RidgeYScale = config.RidgeYScale, RidgeYOff = config.RidgeYOffset,
                RidgeAlongX = doorOnXWall,
                PanelXScale = (doorOnXWall ? d : w) * config.PanelWidthMultiplier,
                PanelYScale = diag / config.PanelDiagonalNorm,
                PanelZScale = (doorOnXWall ? w : d) + config.PanelZOverhang,
                PanelXOff = config.PanelXOffset, PanelYOff = config.PanelYOffset,
                PanelXPosMul = config.PanelXPositionMultiplier, PanelYPosMul = config.PanelYPositionMultiplier,
                GableXScale = (doorOnXWall ? d : w) * config.GableXSnugness,
                GableYScale = peakH / config.GableYBaseHeight,
                GableYPosMul = config.GableYPositionMultiplier,
                RidgePrefab = config.RidgePrefab, PanelPrefab = config.PanelPrefab, GablePrefab = config.GablePrefab
            };

            if (gc.RidgePrefab != null) PlaceGableRidge(in gc);
            if (doorOnXWall)
                PlaceGableDoorX(in gc);
            else
                PlaceGableDoorZ(in gc);
        }

        private static void PlaceScaledRoofAssembly(Transform roofParent, PrefabDependencies prefs, string label,
            int w, int d, Vector3 off, float wallTopY, bool doorOnXWall)
        {
            var buildW = w * CellSize;
            var buildD = d * CellSize;
            var halfW = buildW * 0.5f;
            var halfCell = CellSize * 0.5f;
            var cx = off.x + halfW - halfCell;
            var cz = off.z + buildD * 0.5f - halfCell;

            var peakH = WallHeight;
            var slopeHalfSpan = doorOnXWall ? buildD * 0.5f : halfW;
            // Ridge/panel meshes are long along their LOCAL Z — for door-on-X the
            // ridge runs along world X, so the piece is rotated 90° and the long
            // scale lands on Z while X stays thickness.
            var ridgeScaleZ   = doorOnXWall ? w + 0.11f      : d + 0.11f;
            var ridgeScaleX   = 1f;
            var diag          = Mathf.Sqrt(slopeHalfSpan * slopeHalfSpan + peakH * peakH);
            var roofAngleDeg  = Mathf.Atan(slopeHalfSpan / peakH) * Mathf.Rad2Deg;
            var panelXScale = (doorOnXWall ? d : w) * 0.5f;
            var panelYScale = diag / 2.2f;
            var panelZScale = (doorOnXWall ? w : d) + 0.1f;
            var gableSpanCells = doorOnXWall ? d : w;
            var gableXScale    = gableSpanCells * 0.98f;
            var gableYScale = peakH / 1.5f;

            var gc = new GableAsmCtx
            {
                RoofParent = roofParent, Label = label, Cx = cx, Cz = cz,
                BuildW = buildW, BuildD = buildD, HalfW = halfW, HalfD = buildD * 0.5f,
                PeakH = peakH, WallTopY = wallTopY, RoofAngleDeg = roofAngleDeg,
                RidgeScaleX = ridgeScaleX, RidgeScaleZ = ridgeScaleZ, RidgeYScale = 2f, RidgeYOff = 0.04f,
                RidgeAlongX = doorOnXWall,
                PanelXScale = panelXScale, PanelYScale = panelYScale, PanelZScale = panelZScale,
                PanelXOff = 0.07f, PanelYOff = 0.04f, PanelXPosMul = 0.5f, PanelYPosMul = 0.5f,
                GableXScale = gableXScale, GableYScale = gableYScale, GableYPosMul = 0.5f,
                RidgePrefab = prefs.RoofRidge, PanelPrefab = prefs.RoofPanel, GablePrefab = prefs.RoofGable
            };

            if (gc.RidgePrefab != null) PlaceGableRidge(in gc);
            if (doorOnXWall)
                PlaceGableDoorX(in gc);
            else
                PlaceGableDoorZ(in gc);
        }

        private struct GableAsmCtx
        {
            public Transform RoofParent;
            public string Label;
            public float Cx, Cz, BuildW, BuildD, HalfW, HalfD, PeakH, WallTopY, RoofAngleDeg;
            public float RidgeScaleX, RidgeScaleZ, RidgeYScale, RidgeYOff;
            public bool RidgeAlongX;
            public float PanelXScale, PanelYScale, PanelZScale;
            public float PanelXOff, PanelYOff, PanelXPosMul, PanelYPosMul;
            public float GableXScale, GableYScale, GableYPosMul;
            public GameObject RidgePrefab, PanelPrefab, GablePrefab;
        }

        private static void PlaceGableRidge(in GableAsmCtx c)
        {
            var r = (GameObject)PrefabUtility.InstantiatePrefab(c.RidgePrefab, c.RoofParent);
            if (r == null) return;
            r.name = $"Roof_Ridge_{c.Label}";
            r.transform.localPosition = new Vector3(c.Cx, c.WallTopY + c.PeakH + c.RidgeYOff, c.Cz);
            // The kit ridge mesh is long along LOCAL Z; rotate 90° when the ridge
            // must run along world X (door on an X wall).
            r.transform.localRotation = c.RidgeAlongX
                ? Quaternion.Euler(0f, 90f, 0f)
                : Quaternion.identity;
            r.transform.localScale = new Vector3(c.RidgeScaleX, c.RidgeYScale, c.RidgeScaleZ);
            BakeMeshScale(r);
        }

        private static void PlaceGableDoorX(in GableAsmCtx c)
        {
            if (c.PanelPrefab != null)
            {
                var pf = (GameObject)PrefabUtility.InstantiatePrefab(c.PanelPrefab, c.RoofParent);
                if (pf != null)
                {
                    pf.name = $"Roof_PanelF_{c.Label}";
                    pf.transform.localPosition = new Vector3(c.Cx, c.WallTopY + c.PeakH * c.PanelYPosMul + c.PanelYOff,
                        c.Cz - c.HalfD * c.PanelXPosMul - c.PanelXOff);
                    // Ry(90) lays the panel's long LOCAL Z along world X (ridge dir);
                    // the Rz(±angle) tilt then rises toward the ridge along world Z.
                    pf.transform.localRotation = Quaternion.Euler(0f, 90f, c.RoofAngleDeg);
                    pf.transform.localScale = new Vector3(c.PanelXScale, c.PanelYScale, c.PanelZScale);
                    BakeMeshScale(pf);
                }
                var pb = (GameObject)PrefabUtility.InstantiatePrefab(c.PanelPrefab, c.RoofParent);
                if (pb != null)
                {
                    pb.name = $"Roof_PanelB_{c.Label}";
                    pb.transform.localPosition = new Vector3(c.Cx, c.WallTopY + c.PeakH * c.PanelYPosMul + c.PanelYOff,
                        c.Cz + c.HalfD * c.PanelXPosMul + c.PanelXOff);
                    pb.transform.localRotation = Quaternion.Euler(0f, 90f, -c.RoofAngleDeg);
                    pb.transform.localScale = new Vector3(c.PanelXScale, c.PanelYScale, c.PanelZScale);
                    BakeMeshScale(pb);
                }
            }
            if (c.GablePrefab != null)
            {
                var gl = (GameObject)PrefabUtility.InstantiatePrefab(c.GablePrefab, c.RoofParent);
                if (gl != null)
                {
                    gl.name = $"Roof_GableL_{c.Label}";
                    gl.transform.localPosition = new Vector3(c.Cx - c.BuildW * 0.5f + WallInset,
                        c.WallTopY + c.PeakH * c.GableYPosMul, c.Cz);
                    gl.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
                    gl.transform.localScale = new Vector3(c.GableXScale, c.GableYScale, 1f);
                    BakeMeshScale(gl);
                }
                var gr = (GameObject)PrefabUtility.InstantiatePrefab(c.GablePrefab, c.RoofParent);
                if (gr != null)
                {
                    gr.name = $"Roof_GableR_{c.Label}";
                    gr.transform.localPosition = new Vector3(c.Cx + c.BuildW * 0.5f - WallInset,
                        c.WallTopY + c.PeakH * c.GableYPosMul, c.Cz);
                    gr.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    gr.transform.localScale = new Vector3(c.GableXScale, c.GableYScale, 1f);
                    BakeMeshScale(gr);
                }
            }
        }

        private static void PlaceGableDoorZ(in GableAsmCtx c)
        {
            if (c.PanelPrefab != null)
            {
                var pl = (GameObject)PrefabUtility.InstantiatePrefab(c.PanelPrefab, c.RoofParent);
                if (pl != null)
                {
                    pl.name = $"Roof_PanelL_{c.Label}";
                    pl.transform.localPosition = new Vector3(
                        c.Cx - c.HalfW * c.PanelXPosMul - c.PanelXOff,
                        c.WallTopY + c.PeakH * c.PanelYPosMul + c.PanelYOff, c.Cz);
                    pl.transform.localRotation = Quaternion.Euler(0f, 0f, -c.RoofAngleDeg);
                    pl.transform.localScale = new Vector3(c.PanelXScale, c.PanelYScale, c.PanelZScale);
                    BakeMeshScale(pl);
                }
                var pr = (GameObject)PrefabUtility.InstantiatePrefab(c.PanelPrefab, c.RoofParent);
                if (pr != null)
                {
                    pr.name = $"Roof_PanelR_{c.Label}";
                    pr.transform.localPosition = new Vector3(
                        c.Cx + c.HalfW * c.PanelXPosMul + c.PanelXOff,
                        c.WallTopY + c.PeakH * c.PanelYPosMul + c.PanelYOff, c.Cz);
                    pr.transform.localRotation = Quaternion.Euler(0f, 180f, -c.RoofAngleDeg);
                    pr.transform.localScale = new Vector3(c.PanelXScale, c.PanelYScale, c.PanelZScale);
                    BakeMeshScale(pr);
                }
            }
            if (c.GablePrefab != null)
            {
                var gf = (GameObject)PrefabUtility.InstantiatePrefab(c.GablePrefab, c.RoofParent);
                if (gf != null)
                {
                    gf.name = $"Roof_GableF_{c.Label}";
                    gf.transform.localPosition = new Vector3(c.Cx, c.WallTopY + c.PeakH * c.GableYPosMul,
                        c.Cz - c.BuildD * 0.5f + WallInset);
                    gf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    gf.transform.localScale = new Vector3(c.GableXScale, c.GableYScale, 1f);
                    BakeMeshScale(gf);
                }
                var gb = (GameObject)PrefabUtility.InstantiatePrefab(c.GablePrefab, c.RoofParent);
                if (gb != null)
                {
                    gb.name = $"Roof_GableB_{c.Label}";
                    gb.transform.localPosition = new Vector3(c.Cx, c.WallTopY + c.PeakH * c.GableYPosMul,
                        c.Cz + c.BuildD * 0.5f - WallInset);
                    gb.transform.localRotation = Quaternion.identity;
                    gb.transform.localScale = new Vector3(c.GableXScale, c.GableYScale, 1f);
                    BakeMeshScale(gb);
                }
            }
        }

        // ── RoofTypeConfig-based assembly (configurable params instead of hardcoded defaults) ──

        private static void PlaceKitRoofCapFromConfig(Transform roofParent, RoofTypeConfig config, GenerationPlan plan)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
            var doorOnXWall = IsDoorOnXWall(plan, topLevel);
            PlaceScaledRoofAssembly(roofParent, config, "Top", w, d, off, wallTopY, doorOnXWall);
        }

        private static void PlaceKitStepBackRoofsFromConfig(Transform roofParent, RoofTypeConfig config, GenerationPlan plan)
        {
            for (var level = 0; level < plan.FloorCount - 1; level++)
            {
                var lw = plan.FloorWidths[level]; var ld = plan.FloorDepths[level];
                var uw = plan.FloorWidths[level + 1]; var ud = plan.FloorDepths[level + 1];
                if (lw == uw && ld == ud) continue;
                var ux = (lw - uw) / 2; var uz = (ld - ud) / 2;

                var haveNorth = uz > 0;
                var haveSouth = uz + ud < ld;
                var haveWest  = ux > 0;
                var haveEast  = ux + uw < lw;

                var stepY = LevelFloorSurfaceY(level) + WallHeight;
                var lowerOff = plan.FloorOffsets[level];
                var doorOnXWall = IsDoorOnXWall(plan, level);

                if (haveNorth)
                    PlaceScaledRoofStrip(roofParent, config,
                        new RoofStripInfo($"StepN_L{level}", new Vector2Int(ux, 0), new Vector2Int(uw, uz)),
                        lowerOff, stepY, doorOnXWall);
                if (haveSouth)
                    PlaceScaledRoofStrip(roofParent, config,
                        new RoofStripInfo($"StepS_L{level}", new Vector2Int(ux, uz + ud), new Vector2Int(uw, ld - uz - ud)),
                        lowerOff, stepY, doorOnXWall);
                if (haveWest)
                    PlaceScaledRoofStrip(roofParent, config,
                        new RoofStripInfo($"StepW_L{level}", new Vector2Int(0, uz), new Vector2Int(ux, ud)),
                        lowerOff, stepY, doorOnXWall);
                if (haveEast)
                    PlaceScaledRoofStrip(roofParent, config,
                        new RoofStripInfo($"StepE_L{level}", new Vector2Int(ux + uw, uz), new Vector2Int(lw - ux - uw, ud)),
                        lowerOff, stepY, doorOnXWall);
            }
        }

        // ── Flat roof: single stretched mesh per section, UV-tiled across the footprint ──

        private static void PlaceFlatRoofCap(Transform roofParent, RoofTypeConfig config, GenerationPlan plan)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var (w, d, off) = ResolveFloorFootprint(plan, topLevel);

            var halfCell = CellSize * 0.5f;
            var centerX = off.x + w * CellSize * 0.5f - halfCell;
            var centerZ = off.z + d * CellSize * 0.5f - halfCell;

            PlaceStretchedFlatTile(config, roofParent, "Roof_Top", w, d, centerX, centerZ, wallTopY);
        }

        private static void PlaceFlatStepBackRoofs(Transform roofParent, RoofTypeConfig config, GenerationPlan plan)
        {
            for (var level = 0; level < plan.FloorCount - 1; level++)
            {
                var lw = plan.FloorWidths[level]; var ld = plan.FloorDepths[level];
                var uw = plan.FloorWidths[level + 1]; var ud = plan.FloorDepths[level + 1];
                if (lw == uw && ld == ud) continue;
                var ux = (lw - uw) / 2; var uz = (ld - ud) / 2;

                var y = LevelFloorSurfaceY(level) + WallHeight;
                var lowerOff = plan.FloorOffsets[level];
                var halfCell = CellSize * 0.5f;
                var ftCtx = new FlatTileCtx(roofParent, lowerOff, y, halfCell);

                if (uz > 0)
                    PlaceStretchedFlatStrip(config, in ftCtx, $"Roof_StepN_L{level}",
                        new Vector2Int(ux, 0), new Vector2Int(uw, uz));
                if (uz + ud < ld)
                    PlaceStretchedFlatStrip(config, in ftCtx, $"Roof_StepS_L{level}",
                        new Vector2Int(ux, uz + ud), new Vector2Int(uw, ld - uz - ud));
                if (ux > 0)
                    PlaceStretchedFlatStrip(config, in ftCtx, $"Roof_StepW_L{level}",
                        new Vector2Int(0, uz), new Vector2Int(ux, ud));
                if (ux + uw < lw)
                    PlaceStretchedFlatStrip(config, in ftCtx, $"Roof_StepE_L{level}",
                        new Vector2Int(ux + uw, uz), new Vector2Int(lw - ux - uw, ud));
            }
        }

        private readonly struct FlatTileCtx
        {
            public readonly Transform RoofParent;
            public readonly float WallTopY, HalfCell;
            public readonly Vector3 BaseOffset;
            public FlatTileCtx(Transform parent, Vector3 offset, float wallY, float halfC)
            { RoofParent = parent; BaseOffset = offset; WallTopY = wallY; HalfCell = halfC; }
        }

        private static void PlaceStretchedFlatStrip(RoofTypeConfig config, in FlatTileCtx ctx, string label,
            Vector2Int start, Vector2Int size)
        {
            if (size.x <= 0 || size.y <= 0) return;
            var cx = ctx.BaseOffset.x + (start.x + size.x * 0.5f) * CellSize - ctx.HalfCell;
            var cz = ctx.BaseOffset.z + (start.y + size.y * 0.5f) * CellSize - ctx.HalfCell;
            PlaceStretchedFlatTile(config, ctx.RoofParent, label, size.x, size.y, cx, cz, ctx.WallTopY);
        }

        private static void PlaceStretchedFlatTile(RoofTypeConfig config, Transform roofParent, string tileName,
            int w, int d, float cx, float cz, float alignBottomY)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(config.TilePrefab, roofParent);
            if (go == null) return;
            go.name = tileName;
            go.transform.localPosition = new Vector3(cx, 0f, cz);

            var mf = go.GetComponentInChildren<MeshFilter>();
            var meshSize = mf != null && mf.sharedMesh != null
                ? mf.sharedMesh.bounds.size
                : Vector3.one;
            var cellW = w * CellSize;
            var cellD = d * CellSize;
            var scaleY = Mathf.Max(meshSize.y, 0.001f);
            var scaleZ = Mathf.Max(meshSize.z, 0.001f);

            go.transform.localRotation = Quaternion.Euler(0f, config.FlatRoofYRotation, 90f + config.FlatRoofZRotation);
            go.transform.localScale = new Vector3(
                config.FlatRoofXScale,
                cellW / scaleY * config.FlatRoofZScale,
                cellD / scaleZ * config.FlatRoofYScale);

            AlignRenderersBottomToY(go, alignBottomY + config.FlatRoofYOffset);

            BakeMeshScale(go);

            // Remap UVs so the texture tiles w×d times instead of stretching
            if (config.FlatRoofUvTiling && (w > 1 || d > 1))
                ApplyUvTiling(go, d, 1f, config.FlatRoofYScale);
        }

        private static void ApplyUvTiling(GameObject go, int d, float yScale, float zScale)
        {
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            var mesh = mf.sharedMesh;
            var uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0) return;

            // BakeMeshScale already multiplied UVs by (ls.x, ls.y) = (w, yScale).
            // Divide out the Y contribution, then multiply by (d * zScale) for proper Z tiling.
            var vMult = (d * zScale) / Mathf.Max(yScale, 0.01f);
            for (var i = 0; i < uvs.Length; i++)
                uvs[i] = new Vector2(uvs[i].x, uvs[i].y * vMult);

            mesh.uv = uvs;
        }

        private static (int width, int depth, Vector3 offset) ResolveFloorFootprint(GenerationPlan plan, int level)
        {
            return plan.SkyscraperMode
                ? (plan.FloorWidths[level], plan.FloorDepths[level], plan.FloorOffsets[level])
                : (plan.Width, plan.Depth, Vector3.zero);
        }

        private static void PlaceRoofTile(GameObject roofPrefab, Transform roofParent, string tileName, float cx, float cz, float alignBottomY)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(roofPrefab, roofParent);
            if (go == null) return;
            go.name = tileName;
            go.transform.localPosition = new Vector3(cx, 0f, cz);
            AlignRenderersBottomToY(go, alignBottomY);
        }

        private static void AlignRenderersBottomToY(GameObject root, float worldBottomY)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var delta = worldBottomY - bounds.min.y;
            root.transform.position += new Vector3(0f, delta, 0f);
        }

        /// <summary>
        /// Clones the mesh, bakes transform.localScale into the clone's vertices,
        /// and resets localScale to (1,1,1). The source asset is never modified.
        /// Handles both stripped prefabs (MeshFilter only) and live ProBuilderMesh prefabs.
        /// </summary>
        private static void BakeMeshScale(GameObject go)
        {
            if (go == null) return;
            var ls = go.transform.localScale;
            // Identity scale — nothing to bake.
            if (Mathf.Approximately(ls.x, 1f) &&
                Mathf.Approximately(ls.y, 1f) &&
                Mathf.Approximately(ls.z, 1f))
                return;

            Debug.Log($"[BakeMeshScale] {go.name} ls=({ls.x:F2},{ls.y:F2},{ls.z:F2})");

            // Try ProBuilderMesh first — it owns the canonical mesh.
            var pbm = go.GetComponent<UnityEngine.ProBuilder.ProBuilderMesh>();
            MeshFilter mf = null;
            Mesh sourceMesh = null;

            if (pbm != null)
            {
                var prop = typeof(UnityEngine.ProBuilder.ProBuilderMesh)
                    .GetProperty("mesh",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public);
                sourceMesh = prop?.GetValue(pbm) as Mesh;
                mf = go.GetComponentInChildren<MeshFilter>();
            }
            else
            {
                mf = go.GetComponentInChildren<MeshFilter>();
                if (mf != null)
                    sourceMesh = mf.sharedMesh;
            }

            if (sourceMesh == null)
            {
                Debug.LogWarning($"[BakeMeshScale] {go.name}: no source mesh found (mf={mf != null}, pbm={pbm != null})");
                return;
            }

            // Clone — never touch the source asset.
            var clone = Object.Instantiate(sourceMesh);
            clone.name = go.name;
            _bakedMeshes.Add(clone);

            // Bake localScale into vertices.
            var verts = clone.vertices;
            for (var i = 0; i < verts.Length; i++)
                verts[i] = Vector3.Scale(verts[i], ls);
            clone.vertices = verts;

            Debug.Log($"[BakeMeshScale] {go.name}: cloned {clone.vertexCount} verts, {clone.subMeshCount} submeshes, bounds={clone.bounds}");

            // Scale UV0 so texture density stays consistent.
            var uvs = clone.uv;
            if (uvs != null && uvs.Length > 0)
            {
                for (var i = 0; i < uvs.Length; i++)
                    uvs[i] = Vector2.Scale(uvs[i], new Vector2(ls.x, ls.y));
                clone.uv = uvs;
            }

            clone.RecalculateBounds();
            clone.RecalculateNormals();

            if (mf != null)
                mf.sharedMesh = clone;

            if (pbm != null)
            {
                // Push the clone back so ProBuilderMesh stays in sync.
                var prop = typeof(UnityEngine.ProBuilder.ProBuilderMesh)
                    .GetProperty("mesh",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public);
                prop?.SetValue(pbm, clone);
                pbm.ToMesh();
                pbm.Refresh();
            }

            go.transform.localScale = Vector3.one;
        }

        /// <summary>
        ///     Returns true when the building's main door is on an X-facing wall,
        ///     false when on a Z-facing wall. Determines whether the roof ridge
        ///     runs along Z (door on X wall, default) or X (door on Z wall).
        /// </summary>
        private static bool IsDoorOnXWall(GenerationPlan plan, int topLevel)
        {
            foreach (var levelPlan in plan.PerimeterWallPlans)
            {
                if (levelPlan.Level != topLevel) continue;
                foreach (var seg in levelPlan.Segments)
                {
                    if (!seg.IsDoor) continue;
                    var doorForward = seg.Segment.Rotation * Vector3.forward;
                    return Mathf.Abs(Vector3.Dot(doorForward, Vector3.right)) > 0.5f;
                }
            }
            return false;
        }

        // ── Shed roof: single-slope lean-to ──

        private static void PlaceShedRoof(Transform roofParent, RoofTypeConfig config, GenerationPlan plan, PrefabDependencies prefs)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
            var buildW = w * CellSize;
            var buildD = d * CellSize;
            var highY = wallTopY * config.ShedHighScale;
            var lowY  = wallTopY * config.ShedLowScale;

            var (lowEdge, doorForward) = DetectShedDoorEdge(plan, config, topLevel, off, buildW, buildD);
            var isZAxis = lowEdge == ShedDirection.North || lowEdge == ShedDirection.South;
            // East = maxX, North = maxZ. True when the low edge is at the max-coordinate side.
            var lowAtMax = lowEdge == ShedDirection.East || lowEdge == ShedDirection.North;
            var spanLength = isZAxis ? buildD : buildW;
            var deltaY = highY - lowY;
            var angleDeg = Mathf.Atan(deltaY / spanLength) * Mathf.Rad2Deg;

            var (gableEdge0, gableEdge1) = DetectGableEdges(plan, topLevel, isZAxis, off, buildW, buildD);
            // Gables start at the min side; shift to max when high is at max (low at min).
            if (!lowAtMax)
            {
                var shift = isZAxis ? new Vector3(0f, 0f, buildD) : new Vector3(buildW, 0f, 0f);
                gableEdge0 += shift;
                gableEdge1 += shift;
            }
            var spCtx = new ShedPanelCtx
            {
                TopLevel = topLevel, Off = off, BuildW = buildW, BuildD = buildD, WallTopY = wallTopY,
                IsZAxis = isZAxis, LowAtMax = lowAtMax, SpanLength = spanLength, DeltaY = deltaY, AngleDeg = angleDeg
            };
            PlaceShedPanel(roofParent, config, spCtx);
            var sgCtx = new ShedGableCtx
            {
                TopLevel = topLevel, WallTopY = wallTopY, IsZAxis = isZAxis, LowAtMax = lowAtMax,
                SpanLength = spanLength, DeltaY = deltaY
            };
            PlaceShedGables(roofParent, sgCtx, gableEdge0, gableEdge1);
            PlaceShedHighWalls(roofParent, prefs, plan, config, topLevel, doorForward);
        }

        private static (ShedDirection lowEdge, Vector3 doorForward) DetectShedDoorEdge(
            GenerationPlan plan, RoofTypeConfig config, int topLevel, Vector3 off, float buildW, float buildD)
        {
            var lowEdge = config.ShedLowEdge;
            var doorForward = Vector3.zero;

            foreach (var levelPlan in plan.PerimeterWallPlans)
            {
                if (levelPlan.Level != topLevel) continue;
                foreach (var seg in levelPlan.Segments)
                {
                    if (!seg.IsDoor) continue;
                    doorForward = seg.Segment.Rotation * Vector3.forward;
                    lowEdge = ClassifyShedEdge(seg.Segment.Position, doorForward, off, buildW, buildD);
                    return (lowEdge, doorForward);
                }
                break;
            }
            return (lowEdge, doorForward);
        }

        private static ShedDirection ClassifyShedEdge(
            Vector3 doorPos, Vector3 doorForward, Vector3 off, float buildW, float buildD)
        {
            var minX = off.x; var maxX = off.x + buildW;
            var minZ = off.z; var maxZ = off.z + buildD;

            if (Mathf.Abs(Vector3.Dot(doorForward, Vector3.right)) > 0.5f)
                return doorPos.x < (minX + maxX) * 0.5f
                    ? ShedDirection.East : ShedDirection.West;

            return doorPos.z < (minZ + maxZ) * 0.5f
                ? ShedDirection.North : ShedDirection.South;
        }

        /// <summary>
        ///     Finds the wall position on each of the two gable edges (the sides perpendicular
        ///     to the shed slope). The span-start coordinate (X for !isZAxis, Z for isZAxis)
        ///     is set to the building edge (off - CellSize/2) so the gable spans the full
        ///     building width/depth. The edge coordinate comes from wall segment positions.
        ///
        ///     Reference: both gables share the same span-start coordinate and rotation.
        /// </summary>
        private static (Vector3 edge0, Vector3 edge1) DetectGableEdges(
            GenerationPlan plan, int topLevel, bool isZAxis, Vector3 off, float buildW, float buildD)
        {
            var halfCell = CellSize * 0.5f;
            var spanStartX = off.x - halfCell;
            var spanStartZ = off.z - halfCell;

            var (found0, found1, edgeCoord0, edgeCoord1) = ScanGableEdgeCoords(plan, topLevel, isZAxis);

            Vector3 edge0, edge1;
            if (found0 && found1)
            {
                edge0 = isZAxis
                    ? new Vector3(edgeCoord0, 0f, spanStartZ)
                    : new Vector3(spanStartX, 0f, edgeCoord0);
                edge1 = isZAxis
                    ? new Vector3(edgeCoord1, 0f, spanStartZ)
                    : new Vector3(spanStartX, 0f, edgeCoord1);
            }
            else
            {
                edge0 = isZAxis
                    ? new Vector3(off.x, 0f, spanStartZ)
                    : new Vector3(spanStartX, 0f, off.z);
                edge1 = isZAxis
                    ? new Vector3(off.x + buildW, 0f, spanStartZ)
                    : new Vector3(spanStartX, 0f, off.z + buildD);
            }

            return (edge0, edge1);
        }

        private static (bool found0, bool found1, float coord0, float coord1) ScanGableEdgeCoords(
            GenerationPlan plan, int topLevel, bool isZAxis)
        {
            var axisVec = isZAxis ? Vector3.right : Vector3.forward;
            var edgeCoord0 = float.MaxValue;
            var edgeCoord1 = float.MinValue;
            var found0 = false;
            var found1 = false;

            foreach (var levelPlan in plan.PerimeterWallPlans)
            {
                if (levelPlan.Level != topLevel) continue;
                ScanLevelGableEdges(levelPlan.Segments, axisVec, isZAxis,
                    ref edgeCoord0, ref edgeCoord1, ref found0, ref found1);
            }

            return (found0, found1, edgeCoord0, edgeCoord1);
        }

        private static void ScanLevelGableEdges(
            System.Collections.Generic.List<PlannedPerimeterSegment> segments,
            Vector3 axisVec, bool isZAxis,
            ref float edgeCoord0, ref float edgeCoord1, ref bool found0, ref bool found1)
        {
            foreach (var seg in segments)
            {
                var fwd = seg.Segment.Rotation * Vector3.forward;
                if (Mathf.Abs(Vector3.Dot(fwd, axisVec)) < 0.7f) continue;

                var edgeCoord = isZAxis ? seg.Segment.Position.x : seg.Segment.Position.z;
                if (edgeCoord < edgeCoord0) { edgeCoord0 = edgeCoord; found0 = true; }
                if (edgeCoord > edgeCoord1) { edgeCoord1 = edgeCoord; found1 = true; }
            }
        }

        private struct ShedPanelCtx
        {
            public int TopLevel;
            public Vector3 Off;
            public float BuildW, BuildD, WallTopY, SpanLength, DeltaY, AngleDeg;
            public bool IsZAxis, LowAtMax;
        }

        private static void PlaceShedPanel(Transform roofParent, RoofTypeConfig config, ShedPanelCtx ctx)
        {
            if (config.PanelPrefab == null) return;
            var p = (GameObject)PrefabUtility.InstantiatePrefab(config.PanelPrefab, roofParent);
            if (p == null) return;

            p.name = $"Shed_Panel_L{ctx.TopLevel}";
            p.transform.localPosition = new Vector3(ctx.Off.x + ctx.BuildW * 0.5f - CellSize * 0.5f,
                ctx.WallTopY + ctx.DeltaY * 0.5f,
                ctx.Off.z + ctx.BuildD * 0.5f - CellSize * 0.5f);
            var shedAngle = ctx.LowAtMax ? ctx.AngleDeg : -ctx.AngleDeg;
            p.transform.localRotation = ctx.IsZAxis
                ? Quaternion.Euler(shedAngle, 0f, -90f)
                : Quaternion.Euler(shedAngle, 90f, -90f);

            var diag = Mathf.Sqrt(ctx.SpanLength * ctx.SpanLength + ctx.DeltaY * ctx.DeltaY);
            p.transform.localScale = ctx.IsZAxis
                ? new Vector3(ctx.BuildD, ctx.BuildW, diag)
                : new Vector3(diag, ctx.BuildD, ctx.BuildW);
            BakeMeshScale(p);
        }

        private struct ShedGableCtx
        {
            public int TopLevel;
            public float WallTopY, SpanLength, DeltaY, BaseX, BaseY;
            public bool IsZAxis, LowAtMax;
        }

        private static void PlaceShedGables(Transform roofParent,
            ShedGableCtx ctx, Vector3 gableEdge0, Vector3 gableEdge1)
        {
            var gablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Gable_RightAngle_3m.prefab");
            if (gablePrefab == null) return;

            var gmf = gablePrefab.GetComponentInChildren<MeshFilter>();
            var gmeshSize = gmf != null && gmf.sharedMesh != null ? gmf.sharedMesh.bounds.size : new Vector3(3f, 3f, 0.1f);
            var ctx2 = new ShedGableCtx
            {
                TopLevel = ctx.TopLevel, WallTopY = ctx.WallTopY, IsZAxis = ctx.IsZAxis, LowAtMax = ctx.LowAtMax,
                SpanLength = ctx.SpanLength, DeltaY = ctx.DeltaY,
                BaseX = Mathf.Max(gmeshSize.x, 0.001f), BaseY = Mathf.Max(gmeshSize.y, 0.001f)
            };

            PlaceSingleShedGable(roofParent, gablePrefab, 0, gableEdge0, ctx2);
            PlaceSingleShedGable(roofParent, gablePrefab, 1, gableEdge1, ctx2);
        }

        private static void PlaceSingleShedGable(Transform roofParent, GameObject gablePrefab,
            int side, Vector3 edgePos, ShedGableCtx ctx)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(gablePrefab, roofParent);
            if (g == null) return;

            g.name = $"Shed_Gable_L{ctx.TopLevel}_{side}";
            var insetAmount = side == 0 ? 0.05f : -0.05f;
            var inset = ctx.IsZAxis
                ? new Vector3(insetAmount, 0f, 0f)
                : new Vector3(0f, 0f, insetAmount);
            g.transform.localPosition = new Vector3(edgePos.x, ctx.WallTopY, edgePos.z) + inset;

            var yawDir = ctx.LowAtMax ? 1f : -1f;
            var yawBase = ctx.LowAtMax ? 180f : 0f;
            float yaw = ctx.IsZAxis ? yawDir * 90f : yawBase;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * g.transform.rotation;

            g.transform.localScale = new Vector3(ctx.SpanLength / ctx.BaseX, ctx.DeltaY / ctx.BaseY * 0.967f, 1f);
            BakeMeshScale(g);

            var mf = g.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var uvs = mf.sharedMesh.uv;
                var badUvY = ctx.DeltaY / ctx.BaseY * 0.967f;
                var xScale = ctx.SpanLength / 2f;
                for (var i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(uvs[i].x * xScale, uvs[i].y / badUvY);
                mf.sharedMesh.uv = uvs;
            }
        }

        private static void PlaceShedHighWalls(Transform roofParent, PrefabDependencies prefs, GenerationPlan plan,
            RoofTypeConfig config, int topLevel, Vector3 doorForward)
        {
            if (prefs.Wall == null) return;

            foreach (var levelPlan in plan.PerimeterWallPlans)
            {
                if (levelPlan.Level != topLevel) continue;
                foreach (var seg in levelPlan.Segments)
                {
                    var fwd = seg.Segment.Rotation * Vector3.forward;
                    if (Vector3.Dot(fwd, doorForward) < 0.7f) continue;

                    var wgo = PrefabUtility.InstantiatePrefab(prefs.Wall, roofParent) as GameObject;
                    if (wgo == null) continue;

                    wgo.name = $"Shed_Wall_L{topLevel}_{seg.SegmentIndex:D2}_Hi";
                    wgo.transform.localPosition = seg.Segment.Position + new Vector3(0f, WallHeight, 0f);
                    wgo.transform.localRotation = seg.Segment.Rotation;
                    wgo.transform.localScale = new Vector3(1f, config.ShedHighScale, 1f);
                    BakeMeshScale(wgo);
                }
                break;
            }
        }

        // ── Saltbox roof (asymmetrical gable) ──────────────────────────

        private readonly struct SaltboxCtx
        {
            public readonly int TopLevel;
            public readonly float WallTopY;
            public readonly float PeakH;
            public readonly float RidgeX;
            public readonly float Cz;
            public readonly float LeftSpan, RightSpan;
            public readonly float LeftDiag, RightDiag;
            public readonly float LeftAngleDeg, RightAngleDeg;
            public readonly float RidgeScaleZ, PanelZScale;
            public readonly float HalfW, BuildD;
            public readonly float Cx;

            public SaltboxCtx(GenerationPlan plan, RoofTypeConfig config, int topLevel, float wallTopY)
            {
                TopLevel = topLevel;
                WallTopY = wallTopY;
                var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
                var buildW = w * CellSize;
                BuildD = d * CellSize;
                var halfW = buildW * 0.5f;
                var halfCell = CellSize * 0.5f;
                Cx = off.x + halfW - halfCell;
                Cz = off.z + BuildD * 0.5f - halfCell;
                HalfW = halfW;

                PeakH = WallHeight * config.PeakHeightMultiplier;
                var ridgeXOffset = config.SaltboxRidgeTowardPositiveX
                    ? halfW * config.SaltboxRidgeOffset
                    : -halfW * config.SaltboxRidgeOffset;
                RidgeX = Cx + ridgeXOffset;

                var wideHalfSpan = halfW + Mathf.Abs(ridgeXOffset);
                var narrowHalfSpan = halfW - Mathf.Abs(ridgeXOffset);
                var towardPositiveX = config.SaltboxRidgeTowardPositiveX;
                LeftSpan  = towardPositiveX ? wideHalfSpan : narrowHalfSpan;
                RightSpan = towardPositiveX ? narrowHalfSpan : wideHalfSpan;

                LeftDiag  = Mathf.Sqrt(LeftSpan * LeftSpan + PeakH * PeakH);
                RightDiag = Mathf.Sqrt(RightSpan * RightSpan + PeakH * PeakH);
                LeftAngleDeg  = Mathf.Atan(LeftSpan / PeakH) * Mathf.Rad2Deg;
                RightAngleDeg = Mathf.Atan(RightSpan / PeakH) * Mathf.Rad2Deg;

                RidgeScaleZ = d + config.RidgeZOverhang;
                PanelZScale = d + config.PanelZOverhang;
            }
        }

        private static void PlaceSaltboxRoof(Transform roofParent, RoofTypeConfig config, GenerationPlan plan)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var ctx = new SaltboxCtx(plan, config, topLevel, wallTopY);

            PlaceSaltboxRidge(roofParent, config, in ctx);
            PlaceSaltboxPanel(roofParent, config, in ctx, isRight: false);
            PlaceSaltboxPanel(roofParent, config, in ctx, isRight: true);
            PlaceSaltboxGables(roofParent, config, in ctx);
        }

        private static void PlaceSaltboxRidge(Transform roofParent, RoofTypeConfig config, in SaltboxCtx ctx)
        {
            if (config.RidgePrefab == null) return;
            var r = (GameObject)PrefabUtility.InstantiatePrefab(config.RidgePrefab, roofParent);
            if (r == null) return;
            r.name = $"Roof_Ridge_Saltbox_L{ctx.TopLevel}";
            r.transform.localPosition = new Vector3(ctx.RidgeX, ctx.WallTopY + ctx.PeakH + config.RidgeYOffset, ctx.Cz);
            r.transform.localScale = new Vector3(1f, config.RidgeYScale, ctx.RidgeScaleZ);
            BakeMeshScale(r);
        }

        private static void PlaceSaltboxPanel(Transform roofParent, RoofTypeConfig config, in SaltboxCtx ctx, bool isRight)
        {
            if (config.PanelPrefab == null) return;
            var span   = isRight ? ctx.RightSpan : ctx.LeftSpan;
            var diag   = isRight ? ctx.RightDiag : ctx.LeftDiag;
            var angle  = isRight ? ctx.RightAngleDeg : ctx.LeftAngleDeg;
            if (span < 0.001f) return;

            var p = (GameObject)PrefabUtility.InstantiatePrefab(config.PanelPrefab, roofParent);
            if (p == null) return;

            p.name = $"Roof_Panel{(isRight ? "R" : "L")}_Saltbox_L{ctx.TopLevel}";
            var sign = isRight ? 1f : -1f;
            var panelXCenter = ctx.RidgeX + sign * span * config.PanelXPositionMultiplier;
            p.transform.localPosition = new Vector3(
                panelXCenter + sign * config.PanelXOffset,
                ctx.WallTopY + ctx.PeakH * config.PanelYPositionMultiplier + config.PanelYOffset,
                ctx.Cz);
            p.transform.localRotation = Quaternion.Euler(0f, isRight ? 180f : 0f, -angle);
            p.transform.localScale = new Vector3(
                span * config.PanelWidthMultiplier,
                diag / config.PanelDiagonalNorm,
                ctx.PanelZScale);
            BakeMeshScale(p);
        }

        private static void PlaceSaltboxGables(Transform roofParent, RoofTypeConfig config, in SaltboxCtx ctx)
        {
            var gablePrefab = config.SaltboxGablePrefab
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Gable_RightAngle_3m.prefab");
            if (gablePrefab == null) return;

            var gmf = gablePrefab.GetComponentInChildren<MeshFilter>();
            var gmeshSize = gmf != null && gmf.sharedMesh != null
                ? gmf.sharedMesh.bounds.size : new Vector3(3f, 3f, 0.1f);
            var gBaseX = Mathf.Max(gmeshSize.x, 0.001f);
            var gBaseY = Mathf.Max(gmeshSize.y, 0.001f);

            var halfW = ctx.HalfW;
            var leftEdge  = ctx.Cx - halfW + WallInset;
            var rightEdge = ctx.Cx + halfW - WallInset;
            var halfD = ctx.BuildD * 0.5f;
            var frontZ = ctx.Cz - halfD + WallInset;
            var backZ  = ctx.Cz + halfD - WallInset;

            var ghCtx = new SaltboxGableCtx(gBaseX, gBaseY, ctx.PeakH, ctx.WallTopY, ctx.TopLevel);
            PlaceSaltboxHalfGable(roofParent, gablePrefab, "FL", ridgeX: ctx.RidgeX, wallEdgeX: leftEdge,  zPos: frontZ, in ghCtx);
            PlaceSaltboxHalfGable(roofParent, gablePrefab, "FR", ridgeX: ctx.RidgeX, wallEdgeX: rightEdge, zPos: frontZ, in ghCtx);
            PlaceSaltboxHalfGable(roofParent, gablePrefab, "BL", ridgeX: ctx.RidgeX, wallEdgeX: leftEdge,  zPos: backZ,  in ghCtx);
            PlaceSaltboxHalfGable(roofParent, gablePrefab, "BR", ridgeX: ctx.RidgeX, wallEdgeX: rightEdge, zPos: backZ,  in ghCtx);
        }

        private readonly struct SaltboxGableCtx
        {
            public readonly float BaseX, BaseY, PeakH, WallTopY;
            public readonly int TopLevel;
            public SaltboxGableCtx(float baseX, float baseY, float peakH, float wallTopY, int topLevel)
            { BaseX = baseX; BaseY = baseY; PeakH = peakH; WallTopY = wallTopY; TopLevel = topLevel; }
        }

        /// <summary>
        ///     Places one right-angle gable half for a saltbox end.
        ///     Pivot (right-angle corner) anchored at ridgeX, triangle extends toward wallEdgeX.
        /// </summary>
        private static void PlaceSaltboxHalfGable(Transform roofParent, GameObject gablePrefab,
            string label, float ridgeX, float wallEdgeX, float zPos, in SaltboxGableCtx ctx)
        {
            var spanX = Mathf.Abs(ridgeX - wallEdgeX);
            if (spanX < 0.001f) return;

            var g = (GameObject)PrefabUtility.InstantiatePrefab(gablePrefab, roofParent);
            if (g == null) return;

            g.name = $"Roof_Gable{label}_Saltbox_L{ctx.TopLevel}";

            // Gable_RightAngle_3m after -90deg X root rotation.
            // Pivot at right-angle corner. Both halves anchor at ridgeX.
            // wallEdgeX left of ridge → yaw=0  (extend -X toward wall)
            // wallEdgeX right of ridge → yaw=180 (extend +X toward wall)
            var yaw = wallEdgeX < ridgeX ? 0f : 180f;

            g.transform.localPosition = new Vector3(ridgeX, ctx.WallTopY, zPos);
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * g.transform.rotation;
            g.transform.localScale = new Vector3(spanX / ctx.BaseX, ctx.PeakH / ctx.BaseY * 0.967f, 1f);
            BakeMeshScale(g);

            var mf = g.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var uvs = mf.sharedMesh.uv;
                var badUvY = ctx.PeakH / ctx.BaseY * 0.967f;
                var xScale = spanX / 2f;
                for (var i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(uvs[i].x * xScale, uvs[i].y / badUvY);
                mf.sharedMesh.uv = uvs;
            }
        }

        // ── Gambrel roof (barn-style two-slope per side) ──────────────

        private readonly struct GambrelCtx
        {
            public readonly int TopLevel;
            public readonly float WallTopY, PeakH, BreakH;
            public readonly float BreakX, RidgeX;
            public readonly float Cz, Cx;
            public readonly float RidgeScaleZ, PanelZScale;
            public readonly float HalfW, BuildD;

            public GambrelCtx(GenerationPlan plan, RoofTypeConfig config, int topLevel, float wallTopY)
            {
                TopLevel = topLevel;
                WallTopY = wallTopY;
                var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
                var buildW = w * CellSize;
                BuildD = d * CellSize;
                var halfW = buildW * 0.5f;
                var halfCell = CellSize * 0.5f;
                Cx = off.x + halfW - halfCell;
                Cz = off.z + BuildD * 0.5f - halfCell;
                HalfW = halfW;

                PeakH = WallHeight * config.PeakHeightMultiplier;
                BreakH = PeakH * config.GambrelBreakHeight;
                BreakX = halfW * config.GambrelBreakWidth;
                RidgeX = halfW * config.GambrelRidgeWidth;

                RidgeScaleZ = d + config.RidgeZOverhang;
                PanelZScale = d + config.PanelZOverhang;
            }
        }

        private static void PlaceGambrelRoof(Transform roofParent, RoofTypeConfig config, GenerationPlan plan)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var ctx = new GambrelCtx(plan, config, topLevel, wallTopY);
            var effectiveBreakX = ComputeEffectiveBreakX(in ctx);

            PlaceGambrelPanel(roofParent, config, in ctx, effectiveBreakX, isRight: false, isUpper: false);
            PlaceGambrelPanel(roofParent, config, in ctx, effectiveBreakX, isRight: false, isUpper: true);
            PlaceGambrelPanel(roofParent, config, in ctx, effectiveBreakX, isRight: true,  isUpper: false);
            PlaceGambrelPanel(roofParent, config, in ctx, effectiveBreakX, isRight: true,  isUpper: true);

            PlaceGambrelRidge(roofParent, config, in ctx);
            PlaceGambrelGables(roofParent, in ctx);
        }

        /// <summary>
        ///     The trapezoid gable mesh has a fixed top-to-base taper ratio.
        ///     Compute the effective break X where the gable pieces actually meet
        ///     so the panels match the visual geometry.
        /// </summary>
        private static float ComputeEffectiveBreakX(in GambrelCtx ctx)
        {
            var trapezoidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Trapezoid 1.prefab");
            if (trapezoidPrefab == null) return ctx.BreakX;

            var trapMf = trapezoidPrefab.GetComponentInChildren<MeshFilter>();
            if (trapMf == null || trapMf.sharedMesh == null) return ctx.BreakX;

            var trapVerts = trapMf.sharedMesh.vertices;
            var trapMaxY = float.MinValue;
            foreach (var v in trapVerts) { if (v.y > trapMaxY) trapMaxY = v.y; }

            var trapTopMinX = float.MaxValue;
            var trapTopMaxX = float.MinValue;
            const float vertEps = 0.001f;
            foreach (var v in trapVerts)
            {
                if (Mathf.Abs(v.y - trapMaxY) < vertEps)
                {
                    if (v.x < trapTopMinX) trapTopMinX = v.x;
                    if (v.x > trapTopMaxX) trapTopMaxX = v.x;
                }
            }

            var trapTopWidth = Mathf.Max(trapTopMaxX - trapTopMinX, 0.001f);
            var trapBounds = trapMf.sharedMesh.bounds.size;
            var trapBaseX = Mathf.Max(trapBounds.x, 0.001f);
            return ctx.HalfW * (trapTopWidth / trapBaseX);
        }

        private static void PlaceGambrelRidge(Transform roofParent, RoofTypeConfig config, in GambrelCtx ctx)
        {
            if (config.RidgePrefab == null) return;
            var r = (GameObject)PrefabUtility.InstantiatePrefab(config.RidgePrefab, roofParent);
            if (r == null) return;
            r.name = $"Roof_Ridge_Gambrel_L{ctx.TopLevel}";
            r.transform.localPosition = new Vector3(ctx.Cx, ctx.WallTopY + ctx.BreakH * 2f + (ctx.PeakH - ctx.BreakH) / 3f + config.RidgeYOffset, ctx.Cz);
            r.transform.localScale = new Vector3(ctx.RidgeX * 8f / ctx.HalfW, config.RidgeYScale, ctx.RidgeScaleZ);
            BakeMeshScale(r);
        }

        private static void PlaceGambrelPanel(Transform roofParent, RoofTypeConfig config,
            in GambrelCtx ctx, float effectiveBreakX, bool isRight, bool isUpper)
        {
            if (config.PanelPrefab == null) return;

            var p = (GameObject)PrefabUtility.InstantiatePrefab(config.PanelPrefab, roofParent);
            if (p == null) return;

            var sideLabel  = isRight ? "R" : "L";
            var slopeLabel = isUpper ? "Up" : "Lo";
            p.name = $"Roof_Panel{sideLabel}{slopeLabel}_Gambrel_L{ctx.TopLevel}";

            if (!TryCalcGambrelPanelParams(in ctx, effectiveBreakX, isUpper,
                    out var span, out var panelXCenter, out var panelYCenter,
                    out var scaledHeight))
                return;

            var sign = isRight ? 1f : -1f;
            var diag = Mathf.Sqrt(span * span + scaledHeight * scaledHeight);
            var roofAngleDeg = Mathf.Atan(span / scaledHeight) * Mathf.Rad2Deg;

            p.transform.localPosition = new Vector3(
                ctx.Cx + sign * panelXCenter + sign * config.PanelXOffset,
                panelYCenter + config.PanelYOffset,
                ctx.Cz);
            p.transform.localRotation = Quaternion.Euler(0f, isRight ? 180f : 0f, -roofAngleDeg);

            var thicknessX = ctx.HalfW * 2f / 3f * config.PanelWidthMultiplier * 1.3f;
            p.transform.localScale = new Vector3(
                thicknessX,
                diag / config.PanelDiagonalNorm,
                ctx.PanelZScale);
            BakeMeshScale(p);
        }

        private static bool TryCalcGambrelPanelParams(
            in GambrelCtx ctx, float effectiveBreakX, bool isUpper,
            out float span, out float panelXCenter, out float panelYCenter,
            out float scaledHeight)
        {
            if (isUpper)
            {
                var upperH = ctx.PeakH - ctx.BreakH;
                scaledHeight = upperH / 3f;
                span = effectiveBreakX;
                panelXCenter = effectiveBreakX * 0.5f;
                panelYCenter = ctx.WallTopY + ctx.BreakH * 2f + scaledHeight * 0.5f;
            }
            else
            {
                scaledHeight = ctx.BreakH * 2f;
                span = ctx.HalfW - effectiveBreakX;
                panelXCenter = (ctx.HalfW + effectiveBreakX) * 0.5f;
                panelYCenter = ctx.WallTopY + scaledHeight * 0.5f;
            }

            return span >= 0.001f;
        }

        private static void PlaceGambrelGables(Transform roofParent, in GambrelCtx ctx)
        {
            var trapezoidPrefab = LoadTrapezoidPrefab();
            var trianglePrefab = LoadTrianglePrefab();
            if (trapezoidPrefab == null || trianglePrefab == null) return;

            var (trapBaseX, trapBaseY, trapTopWidth) = MeasureTrapezoidMesh(trapezoidPrefab);
            var (triBaseX, triBaseY) = MeasureGableMesh(trianglePrefab);

            var halfD = ctx.BuildD * 0.5f;
            var frontZ = ctx.Cz - halfD + WallInset;
            var backZ  = ctx.Cz + halfD - WallInset;

            var gableCtxF = new GambrelGableEndCtx(roofParent, trapezoidPrefab, trianglePrefab,
                "F", ctx.Cx, frontZ, trapBaseX, trapBaseY, trapTopWidth, triBaseX, triBaseY, in ctx);
            PlaceGambrelGableEnd(in gableCtxF);
            var gableCtxB = new GambrelGableEndCtx(roofParent, trapezoidPrefab, trianglePrefab,
                "B", ctx.Cx, backZ, trapBaseX, trapBaseY, trapTopWidth, triBaseX, triBaseY, in ctx);
            PlaceGambrelGableEnd(in gableCtxB);
        }

        private static GameObject LoadTrapezoidPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Trapezoid 1.prefab");

        private static GameObject LoadTrianglePrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Gable.prefab");

        private static (float baseX, float baseY, float topWidth) MeasureTrapezoidMesh(GameObject prefab)
        {
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            var bounds = mf != null && mf.sharedMesh != null
                ? mf.sharedMesh.bounds.size : new Vector3(1f, 1f, 0.02f);
            var baseX = Mathf.Max(bounds.x, 0.001f);
            var baseY = Mathf.Max(bounds.y, bounds.z, 0.001f);
            var topWidth = mf != null && mf.sharedMesh != null
                ? MeasureTrapezoidTopWidth(mf.sharedMesh.vertices) : baseX;

            return (baseX, baseY, topWidth);
        }

        private static float MeasureTrapezoidTopWidth(Vector3[] verts)
        {
            var maxY = float.MinValue;
            foreach (var v in verts) { if (v.y > maxY) maxY = v.y; }

            var topMinX = float.MaxValue;
            var topMaxX = float.MinValue;
            const float eps = 0.001f;
            foreach (var v in verts)
            {
                if (Mathf.Abs(v.y - maxY) < eps)
                {
                    if (v.x < topMinX) topMinX = v.x;
                    if (v.x > topMaxX) topMaxX = v.x;
                }
            }

            return Mathf.Max(topMaxX - topMinX, 0.001f);
        }

        private static (float baseX, float baseY) MeasureGableMesh(GameObject prefab)
        {
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            var bounds = mf != null && mf.sharedMesh != null
                ? mf.sharedMesh.bounds.size : new Vector3(3.05f, 1.5f, 0.1f);
            return (Mathf.Max(bounds.x, 0.001f), Mathf.Max(bounds.y, bounds.z, 0.001f));
        }

        private readonly struct GambrelGableEndCtx
        {
            public readonly Transform RoofParent;
            public readonly GameObject TrapezoidPrefab, TrianglePrefab;
            public readonly string Label;
            public readonly float Cx, ZPos;
            public readonly float TrapBaseX, TrapBaseY;
            public readonly float TriBaseX, TriBaseY;
            public readonly float UpperH, Yaw, TrapXScale, ActualTopWidth;
            public readonly GambrelCtx ParentCtx;

            public GambrelGableEndCtx(
                Transform roofParent,
                GameObject trapezoidPrefab, GameObject trianglePrefab,
                string label, float cx, float zPos,
                float trapBaseX, float trapBaseY, float trapTopWidth,
                float triBaseX, float triBaseY,
                in GambrelCtx ctx)
            {
                RoofParent = roofParent;
                TrapezoidPrefab = trapezoidPrefab;
                TrianglePrefab = trianglePrefab;
                Label = label;
                Cx = cx;
                ZPos = zPos;
                TrapBaseX = trapBaseX;
                TrapBaseY = trapBaseY;
                TriBaseX = triBaseX;
                TriBaseY = triBaseY;
                ParentCtx = ctx;
                UpperH = ctx.PeakH - ctx.BreakH;
                Yaw = label == "F" ? 180f : 0f;
                TrapXScale = ctx.HalfW * 2f / trapBaseX;
                ActualTopWidth = TrapXScale * trapTopWidth;
            }

            public int TopLevel => ParentCtx.TopLevel;
            public float WallTopY => ParentCtx.WallTopY;
            public float BreakH => ParentCtx.BreakH;
            public float HalfW => ParentCtx.HalfW;
        }

        private static void PlaceGambrelGableEnd(in GambrelGableEndCtx c)
        {
            PlaceGambrelLowerGable(in c);
            PlaceGambrelUpperGable(in c);
        }

        private static void PlaceGambrelLowerGable(in GambrelGableEndCtx c)
        {
            var t = (GameObject)PrefabUtility.InstantiatePrefab(c.TrapezoidPrefab, c.RoofParent);
            if (t == null) return;

            t.name = $"Roof_Gable{c.Label}Lo_Gambrel_L{c.TopLevel}";
            var trapYScale = c.BreakH * 2f / c.TrapBaseY;
            t.transform.localPosition = new Vector3(c.Cx, c.WallTopY + c.BreakH * 1.0f, c.ZPos);
            t.transform.localRotation = Quaternion.Euler(0f, c.Yaw, 0f);
            t.transform.localScale = new Vector3(c.TrapXScale, trapYScale, 1f);
            BakeMeshScale(t);
            NormalizeGableUVs(t, c.TrapXScale, trapYScale,
                c.HalfW * 2f, c.BreakH * 2f, c.TrapBaseX, c.TrapBaseY, swapUV: true);
        }

        private static void PlaceGambrelUpperGable(in GambrelGableEndCtx c)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(c.TrianglePrefab, c.RoofParent);
            if (g == null) return;

            g.name = $"Roof_Gable{c.Label}Up_Gambrel_L{c.TopLevel}";
            var triXScale = c.ActualTopWidth / c.TriBaseX;
            var triYScale = c.UpperH / 3f / c.TriBaseY;
            g.transform.localPosition = new Vector3(c.Cx, c.WallTopY + c.BreakH * 2f + c.UpperH / 6f, c.ZPos);
            g.transform.localRotation = Quaternion.Euler(0f, c.Yaw, 0f);
            g.transform.localScale = new Vector3(triXScale, triYScale, 1f);
            BakeMeshScale(g);
        }

        /// <summary>
        /// After BakeMeshScale has multiplied UVs by (scaleX, scaleY),
        /// undo the axis misalignment caused by the trapezoid mesh having its
        /// UVs swapped (U→Y, V→X) and restore the original texture density.
        /// </summary>
        private static void NormalizeGableUVs(GameObject go, float scaleX, float scaleY,
            float visualWidth, float visualHeight, float meshBaseX, float meshBaseY, bool swapUV)
        {
            if (go == null || scaleX <= 0.001f || scaleY <= 0.001f) return;
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var uvs = mf.sharedMesh.uv;
            if (uvs == null || uvs.Length == 0) return;

            if (swapUV)
            {
                // Trapezoid mesh: original U→Y (vertical), V→X (horizontal).
                // BakeMeshScale scaled U by scaleX (wrong axis for vertical) and
                // V by scaleY (wrong axis for horizontal).  Swap, then normalise
                // so each axis tiles at the original 1-tile-per-mesh-metre density.
                const float density = 2f; // tiles per mesh metre
                var normU = (visualWidth  / meshBaseX) / scaleY * density;
                var normV = (visualHeight / meshBaseY) / scaleX * density;
                for (var i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(uvs[i].y * normU, uvs[i].x * normV);
            }

            mf.sharedMesh.uv = uvs;
        }
    }
}
#endif