#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>
    /// Ocean / lake / river capture for the Large hydrology auto-tune loop.
    /// Uses dedicated cameras (not SceneView.LookAt).
    /// </summary>
    public static class RiverHydrologyCaptureRunner
    {
        public const long MinCaptureBytes = 200_000L;
        public const string CaptureRelativeDir = "Library/StyleMatch/river-captures";

        public sealed class FocusOverrides
        {
            public Vector3? RiverWorld;
            public Vector3? LakeWorld;
            public Vector3? OceanShoreWorld;
            public Vector3? AerialLookAt;
        }

        public sealed class CaptureResult
        {
            public bool Ok;
            public string Error;
            public string MetaPath;
            public string AerialPath;
            public string RiverCloseupPath;
            public string LakeCloseupPath;
            public string OceanShorePath;
            public Vector3 RiverFocus;
            public Vector3 LakeFocus;
            public Vector3 OceanShoreFocus;
            public Vector3 AerialLookAt;
            public bool RiverMissing;
            public bool LakeMissing;
            public bool OceanMissing;
        }

        public static CaptureResult Capture(
            int iterationIndex,
            FocusOverrides focusOverrides = null,
            long minBytes = MinCaptureBytes)
        {
            var result = new CaptureResult();
            var outDir = Path.Combine(CaptureRelativeDir.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(outDir);

            var tag = "iter-" + iterationIndex.ToString("D3", CultureInfo.InvariantCulture);
            var inland = FindInlandBodies();
            var ocean = GameObject.Find("CrestOcean") ?? FindByNameContains("Ocean");

            var riverFocusGo = PickRiver(inland);
            var lakeFocusGo = PickLake(inland);

            var riverPos = ResolveFocus(
                focusOverrides?.RiverWorld,
                riverFocusGo != null ? riverFocusGo.transform.position : (Vector3?)null);
            var lakePos = ResolveFocus(
                focusOverrides?.LakeWorld,
                lakeFocusGo != null ? lakeFocusGo.transform.position : (Vector3?)null);

            var aerialLook = focusOverrides?.AerialLookAt ?? EstimateAerialLookAt(inland, ocean);
            var oceanShore = ResolveFocus(
                focusOverrides?.OceanShoreWorld,
                EstimateOceanShore(ocean, aerialLook));

            result.RiverMissing = !riverPos.HasValue;
            result.LakeMissing = !lakePos.HasValue;
            result.OceanMissing = ocean == null || !oceanShore.HasValue;
            result.AerialLookAt = aerialLook;
            if (riverPos.HasValue)
                result.RiverFocus = riverPos.Value;
            if (lakePos.HasValue)
                result.LakeFocus = lakePos.Value;
            if (oceanShore.HasValue)
                result.OceanShoreFocus = oceanShore.Value;

            var sb = new StringBuilder();
            sb.AppendLine("inlandCount=" + inland.Count);
            for (var i = 0; i < inland.Count && i < 12; i++)
            {
                var t = inland[i].transform.position;
                sb.AppendLine(
                    inland[i].name + " pos=" +
                    t.x.ToString("F1", CultureInfo.InvariantCulture) + "," +
                    t.y.ToString("F1", CultureInfo.InvariantCulture) + "," +
                    t.z.ToString("F1", CultureInfo.InvariantCulture));
            }

            if (ocean != null)
                sb.AppendLine("ocean=" + ocean.name + " pos=" + FormatVec(ocean.transform.position));
            else
                sb.AppendLine("ocean=MISSING");

            var paths = new List<string>(4);
            result.AerialPath = Path.Combine(outDir, tag + "-aerial.png");
            paths.Add(CapturePng(
                result.AerialPath,
                aerialLook + new Vector3(0f, 4200f, 0f),
                aerialLook,
                60f));

            if (riverPos.HasValue)
            {
                var bank = riverPos.Value;
                result.RiverCloseupPath = Path.Combine(outDir, tag + "-river-closeup.png");
                paths.Add(CapturePng(
                    result.RiverCloseupPath,
                    bank + new Vector3(-18f, 8f, -22f),
                    bank + Vector3.up * 1.5f,
                    50f));
                sb.AppendLine(
                    "riverFocus=" + (riverFocusGo != null ? riverFocusGo.name : "override") +
                    " pos=" + FormatVec(bank));
            }
            else
            {
                sb.AppendLine("riverFocus=MISSING");
            }

            if (lakePos.HasValue)
            {
                var p = lakePos.Value;
                result.LakeCloseupPath = Path.Combine(outDir, tag + "-lake-closeup.png");
                paths.Add(CapturePng(
                    result.LakeCloseupPath,
                    p + new Vector3(-28f, 14f, -28f),
                    p + Vector3.up * 1.0f,
                    50f));
                sb.AppendLine(
                    "lakeFocus=" + (lakeFocusGo != null ? lakeFocusGo.name : "override") +
                    " pos=" + FormatVec(p));
            }
            else
            {
                sb.AppendLine("lakeFocus=MISSING");
            }

            if (oceanShore.HasValue && ocean != null)
            {
                var coast = oceanShore.Value;
                result.OceanShorePath = Path.Combine(outDir, tag + "-ocean-shore.png");
                paths.Add(CapturePng(
                    result.OceanShorePath,
                    coast + new Vector3(-40f, 25f, -60f),
                    coast,
                    50f));
                sb.AppendLine("oceanShore=ok pos=" + FormatVec(coast));
            }
            else
            {
                sb.AppendLine("oceanShore=MISSING");
            }

            result.MetaPath = Path.Combine(outDir, tag + "-meta.txt");
            File.WriteAllText(result.MetaPath, sb.ToString());

            if (result.RiverMissing)
            {
                result.Ok = false;
                result.Error = "river=MISSING";
                return result;
            }

            if (result.OceanMissing)
            {
                result.Ok = false;
                result.Error = "ocean=MISSING";
                return result;
            }

            if (!AllCapturesMeetMinBytes(paths, minBytes, out var bytesError))
            {
                result.Ok = false;
                result.Error = bytesError;
                return result;
            }

            result.Ok = true;
            return result;
        }

        private static bool AllCapturesMeetMinBytes(List<string> paths, long minBytes, out string error)
        {
            error = null;
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (!File.Exists(path))
                {
                    error = "missing capture file: " + path;
                    return false;
                }

                var len = new FileInfo(path).Length;
                if (len < minBytes)
                {
                    error = "capture too small (" + len + " < " + minBytes + "): " + path;
                    return false;
                }
            }

            return true;
        }

        private static Vector3? ResolveFocus(Vector3? overridePos, Vector3? discovered)
        {
            if (overridePos.HasValue)
                return overridePos;
            return discovered;
        }

        private static Vector3 EstimateAerialLookAt(List<GameObject> inland, GameObject ocean)
        {
            if (inland.Count > 0)
            {
                var sum = Vector3.zero;
                for (var i = 0; i < inland.Count; i++)
                    sum += inland[i].transform.position;
                return sum / inland.Count;
            }

            if (ocean != null)
                return ocean.transform.position;

            return new Vector3(5000f, 0f, 5000f);
        }

        private static Vector3? EstimateOceanShore(GameObject ocean, Vector3 aerialLook)
        {
            if (ocean == null)
                return null;

            // Prefer a shore toward map interior from ocean root toward aerial land focus.
            var oceanPos = ocean.transform.position;
            var toLand = aerialLook - oceanPos;
            toLand.y = 0f;
            if (toLand.sqrMagnitude < 1f)
                toLand = new Vector3(1f, 0f, 0f);
            toLand.Normalize();
            return oceanPos + toLand * 800f + Vector3.up * 5f;
        }

        private static List<GameObject> FindInlandBodies()
        {
            var list = new List<GameObject>();
            var all = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var n = all[i].name;
                if (n.IndexOf("CrestWaterBody_Inland", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("Lake", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("River", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (all[i].position.magnitude > 50f)
                    list.Add(all[i].gameObject);
            }

            return list;
        }

        private static GameObject FindByNameContains(string token)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return all[i].gameObject;
            }

            return null;
        }

        private static GameObject PickRiver(List<GameObject> inland)
        {
            GameObject best = null;
            var bestScore = float.MinValue;
            for (var i = 0; i < inland.Count; i++)
            {
                var n = inland[i].name.ToLowerInvariant();
                var score = 0f;
                if (n.Contains("river"))
                    score += 100f;
                if (n.Contains("spline"))
                    score += 20f;
                score += inland[i].transform.position.magnitude * 0.001f;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = inland[i];
            }

            return best;
        }

        private static GameObject PickLake(List<GameObject> inland)
        {
            GameObject best = null;
            var bestScore = float.MinValue;
            for (var i = 0; i < inland.Count; i++)
            {
                var n = inland[i].name.ToLowerInvariant();
                var score = 0f;
                if (n.Contains("lake"))
                    score += 100f;
                if (n.Contains("river"))
                    score -= 50f;
                score += inland[i].transform.localScale.x;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = inland[i];
            }

            return best;
        }

        private static string FormatVec(Vector3 v) =>
            v.x.ToString("F1", CultureInfo.InvariantCulture) + "," +
            v.y.ToString("F1", CultureInfo.InvariantCulture) + "," +
            v.z.ToString("F1", CultureInfo.InvariantCulture);

        private static string CapturePng(string path, Vector3 camPos, Vector3 lookAt, float fov)
        {
            var go = new GameObject("RiverHydroCam_Temp");
            var cam = go.AddComponent<Camera>();
            cam.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 50000f;
            go.transform.position = camPos;
            go.transform.LookAt(lookAt);

            const int w = 1920;
            const int h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);

            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
#endif
