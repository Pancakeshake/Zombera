using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Footpath, road-marking and street-lamp passes for the City Prefab hub.
    ///     Split out of the primary partial to stay under the 500-line file rule.
    /// </summary>
    public sealed partial class CityPrefabRoadNetworkBuilder : MonoBehaviour
    {
        /// <summary>
        ///     Step 2: Generate 2m footpath strips along street-facing district block outlines.
        ///     Requires named areas (Step: Generate Named Areas). Lots/fences recommended for fence gaps.
        /// </summary>
        [ContextMenu("Generate Footpaths")]
        public void GenerateFootpaths()
        {
            var areasRoot = transform.Find("CityNamedAreas");
            if (areasRoot == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No CityNamedAreas — generate named areas first.", this);
                return;
            }

            RefreshReferences();
            var settings = roadNetworkSettings != null
                ? roadNetworkSettings
                : Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
            if (settings == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] RoadNetworkSettings not found.", this);
                return;
            }

            if (!settings.spawnFootpathMeshes)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Footpaths disabled in RoadNetworkSettings (enable spawnFootpathMeshes).", this);
                return;
            }

            if (CityFootpathPlacer.ResolveFootpathMaterialForSettings(settings) == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No footpath material assigned (set footpathMaterial in RoadNetworkSettings).", this);
                return;
            }

            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Road content root not found — generate roads first.", this);
                return;
            }

            ClearFootpathsInternal(roadNetwork);

            var footpathsContainer = GetOrCreateFootpathsContainer(roadNetwork);
            if (footpathsContainer == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Failed to create Footpaths container.", this);
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(footpathsContainer.gameObject, "Generate Footpaths");
#endif

            var totalStrips = CityDistrictEdgeFootpathBuilder.Build(
                footpathsContainer, areasRoot, settings, ResolveGroundHeight);

            Debug.Log("[CityPrefabRoadNetworkBuilder] " + (totalStrips > 0
                ? "Placed district-edge footpath strips=" + totalStrips + "."
                : "No footpath strips placed (check named areas and settings)."), this);
        }

        /// <summary>
        ///     Remove generated footpath meshes from the scene.
        /// </summary>
        [ContextMenu("Clear Footpaths")]
        public void ClearFootpaths()
        {
            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null)
                return;

            ClearFootpathsInternal(roadNetwork);
        }

        private static void ClearFootpathsInternal(Transform networkRoot)
        {
            var container = networkRoot.Find("Footpaths");
            if (container == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(container.gameObject);
            else
                Object.DestroyImmediate(container.gameObject);
        }

        private static Transform GetOrCreateFootpathsContainer(Transform networkRoot)
        {
            var existing = networkRoot.Find("Footpaths");
            if (existing != null)
                return existing;

            var go = new GameObject("Footpaths");
            go.transform.SetParent(networkRoot, false);
            return go.transform;
        }

        /// <summary>
        ///     Generate intersection markings (stop lines, crosswalks) as mesh quads
        ///     on the road surface at every junction. Replaces the old DecalProjector approach.
        /// </summary>
        [ContextMenu("Generate Road Markings")]
        public void GenerateRoadDecals()
        {
            RefreshReferences();
            if (proceduralRoadSystem == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] ProceduralRoadSystem not found.", this);
                return;
            }

            var settings = roadNetworkSettings != null
                ? roadNetworkSettings
                : ScriptableObject.CreateInstance<RoadNetworkSettings>();

            var armsMarked = CityIntersectionMarkingBuilder.BuildForHub(proceduralRoadSystem, settings);
            Debug.Log("[CityPrefabRoadNetworkBuilder] " + (armsMarked > 0
                ? "Marked approach arms=" + armsMarked + " (mesh quads)."
                : "No junction markings placed (check settings or materials)."), this);
        }

        /// <summary>
        ///     Remove both old decals and new mesh markings without touching roads.
        /// </summary>
        [ContextMenu("Clear Road Markings")]
        public void ClearRoadDecals()
        {
            RoadDecalPlacer.Clear();
            CityIntersectionMarkingBuilder.Clear();
        }

        /// <summary>
        ///     Step 4: Generate street lamps around every named area's street-facing
        ///     outline edges (1 m outside the block boundary, on the footpath).
        ///     Requires roads and named areas to be generated first. Lamps are placed
        ///     in a "Road Lamps" container under the Road Network GameObject.
        /// </summary>
        [ContextMenu("Generate Street Lamps")]
        public void GenerateStreetLamps()
        {
            EnsureRoadCache();

            if (!TryGetCachedRoads(out var roads))
                return;

            if (!TryGetLampSettings(out var settings, out var lampPrefab))
                return;

            if (!TryGetLampsContainer(out var lampsContainer))
                return;

            var areasRoot = transform.Find("CityNamedAreas");
            if (areasRoot == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No CityNamedAreas — generate named areas first.", this);
                return;
            }

            var totalLamps = PlaceAllStreetLamps(lampsContainer, areasRoot, roads, settings, lampPrefab);

            Debug.Log("[CityPrefabRoadNetworkBuilder] " + (totalLamps > 0
                ? $"Placed {totalLamps} street lamps around {areasRoot.childCount} named areas."
                : "No street lamps placed (check settings and named areas)."), this);
        }

        private bool TryGetCachedRoads(out IReadOnlyList<RoadPolyline> roads)
        {
            roads = null;
            if (_lastGeneratedRoadNetwork != null && _lastGeneratedRoadNetwork.Roads.Count > 0)
            {
                roads = _lastGeneratedRoadNetwork.Roads;
                return true;
            }

            Debug.LogWarning("[CityPrefabRoadNetworkBuilder] No generated roads available. Run 'Generate City Road Network' first.", this);
            return false;
        }

        private bool TryGetLampSettings(out RoadNetworkSettings settings, out GameObject lampPrefab)
        {
            RefreshReferences();
            settings = roadNetworkSettings != null
                ? roadNetworkSettings
                : Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
            if (settings == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] RoadNetworkSettings not found.", this);
                lampPrefab = null;
                return false;
            }

            // Resolve lamp prefab from streetscapeConfig when available, falling back to RoadNetworkSettings.
            lampPrefab = settings.streetLampPrefab;
            if (streetscapeConfig != null && streetscapeConfig.streetLampPrefab != null)
                lampPrefab = streetscapeConfig.streetLampPrefab;

            if (settings.spawnStreetLamps && lampPrefab != null)
                return true;

            Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Street lamps disabled or missing lamp prefab.", this);
            return false;
        }

        private bool TryGetLampsContainer(out Transform lampsContainer)
        {
            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Road content root not found — generate roads first.", this);
                lampsContainer = null;
                return false;
            }

            ClearStreetLampsInternal(roadNetwork);
            lampsContainer = GetOrCreateLampsContainer(roadNetwork);
            if (lampsContainer != null)
                return true;

            Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Failed to create Road Lamps container.", this);
            return false;
        }

        private int PlaceAllStreetLamps(
            Transform lampsContainer,
            Transform areasRoot,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            GameObject lampPrefab)
        {
            return CityStreetLampPlacer.PlaceStreetLampsForAreas(
                lampsContainer, areasRoot, roads, settings, lampPrefab, ResolveGroundHeight);
        }

        /// <summary>
        ///     Remove the Road Lamps container (and all placed lamps) from the scene.
        /// </summary>
        [ContextMenu("Clear Street Lamps")]
        public void ClearStreetLamps()
        {
            var roadNetwork = ResolveRoadContentRoot();
            if (roadNetwork == null) return;

            ClearStreetLampsInternal(roadNetwork);
        }

        private static void ClearStreetLampsInternal(Transform networkRoot)
        {
            var container = networkRoot.Find("Road Lamps");
            if (container == null) return;

            if (Application.isPlaying)
                Object.Destroy(container.gameObject);
            else
                Object.DestroyImmediate(container.gameObject);
        }

        private static Transform GetOrCreateLampsContainer(Transform networkRoot)
        {
            var existing = networkRoot.Find("Road Lamps");
            if (existing != null) return existing;

            var go = new GameObject("Road Lamps");
            go.transform.SetParent(networkRoot, false);
            return go.transform;
        }
    }
}
