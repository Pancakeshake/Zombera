#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>
    ///     Scene-persisted framing camera for full-island style-match captures.
    /// </summary>
    public static partial class StyleMatchCaptureRig
    {
        public const string RigRootName = "StyleMatchRig";
        public const string CameraObjectName = "StyleMatchCamera";
        public const string RigHierarchyPath = "WorldGen/" + RigRootName + "/" + CameraObjectName;

        private const float PitchDegrees = 48f;
        private const float YawDegrees = 22f;
        private const float OceanPadFraction = 0.32f;
        private const float FrameMargin = 1.22f;
        private const float VerticalFovDegrees = 42f;
        private const float CaptureAspect = 1920f / 1080f;

        public static bool EnsureSceneRig()
        {
            var cameraGo = FindOrCreateCameraObject();
            if (cameraGo == null)
                return false;

            ConfigureFramingCamera(cameraGo.GetComponent<Camera>());
            AlignCameraTransform(cameraGo.transform);
            MarkSceneDirty(cameraGo.scene);
            Debug.Log("[StyleMatch] Capture rig ready at " + RigHierarchyPath);
            return true;
        }

        public static bool TryGetCaptureCamera(out Camera camera)
        {
            camera = null;
            var cameraGo = GameObject.Find(RigHierarchyPath);
            if (cameraGo == null)
                cameraGo = GameObject.Find(CameraObjectName);

            if (cameraGo == null || !cameraGo.TryGetComponent(out camera))
                return false;

            return true;
        }

        public static bool TryAlignCaptureCamera(out Camera camera)
        {
            if (!TryGetCaptureCamera(out camera))
            {
                if (!EnsureSceneRig() || !TryGetCaptureCamera(out camera))
                    return false;
            }

            if (!TryResolveCaptureBounds(out var min, out var max))
                return false;

            ApplyFraming(camera.transform, min, max);
            ConfigureFramingCamera(camera);
            return true;
        }

        internal static bool TryResolveCaptureBounds(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            if (!TryResolveIslandBoundsXZ(out var islandXZ, out var terrains))
                return false;

            if (!TryUnionTerrainHeights(min, max, out min, out max, terrains))
            {
                min.y = 0f;
                max.y = 400f;
            }

            var padX = islandXZ.width * OceanPadFraction;
            var padZ = islandXZ.height * OceanPadFraction;
            min.x = islandXZ.xMin - padX;
            max.x = islandXZ.xMax + padX;
            min.z = islandXZ.yMin - padZ;
            max.z = islandXZ.yMax + padZ;
            return min.x < max.x && min.z < max.z;
        }

        private static GameObject FindOrCreateCameraObject()
        {
            var worldGen = GameObject.Find("WorldGen");
            if (worldGen == null)
            {
                Debug.LogError("[StyleMatch] WorldGen root not found — open World Generation scene.");
                return null;
            }

            var rigRoot = worldGen.transform.Find(RigRootName)?.gameObject;
            if (rigRoot == null)
            {
                rigRoot = new GameObject(RigRootName);
                Undo.RegisterCreatedObjectUndo(rigRoot, "Create StyleMatch Rig");
                rigRoot.transform.SetParent(worldGen.transform, false);
            }

            var cameraGo = rigRoot.transform.Find(CameraObjectName)?.gameObject;
            if (cameraGo == null)
            {
                cameraGo = new GameObject(CameraObjectName);
                Undo.RegisterCreatedObjectUndo(cameraGo, "Create StyleMatch Camera");
                cameraGo.transform.SetParent(rigRoot.transform, false);
                cameraGo.AddComponent<Camera>();
            }

            if (!cameraGo.TryGetComponent<Camera>(out _))
                cameraGo.AddComponent<Camera>();

            return cameraGo;
        }

        private static void ConfigureFramingCamera(Camera camera)
        {
            if (camera == null)
                return;

            camera.enabled = true;
            camera.orthographic = false;
            camera.nearClipPlane = 0.3f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.depth = -100;
            camera.tag = "Untagged";
            camera.cullingMask = ~0;
            camera.useOcclusionCulling = false;
        }

        private static void AlignCameraTransform(Transform cameraTransform)
        {
            if (!TryResolveCaptureBounds(out var min, out var max))
            {
                cameraTransform.position = new Vector3(4000f, 4500f, 2500f);
                cameraTransform.rotation = Quaternion.Euler(PitchDegrees, YawDegrees, 0f);
                return;
            }

            ApplyFraming(cameraTransform, min, max);
        }

        private static void ApplyFraming(Transform cameraTransform, Vector3 min, Vector3 max)
        {
            var center = (min + max) * 0.5f;
            var span = Mathf.Max(max.x - min.x, max.z - min.z);
            var focus = new Vector3(center.x, Mathf.Lerp(min.y, max.y, 0.3f), center.z);
            var rotation = Quaternion.Euler(PitchDegrees, YawDegrees, 0f);
            var lookDirection = rotation * Vector3.forward;
            var framedSpan = span * FrameMargin;
            var halfHorizFovRad = Mathf.Atan(
                Mathf.Tan(VerticalFovDegrees * 0.5f * Mathf.Deg2Rad) * CaptureAspect);
            var distance = (framedSpan * 0.5f) / Mathf.Max(halfHorizFovRad, 0.01f);
            distance *= 1.18f;

            cameraTransform.rotation = rotation;
            cameraTransform.position = focus - lookDirection * distance + Vector3.up * (span * 0.06f);

            if (!cameraTransform.TryGetComponent<Camera>(out var camera))
                return;

            camera.orthographic = false;
            camera.fieldOfView = VerticalFovDegrees;
            camera.farClipPlane = distance + span * 3f;
            Debug.Log(
                $"[StyleMatch] Capture island span={span:F0} center={focus} fov={VerticalFovDegrees:F0} dist={distance:F0}");
        }

        private static bool TryResolveIslandBoundsXZ(out Rect boundsXZ, out List<Terrain> terrains)
        {
            terrains = ResolveAllTerrains();
            var service = Object.FindFirstObjectByType<WorldBuilderService>(FindObjectsInactive.Include);
            if (service?.Session != null)
            {
                var scope = GameObject.Find("WorldGen/WorldBuilderStack/WorldTerrainGrid")?.transform;
                boundsXZ = WorldTerrainBoundsResolver.Resolve(service.TileCatalog, service.Session, scope);
                if (boundsXZ.width > 0f && boundsXZ.height > 0f)
                    return true;
            }

            if (WorldTileInfoUtility.TryGetWorldTerrainGridBounds(out boundsXZ))
                return true;

            return terrains.Count > 0 &&
                   WorldTileInfoUtility.TryUnionTerrainBounds(terrains, out boundsXZ);
        }

        private static List<Terrain> ResolveAllTerrains()
        {
            if (WorldTileInfoUtility.TryGetPinnedTerrains(out var pinned) &&
                pinned != null &&
                pinned.Count > 0)
            {
                return pinned;
            }

            if (WorldTileInfoUtility.TryGetWorldTerrainGridTerrains(out var grid) &&
                grid != null &&
                grid.Count > 0)
            {
                return grid;
            }

            return CollectActiveTerrains();
        }

        private static List<Terrain> CollectActiveTerrains()
        {
            var all = Terrain.activeTerrains;
            var list = new List<Terrain>(all?.Length ?? 0);
            if (all == null)
                return list;

            for (var i = 0; i < all.Length; i++)
            {
                var terrain = all[i];
                if (terrain?.terrainData == null)
                    continue;

                list.Add(terrain);
            }

            return list;
        }

        private static bool TryUnionTerrainHeights(
            Vector3 min,
            Vector3 max,
            out Vector3 outMin,
            out Vector3 outMax,
            List<Terrain> terrains)
        {
            outMin = min;
            outMax = max;
            var found = false;

            for (var i = 0; i < terrains.Count; i++)
            {
                var terrain = terrains[i];
                if (terrain?.terrainData == null)
                    continue;

                var bounds = terrain.terrainData.bounds;
                var pos = terrain.transform.position;
                outMin.y = Mathf.Min(outMin.y, pos.y + bounds.min.y);
                outMax.y = Mathf.Max(outMax.y, pos.y + bounds.max.y);
                found = true;
            }

            return found;
        }

        private static void MarkSceneDirty(Scene scene)
        {
            if (!scene.IsValid())
                return;

            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
