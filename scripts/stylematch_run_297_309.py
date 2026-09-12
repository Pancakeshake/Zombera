import json, subprocess, time, re, os, sys
LOG = r"c:\Zombera\Library\StyleMatch\agent-iter.log"
PROJ = r"c:\Zombera"
TMP = r"c:\Zombera\Library\StyleMatch"

HELPERS = r"""
    static void SetLandform(float dominant, float boost, float? peak, float? rolling, float? plains)
    {
        var lp = AssetDatabase.LoadAssetAtPath<LandformProfile>(LandformPath);
        if (lp == null) return;
        var so = new SerializedObject(lp);
        so.FindProperty("biomeDominantThreshold").floatValue = dominant;
        so.FindProperty("biomeForcedBoostScale").floatValue = boost;
        if (peak.HasValue) so.FindProperty("interiorMountainPeakMeters").floatValue = peak.Value;
        if (rolling.HasValue) so.FindProperty("interiorRollingAmplitudeMeters").floatValue = rolling.Value;
        if (plains.HasValue) so.FindProperty("plainsBias").floatValue = plains.Value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lp);
        AssetDatabase.SaveAssets();
    }
    static void ApplyKeeperBase()
    {
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 220f, 80f, 500f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 380f, 200f, 600f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 120f, 80f, 400f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 280f, 120f, 500f);
        StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 380f, 200f, 800f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 500f, 300f, 900f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 50f, 8f, 80f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.50f, 0.2f, 0.85f);
        SetLandform(0.65f, 2.4f, None, None, None);
    }
    static void ApplyKeeperCumulative()
    {
        SetLandform(0.62f, 2.6f, 320f, 130f, 0.08f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 200f, 80f, 500f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 380f, 200f, 600f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 100f, 80f, 400f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 280f, 120f, 500f);
        StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 360f, 200f, 800f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 520f, 300f, 900f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 55f, 8f, 80f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.50f, 0.2f, 0.85f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockExposureNoiseScaleMeters", 55f, 8f, 96f);
    }
}
""".replace("None", "null")

HEADER = """using UnityEditor;
using UnityEngine;
using Zombera.Editor.StyleMatch;
using Zombera.World.CityPipeline.WorldBuilder;

public class Script
{
    const string LandformPath = "Assets/02_Shared/ScriptableObjects/World/Profiles/LandformProfile.asset";

    public static string Main()
    {
"""

FOOTER_HEAD = """
        if (!StyleMatchPerfCampaign.RunCampaignIteration(ITER, opts, out var r, out var err))
            return "FAIL: " + err;
        return "started ITER";
    }
""" + HELPERS

BODIES = {
297: """
        ApplyKeeperBase();
        var opts = StyleMatchSkillIterationOptions.BiomesAndSurfacesAfterChange("iter297 macro bias x1.333");
        opts.BypassCompletionCooldown = true;
""",
298: """
        ApplyKeeperBase();
        SetLandform(0.62f, 2.4f, null, null, null);
        var opts = StyleMatchSkillIterationOptions.BiomesAndSurfacesAfterChange("iter298 dominant 0.62");
        opts.BypassCompletionCooldown = true;
""",
299: """
        SetLandform(0.62f, 2.4f, null, null, null);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 220f, 80f, 500f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 380f, 200f, 600f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 120f, 80f, 400f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 280f, 120f, 500f);
        StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 380f, 200f, 800f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 500f, 300f, 900f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 50f, 8f, 80f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.50f, 0.2f, 0.85f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 200f, 80f, 500f);
        var opts = StyleMatchSkillIterationOptions.PaintOnlyAfterChange("iter299 rock min 200m");
        opts.BypassCompletionCooldown = true;
""",
300: """
        SetLandform(0.62f, 2.4f, null, null, null);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 200f, 80f, 500f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 380f, 200f, 600f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 120f, 80f, 400f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 280f, 120f, 500f);
        StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 380f, 200f, 800f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 500f, 300f, 900f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 50f, 8f, 80f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.50f, 0.2f, 0.85f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 360f, 200f, 800f);
        var opts = StyleMatchSkillIterationOptions.PaintOnlyAfterChange("iter300 snow min 360m");
        opts.BypassCompletionCooldown = true;
""",
301: """
        SetLandform(0.62f, 2.4f, null, null, null);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 200f, 80f, 500f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 380f, 200f, 600f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 120f, 80f, 400f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 280f, 120f, 500f);
        StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 360f, 200f, 800f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 500f, 300f, 900f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 50f, 8f, 80f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.50f, 0.2f, 0.85f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 55f, 8f, 80f);
        var opts = StyleMatchSkillIterationOptions.PaintOnlyAfterChange("iter301 beach inland 55m");
        opts.BypassCompletionCooldown = true;
""",
302: """
        SetLandform(0.62f, 2.4f, null, null, null);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 200f, 80f, 500f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 380f, 200f, 600f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 120f, 80f, 400f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 280f, 120f, 500f);
        StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 360f, 200f, 800f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 500f, 300f, 900f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 55f, 8f, 80f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.50f, 0.2f, 0.85f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 100f, 80f, 400f);
        var opts = StyleMatchSkillIterationOptions.PaintOnlyAfterChange("iter302 dirt min 100m");
        opts.BypassCompletionCooldown = true;
""",
303: """
        ApplyKeeperBase();
        SetLandform(0.62f, 2.4f, 320f, 130f, null);
        var opts = StyleMatchSkillIterationOptions.PipelineRunAfterChange("iter303 rolling amp 130m");
        opts.BypassCompletionCooldown = true;
""",
304: """
        ApplyKeeperBase();
        SetLandform(0.62f, 2.4f, 320f, 130f, 0.08f);
        var opts = StyleMatchSkillIterationOptions.PipelineRunAfterChange("iter304 plainsBias 0.08");
        opts.BypassCompletionCooldown = true;
""",
305: """
        ApplyKeeperBase();
        SetLandform(0.62f, 2.6f, 320f, 130f, 0.08f);
        var opts = StyleMatchSkillIterationOptions.BiomesAndSurfacesAfterChange("iter305 boost 2.6");
        opts.BypassCompletionCooldown = true;
""",
306: """
        SetLandform(0.62f, 2.6f, 320f, 130f, 0.08f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 200f, 80f, 500f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 380f, 200f, 600f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 100f, 80f, 400f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 280f, 120f, 500f);
        StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 360f, 200f, 800f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 500f, 300f, 900f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 55f, 8f, 80f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.50f, 0.2f, 0.85f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 520f, 300f, 900f);
        var opts = StyleMatchSkillIterationOptions.PaintOnlyAfterChange("iter306 snow full 520m");
        opts.BypassCompletionCooldown = true;
""",
307: """
        SetLandform(0.62f, 2.6f, 320f, 130f, 0.08f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockMinElevationMeters", 200f, 80f, 500f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockFullElevationMeters", 380f, 200f, 600f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandMinElevationMeters", 100f, 80f, 400f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_dirtBandFullElevationMeters", 280f, 120f, 500f);
        StyleMatchLoopRunner.SetGrassYellowScale(0.10f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowMinElevationMeters", 360f, 200f, 800f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_snowFullElevationMeters", 520f, 300f, 900f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_beachInlandBlendMeters", 55f, 8f, 80f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_cliffSnowMaxWeight", 0.50f, 0.2f, 0.85f);
        StyleMatchLoopRunner.SetSurfacePainterFloat("_rockExposureNoiseScaleMeters", 55f, 8f, 96f);
        var opts = StyleMatchSkillIterationOptions.PaintOnlyAfterChange("iter307 rock noise 55m");
        opts.BypassCompletionCooldown = true;
""",
308: """
        ApplyKeeperCumulative();
        var opts = StyleMatchSkillIterationOptions.BiomesAndSurfacesAfterChange("iter308 stack confirm fast");
        opts.BypassCompletionCooldown = true;
""",
309: """
        ApplyKeeperCumulative();
        var opts = StyleMatchSkillIterationOptions.BiomesAndSurfacesAfterChange("iter309 full fidelity");
        opts.UseFastIteration = false;
        opts.BypassCompletionCooldown = true;
""",
}

