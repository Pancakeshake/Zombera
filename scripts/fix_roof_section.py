import re

path = r"c:\Zombera\Assets\Editor\ModularSingleLevelHouseGeneratorTool.Building.cs"
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Find the roof section - from PlaceSkyscraperRoofs to the end of PlaceRoofTile
# We'll replace everything from "private static void PlaceSkyscraperRoofs" 
# through to the line before "#endif" or next method outside the roof system

replacement = '''        private static void PlaceSkyscraperRoofs(GenerationContext context, Transform roofParent)
        {
            var plan = context.Plan;
            var prefs = context.Prefabs;

            // Use kit-of-parts if available, otherwise fall back to single-tile
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

        // ── Kit-of-parts roof (Ridge + Panel + Gable per cell) ─────────────

        private static void PlaceKitRoofCap(Transform roofParent, PrefabDependencies prefs, GenerationPlan plan)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
            PlaceKitAssembly(roofParent, prefs, "Top", w, d, off, wallTopY);
        }

        private static void PlaceKitStepBackRoofs(Transform roofParent, PrefabDependencies prefs, GenerationPlan plan)
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
                        PlaceSingleCellAssembly(roofParent, prefs, $"Step_L{level}", ix, iz,
                            ix * CellSize + plan.FloorOffsets[level].x,
                            iz * CellSize + plan.FloorOffsets[level].z, y, lw, ld);
                    }
            }
        }

        private static void PlaceKitAssembly(Transform roofParent, PrefabDependencies prefs, string label,
            int w, int d, Vector3 off, float wallTopY)
        {
            for (var ix = 0; ix < w; ix++)
                for (var iz = 0; iz < d; iz++)
                    PlaceSingleCellAssembly(roofParent, prefs, label, ix, iz,
                        ix * CellSize + off.x, iz * CellSize + off.z, wallTopY, w, d);
        }

        private static void PlaceSingleCellAssembly(Transform roofParent, PrefabDependencies prefs, string label,
            int ix, int iz, float cx, float cz, float wallTopY, int totalW, int totalD)
        {
            var ccx = cx + CellSize * 0.5f;
            var ccz = cz + CellSize * 0.5f;
            var ridgeY = wallTopY + 1.5f;

            // Ridge
            if (prefs.RoofRidge != null)
            {
                var r = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofRidge, roofParent);
                if (r != null) { r.name = $"Roof_Ridge_{label}_{ix}_{iz}"; r.transform.localPosition = new Vector3(ccx, ridgeY, ccz); }
            }

            // Panels (front + back slopes)
            if (prefs.RoofPanel != null)
            {
                var pf = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofPanel, roofParent);
                if (pf != null)
                {
                    pf.name = $"Roof_PanelF_{label}_{ix}_{iz}";
                    pf.transform.localPosition = new Vector3(ccx + CellSize * 0.25f, wallTopY + 0.75f, ccz);
                    pf.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                }
                var pb = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofPanel, roofParent);
                if (pb != null)
                {
                    pb.name = $"Roof_PanelB_{label}_{ix}_{iz}";
                    pb.transform.localPosition = new Vector3(ccx - CellSize * 0.25f, wallTopY + 0.75f, ccz);
                    pb.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
            }

            // Gable ends (only at depth edges)
            if (prefs.RoofGable != null)
            {
                if (iz == 0)
                {
                    var gf = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofGable, roofParent);
                    if (gf != null) { gf.name = $"Roof_GableF_{label}_{ix}"; gf.transform.localPosition = new Vector3(ccx, wallTopY, cz); gf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); }
                }
                if (iz == totalD - 1)
                {
                    var gb = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofGable, roofParent);
                    if (gb != null) { gb.name = $"Roof_GableB_{label}_{ix}"; gb.transform.localPosition = new Vector3(ccx, wallTopY, cz + CellSize); gb.transform.localRotation = Quaternion.identity; }
                }
            }
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
        }'''

# Find the start and end markers
start = content.find('        private static void PlaceSkyscraperRoofs')
end = content.find('        private static void AlignRenderersBottomToY')
if end == -1:
    end = content.find('    }\n}', content.find('PlaceRoofTile'))

# Find the end of PlaceRoofTile
end_marker = '            AlignRenderersBottomToY(go, alignBottomY);\n        }'
end_pos = content.find(end_marker, start)
if end_pos != -1:
    end_pos += len(end_marker)
    
    # Replace
    new_content = content[:start] + replacement + content[end_pos:]
    
    with open(path, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("OK - replaced roof section")
else:
    print("ERROR: could not find end marker")
    print(f"Content around start: {repr(content[start:start+200])}")
