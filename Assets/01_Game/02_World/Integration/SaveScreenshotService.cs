using UnityEngine;
using System;
using Zombera.Characters;
using Zombera.UI.SquadManagement;

namespace Zombera.Systems
{
    /// <summary>
    ///     Utility for capturing gameplay screenshots as Base64 strings for save game metadata.
    /// </summary>
    public static class SaveScreenshotService
    {
        private static readonly int TargetWidth = 512;
        private static readonly int TargetHeight = 288;

        /// <summary>
        ///     Captures the main camera's current view and returns a Base64 encoded PNG.
        /// </summary>
        public static string CaptureMainCameraToBase64()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[SaveScreenshotService] No MainCamera found for capture.");
                return string.Empty;
            }

            var rt = new RenderTexture(TargetWidth, TargetHeight, 24);
            var prevRT = mainCamera.targetTexture;

            try
            {
                mainCamera.targetTexture = rt;
                mainCamera.Render();

                RenderTexture.active = rt;
                var screenShot = new Texture2D(TargetWidth, TargetHeight, TextureFormat.RGB24, false);
                screenShot.ReadPixels(new Rect(0, 0, TargetWidth, TargetHeight), 0, 0);
                screenShot.Apply();

                byte[] bytes = screenShot.EncodeToPNG();

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(screenShot);
                else
                    UnityEngine.Object.DestroyImmediate(screenShot);

                return Convert.ToBase64String(bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveScreenshotService] Screenshot capture failed: {e.Message}");
                return string.Empty;
            }
            finally
            {
                mainCamera.targetTexture = prevRT;
                RenderTexture.active = null;
                if (rt != null)
                {
                    rt.Release();
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(rt);
                    else
                        UnityEngine.Object.DestroyImmediate(rt);
                }
            }
        }

        /// <summary>
        ///     Captures a fresh preview for save metadata: gameplay camera first, then the live player portrait.
        /// </summary>
        public static string CaptureSaveSlotPreviewBase64()
        {
            var screenshot = CaptureMainCameraToBase64();
            if (!string.IsNullOrWhiteSpace(screenshot))
                return screenshot;

            var portraitStudio = PortraitStudioManager.Instance;
            if (portraitStudio == null)
                return string.Empty;

            var unitManager = UnityEngine.Object.FindFirstObjectByType<UnitManager>();
            var player = unitManager != null ? unitManager.FindFirstUnitByRole(UnitRole.Player) : null;
            if (player == null)
                return portraitStudio.CapturePortraitToBase64();

            return portraitStudio.CapturePortraitFromUnitRoot(player.gameObject);
        }
    }
}
