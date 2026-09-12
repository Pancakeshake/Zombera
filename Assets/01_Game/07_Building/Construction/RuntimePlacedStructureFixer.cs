#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

#endregion

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Ensures runtime-placed build parts are physically solid, carve NavMesh, and flatten terrain under their footprint.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class RuntimePlacedStructureFixer : MonoBehaviour
    {
        private const string BuildingPartTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.BuildingPart";

        private const string BuildingPartStateTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.BuildingPart+BuildingState";

        private const string BuildingManagerTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Managers.BuildingManager";

        [Header("EasyBuild Detection")]
        [SerializeField]
        [Tooltip("Poll EasyBuild's placed-part state so radial-menu placements get post-processing too.")]
        private bool processEasyBuildPlacements = true;

        [SerializeField] [Min(0.05f)]
        private float easyBuildFallbackScanIntervalSeconds = 0.35f;

        [SerializeField] [Min(0.05f)]
        private float easyBuildLastPlacedPollIntervalSeconds = 0.2f;

        [SerializeField]
        [Tooltip("Allow this component to own global EasyBuild placement polling. Only one fixer polls to avoid duplicate scans.")]
        private bool allowGlobalPlacementPolling = true;

        [SerializeField]
        [Tooltip("When enabled, pre-existing placed parts are marked as seen so only newly placed parts are processed.")]
        private bool skipExistingPlacedPartsOnStartup = true;

        [Header("Collision + NavMesh")]
        [SerializeField]
        private bool ensureSolidColliders = true;

        [SerializeField]
        private bool addBoxColliderWhenMissing = true;

        [SerializeField]
        private bool ensureNavMeshObstacle = true;

        [SerializeField] [Min(0f)]
        private float navMeshObstaclePaddingMeters = 0.05f;

        [Header("Terrain Flattening")]
        [SerializeField]
        private bool flattenTerrainOnPlacement = true;

        [SerializeField] [Min(0f)]
        private float terrainFlattenPaddingMeters = 0.15f;

        [SerializeField] [Min(0f)]
        private float terrainFlattenBlendMeters = 0.8f;

        [SerializeField]
        private float terrainFlattenTargetYOffsetMeters = 0.02f;

        [SerializeField] [Min(1f)]
        private float maxFlattenAreaPerAxisMeters = 80f;

        [SerializeField]
        [Tooltip(
            "When off (recommended for streamed batch spawns), height edits use SetHeightsDelayLOD only. " +
            "SyncHeightmap forces an expensive immediate CPU sync — enable only if same-frame physics must match new heights.")]
        private bool syncHeightmapImmediatelyAfterFlatten;

        [Header("Structure Detection")]
        [SerializeField]
        private bool processOnlyLikelyStructures = true;

        [SerializeField] [Min(0f)]
        private float minimumStructureFootprintMeters = 1.0f;

        [SerializeField]
        private string[] structureNameTokens =
        {
            "wall", "foundation", "floor", "roof", "building", "house", "shelter", "door"
        };

        [Header("Debug")]
        [SerializeField]
        private bool verboseLogs;

        private readonly HashSet<int> _processedRootIds = new();
        private static RuntimePlacedStructureFixer s_globalPollingOwner;
        private readonly object[] _getPartsByStateInvokeArgs = new object[1];

#pragma warning disable S2933 // Assigned in RuntimePlacedStructureFixer.EasyBuild.cs partial class
        private bool _seededExistingPlacedParts = false;
        private float _nextEasyBuildScanAt;
        private float _nextLastPlacedPollAt;
#pragma warning disable S2933 // Assigned in RuntimePlacedStructureFixer.EasyBuild.cs partial class
        private int _lastObservedPlacedFrame = -1;
        private int _lastFallbackScanObservedPlacedFrame = -1;
        private bool _isGlobalPollingOwner;

        private Type _buildingPartType;
        private Type _buildingPartStateType;
        private Type _buildingManagerType;
        private PropertyInfo _lastPlacedPartProperty;
        private PropertyInfo _lastPlacedFrameProperty;
        private PropertyInfo _buildingManagerInstanceProperty;
        private MethodInfo _getPartsByStateMethod;
        private object _placedStateValue;

#pragma warning disable S2325 // Unity message method — must be instance, called via reflection
        private void Awake()
        {
            PrimeEasyBuildReflectionCache();
        }

        private void OnEnable()
        {
            ResolveGlobalPollingOwnership();

            if (!processEasyBuildPlacements) return;

            if (skipExistingPlacedPartsOnStartup)
                TrySeedExistingPlacedParts();
        }

        private void OnDisable()
        {
            if (s_globalPollingOwner == this)
                s_globalPollingOwner = null;

            _isGlobalPollingOwner = false;
        }

        private void Update()
        {
            if (!processEasyBuildPlacements) return;

            if (allowGlobalPlacementPolling && !_isGlobalPollingOwner)
            {
                ResolveGlobalPollingOwnership();
            }
            if (!_isGlobalPollingOwner) return;

            if (!_seededExistingPlacedParts && skipExistingPlacedPartsOnStartup)
                TrySeedExistingPlacedParts();

            if (Time.unscaledTime >= _nextLastPlacedPollAt)
            {
                _nextLastPlacedPollAt = Time.unscaledTime + Mathf.Max(0.05f, easyBuildLastPlacedPollIntervalSeconds);
                TryProcessLastPlacedPart();
            }

            if (Time.unscaledTime < _nextEasyBuildScanAt) return;

            _nextEasyBuildScanAt = Time.unscaledTime + Mathf.Max(1.5f, easyBuildFallbackScanIntervalSeconds);

            if (!TryGetLastPlacedPart(out _, out var latestPlacedFrame)) return;
            if (latestPlacedFrame <= _lastFallbackScanObservedPlacedFrame) return;

            _lastFallbackScanObservedPlacedFrame = latestPlacedFrame;
            TryProcessUntrackedPlacedParts();
        }

        private void ResolveGlobalPollingOwnership()
        {
            if (!allowGlobalPlacementPolling)
            {
                _isGlobalPollingOwner = true;
                return;
            }

            if (s_globalPollingOwner == null || !s_globalPollingOwner.isActiveAndEnabled)
                s_globalPollingOwner = this;

            _isGlobalPollingOwner = s_globalPollingOwner == this;
        }

        public void ProcessPlacedStructure(GameObject placedRoot)
        {
            ProcessPlacedStructure(placedRoot, true);
        }

        /// <summary>
        ///     Re-runs placement fixes (terrain flatten, collider/obstacle checks) for a structure
        ///     instance that was pooled and moved to a new location. Bypasses the processed-root cache.
        /// </summary>
        public void ReprocessPlacedStructure(GameObject placedRoot)
        {
            ProcessPlacedStructure(placedRoot, false);
        }

        private void ProcessPlacedStructure(GameObject placedRoot, bool trackAsProcessed)
        {
            if (placedRoot == null) return;

            var rootId = placedRoot.GetInstanceID();
            if (trackAsProcessed && !_processedRootIds.Add(rootId)) return;

            if (!TryComputeWorldBounds(placedRoot, out var worldBounds))
                worldBounds = new Bounds(placedRoot.transform.position, Vector3.one * Mathf.Max(0.2f, minimumStructureFootprintMeters));

            if (processOnlyLikelyStructures && !IsLikelyStructure(placedRoot, worldBounds)) return;

            if (ensureSolidColliders && EnsureSolidCollider(placedRoot, worldBounds))
                _ = TryComputeWorldBounds(placedRoot, out worldBounds);

            if (ensureNavMeshObstacle)
                EnsureNavMeshBlocking(placedRoot, worldBounds);

            if (flattenTerrainOnPlacement)
                FlattenTerrainUnderStructure(placedRoot, worldBounds);

            if (verboseLogs)
                Debug.Log($"[RuntimePlacedStructureFixer] Processed {placedRoot.name} at {placedRoot.transform.position}.",
                    placedRoot);
        }

        private bool IsLikelyStructure(GameObject root, Bounds worldBounds)
        {
            if (root == null) return false;

            if (root.GetComponentInChildren<BuildPiece>(true) != null) return true;
            if (root.GetComponentInChildren<StructureHealth>(true) != null) return true;

            var footprint = Mathf.Max(worldBounds.size.x, worldBounds.size.z);
            if (footprint >= Mathf.Max(0.1f, minimumStructureFootprintMeters) && worldBounds.size.y >= 0.9f)
                return true;

            var lowerName = root.name.ToLowerInvariant();
            if (structureNameTokens != null)
                for (var i = 0; i < structureNameTokens.Length; i++)
                {
                    var token = structureNameTokens[i];
                    if (string.IsNullOrWhiteSpace(token)) continue;
                    if (lowerName.Contains(token.Trim().ToLowerInvariant())) return true;
                }

            return false;
        }
    }
}
