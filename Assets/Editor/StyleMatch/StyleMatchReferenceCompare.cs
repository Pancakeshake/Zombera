#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>Fast downsampled pixel compare of capture vs reference.png (timed in ms).</summary>
    internal static class StyleMatchReferenceCompare
    {
        private const int CompareWidth = 128;
        private const int CompareHeight = 72;

        public static bool TryCompare(
            string capturePath,
            string referencePath,
            out StyleMatchReferenceCompareResult result)
        {
            result = default;
            if (string.IsNullOrEmpty(capturePath) || !File.Exists(capturePath))
                return false;

            if (string.IsNullOrEmpty(referencePath) || !File.Exists(referencePath))
                referencePath = ResolveDefaultReferencePath();

            if (!File.Exists(referencePath))
                return false;

            var sw = Stopwatch.StartNew();
            try
            {
                var capture = LoadAndResize(capturePath);
                var reference = LoadAndResize(referencePath);
                if (capture == null || reference == null)
                    return false;

                var mae = ComputeMeanAbsoluteError(capture, reference);
                UnityEngine.Object.DestroyImmediate(capture);
                UnityEngine.Object.DestroyImmediate(reference);

                sw.Stop();
                result.CompareMs = sw.ElapsedMilliseconds;
                result.MeanAbsoluteError = mae;
                result.SimilarityScore = Mathf.Clamp01(1f - mae / 255f);
                result.CapturePath = capturePath;
                result.ReferencePath = referencePath;
                return true;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.CompareMs = sw.ElapsedMilliseconds;
                result.Error = ex.Message;
                return false;
            }
        }

        public static string ResolveDefaultReferencePath()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(repoRoot, "reference.png");
        }

        private static Texture2D LoadAndResize(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return null;
            }

            var rt = RenderTexture.GetTemporary(CompareWidth, CompareHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var resized = new Texture2D(CompareWidth, CompareHeight, TextureFormat.RGBA32, false);
            resized.ReadPixels(new Rect(0, 0, CompareWidth, CompareHeight), 0, 0);
            resized.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            return resized;
        }

        private static float ComputeMeanAbsoluteError(Texture2D a, Texture2D b)
        {
            var w = Mathf.Min(a.width, b.width);
            var h = Mathf.Min(a.height, b.height);
            var pixelsA = a.GetPixels32();
            var pixelsB = b.GetPixels32();
            var count = w * h;
            if (count <= 0)
                return 255f;

            double sum = 0d;
            for (var i = 0; i < count; i++)
            {
                var ca = pixelsA[i];
                var cb = pixelsB[i];
                sum += Math.Abs(ca.r - cb.r);
                sum += Math.Abs(ca.g - cb.g);
                sum += Math.Abs(ca.b - cb.b);
            }

            return (float)(sum / (count * 3d));
        }
    }

    internal struct StyleMatchReferenceCompareResult
    {
        public long CompareMs;
        public float MeanAbsoluteError;
        public float SimilarityScore;
        public string CapturePath;
        public string ReferencePath;
        public string Error;
    }
}
#endif
