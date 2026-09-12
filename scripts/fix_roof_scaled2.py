import re

path = r"c:\Zombera\Assets\Editor\ModularSingleLevelHouseGeneratorTool.Building.cs"
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Find the PlaceScaledRoofAssembly method
start_marker = 'private static void PlaceScaledRoofAssembly'
start = content.find(start_marker)
if start == -1:
    print("ERROR: PlaceScaledRoofAssembly not found")
else:
    # Find the end: the closing brace of this method, before the next method
    # Look for the pattern: two closing braces followed by next method
    end_marker = '        private static (int width, int depth, Vector3 offset) ResolveFloorFootprint'
    end = content.find(end_marker, start)
    if end == -1:
        print("ERROR: ResolveFloorFootprint not found after PlaceScaledRoofAssembly")
    else:
        replacement = '''        private static void PlaceScaledRoofAssembly(Transform roofParent, PrefabDependencies prefs, string label,
            int w, int d, Vector3 off, float wallTopY)
        {
            var buildW = w * CellSize;
            var buildD = d * CellSize;
            var halfW = buildW * 0.5f;
            var cx = off.x + buildW * 0.5f;
            var cz = off.z + buildD * 0.5f;

            // Roof pitch: 2:1 slope = atan(2) ~ 63.4 deg (WallHeight=3m / half-cell=1.5m)
            const float roofSlope = 2f;
            var peakH = halfW * roofSlope;
            var diag = Mathf.Sqrt(halfW * halfW + peakH * peakH);
            var panelYScale = diag / 2.2f;
            var roofAngleDeg = Mathf.Atan(roofSlope) * Mathf.Rad2Deg;
            var gableYScale = peakH / 1.5f;

            // Ridge
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

            // Front panel (ridge -> +X edge, +angle)
            if (prefs.RoofPanel != null)
            {
                var pf = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofPanel, roofParent);
                if (pf != null)
                {
                    pf.name = $"Roof_PanelF_{label}";
                    pf.transform.localPosition = new Vector3(cx + halfW * 0.5f, wallTopY + peakH * 0.5f, cz);
                    pf.transform.localRotation = Quaternion.Euler(0f, 0f, roofAngleDeg);
                    pf.transform.localScale = new Vector3(w, panelYScale, d);
                }
            }

            // Back panel (ridge -> -X edge, -angle)
            if (prefs.RoofPanel != null)
            {
                var pb = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofPanel, roofParent);
                if (pb != null)
                {
                    pb.name = $"Roof_PanelB_{label}";
                    pb.transform.localPosition = new Vector3(cx - halfW * 0.5f, wallTopY + peakH * 0.5f, cz);
                    pb.transform.localRotation = Quaternion.Euler(0f, 0f, -roofAngleDeg);
                    pb.transform.localScale = new Vector3(w, panelYScale, d);
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
                    gf.transform.localScale = new Vector3(w, gableYScale, 1f);
                }

                var gb = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofGable, roofParent);
                if (gb != null)
                {
                    gb.name = $"Roof_GableB_{label}";
                    gb.transform.localPosition = new Vector3(cx, wallTopY, off.z + buildD);
                    gb.transform.localRotation = Quaternion.identity;
                    gb.transform.localScale = new Vector3(w, gableYScale, 1f);
                }
            }
        }

'''
        new_content = content[:start] + replacement + content[end:]
        with open(path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print("OK - PlaceScaledRoofAssembly replaced")
