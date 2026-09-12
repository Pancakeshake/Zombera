#region

using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;
using Zombera.Systems;

#endregion

// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Zombera.Core
{
    /// <summary>
    ///     Raised when a unit takes damage.
    /// </summary>
    public struct UnitDamagedEvent : IGameEvent
    {
        public string UnitId { get; set; }
        public UnitRole Role { get; set; }
        public float Amount { get; set; }
        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }
        public Vector3 Position { get; set; }
        public GameObject UnitObject { get; set; }
        public GameObject DamageSource { get; set; }
    }

    /// <summary>
    ///     Raised when a unit dies.
    /// </summary>
    public struct UnitDeathEvent : IGameEvent
    {
        public string UnitId { get; set; }
        public UnitRole Role { get; set; }
        public Vector3 Position { get; set; }
        public GameObject UnitObject { get; set; }
        public GameObject DamageSource { get; set; }
    }

    /// <summary>
    ///     Raised when loot is generated for a container.
    /// </summary>
    public struct LootGeneratedEvent : IGameEvent
    {
        public string ContainerId { get; set; }
        public LootLocationType LocationType { get; set; }
        public int ItemCount { get; set; }
        public float TotalWeight { get; set; }
        public Vector3 Position { get; set; }
    }

    /// <summary>
    ///     Raised when a zombie is spawned.
    /// </summary>
    public struct ZombieSpawnedEvent : IGameEvent
    {
        public string ZombieTypeId { get; set; }
        public Vector3 Position { get; set; }
        public GameObject Zombie { get; set; }
    }

    /// <summary>
    ///     Raised when a base structure finishes construction.
    /// </summary>
    public struct BuildingCompletedEvent : IGameEvent
    {
        public string BuildingId { get; set; }
        public Vector3 Position { get; set; }
        public GameObject BuildingObject { get; set; }
    }

    /// <summary>
    ///     Raised when a squad command is issued.
    /// </summary>
    public struct SquadCommandIssuedEvent : IGameEvent
    {
        public SquadCommandType CommandType { get; set; }
        public Vector3 TargetPosition { get; set; }
        public int MemberCount { get; set; }
    }

    /// <summary>
    ///     Raised on world simulation pulse ticks.
    /// </summary>
    public struct WorldSimulationTickEvent : IGameEvent
    {
        public float DeltaTime { get; set; }
        public Vector3 PlayerPosition { get; set; }
    }

    /// <summary>
    ///     Raised when a tactical combat encounter starts.
    /// </summary>
    public struct CombatEncounterStartedEvent : IGameEvent
    {
        public int EncounterId { get; set; }
        public Unit Initiator { get; set; }
        public Unit Defender { get; set; }
        public Vector3 Position { get; set; }
    }

    /// <summary>
    ///     Raised at the start of a tactical attack windup, before damage resolves.
    /// </summary>
    public struct CombatAttackWindupEvent : IGameEvent
    {
        public int EncounterId { get; set; }
        public Unit Attacker { get; set; }
        public Unit Defender { get; set; }
        public float WindupSeconds { get; set; }
        public float HitChance01 { get; set; }
    }

    /// <summary>
    ///     Raised once per tactical tick after resolving one attack exchange.
    /// </summary>
    public struct CombatTickResolvedEvent : IGameEvent
    {
        public int EncounterId { get; set; }
        public Unit Attacker { get; set; }
        public Unit Defender { get; set; }
        public CombatAttackStyle AttackStyle { get; set; }
        public CombatReactionArea PreferredReactionArea { get; set; }
        public bool DidHit { get; set; }
        public bool DidDefenderDodge { get; set; }
        public bool IsCritical { get; set; }
        public float Damage { get; set; }
        public float HitChance01 { get; set; }
        public float AttackerStunChance01 { get; set; }
    }

    /// <summary>
    ///     Raised when a tactical combat encounter ends.
    /// </summary>
    public struct CombatEncounterEndedEvent : IGameEvent
    {
        public int EncounterId { get; set; }
        public Unit Winner { get; set; }
        public Unit Loser { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    ///     Raised when a unit fully loots a container (all items transferred to their inventory).
    /// </summary>
    public struct ContainerLootedEvent : IGameEvent
    {
        public string ContainerId { get; set; }
        public Vector3 Position { get; set; }
        public int ItemCount { get; set; }
        public GameObject LooterObject { get; set; }
    }

    /// <summary>
    ///     Raised when a noise occurs at a world position (gunshot, explosion, shout).
    ///     Nearby zombies with a NoiseListener component will investigate the source.
    /// </summary>
    public struct NoiseEvent : IGameEvent
    {
        public Vector3 Position { get; set; }
        public float Radius { get; set; }
        public NoiseType NoiseType { get; set; }
        public GameObject Source { get; set; }
    }

    public enum NoiseType
    {
        Generic,
        Gunshot,
        Explosion,
        Voice
    }

    /// <summary>
    ///     Raised whenever the GameManager transitions between GameState values.
    /// </summary>
    public struct GameStateChangedEvent : IGameEvent
    {
        public GameState PreviousState { get; set; }
        public GameState NewState { get; set; }
    }

    /// <summary>
    ///     Raised when a unit's encumbrance tier changes (e.g. Light → Heavy).
    /// </summary>
    public struct EncumbranceChangedEvent : IGameEvent
    {
        public GameObject InventoryObject { get; set; }
        public EncumbranceState PreviousState { get; set; }
        public EncumbranceState NewState { get; set; }
        public float CarryRatio { get; set; }
    }

    /// <summary>
    ///     Raised when a squad member is added to or removed from the active roster.
    /// </summary>
    public struct SquadRosterChangedEvent : IGameEvent
    {
        public SquadMember Member { get; set; }
        public bool WasAdded { get; set; } // true = added, false = removed
    }
}
// ReSharper restore UnusedAutoPropertyAccessor.Global