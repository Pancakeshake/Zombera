using UnityEngine;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.Core
{
    /// <summary>
    /// Raised when a unit dies.
    /// </summary>
    public struct UnitDeathEvent : IGameEvent
    {
        public string UnitId;
        public UnitRole Role;
        public Vector3 Position;
        public GameObject UnitObject;
        public GameObject DamageSource;
    }

    /// <summary>
    /// Raised when loot is generated for a container.
    /// </summary>
    public struct LootGeneratedEvent : IGameEvent
    {
        public string ContainerId;
        public LootLocationType LocationType;
        public int ItemCount;
        public float TotalWeight;
        public Vector3 Position;
    }

    /// <summary>
    /// Raised when a zombie is spawned.
    /// </summary>
    public struct ZombieSpawnedEvent : IGameEvent
    {
        public string ZombieTypeId;
        public Vector3 Position;
        public ZombieAI Zombie;
    }

    /// <summary>
    /// Raised when a base structure finishes construction.
    /// </summary>
    public struct BuildingCompletedEvent : IGameEvent
    {
        public string BuildingId;
        public Vector3 Position;
        public GameObject BuildingObject;
    }

    /// <summary>
    /// Raised when a squad command is issued.
    /// </summary>
    public struct SquadCommandIssuedEvent : IGameEvent
    {
        public SquadCommandType CommandType;
        public Vector3 TargetPosition;
        public int MemberCount;
    }

    /// <summary>
    /// Raised on world simulation pulse ticks.
    /// </summary>
    public struct WorldSimulationTickEvent : IGameEvent
    {
        public float DeltaTime;
        public Vector3 PlayerPosition;
    }
}