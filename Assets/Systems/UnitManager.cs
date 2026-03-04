using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;

namespace Zombera.Systems
{
    /// <summary>
    /// Global registry and query service for active units in the simulation.
    /// </summary>
    public sealed class UnitManager : MonoBehaviour
    {
        public static UnitManager Instance { get; private set; }

        private readonly HashSet<Unit> activeUnits = new HashSet<Unit>();

        public int ActiveUnitCount => activeUnits.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            RefreshRegistry();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        public void RefreshRegistry()
        {
            activeUnits.Clear();
            Unit[] units = FindObjectsOfType<Unit>();

            for (int i = 0; i < units.Length; i++)
            {
                RegisterUnit(units[i]);
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
            if (unit == null)
            {
                return;
            }

            activeUnits.Add(unit);
        }

        public void UnregisterUnit(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            activeUnits.Remove(unit);
        }

        public List<Unit> GetAllActiveUnits(List<Unit> result = null)
        {
            List<Unit> units = result ?? new List<Unit>(activeUnits.Count);
            units.Clear();

            foreach (Unit unit in activeUnits)
            {
                if (unit != null)
                {
                    units.Add(unit);
                }
            }

            return units;
        }

        public List<Unit> GetUnitsByRole(UnitRole role, List<Unit> result = null)
        {
            List<Unit> units = result ?? new List<Unit>();
            units.Clear();

            foreach (Unit unit in activeUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                if (unit.Role == role)
                {
                    units.Add(unit);
                }
            }

            return units;
        }

        public int CountByRole(UnitRole role)
        {
            int count = 0;

            foreach (Unit unit in activeUnits)
            {
                if (unit != null && unit.Role == role)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountZombies()
        {
            return CountByRole(UnitRole.Zombie);
        }

        public List<Unit> GetSquadMembers(List<Unit> result = null)
        {
            return GetUnitsByRole(UnitRole.SquadMember, result);
        }

        public List<Unit> FindNearbyUnits(Vector3 worldPosition, float radius, List<Unit> result = null)
        {
            float radiusSqr = radius * radius;
            List<Unit> units = result ?? new List<Unit>();
            units.Clear();

            foreach (Unit unit in activeUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                Vector3 offset = unit.transform.position - worldPosition;

                if (offset.sqrMagnitude <= radiusSqr)
                {
                    units.Add(unit);
                }
            }

            return units;
        }

        public List<Unit> FindNearbyEnemies(Unit source, float radius, List<Unit> result = null)
        {
            List<Unit> nearby = FindNearbyUnits(source != null ? source.transform.position : Vector3.zero, radius, result);

            if (source == null)
            {
                nearby.Clear();
                return nearby;
            }

            for (int i = nearby.Count - 1; i >= 0; i--)
            {
                Unit candidate = nearby[i];

                if (candidate == null || candidate == source || !AreHostile(source.Role, candidate.Role))
                {
                    nearby.RemoveAt(i);
                }
            }

            return nearby;
        }

        private static bool AreHostile(UnitRole sourceRole, UnitRole targetRole)
        {
            if (sourceRole == UnitRole.Bandit)
            {
                return targetRole != UnitRole.Bandit;
            }

            if (sourceRole == UnitRole.Zombie || sourceRole == UnitRole.Enemy)
            {
                return targetRole != UnitRole.Zombie && targetRole != UnitRole.Enemy;
            }

            if (targetRole == UnitRole.Zombie || targetRole == UnitRole.Enemy || targetRole == UnitRole.Bandit)
            {
                return true;
            }

            return false;
        }
    }
}