import re

path = r"c:\Zombera\Assets\Editor\ModularSingleLevelHouseGeneratorTool.Building.cs"
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

start = content.find('        private static void PlaceScaledRoofAssembly')
end = content.find('        private static (int width, int depth, Vector3 offset) ResolveFloorFootprint', start)

replacement = '''        private static void PlaceScaledRoofAssembly(Transform roofParent, PrefabDependencies prefs, string label,
            int w, int d, Vector3 off, float wallTopY)
        {
            var buildW = w * CellSize;
            var buildD = d * CellSize;
            var halfW = buildW * 0.5f;
            var cx = off.x + halfW;
            var cz = off.z + buildD * 0.5f;

            // Roof pitch: 2:1 slope = atan(2) ~ 63.4 deg, peak = WallHeight (constant)
            const float roofSlope = 2f;
            var peakH = WallHeight;
            var diag = Mathf.Sqrt(halfW * halfW + peakH * peakH);
            var roofAngleDeg = Mathf.Atan(roofSlope) * Mathf.Rad2Deg;
            var gableOverhang = CellSize * 0.5f;

            // Panel scales: half-width (one slope), diagonal-matched Y, full depth
            var panelXScale = w * 0.5f;
            var panelYScale = diag / 2.2f;
            var panelZScale = (float)d;

            // Gable Y-scale: peak over base height (1.5m)
            var gableYScale = peakH / 1.5f;

            // ── Ridge ──
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

            // ── Left panel (-X side, slopes up toward ridge) ──
            if (prefs.RoofPanel != null)
            {
                var pl = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofPanel, roofParent);
                if (pl != null)
                {
                    pl.name = $"Roof_PanelL_{label}";
                    pl.transform.localPosition = new Vector3(cx - halfW * 0.5f, wallTopY + peakH * 0.5f, cz);
                    pl.transform.localRotation = Quaternion.Euler(0f, 0f, roofAngleDeg);
                    pl.transform.localScale = new Vector3(panelXScale, panelYScale, panelZScale);
                }
            }

            // ── Right panel (+X side, slopes up toward ridge) ──
            if (prefs.RoofPanel != null)
            {
                var pr = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofPanel, roofParent);
                if (pr != null)
                {
                    pr.name = $"Roof_PanelR_{label}";
                    pr.transform.localPosition = new Vector3(cx + halfW * 0.5f, wallTopY + peakH * 0.5f, cz);
                    pr.transform.localRotation = Quaternion.Euler(0f, 0f, -roofAngleDeg);
                    pr.transform.localScale = new Vector3(panelXScale, panelYScale, panelZScale);
                }
            }

            // ── Front gable (Z- edge, overhang half-cell) ──
            if (prefs.RoofGable != null)
            {
                var gf = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofGable, roofParent);
                if (gf != null)
                {
                    gf.name = $"Roof_GableF_{label}";
                    gf.transform.localPosition = new Vector3(cx, wallTopY, off.z - gableOverhang);
                    gf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    gf.transform.localScale = new Vector3(w, gableYScale, 1f);
                }
            }

            // ── Back gable (Z+ edge, overhang half-cell) ──
            if (prefs.RoofGable != null)
            {
                var gb = (GameObject)PrefabUtility.InstantiatePrefab(prefs.RoofGable, roofParent);
                if (gb != null)
                {
                    gb.name = $"Roof_GableB_{label}";
                    gb.transform.localPosition = new Vector3(cx, wallTopY, off.z + buildD + gableOverhang);
                    gb.transform.localRotation = Quaternion.identity;
                    gb.transform.localScale = new Vector3(w, gableYScale, 1f);
                }
            }
        }

'''

new_content = content[:start] + replacement + content[end:]
with open(path, 'w', encoding='utf-8') as f:
    f.write(new_content)
print("OK")
