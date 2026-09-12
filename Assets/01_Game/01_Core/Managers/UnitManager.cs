#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Factions;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Global registry and query service for active units in the simulation.
    /// </summary>
    public sealed class UnitManager : MonoBehaviour
    {
        private const float CellSize = 10f;
        [Header("Spatial Grid Performance")]
        [SerializeField] [Min(1)] private int maxGridUpdatesPerFrame = 256;

        private readonly HashSet<Unit> _activeUnits = new();
        private readonly Dictionary<Vector2Int, List<Unit>> _grid = new();
        private readonly HashSet<Unit> _registryRefreshSeenUnits = new();
        private readonly List<Unit> _registryRemovalBuffer = new();
        private readonly Dictionary<Unit, Vector2Int> _unitGridPositions = new();
        private readonly List<Unit> _unitUpdateList = new();
        private bool _unitUpdateListDirty = true;
        private int _nextGridUpdateIndex;
        private static UnitManager _instance;
        private static bool _warnedAboutMissingInstance;

        public static bool HasInstance => _instance != null;

        public static UnitManager Instance
        {
            get
            {
                if (_instance == null && !_warnedAboutMissingInstance)
                {
                    _warnedAboutMissingInstance = true;
                    Debug.LogWarning("[UnitManager] Instance accessed before Awake assigned a runtime instance.");
                }

                return _instance;
            }
            private set
            {
                _instance = value;
                if (_instance != null) _warnedAboutMissingInstance = false;
            }
        }


        private void Awake()
        {
            var persistentRoot = transform.root.gameObject;

            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    $"[UnitManager] Duplicate instance detected on '{name}'. Keeping '{_instance.name}' and destroying duplicate component.",
                    this);
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(persistentRoot);

            if (GameManagerGateway.HasInstance)
                GameManagerGateway.Instance.RegisterSystem(this);

            RefreshRegistry();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Update()
        {
            if (_activeUnits.Count == 0) return;

            RebuildUnitUpdateListIfNeeded();
            if (_unitUpdateList.Count == 0) return;

            var updatesThisFrame = Mathf.Clamp(maxGridUpdatesPerFrame, 1, _unitUpdateList.Count);

            for (var i = 0; i < updatesThisFrame; i++)
            {
                if (_nextGridUpdateIndex >= _unitUpdateList.Count)
                    _nextGridUpdateIndex = 0;

                var unit = _unitUpdateList[_nextGridUpdateIndex++];
                if (unit == null) continue;
                UpdateUnitGridPosition(unit);
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        public void RefreshRegistry()
        {
            var units = FindObjectsByType<Unit>(FindObjectsSortMode.None);

            _registryRefreshSeenUnits.Clear();

            foreach (var unit in units)
            {
                if (unit == null) continue;

                _registryRefreshSeenUnits.Add(unit);

                if (_activeUnits.Add(unit))
                    _unitUpdateListDirty = true;

                UpdateUnitGridPosition(unit);
            }

            _registryRemovalBuffer.Clear();

            foreach (var trackedUnit in _activeUnits)
                if (trackedUnit == null || !_registryRefreshSeenUnits.Contains(trackedUnit))
                    _registryRemovalBuffer.Add(trackedUnit);

            for (var i = 0; i < _registryRemovalBuffer.Count; i++)
            {
                var staleUnit = _registryRemovalBuffer[i];
                _activeUnits.Remove(staleUnit);
                RemoveUnitFromGrid(staleUnit);
                _unitUpdateListDirty = true;
            }

            _registryRefreshSeenUnits.Clear();
            _registryRemovalBuffer.Clear();

            if (_activeUnits.Count == 0)
            {
                _unitUpdateList.Clear();
                _nextGridUpdateIndex = 0;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            RefreshRegistry();
        }

        public void RegisterUnit(Unit unit)
        {
            if (unit == null) return;

            EnsureUniqueUnitIdAmongActive(unit);

            if (_activeUnits.Add(unit))
            {
                UpdateUnitGridPosition(unit);
                _unitUpdateListDirty = true;
            }
        }

        /// <summary>
        ///     Runtime squad clones often inherit the same baked prefab <see cref="Unit.UnitId" />; fix before save/load matching.
        /// </summary>
        public void EnsureUniqueUnitIdsForActiveUnits()
        {
            if (_activeUnits.Count == 0) return;

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicatesFixed = 0;

            foreach (var unit in _activeUnits)
            {
                if (unit == null) continue;

                if (string.IsNullOrWhiteSpace(unit.UnitId))
                    unit.RegenerateUnitId();

                while (!seenIds.Add(unit.UnitId))
                {
                    unit.RegenerateUnitId();
                    duplicatesFixed++;
                }
            }

            if (duplicatesFixed > 0)
                Debug.Log("[UnitManager] Regenerated " + duplicatesFixed + " duplicate unit id(s) for active units.");
        }

        private void EnsureUniqueUnitIdAmongActive(Unit unit)
        {
            if (string.IsNullOrWhiteSpace(unit.UnitId))
                unit.RegenerateUnitId();

            foreach (var other in _activeUnits)
            {
                if (other == null || other == unit) continue;
                if (!string.Equals(other.UnitId, unit.UnitId, StringComparison.Ordinal)) continue;

                unit.RegenerateUnitId();
                return;
            }
        }

        public void UnregisterUnit(Unit unit)
        {
            if (unit == null) return;

            if (_activeUnits.Remove(unit))
            {
                RemoveUnitFromGrid(unit);
                _unitUpdateListDirty = true;
            }
        }

        private void RebuildUnitUpdateListIfNeeded()
        {
            if (!_unitUpdateListDirty) return;

            _unitUpdateListDirty = false;
            PruneNullActiveUnits();

            _unitUpdateList.Clear();
            foreach (var unit in _activeUnits)
                _unitUpdateList.Add(unit);

            if (_unitUpdateList.Count == 0)
            {
                _nextGridUpdateIndex = 0;
                return;
            }

            if (_nextGridUpdateIndex >= _unitUpdateList.Count)
                _nextGridUpdateIndex = 0;
        }

        private void PruneNullActiveUnits()
        {
            if (_activeUnits.Count == 0) return;

            _registryRemovalBuffer.Clear();

            foreach (var unit in _activeUnits)
                if (unit == null)
                    _registryRemovalBuffer.Add(unit);

            for (var i = 0; i < _registryRemovalBuffer.Count; i++)
            {
                var staleUnit = _registryRemovalBuffer[i];
                _activeUnits.Remove(staleUnit);
                RemoveUnitFromGrid(staleUnit);
            }

            _registryRemovalBuffer.Clear();
        }

        private void UpdateUnitGridPosition(Unit unit)
        {
            var pos = unit.transform.position;
            var newGridPos = new Vector2Int(Mathf.FloorToInt(pos.x / CellSize), Mathf.FloorToInt(pos.z / CellSize));

            if (_unitGridPositions.TryGetValue(unit, out var oldGridPos))
            {
                if (oldGridPos == newGridPos) return;

                if (_grid.TryGetValue(oldGridPos, out var list)) list.Remove(unit);
            }

            _unitGridPositions[unit] = newGridPos;
            if (!_grid.TryGetValue(newGridPos, out var newList))
            {
                newList = new List<Unit>();
                _grid[newGridPos] = newList;
            }

            newList.Add(unit);
        }

        private void RemoveUnitFromGrid(Unit unit)
        {
            if (unit == null) return;

            if (!_unitGridPositions.TryGetValue(unit, out var gridPos)) return;

            if (_grid.TryGetValue(gridPos, out var list)) list.Remove(unit);
            _unitGridPositions.Remove(unit);
        }

        public List<Unit> GetAllActiveUnits(List<Unit> result = null)
        {
            var units = result ?? new List<Unit>(_activeUnits.Count);
            units.Clear();

            foreach (var unit in _activeUnits)
                if (unit != null)
                    units.Add(unit);

            return units;
        }

        public List<Unit> GetUnitsByRole(UnitRole role, List<Unit> result = null)
        {
            var units = result ?? new List<Unit>();
            units.Clear();

            foreach (var unit in _activeUnits)
                if (unit != null && unit.Role == role)
                    units.Add(unit);

            return units;
        }

        public int CountByRole(UnitRole role)
        {
            var count = 0;
            foreach (var unit in _activeUnits)
                if (unit != null && unit.Role == role)
                    count++;

            return count;
        }

        public int CountZombies()
        {
            return CountByRole(UnitRole.Zombie);
        }

        /// <summary>Returns the first registered alive unit with the given role, or null.</summary>
        public Unit FindFirstUnitByRole(UnitRole role)
        {
            foreach (var unit in _activeUnits)
                if (unit != null && unit.Role == role && unit.IsAlive)
                    return unit;

            return null;
        }

        public List<Unit> FindNearbyUnits(Vector3 worldPosition, float radius, List<Unit> result = null)
        {
            var radiusSqr = radius * radius;
            var units = result ?? new List<Unit>();
            units.Clear();

            ResolveNearbyCellBounds(worldPosition, radius, out var minX, out var maxX, out var minZ, out var maxZ);

            for (var x = minX; x <= maxX; x++)
            {
                for (var z = minZ; z <= maxZ; z++)
                    AppendCellUnitsWithinRadius(new Vector2Int(x, z), worldPosition, radiusSqr, units);
            }

            return units;
        }

        private static void ResolveNearbyCellBounds(
            Vector3 worldPosition,
            float radius,
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
        {
            minX = Mathf.FloorToInt((worldPosition.x - radius) / CellSize);
            maxX = Mathf.FloorToInt((worldPosition.x + radius) / CellSize);
            minZ = Mathf.FloorToInt((worldPosition.z - radius) / CellSize);
            maxZ = Mathf.FloorToInt((worldPosition.z + radius) / CellSize);
        }

        private void AppendCellUnitsWithinRadius(Vector2Int cell, Vector3 worldPosition, float radiusSqr,
            List<Unit> units)
        {
            if (!_grid.TryGetValue(cell, out var cellUnits)) return;

            for (var i = 0; i < cellUnits.Count; i++)
            {
                var unit = cellUnits[i];
                if (unit == null) continue;

                var offset = unit.transform.position - worldPosition;
                if (offset.sqrMagnitude <= radiusSqr)
                    units.Add(unit);
            }
        }

        public List<Unit> FindNearbyEnemies(Unit source, float radius, List<Unit> result = null)
        {
            var nearby = FindNearbyUnits(source != null ? source.transform.position : Vector3.zero, radius, result);

            if (source == null)
            {
                nearby.Clear();
                return nearby;
            }

            for (var i = nearby.Count - 1; i >= 0; i--)
            {
                var candidate = nearby[i];
                if (candidate == null || candidate == source || !FactionManager.AreUnitsHostile(source, candidate))
                    nearby.RemoveAt(i);
            }

            return nearby;
        }

        public List<Unit> FindNearbyAllies(Unit source, float radius, List<Unit> result = null)
        {
            var nearby = FindNearbyUnits(source != null ? source.transform.position : Vector3.zero, radius, result);

            if (source == null)
            {
                nearby.Clear();
                return nearby;
            }

            for (var i = nearby.Count - 1; i >= 0; i--)
            {
                var candidate = nearby[i];
                if (candidate == null || candidate == source || FactionManager.AreUnitsHostile(source, candidate))
                    nearby.RemoveAt(i);
            }

            return nearby;
        }
    }
}