using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory.Crafting;

namespace Zombera.Building.Crafting
{
    /// <summary>
    /// Represents a physical crafting station in the world.
    /// </summary>
    public class CraftingStation : MonoBehaviour
    {
        private static readonly List<CraftingStation> _allStations = new List<CraftingStation>();
        public static IReadOnlyList<CraftingStation> AllStations => _allStations;

        [Header("Settings")]
        public string stationId;
        public CraftingStationType stationType;
        public float interactionRadius = 3f;

        [Header("Power")]
        public bool powerRequired;
        public bool currentPoweredState;

        private void OnEnable()
        {
            _allStations.Add(this);
        }

        private void OnDisable()
        {
            _allStations.Remove(this);
        }

        /// <summary>
        /// Checks if a unit is within range and the station is operational.
        /// </summary>
        public bool IsUsableBy(Vector3 unitPosition)
        {
            if (powerRequired && !currentPoweredState) return false;
            
            float distanceSqr = (transform.position - unitPosition).sqrMagnitude;
            return distanceSqr <= (interactionRadius * interactionRadius);
        }

        public static CraftingStation FindAvailable(CraftingStationType type, Vector3 unitPosition)
        {
            if (type == CraftingStationType.None) return null;

            foreach (var station in _allStations)
            {
                if (station.stationType == type && station.IsUsableBy(unitPosition))
                {
                    return station;
                }
            }

            return null;
        }
    }
}