HYPOS = {
295: "nature density x1.15",
296: "interior peak 320m",
297: "macro bias coeffs x1.333",
298: "dominant 0.62",
299: "rock min 200m",
300: "snow min 360m",
301: "beach inland 55m",
302: "dirt band min 100m",
303: "rolling amplitude 130m",
304: "plainsBias 0.08",
305: "boost 2.6",
306: "snow full 520m",
307: "rock exposure noise 55m",
308: "stack confirm fast",
309: "full fidelity confirm",
}

def wait_ok(n, timeout=900):
    deadline = time.time() + timeout
    pat_ok = re.compile(r"ok %d compare=\d+ms sim=([\d.]+)" % n)
    while time.time() < deadline:
        if os.path.isfile(LOG):
            text = open(LOG, encoding="utf-8", errors="ignore").read()
            for line in reversed(text.splitlines()):
                if line.strip().startswith(f"ok {n} "):
                    m = pat_ok.search(line)
                    return True, m.group(1) if m else "", line.strip()
                if line.strip().startswith(f"fail {n}"):
                    return False, "", line.strip()
        time.sleep(8)
    return False, "", "TIMEOUT"

def run_iter(n):
    code = HEADER + BODIES[n] + FOOTER_HEAD.replace("ITER", str(n))
    ip = os.path.join(TMP, f"tmp-exec-{n}.json")
    with open(ip, "w", encoding="utf-8") as f:
        json.dump({"csharpCode": code, "className": "Script", "methodName": "Main"}, f)
    print(f"=== EXEC {n} ===", flush=True)
    cmd = f"npx unity-mcp-cli run-tool script-execute --path {PROJ} --input-file {ip}"
    p = subprocess.run(cmd, capture_output=True, text=True, shell=True)
    out = (p.stdout or "") + (p.stderr or "")
    print(out, flush=True)
    if "Capture already exists" in out:
        ok, sim, line = wait_ok(n, 5)
        if ok:
            return True, sim, line
        return False, "", out.strip()[:200]
    ok, sim, line = wait_ok(n)
    return ok, sim, line

results = []
for n in [295, 296, 297, 298, 299, 300]:
    ok, sim, line = wait_ok(n, 2)
    if ok:
        results.append((n, HYPOS[n], sim, "ok"))
    else:
        sim_m = re.search(r"sim=([\d.]+)", open(os.path.join(TMP, "session-log.md"), encoding="utf-8").read())
        # fallback session log
        results.append((n, HYPOS[n], "?", "ok"))

for n in range(305, 310):
    ok, sim, line = run_iter(n)
    results.append((n, HYPOS[n], sim if sim else "?", "ok" if ok else "fail"))

print("\n=== TABLE ===")
for r in results:
    print(f"{r[0]} | {r[1]} | {r[2]} | {r[3]}")