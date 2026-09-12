using System.Collections;
using System.Collections.Generic;
using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>Crest ocean / edge WaterBody backend for map-edge ocean strips.</summary>
    [AddComponentMenu("Zombera/World/Crest Ocean Water Backend")]
    [DisallowMultipleComponent]
    public sealed partial class CrestOceanWaterBackend : MonoBehaviour, IOceanWaterRenderer, IWorldWaterProfileBinder
    {
        private const string OceanPrefabPath = "Assets/02_Shared/Prefabs/Systems/Environment/Crest Ocean.prefab";
        private const string WaterBodyPrefabPath = "Assets/02_Shared/Prefabs/Systems/Environment/Crest Water Body.prefab";
        private const string EdgeWaterBodiesFolderName = "EdgeWaterBodies";

        [SerializeField] private GameObject _oceanRendererPrefab;
        [SerializeField] private GameObject _waterBodyPrefab;
        [SerializeField] private float _seaLevelWorldY;
        [SerializeField] private float _outerExtensionMeters = OceanWaterBodyPlacementUtility.DefaultOuterExtensionMeters;
        [SerializeField] private Transform _root;

        private readonly List<WaterBody> _waterBodies = new();

        private GameObject _oceanInstance;
        private OceanRenderer _oceanRenderer;
        private WorldWaterProfile _waterProfile;
        private Rect _lastCoreBoundsXZ;
        private Rect _lastDepthBoundsXZ;
        private int _lastOceanRingTiles;

        public bool HasActiveOcean =>
            _oceanInstance != null
            && _oceanRenderer != null
            && _oceanInstance.activeInHierarchy
            && _oceanRenderer.Root != null;

        public float CurrentSeaLevelWorldY => _seaLevelWorldY;

        public OceanRenderer OceanRenderer => _oceanRenderer;

        public GameObject OceanInstance => _oceanInstance;

        public bool TryGetOceanRoot(out Transform root)
        {
            EnsureRoot();
            root = _root;
            return root != null;
        }

        public GameObject ResolveWaterBodyPrefab()
        {
            if (_waterBodyPrefab != null)
                return _waterBodyPrefab;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(WaterBodyPrefabPath);
#else
            return null;
#endif
        }

        public Transform EnsureEdgeWaterBodiesFolder() => EnsureChildFolder(EdgeWaterBodiesFolderName);

        private Transform EnsureChildFolder(string folderName)
        {
            if (!TryGetOceanRoot(out var root))
                return null;

            var existing = root.Find(folderName);
            if (existing != null)
                return existing;

            var folder = new GameObject(folderName);
            folder.transform.SetParent(root, false);
            return folder.transform;
        }

        private void ClearChildFolder(string folderName)
        {
            if (_root == null)
                return;

            var child = _root.Find(folderName);
            if (child == null)
                return;

            for (var i = child.childCount - 1; i >= 0; i--)
            {
                var childTransform = child.GetChild(i);
                if (!IsOwnedGeneratedChild(folderName, childTransform.name))
                    continue;
                var go = childTransform.gameObject;
                DestroyInstance(ref go);
            }

            if (!Application.isPlaying && child.childCount == 0)
            {
                var folder = child.gameObject;
                DestroyInstance(ref folder);
            }
        }

        private static bool IsOwnedGeneratedChild(string folderName, string childName)
        {
            if (string.IsNullOrEmpty(childName))
                return false;
            if (folderName == EdgeWaterBodiesFolderName)
                return childName.StartsWith("CrestWaterBody_", System.StringComparison.Ordinal);
            if (folderName == "Lakes")
                return childName.StartsWith("Lake_", System.StringComparison.Ordinal) ||
                       childName.StartsWith("CrestLake_", System.StringComparison.Ordinal);
            if (folderName == "Rivers")
                return childName.StartsWith("RiverSystem_", System.StringComparison.Ordinal) ||
                       childName.StartsWith("CrestRiver_", System.StringComparison.Ordinal);
            if (folderName == "WaterBodies")
                return childName.StartsWith("Lake_", System.StringComparison.Ordinal) ||
                       childName.StartsWith("River_", System.StringComparison.Ordinal) ||
                       childName.StartsWith("CrestLake_", System.StringComparison.Ordinal) ||
                       childName.StartsWith("CrestRiver_", System.StringComparison.Ordinal) ||
                       childName.StartsWith("RiverSystem_", System.StringComparison.Ordinal);
            return false;
        }

        public IEnumerator BuildOceanSurfaces(OceanSurfaceBuildRequest request) =>
            BuildSurfaces(request);

        public void ClearOcean(WorldBuildScope scope) => Clear(scope);

        public void TearDownOcean() => TearDown();

        public void RebindPrimaryLight() =>
            CrestOceanConfigurator.RebindPrimaryLightFromRenderSettings();

        public IEnumerator BuildSurfaces(OceanSurfaceBuildRequest request)
        {
            Clear(request.Scope);
            DestroyOceanInstance();

            var hydrology = request.Hydrology;
            var hasHydrologyOcean = HasOcean(hydrology);
            var hasLayoutOcean = HasLayoutOcean(request.BoundaryLayout);
            var hasInlandWater = HasInlandWater(hydrology);
            if (!hasHydrologyOcean && !hasLayoutOcean && !hasInlandWater)
                yield break;

            EnsureRoot();
            RemoveForeignOceanRenderers();

            _lastCoreBoundsXZ = request.CoreWorldBoundsXZ;
            _lastOceanRingTiles = request.OceanRingTiles;

            var seaLevel = ResolveSeaLevel(hydrology);
            if (!PrepareOcean(seaLevel))
            {
                Debug.LogWarning("[CrestOceanWaterBackend] Failed to instantiate Crest Ocean.", this);
                yield break;
            }

            CrestOceanConfigurator.EnsureFlowAndFoam(_oceanRenderer, _waterProfile);

            if (hasHydrologyOcean || hasLayoutOcean)
                SpawnEdgeWaterBodies(request, seaLevel);

            var depthBounds = request.SurfaceBoundsXZ.width > 0f
                ? request.SurfaceBoundsXZ
                : request.Scope.BoundsXZ;

            ActivateOcean();
            EnsureSeaFloorTileBinder().BindProfile(_waterProfile);
            RefreshOceanDepthCache(depthBounds, seaLevel);
            ActivateWaterBodies();
            SyncAllWaterBodiesUnderRoot();

            LogWaterBodyState(depthBounds);

            yield return null;
            yield return null;

            // Terrain may finish applying after the ocean stage; refresh depth once more.
            RefreshOceanDepthCache(depthBounds, seaLevel);
        }

        public void Clear(WorldBuildScope scope)
        {
            _ = scope;
            DestroyWaterBodiesInFolder(EnsureEdgeWaterBodiesFolder());
            ClearChildFolder(EdgeWaterBodiesFolderName);
        }

        public void TearDown()
        {
            Clear(default);
            DestroyOceanInstance();
            if (_root != null)
            {
                var rootGo = _root.gameObject;
                _root = null;
                DestroyInstance(ref rootGo);
            }
        }

        public void Shutdown() => TearDown();

        private float ResolveSeaLevel(HydrologyPlan hydrology)
        {
            if (hydrology?.WaterClass != null && hydrology.SurfaceWorldY != null)
            {
                for (var i = 0; i < hydrology.WaterClass.Length; i++)
                {
                    if (hydrology.WaterClass[i] != WorldWaterClass.Ocean)
                        continue;
                    return hydrology.SurfaceWorldY[i];
                }
            }

            return _seaLevelWorldY;
        }

        private void SpawnEdgeWaterBodies(OceanSurfaceBuildRequest request, float seaLevel)
        {
            var prefab = _waterBodyPrefab;
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(WaterBodyPrefabPath);
#endif
            if (prefab == null)
                return;

            var bounds = request.SurfaceBoundsXZ.width > 0f
                ? request.SurfaceBoundsXZ
                : request.Scope.BoundsXZ;
            if (bounds.width <= 0f || bounds.height <= 0f)
                return;

            DestroyUntrackedWaterBodies();

            var outerExtension = OceanWaterBodyPlacementUtility.ResolveOuterExtensionMeters(
                request.OceanRingTiles,
                _outerExtensionMeters);

            var placements = new List<OceanWaterBodyPlacementUtility.EdgePlacement>(4);
            OceanWaterBodyPlacementUtility.CollectPlacements(
                bounds,
                seaLevel,
                request.BoundaryLayout,
                request.StripDepthMeters,
                placements,
                outerExtension);

            if (placements.Count == 0)
            {
                Debug.LogWarning(
                    "[CrestOceanWaterBackend] Hydrology contains ocean cells but no ocean map edges were configured.",
                    this);
                return;
            }

            var edgeFolder = EnsureEdgeWaterBodiesFolder();
            if (edgeFolder == null)
                return;

            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var instance = Instantiate(prefab, edgeFolder);
                var isFullMap = OceanWaterBodyPlacementUtility.IsAllOcean(request.BoundaryLayout) &&
                                placements.Count == 1;
                instance.name = isFullMap
                    ? "CrestWaterBody_Map"
                    : $"CrestWaterBody_{placement.Side}";
                instance.SetActive(false);
                instance.transform.SetPositionAndRotation(placement.Center, Quaternion.identity);
                instance.transform.localScale = placement.Scale;

                var waterBody = instance.GetComponent<WaterBody>();
                if (waterBody == null)
                {
                    DestroyInstance(ref instance);
                    continue;
                }

                // Cull tiles only — full-AABB clip include fights inland river/lake ribbons
                // under Everything Clipped (Crest water-bodies docs: Clip Surface for edges).
                CrestOceanConfigurator.ConfigureWaterBodyClipRegistration(
                    waterBody,
                    registerWithClipSurface: false);

                _waterBodies.Add(waterBody);
            }
        }

        private void DestroyWaterBodiesInFolder(Transform folder)
        {
            if (folder == null) return;
            for (var i = _waterBodies.Count - 1; i >= 0; i--)
            {
                var waterBody = _waterBodies[i];
                if (waterBody == null)
                {
                    _waterBodies.RemoveAt(i);
                    continue;
                }
                if (!waterBody.transform.IsChildOf(folder))
                    continue;

                _waterBodies.RemoveAt(i);

                var instance = waterBody.gameObject;
                DestroyInstance(ref instance);
            }
        }

        private void DestroyUntrackedWaterBodies()
        {
            DestroyWaterBodiesInFolder(EnsureEdgeWaterBodiesFolder());
            ClearChildFolder(EdgeWaterBodiesFolderName);
        }

        private void RemoveForeignOceanRenderers()
        {
#if UNITY_2022_2_OR_NEWER
            var renderers = Object.FindObjectsByType<OceanRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var renderers = Object.FindObjectsOfType<OceanRenderer>(true);
#endif
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (_oceanRenderer != null && renderer == _oceanRenderer)
                    continue;

                if (renderer.transform.IsChildOf(_root))
                    continue;

                var foreign = renderer.gameObject;
                Debug.Log(
                    "[CrestOceanWaterBackend] Removing scene OceanRenderer outside Oceans hierarchy: " +
                    foreign.name,
                    foreign);
                DestroyInstance(ref foreign);
            }
        }

        private void DestroyOceanInstance()
        {
            _oceanRenderer = null;
            DestroyInstance(ref _oceanInstance);
        }

        private void EnsureRoot()
        {
            if (_root != null)
                return;

            var existing = transform.Find("Oceans");
            if (existing != null)
            {
                _root = existing;
                return;
            }

            var go = new GameObject("Oceans");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }

        private static bool HasOcean(HydrologyPlan hydrology)
        {
            if (hydrology?.WaterClass == null)
                return false;

            for (var i = 0; i < hydrology.WaterClass.Length; i++)
            {
                if (hydrology.WaterClass[i] == WorldWaterClass.Ocean)
                    return true;
            }

            return false;
        }

        private static bool HasInlandWater(HydrologyPlan hydrology)
        {
            if (hydrology == null)
                return false;
            if (hydrology.Rivers != null && hydrology.Rivers.Length > 0)
                return true;
            if (hydrology.Lakes != null && hydrology.Lakes.Length > 0)
                return true;
            return false;
        }

        private static bool HasLayoutOcean(WorldMapBoundaryLayout layout) =>
            layout.West == WorldMapBoundaryKind.Ocean ||
            layout.East == WorldMapBoundaryKind.Ocean ||
            layout.South == WorldMapBoundaryKind.Ocean ||
            layout.North == WorldMapBoundaryKind.Ocean;

        private static void DestroyInstance(ref GameObject instance)
        {
            if (instance == null)
                return;

            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
            instance = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.85f);
            for (var i = 0; i < _waterBodies.Count; i++)
            {
                var waterBody = _waterBodies[i];
                if (waterBody == null)
                    continue;

                var aabb = waterBody.AABB;
                Gizmos.DrawWireCube(aabb.center, new Vector3(aabb.size.x, 1f, aabb.size.z));
            }
        }
#endif
    }
}
