import re

path = r"c:\Zombera\Assets\Editor\ModularSingleLevelHouseGeneratorTool.Building.cs"
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

replacement = '''        // ── Kit-of-parts roof (Ridge + Panel + Gable, scaled to footprint) ──

        private static void PlaceKitRoofCap(Transform roofParent, PrefabDependencies prefs, GenerationPlan plan)
        {
            var topLevel = plan.FloorCount - 1;
            var wallTopY = LevelFloorSurfaceY(topLevel) + WallHeight;
            var (w, d, off) = ResolveFloorFootprint(plan, topLevel);
            PlaceScaledRoofAssembly(roofParent, prefs, "Top", w, d, off, wallTopY);
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

                if (haveNorth)
                    PlaceScaledRoofStrip(roofParent, prefs, $"StepN_L{level}",
                        ux, uw, 0, uz, lowerOff, stepY);
                if (haveSouth)
                    PlaceScaledRoofStrip(roofParent, prefs, $"StepS_L{level}",
                        ux, uw, uz + ud, ld - uz - ud, lowerOff, stepY);
                if (haveWest)
                    PlaceScaledRoofStrip(roofParent, prefs, $"StepW_L{level}",
                        0, ux, uz, ud, lowerOff, stepY);
                if (haveEast)
                    PlaceScaledRoofStrip(roofParent, prefs, $"StepE_L{level}",
                        ux + uw, lw - ux - uw, uz, ud, lowerOff, stepY);
            }
        }

        private static void PlaceScaledRoofStrip(Transform roofParent, PrefabDependencies prefs, string label,
            int startX, int stripW, int startZ, int stripD, Vector3 baseOffset, float wallTopY)
        {
            if (stripW <= 0 || stripD <= 0) return;
            var off = new Vector3(baseOffset.x + startX * CellSize, 0f, baseOffset.z + startZ * CellSize);
            PlaceScaledRoofAssembly(roofParent, prefs, label, stripW, stripD, off, wallTopY);
        }

        private static void PlaceScaledRoofAssembly(Transform roofParent, PrefabDependencies prefs, string label,
            int w, int d, Vector3 off, float wallTopY)
        {
            var buildW = w * CellSize;
            var buildD = d * CellSize;
            var peakH = buildW * 0.5f;
            var cx = off.x + buildW * 0.5f;
            var cz = off.z + buildD * 0.5f;

            // Ridge (spans full depth)
            if (prefs.RoofRidge != null)
            {
                var r = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofRidge, roofParent);
                if (r != null)
                {
                    r.name = $"Roof_Ridge_{label}";
                    r.transform.localPosition = new Vector3(cx, wallTopY + peakH, cz);
                    r.transform.localScale = new Vector3(1f, 1f, d);
                }
            }

            // Front panel (ridge → +X edge, -45°)
            if (prefs.RoofPanel != null)
            {
                var pf = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofPanel, roofParent);
                if (pf != null)
                {
                    pf.name = $"Roof_PanelF_{label}";
                    pf.transform.localPosition = new Vector3(cx + buildW * 0.25f, wallTopY + peakH * 0.5f, cz);
                    pf.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                    pf.transform.localScale = new Vector3(w, 1f, d);
                }
            }

            // Back panel (ridge → -X edge, +45°)
            if (prefs.RoofPanel != null)
            {
                var pb = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofPanel, roofParent);
                if (pb != null)
                {
                    pb.name = $"Roof_PanelB_{label}";
                    pb.transform.localPosition = new Vector3(cx - buildW * 0.25f, wallTopY + peakH * 0.5f, cz);
                    pb.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    pb.transform.localScale = new Vector3(w, 1f, d);
                }
            }

            // Gable ends
            if (prefs.RoofGable != null)
            {
                var gf = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofGable, roofParent);
                if (gf != null)
                {
                    gf.name = $"Roof_GableF_{label}";
                    gf.transform.localPosition = new Vector3(cx, wallTopY, off.z);
                    gf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    gf.transform.localScale = new Vector3(w, 1f, 1f);
                }

                var gb = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofGable, roofParent);
                if (gb != null)
                {
                    gb.name = $"Roof_GableB_{label}";
                    gb.transform.localPosition = new Vector3(cx, wallTopY, off.z + buildD);
                    gb.transform.localRotation = Quaternion.identity;
                    gb.transform.localScale = new Vector3(w, 1f, 1f);
                }
            }
        }'''

# Find start: "// ── Kit-of-parts roof"
start = content.find('        // \xe2\x94\x80\xe2\x94\x80 Kit-of-parts roof')
if start == -1:
    # Try without box-drawing chars
    start = content.find('        //')
    # find the specific comment
    lines = content.split('\n')
    for i, line in enumerate(lines):
        if 'Kit-of-parts roof' in line and 'Ridge' in line:
            start = content.find(line)
            break

if start == -1:
    print("ERROR: could not find 'Kit-of-parts roof' comment")
    # print surrounding context
    idx = content.find('PlaceKitRoofCap')
    if idx >= 0:
        print(content[idx-200:idx+200])
else:
    # Find end: after PlaceRoofTile method
    end_marker = 'AlignRenderersBottomToY(go, alignBottomY);'
    end_pos = content.find(end_marker, start)
    if end_pos != -1:
        end_pos = content.find('\n        }', end_pos)
        if end_pos != -1:
            end_pos = content.find('\n', end_pos + 1) + 1  # include closing brace line
    
    if end_pos != -1 and end_pos > start:
        new_content = content[:start] + replacement + content[end_pos:]
        with open(path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print("OK - replaced roof kit section with scaled version")
    else:
        print(f"ERROR: could not find end. start={start}")
