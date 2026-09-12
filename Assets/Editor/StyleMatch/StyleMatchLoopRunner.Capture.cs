#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor.StyleMatch
{
    public sealed partial class StyleMatchLoopRunner
    {
        private Camera _captureCamera;

        private void EnsureCaptureCamera()
        {
            if (!StyleMatchCaptureRig.TryAlignCaptureCamera(out _captureCamera))
            {
                _captureCamera = null;
                Debug.LogWarning("[StyleMatch] Capture camera unavailable — ensure World Generation scene has terrain.");
            }
        }

        private bool TryCaptureIterationScreenshot(int iter, out string error)
        {
            error = null;
            EnsureCaptureCamera();
            if (_captureCamera == null || !_captureCamera)
            {
                error = "No terrain bounds for capture camera.";
                Debug.LogWarning("[StyleMatch] Capture skipped — " + error + " iter " + iter);
                return false;
            }

            CaptureIterationScreenshot(iter);
            return true;
        }

        private void CaptureIterationScreenshot(int iter)
        {
            EnsureCaptureCamera();
            if (_captureCamera == null || !_captureCamera)
            {
                Debug.LogWarning("[StyleMatch] Capture skipped — no terrain or camera for iter " + iter);
                return;
            }

            Directory.CreateDirectory(OutputDir);
            var path = Path.Combine(OutputDir, $"iter-{iter:D3}.png");
            StyleMatchCaptureRig.PrepareSceneForCapture(_captureCamera);

            var fogEnabled = RenderSettings.fog;
            var fogDensity = RenderSettings.fogDensity;
            RenderSettings.fog = false;

            var rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var prevTarget = _captureCamera.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                _captureCamera.targetTexture = rt;
                _captureCamera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log("[StyleMatch] Screenshot: " + path + " bytes=" + new FileInfo(path).Length);
            }
            finally
            {
                _captureCamera.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                RenderSettings.fog = fogEnabled;
                RenderSettings.fogDensity = fogDensity;
            }
        }
    }
}
#endif
