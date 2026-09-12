#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Systems;

#endregion

namespace Zombera.World.City
{
    /// <summary>
    ///     Runtime proxy that swaps into a full building prefab when a character gets close or the proxy is damaged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreamedCityBuildingProxySwap : MonoBehaviour
    {
        private const string BuildingCollapseConditionTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.Implementations.Conditions.Implementations.Collapse.BuildingCollapseCondition";

        private const string BuildingDebrisBehaviorTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.Implementations.Behaviors.Implementations.BuildingDebrisBehavior";

        private static readonly List<Unit> SNearbyUnitsBuffer = new(16);

        private static Type s_cachedCollapseConditionType;
        private static bool s_checkedCollapseConditionType;
        private static Type s_cachedDebrisBehaviorType;
        private static bool s_checkedDebrisBehaviorType;

        private static readonly Dictionary<int, bool> SPrefabHasCollapseConditionById = new();
        private static readonly Dictionary<int, bool> SPrefabHasDebrisBehaviorById = new();

        [Header("Runtime Wiring")]
        [SerializeField] private GameObject fullBuildingPrefab;
        [SerializeField] private RuntimePlacedStructureFixer runtimePlacedStructureFixer;

        [Header("Swap")]
        [SerializeField] [Min(1f)] private float swapDistanceMeters = 10f;
        [SerializeField] [Min(0.05f)] private float distanceCheckIntervalSeconds = 0.3f;
        [SerializeField] private bool swapOnFirstDamage = true;

        [Header("Spawned Full Building")]
        [SerializeField] private bool disableEasyBuildCollapseOnSwap = true;
        [SerializeField] private bool ensureStructureHealthOnSwap = true;
        [SerializeField] [Min(1f)] private float structureMaxHealthOnSwap = 250f;
        [SerializeField] private bool ensureBuildPieceOnSwap = true;
        [SerializeField] private BuildPieceCategory buildPieceCategoryOnSwap = BuildPieceCategory.Other;

        [Header("Debug")]
        [SerializeField] private bool logDiagnostics;

        private bool _initialized;
        private bool _swapped;
        private bool _subscribedToProxyDamage;
        private float _nextDistanceCheckAt;
        private StructureHealth _proxyHealth;

        public void Initialize(
            GameObject targetFullBuildingPrefab,
            RuntimePlacedStructureFixer fixer,
            float swapDistance,
            bool swapOnDamage,
            bool disableCollapseOnSwap,
            bool ensureStructureHealth,
            float structureMaxHealth,
            bool ensureBuildPiece,
            BuildPieceCategory buildPieceCategory,
            bool enableDiagnostics)
        {
            fullBuildingPrefab = targetFullBuildingPrefab;
            runtimePlacedStructureFixer = fixer;
            swapDistanceMeters = Mathf.Max(1f, swapDistance);
            swapOnFirstDamage = swapOnDamage;
            disableEasyBuildCollapseOnSwap = disableCollapseOnSwap;
            ensureStructureHealthOnSwap = ensureStructureHealth;
            structureMaxHealthOnSwap = Mathf.Max(1f, structureMaxHealth);
            ensureBuildPieceOnSwap = ensureBuildPiece;
            buildPieceCategoryOnSwap = buildPieceCategory;
            logDiagnostics = enableDiagnostics;

            EnsureProxyDamageSurface();

            _initialized = true;
            _nextDistanceCheckAt = Time.unscaledTime + UnityEngine.Random.Range(0f, Mathf.Max(0.05f, distanceCheckIntervalSeconds));
        }

        public void EnsureProxyDamageSurface()
        {
            if (!swapOnFirstDamage) return;

            if (_proxyHealth == null) _proxyHealth = GetComponent<StructureHealth>();
            if (_proxyHealth == null) _proxyHealth = gameObject.AddComponent<StructureHealth>();
            _proxyHealth.SetMaxHealth(Mathf.Max(1f, structureMaxHealthOnSwap), true);

            if (ensureBuildPieceOnSwap)
            {
                var piece = GetComponent<BuildPiece>();
                if (piece == null) piece = gameObject.AddComponent<BuildPiece>();
                piece.SetCategory(buildPieceCategoryOnSwap);
            }

            HookProxyDamageListener();
        }

        private void OnEnable()
        {
            if (_initialized) HookProxyDamageListener();
        }

        private void OnDisable()
        {
            UnhookProxyDamageListener();
        }

        private void Update()
        {
            if (_swapped || fullBuildingPrefab == null) return;

            if (Time.unscaledTime < _nextDistanceCheckAt) return;
            _nextDistanceCheckAt = Time.unscaledTime + Mathf.Max(0.05f, distanceCheckIntervalSeconds);

            var radius = Mathf.Max(1f, swapDistanceMeters);
            if (!TryHasNearbyCharacter(radius)) return;

            _ = TrySwapToFullBuilding(0f, null);
        }

        private void HookProxyDamageListener()
        {
            if (!swapOnFirstDamage || _swapped) return;

            if (_proxyHealth == null) _proxyHealth = GetComponent<StructureHealth>();
            if (_proxyHealth == null) return;
            if (_subscribedToProxyDamage) return;

            _proxyHealth.Damaged += HandleProxyDamaged;
            _subscribedToProxyDamage = true;
        }

        private void UnhookProxyDamageListener()
        {
            if (!_subscribedToProxyDamage || _proxyHealth == null) return;

            _proxyHealth.Damaged -= HandleProxyDamaged;
            _subscribedToProxyDamage = false;
        }

        private void HandleProxyDamaged(float amount, GameObject source)
        {
            if (_swapped) return;
            _ = TrySwapToFullBuilding(Mathf.Max(0f, amount), source);
        }

        private bool TrySwapToFullBuilding(float forwardedDamage, GameObject damageSource)
        {
            if (_swapped || fullBuildingPrefab == null) return false;

            _swapped = true;
            UnhookProxyDamageListener();

            var parent = transform.parent;
            var position = transform.position;
            var rotation = transform.rotation;
            var localScale = transform.localScale;

            var fullBuilding = Instantiate(fullBuildingPrefab, position, rotation, parent);
            if (fullBuilding == null)
            {
                _swapped = false;
                HookProxyDamageListener();
                return false;
            }

            fullBuilding.transform.localScale = localScale;

            AddStructuralComponentsIfNeeded(fullBuilding);

            if (disableEasyBuildCollapseOnSwap)
                DisableEasyBuildCollapseBehaviors(fullBuilding, fullBuildingPrefab);

            if (runtimePlacedStructureFixer != null)
                runtimePlacedStructureFixer.ProcessPlacedStructure(fullBuilding);

            if (forwardedDamage > 0f)
            {
                var health = fullBuilding.GetComponent<StructureHealth>();
                if (health != null) health.TakeDamage(forwardedDamage, damageSource);
            }

            if (logDiagnostics)
                Debug.Log("[StreamedCityProxySwap] Swapped proxy '" + name + "' to full building '" + fullBuilding.name + "'.",
                    fullBuilding);

            Destroy(gameObject);
            return true;
        }

        private void AddStructuralComponentsIfNeeded(GameObject go)
        {
            if (go == null) return;

            if (ensureStructureHealthOnSwap)
            {
                var health = go.GetComponent<StructureHealth>();
                if (health == null) health = go.AddComponent<StructureHealth>();
                health.SetMaxHealth(Mathf.Max(1f, structureMaxHealthOnSwap), true);
            }

            if (ensureBuildPieceOnSwap)
            {
                var piece = go.GetComponent<BuildPiece>();
                if (piece == null) piece = go.AddComponent<BuildPiece>();
                piece.SetCategory(buildPieceCategoryOnSwap);
            }
        }

        private bool TryHasNearbyCharacter(float radius)
        {
            var unitManager = UnitManager.Instance;
            if (unitManager == null) return false;

            var nearby = unitManager.FindNearbyUnits(transform.position, Mathf.Max(1f, radius), SNearbyUnitsBuffer);
            if (nearby == null || nearby.Count == 0) return false;

            for (var i = 0; i < nearby.Count; i++)
            {
                var unit = nearby[i];
                if (unit == null || !unit.IsAlive || !unit.gameObject.activeInHierarchy) continue;
                return true;
            }

            return false;
        }

        private static void DisableEasyBuildCollapseBehaviors(GameObject root, GameObject sourcePrefab)
        {
            if (root == null) return;

            if (PrefabContainsBehaviorType(
                    sourcePrefab,
                    BuildingCollapseConditionTypeName,
                    ref s_cachedCollapseConditionType,
                    ref s_checkedCollapseConditionType,
                    SPrefabHasCollapseConditionById))
                DisableBehaviorsByTypeName(
                    root,
                    BuildingCollapseConditionTypeName,
                    ref s_cachedCollapseConditionType,
                    ref s_checkedCollapseConditionType);

            if (PrefabContainsBehaviorType(
                    sourcePrefab,
                    BuildingDebrisBehaviorTypeName,
                    ref s_cachedDebrisBehaviorType,
                    ref s_checkedDebrisBehaviorType,
                    SPrefabHasDebrisBehaviorById))
                DisableBehaviorsByTypeName(
                    root,
                    BuildingDebrisBehaviorTypeName,
                    ref s_cachedDebrisBehaviorType,
                    ref s_checkedDebrisBehaviorType);
        }

        private static bool PrefabContainsBehaviorType(
            GameObject sourcePrefab,
            string fullTypeName,
            ref Type cachedType,
            ref bool typeLookupDone,
            Dictionary<int, bool> presenceByPrefabId)
        {
            if (sourcePrefab == null) return true;

            var prefabId = sourcePrefab.GetInstanceID();
            if (presenceByPrefabId.TryGetValue(prefabId, out var cachedPresence))
                return cachedPresence;

            if (!typeLookupDone)
            {
                cachedType = FindType(fullTypeName);
                typeLookupDone = true;
            }

            if (cachedType == null || !typeof(Behaviour).IsAssignableFrom(cachedType))
            {
                presenceByPrefabId[prefabId] = false;
                return false;
            }

            var hasBehavior = sourcePrefab.GetComponentInChildren(cachedType, true) != null;
            presenceByPrefabId[prefabId] = hasBehavior;
            return hasBehavior;
        }

        private static void DisableBehaviorsByTypeName(
            GameObject root,
            string fullTypeName,
            ref Type cachedType,
            ref bool typeLookupDone)
        {
            if (!typeLookupDone)
            {
                cachedType = FindType(fullTypeName);
                typeLookupDone = true;
            }

            if (cachedType == null || !typeof(Behaviour).IsAssignableFrom(cachedType))
                return;

            var components = root.GetComponentsInChildren(cachedType, true);
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is Behaviour behavior)
                    behavior.enabled = false;
            }
        }

        private static Type FindType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null) return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }
    }
}
