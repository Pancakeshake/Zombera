#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor.StyleMatch
{
    public static partial class StyleMatchCaptureRig
    {
        public static void PrepareSceneForCapture(Camera framingCamera)
        {
            if (framingCamera == null)
                return;

            PrepareTerrainsForDraw();
            SyncSceneView(framingCamera);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void PrepareTerrainsForDraw()
        {
            var terrains = Terrain.activeTerrains;
            if (terrains == null)
                return;

            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null)
                    continue;

                terrain.drawHeightmap = true;
                terrain.drawTreesAndFoliage = true;
                terrain.drawInstanced = true;
            }
        }

        private static void SyncSceneView(Camera framingCamera)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                sceneView = EditorWindow.GetWindow<SceneView>();

            if (sceneView?.camera == null || framingCamera == null)
                return;

            if (!TryResolveCaptureBounds(out var min, out var max))
                return;

            var pivot = new Vector3(
                (min.x + max.x) * 0.5f,
                Mathf.Lerp(min.y, max.y, 0.3f),
                (min.z + max.z) * 0.5f);

            sceneView.orthographic = false;
            sceneView.cameraSettings.fieldOfView = framingCamera.fieldOfView;
            sceneView.cameraSettings.nearClip = framingCamera.nearClipPlane;
            sceneView.cameraSettings.farClip = framingCamera.farClipPlane;
            sceneView.LookAt(pivot, framingCamera.transform.rotation);
            sceneView.Repaint();
        }
    }
}
#endif
