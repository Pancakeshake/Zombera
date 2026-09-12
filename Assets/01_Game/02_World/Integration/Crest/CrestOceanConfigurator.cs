using System.Reflection;
using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>Configures Crest ocean + edge water bodies after procedural spawn.</summary>
    internal static partial class CrestOceanConfigurator
    {
        private const string ClipSurfaceKeyword = "_CLIPSURFACE_ON";

        public static void Configure(
            OceanRenderer ocean,
            float seaLevelWorldY,
            WorldWaterProfile water = null,
            Rect coreBoundsXZ = default,
            int oceanRingTiles = 0)
        {
            if (ocean == null)
                return;

            var transform = ocean.transform;
            var pos = transform.position;
            pos.y = seaLevelWorldY;
            transform.position = pos;

            var camera = ResolveViewCamera();
            if (camera != null)
            {
                ocean.ViewCamera = camera;
                EnsureUnderwaterRenderer(camera);
            }

            BindPrimaryLight(ocean);

            ApplyBoundedOceanSettings(ocean, water);
            EnsureWaveShapes(ocean.transform, water, coreBoundsXZ, oceanRingTiles);
        }

        /// <summary>
        /// One-shot rebind after Enviro/Hub publishes <see cref="RenderSettings.sun"/>.
        /// Safe when no OceanRenderer exists yet.
        /// </summary>
        public static void RebindPrimaryLightFromRenderSettings()
        {
            var light = ResolvePrimaryDirectionalLight();
            if (light == null)
                return;

            var oceans = Object.FindObjectsByType<OceanRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < oceans.Length; i++)
            {
                var ocean = oceans[i];
                if (ocean == null)
                    continue;
                ocean._primaryLight = light;
            }
        }

        public static void BindPrimaryLight(OceanRenderer ocean)
        {
            if (ocean == null)
                return;

            var light = ResolvePrimaryDirectionalLight();
            if (light != null)
                ocean._primaryLight = light;
        }

        public static void ApplyBoundedOceanSettings(OceanRenderer ocean, WorldWaterProfile water = null)
        {
            if (ocean == null)
                return;

            ocean._waterBodyCulling = true;
            SetPrivateField(ocean, "_createClipSurfaceData", true);
            ocean._defaultClippingState = OceanRenderer.DefaultClippingState.EverythingClipped;
            EnsureClipSurfaceMaterialKeyword(ocean.OceanMaterial);
            EnsureFlowAndFoam(ocean, water);
        }

        public static void SyncWaterBody(WaterBody waterBody)
        {
            if (waterBody == null)
                return;

            EnableEditModeLodInput(waterBody);

            waterBody.gameObject.SetActive(false);
            waterBody.gameObject.SetActive(true);
        }

        /// <summary>
        /// Lakes keep clip registration (docs). River corridor WaterBodies cull tiles only;
        /// precise include comes from Clip Surface ribbons.
        /// </summary>
        public static void ConfigureWaterBodyClipRegistration(WaterBody waterBody, bool registerWithClipSurface)
        {
            if (waterBody == null)
                return;
            SetPrivateField(waterBody, "_registerWithClipSurfaceData", registerWithClipSurface);
        }

        /// <summary>
        /// Hub sync-pumps skip Crest's deferred <c>runInEditMode</c> Invoke on LOD inputs
        /// (height/flow) and WaterBodies. Edit-mode only.
        /// </summary>
        public static void EnableEditModeLodInput(MonoBehaviour behaviour)
        {
            if (behaviour == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                behaviour.runInEditMode = true;
#endif
        }

        /// <summary>
        /// Hub sync-pumps skip Crest's deferred <c>runInEditMode</c> Invoke.
        /// Setting it here forces OnEnable so Root/tiles exist immediately.
        /// Edit-mode only. Never calls Rebuild (unsafe when Root is null).
        /// </summary>
        public static bool ForceEditModeReady(OceanRenderer ocean)
        {
            if (ocean == null)
                return false;

#if UNITY_EDITOR
            if (Application.isPlaying)
                return ocean.Root != null;

            if (!ocean.gameObject.activeInHierarchy)
                return false;

            ClearUtilityViewCamera(ocean);
            ocean.runInEditMode = true;
            HealBrokenLocalWaveShapes(ocean.transform);

            if (ocean.Root != null)
                return true;

            Debug.LogWarning(
                "[CrestOceanConfigurator] ForceEditModeReady: OceanRenderer has no Root after runInEditMode.",
                ocean);
            return false;
#else
            return ocean.Root != null;
#endif
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static Camera ResolveViewCamera()
        {
            if (Camera.main != null && !IsUtilityCaptureCamera(Camera.main))
                return Camera.main;

            var cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || !cam.enabled || cam.cameraType != CameraType.Game)
                    continue;
                if (IsUtilityCaptureCamera(cam))
                    continue;
                return cam;
            }

            return null;
        }

        private static void ClearUtilityViewCamera(OceanRenderer ocean)
        {
            if (ocean == null)
                return;

            var camera = ocean.ViewCameraExcludingSceneCamera;
            if (camera != null && IsUtilityCaptureCamera(camera))
                ocean.ViewCamera = null;
        }

        private static bool IsUtilityCaptureCamera(Camera camera)
        {
            if (camera == null)
                return false;

            if (camera.name == "StyleMatchCamera")
                return true;

            var t = camera.transform;
            while (t != null)
            {
                if (t.name == "StyleMatchRig")
                    return true;
                t = t.parent;
            }

            return false;
        }

        /// <summary>
        /// Prefer <see cref="RenderSettings.sun"/>, then brightest enabled directional
        /// (matches CrestWaterSetupTool).
        /// </summary>
        private static Light ResolvePrimaryDirectionalLight()
        {
            var sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional && sun.isActiveAndEnabled)
                return sun;

            var lights = Object.FindObjectsByType<Light>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Light best = null;
            var bestIntensity = float.NegativeInfinity;
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null || light.type != LightType.Directional || !light.enabled)
                    continue;
                if (light.intensity <= bestIntensity)
                    continue;
                best = light;
                bestIntensity = light.intensity;
            }

            return best;
        }

        private static void EnsureClipSurfaceMaterialKeyword(Material material)
        {
            if (material == null)
                return;

            material.EnableKeyword(ClipSurfaceKeyword);
            if (material.HasProperty("_Clipsurface"))
                material.SetFloat("_Clipsurface", 1f);
        }
    }
}
