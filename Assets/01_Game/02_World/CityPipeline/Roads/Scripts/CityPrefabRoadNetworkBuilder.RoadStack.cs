using System.Reflection;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        // ──────────────────────────────────────────────
        //  Road stack setup + ground height utilities
        // ──────────────────────────────────────────────

        [System.NonSerialized] private RoadBuildTerrainSampler _terrainSampler;
        [System.NonSerialized] private System.Func<Vector2, float> _terrainSamplerFallback;

        /// <summary>
        ///     Drops the cached ground-height terrain sampler so the next sample re-reads
        ///     <see cref="Terrain.activeTerrains" />. Call it after terrains are created,
        ///     destroyed or streamed in.
        /// </summary>
        public void InvalidateTerrainSamplerCache() => _terrainSampler = null;

        private void EnsureTerrainSampler()
        {
            if (_terrainSampler != null)
                return;

            // Hoisted once — a fresh method-group delegate per sample would allocate.
            _terrainSamplerFallback = ResolveGroundHeightWithoutHeightmap;
            _terrainSampler = new RoadBuildTerrainSampler();
        }

        [ContextMenu("Ensure Minimal Road Stack")]
        public void EnsureMinimalRoadStack()
        {
            proceduralRoadSystem ??= GetComponentInChildren<ProceduralRoadSystem>();
            if (proceduralRoadSystem == null)
            {
                var stack = transform.Find("CityRoadStack");
                if (stack == null)
                {
                    var stackObject = new GameObject("CityRoadStack");
                    stackObject.transform.SetParent(transform, false);
                    stack = stackObject.transform;
                }

                proceduralRoadSystem = stack.GetComponent<ProceduralRoadSystem>();
                if (proceduralRoadSystem == null)
                    proceduralRoadSystem = stack.gameObject.AddComponent<ProceduralRoadSystem>();
            }

            RefreshReferences();
        }

        [ContextMenu("Refresh References")]
        public void RefreshReferences()
        {
            // Terrains may have been reallocated since the last sample.
            InvalidateTerrainSamplerCache();
            proceduralRoadSystem ??= GetComponentInChildren<ProceduralRoadSystem>();
            proceduralRoadSystem ??= FindFirstObjectByType<ProceduralRoadSystem>();
            if (roadNetworkSettings == null && proceduralRoadSystem != null)
            {
                var field = typeof(ProceduralRoadSystem).GetField(
                    "settings",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                roadNetworkSettings = field?.GetValue(proceduralRoadSystem) as RoadNetworkSettings;
            }

            roadNetworkSettings ??= Resources.Load<RoadNetworkSettings>("RoadNetworkSettings");
            ApplyHubConfigurationToRoadSystem();
        }

        private void ApplyHubConfigurationToRoadSystem()
        {
            if (proceduralRoadSystem == null) return;

            SetPrivateField(proceduralRoadSystem, "settings", roadNetworkSettings);
            SetPrivateField(proceduralRoadSystem, "autoGenerateOnTileApply", false);
            SetPrivateField(proceduralRoadSystem, "autoResolveReferences", false);
            SetPrivateField(proceduralRoadSystem, "enableEditorPinnedTileAuthoringMode", false);
            SetPrivateField(proceduralRoadSystem, "tileStreamBridge", null);
            SetPrivateField(proceduralRoadSystem, "roadGameplayService", null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        public Vector2 ResolveLayoutCenterXZ()
        {
            return ResolvedAlignLayoutCenterToTransform
                ? new Vector2(transform.position.x, transform.position.z)
                : Layout.centerXZ;
        }

        public float SampleGroundHeight(Vector2 worldXZ) => ResolveGroundHeight(worldXZ);

        private float ResolveGroundHeight(Vector2 worldXZ)
        {
            // Reuses the cached sampler: terrain first (same first-wins order as the old
            // linear scan), raycast fallback when no terrain covers the point.
            EnsureTerrainSampler();
            return _terrainSampler.Sample(worldXZ, _terrainSamplerFallback);
        }

        private float ResolveGroundHeightWithoutHeightmap(Vector2 worldXZ)
        {
            var origin = groundReference != null ? groundReference.position : transform.position;
            var rayOrigin = new Vector3(worldXZ.x, origin.y + ResolvedGroundRaycastHeight, worldXZ.y);

            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out var hit,
                    ResolvedGroundRaycastHeight * 2f,
                    ResolvedGroundLayers,
                    QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return ResolvedFlatGroundHeight;
        }
    }
}
