#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.AI;
using Zombera.Systems;
using Zombera.World.Simulation;

#endregion

namespace Zombera.World.Regions
{
    /// <summary>
    ///     Region-level simulation snapshot.
    ///     Stores abstract population data and temporary runtime materialization data.
    /// </summary>
    [Serializable]
    public sealed class Region
    {
        [Header("Identity")] public string regionId;

        public Bounds bounds = new(Vector3.zero, new Vector3(200f, 120f, 200f));

        [Header("Simulation Stats")] [Min(0)] public int zombiePopulation = 25;

        [Range(0f, 1f)] public float lootLevel = 0.6f;
        [Range(0f, 1f)] public float dangerLevel = 0.45f;

        [Header("Abstract Entities")] public List<ZombieHorde> zombieHordes = new();

        public List<SurvivorGroup> survivorGroups = new();
        [NonSerialized] public readonly List<SurvivorController> RuntimeSurvivors = new();
        [NonSerialized] public readonly List<ZombieController> RuntimeZombies = new();

        [NonSerialized] public WorldSimulationLayer ActiveLayer = WorldSimulationLayer.Abstract;
        [NonSerialized] public bool RuntimeMaterialized;

        public Vector3 Center => bounds.center;

        public bool Contains(Vector3 worldPosition)
        {
            return bounds.Contains(worldPosition);
        }
    }
}